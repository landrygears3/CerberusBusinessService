using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Relevos
{
    public class RelevoConsultaFunctions
    {
        #region CONSTANTES

        private const string ESTATUS_PENDIENTE_FIRMA_EMPLEADO =
            "PENDIENTE_FIRMA_EMPLEADO";

        private const string ESTATUS_EN_PROCESO =
            "EN_PROCESO";

        #endregion


        #region PROPIEDADES

        private readonly RelevoNoPlaneadoDataService _data;

        #endregion


        #region CONSTRUCTOR

        public RelevoConsultaFunctions(
            RelevoNoPlaneadoDataService data)
        {
            _data = data;
        }

        #endregion


        #region OBTENER SOLICITUD

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerSolicitudAsync(
                long solicitudId,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<SolicitudRelevoNoPlaneadoResponse>();

            try
            {
                if (solicitudId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "SolicitudRelevoNoPlaneadoId es inválido.";
                    response.data = null;

                    return response;
                }

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                // ====================================================
                // SOLICITUD
                // ====================================================

                const string sqlSolicitud = @"
SELECT
    SR.SolicitudRelevoNoPlaneadoId,
    SR.ServicioEmpleadoAfectadoId,
    SR.ServicioEmpleadoSalienteId,

    SE.ServicioId,
    S.NombreServicio,

    SE.EmpleadoId AS EmpleadoAfectadoId,

    ISNULL(
        LTRIM(RTRIM(E.UsuarioAsignado)),
        ''
    ) AS NumeroUsuarioAfectado,

    CONCAT_WS(
        ' ',
        NULLIF(LTRIM(RTRIM(E.Nombres)), ''),
        NULLIF(LTRIM(RTRIM(E.ApellidoPaterno)), ''),
        NULLIF(LTRIM(RTRIM(E.ApellidoMaterno)), '')
    ) AS NombreEmpleadoAfectado,

    O.Clave AS OrigenClave,
    O.Nombre AS OrigenNombre,

    ES.Clave AS EstatusClave,
    ES.Nombre AS EstatusNombre,

    SR.FechaHoraInicioCobertura,
    SR.FechaHoraFinCobertura,

    SR.MotivoRelevo,
    SR.MotivoNoPermanencia,
    SR.RutaFotoEvidencia,

    SR.FechaRegistro

FROM dbo.SolicitudRelevoNoPlaneado SR

INNER JOIN dbo.ServicioEmpleado SE
    ON SE.ServicioEmpleadoId =
       SR.ServicioEmpleadoAfectadoId

INNER JOIN dbo.Servicio S
    ON S.ServicioId =
       SE.ServicioId

INNER JOIN dbo.DatosGeneralesEmpleado E
    ON E.ID =
       SE.EmpleadoId

INNER JOIN dbo.CAT_RelevoNoPlaneadoOrigen O
    ON O.RelevoNoPlaneadoOrigenId =
       SR.RelevoNoPlaneadoOrigenId

INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ES
    ON ES.RelevoNoPlaneadoEstatusId =
       SR.RelevoNoPlaneadoEstatusId

WHERE SR.SolicitudRelevoNoPlaneadoId =
      @SolicitudId;";

                SolicitudRelevoNoPlaneadoResponse? solicitud =
                    await conn.QueryFirstOrDefaultAsync<
                        SolicitudRelevoNoPlaneadoResponse>(
                        new CommandDefinition(
                            sqlSolicitud,
                            new
                            {
                                SolicitudId =
                                    solicitudId
                            },
                            cancellationToken: ct));

                if (solicitud == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la solicitud de relevo no planeado.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // HISTORIAL DE ASIGNACIONES
                // ====================================================

                const string sqlAsignaciones = @"
SELECT
    A.RelevoNoPlaneadoAsignacionId,
    A.SolicitudRelevoNoPlaneadoId,
    A.EmpleadoIdAsignado,

    ISNULL(
        LTRIM(RTRIM(E.UsuarioAsignado)),
        ''
    ) AS NumeroUsuarioAsignado,

    CONCAT_WS(
        ' ',
        NULLIF(LTRIM(RTRIM(E.Nombres)), ''),
        NULLIF(LTRIM(RTRIM(E.ApellidoPaterno)), ''),
        NULLIF(LTRIM(RTRIM(E.ApellidoMaterno)), '')
    ) AS NombreEmpleadoAsignado,

    TC.Clave AS TipoCoberturaClave,
    TC.Nombre AS TipoCoberturaNombre,

    EA.Clave AS EstatusClave,
    EA.Nombre AS EstatusNombre,

    A.TextoResponsiva,

    A.SupervisorEmpleadoIdAutoriza,
    A.FechaHoraFirmaSupervisor,

    A.FechaHoraFirmaEmpleado
        AS FechaHoraFirmaAceptacion,

    A.MotivoRechazo,
    A.FechaHoraRechazo,

    A.ServicioEmpleadoTemporalId,

    A.FechaAsignacion

FROM dbo.RelevoNoPlaneadoAsignacion A

INNER JOIN dbo.DatosGeneralesEmpleado E
    ON E.ID =
       A.EmpleadoIdAsignado

INNER JOIN dbo.CAT_RelevoTipoCobertura TC
    ON TC.RelevoTipoCoberturaId =
       A.RelevoTipoCoberturaId

INNER JOIN dbo.CAT_RelevoAsignacionEstatus EA
    ON EA.RelevoAsignacionEstatusId =
       A.RelevoAsignacionEstatusId

WHERE A.SolicitudRelevoNoPlaneadoId =
      @SolicitudId

ORDER BY
    A.FechaAsignacion ASC,
    A.RelevoNoPlaneadoAsignacionId ASC;";

                var asignaciones =
                    await conn.QueryAsync<
                        RelevoNoPlaneadoAsignacionResponse>(
                        new CommandDefinition(
                            sqlAsignaciones,
                            new
                            {
                                SolicitudId =
                                    solicitudId
                            },
                            cancellationToken: ct));

                solicitud.Asignaciones =
                    asignaciones.ToList();

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Solicitud de relevo obtenida correctamente.";
                response.desc = null;
                response.data =
                    solicitud;

                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al obtener la solicitud de relevo.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener la solicitud de relevo.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region OBTENER ASIGNACION

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerAsignacionAsync(
                long relevoNoPlaneadoAsignacionId,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<SolicitudRelevoNoPlaneadoResponse>();

            try
            {
                if (relevoNoPlaneadoAsignacionId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "RelevoNoPlaneadoAsignacionId es inválido.";
                    response.data = null;

                    return response;
                }

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                long? solicitudId =
                    await _data.ObtenerSolicitudIdPorAsignacionAsync(
                        conn,
                        relevoNoPlaneadoAsignacionId,
                        ct);

                if (!solicitudId.HasValue)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la asignación de relevo no planeado.";
                    response.data = null;

                    return response;
                }

                return await ObtenerSolicitudAsync(
                    solicitudId.Value,
                    ct);
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al obtener la asignación de relevo.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener la asignación de relevo.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region PENDIENTES EMPLEADO

        public async Task<ResponseModel<List<RelevoPendienteEmpleadoResponse>>>
            ObtenerPendientesEmpleadoAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<
                    List<RelevoPendienteEmpleadoResponse>>();

            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message =
                        "No fue posible identificar al empleado.";
                    response.data = null;

                    return response;
                }

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                // ====================================================
                // EMPLEADO AUTENTICADO
                // ====================================================

                EmpleadoRelevoDto? empleado =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario.Trim(),
                        ct);

                if (empleado == null)
                {
                    response.isSuccess = true;
                    response.code = 200;
                    response.message =
                        "El usuario no tiene relevos pendientes.";
                    response.desc = null;
                    response.data =
                        new List<RelevoPendienteEmpleadoResponse>();

                    return response;
                }

                // ====================================================
                // PROPUESTAS PENDIENTES DE FIRMA
                // ====================================================

                const string sql = @"
SELECT
    SR.SolicitudRelevoNoPlaneadoId,
    A.RelevoNoPlaneadoAsignacionId,

    SE.ServicioId,
    S.NombreServicio,

    SR.ServicioEmpleadoAfectadoId,

    TC.Clave AS TipoCoberturaClave,
    EA.Clave AS EstatusClave,

    SR.FechaHoraInicioCobertura,
    SR.FechaHoraFinCobertura,

    A.TextoResponsiva,
    A.FechaAsignacion

FROM dbo.RelevoNoPlaneadoAsignacion A

INNER JOIN dbo.SolicitudRelevoNoPlaneado SR
    ON SR.SolicitudRelevoNoPlaneadoId =
       A.SolicitudRelevoNoPlaneadoId

INNER JOIN dbo.ServicioEmpleado SE
    ON SE.ServicioEmpleadoId =
       SR.ServicioEmpleadoAfectadoId

INNER JOIN dbo.Servicio S
    ON S.ServicioId =
       SE.ServicioId

INNER JOIN dbo.CAT_RelevoTipoCobertura TC
    ON TC.RelevoTipoCoberturaId =
       A.RelevoTipoCoberturaId

INNER JOIN dbo.CAT_RelevoAsignacionEstatus EA
    ON EA.RelevoAsignacionEstatusId =
       A.RelevoAsignacionEstatusId

INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ES
    ON ES.RelevoNoPlaneadoEstatusId =
       SR.RelevoNoPlaneadoEstatusId

WHERE A.EmpleadoIdAsignado =
      @EmpleadoId

  AND EA.Clave =
      @EstatusPendienteFirma

  AND ES.Clave =
      @EstatusEnProceso

  AND SR.FechaHoraFinCobertura >
      SYSDATETIME()

ORDER BY
    SR.FechaHoraInicioCobertura ASC,
    A.FechaAsignacion ASC;";

                var result =
                    await conn.QueryAsync<
                        RelevoPendienteEmpleadoResponse>(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                EmpleadoId =
                                    empleado.EmpleadoId,

                                EstatusPendienteFirma =
                                    ESTATUS_PENDIENTE_FIRMA_EMPLEADO,

                                EstatusEnProceso =
                                    ESTATUS_EN_PROCESO
                            },
                            cancellationToken: ct));

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Relevos pendientes del empleado obtenidos correctamente.";
                response.desc = null;
                response.data =
                    result.ToList();

                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al obtener los relevos pendientes del empleado.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los relevos pendientes del empleado.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region PENDIENTES SUPERVISOR

        public async Task<ResponseModel<List<RelevoPendienteSupervisorResponse>>>
            ObtenerPendientesSupervisorAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<
                    List<RelevoPendienteSupervisorResponse>>();

            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message =
                        "No fue posible identificar al supervisor.";
                    response.data = null;

                    return response;
                }

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                // ====================================================
                // SUPERVISOR AUTENTICADO
                // ====================================================

                EmpleadoRelevoDto? supervisor =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario.Trim(),
                        ct);

                if (supervisor == null)
                {
                    response.isSuccess = true;
                    response.code = 200;
                    response.message =
                        "El usuario no tiene relevos pendientes.";
                    response.desc = null;
                    response.data =
                        new List<
                            RelevoPendienteSupervisorResponse>();

                    return response;
                }

                // ====================================================
                // SOLICITUDES DEL ALCANCE DEL SUPERVISOR
                // ====================================================

                const string sql = @"
SELECT
    SR.SolicitudRelevoNoPlaneadoId,

    AP.RelevoNoPlaneadoAsignacionId,

    SE.ServicioId,
    S.NombreServicio,

    SR.ServicioEmpleadoAfectadoId,

    SE.EmpleadoId AS EmpleadoAfectadoId,

    ISNULL(
        LTRIM(RTRIM(EA.UsuarioAsignado)),
        ''
    ) AS NumeroUsuarioAfectado,

    CONCAT_WS(
        ' ',
        NULLIF(LTRIM(RTRIM(EA.Nombres)), ''),
        NULLIF(LTRIM(RTRIM(EA.ApellidoPaterno)), ''),
        NULLIF(LTRIM(RTRIM(EA.ApellidoMaterno)), '')
    ) AS NombreEmpleadoAfectado,

    O.Clave AS OrigenClave,

    ESR.Clave AS SolicitudEstatusClave,

    SR.FechaHoraInicioCobertura,
    SR.FechaHoraFinCobertura,

    SR.MotivoRelevo,
    SR.RutaFotoEvidencia,

    AP.EmpleadoIdAsignado,

    CASE
        WHEN AP.EmpleadoIdAsignado IS NULL
            THEN NULL
        ELSE
            LTRIM(RTRIM(EP.UsuarioAsignado))
    END AS NumeroUsuarioAsignado,

    CASE
        WHEN AP.EmpleadoIdAsignado IS NULL
            THEN NULL
        ELSE CONCAT_WS(
            ' ',
            NULLIF(LTRIM(RTRIM(EP.Nombres)), ''),
            NULLIF(LTRIM(RTRIM(EP.ApellidoPaterno)), ''),
            NULLIF(LTRIM(RTRIM(EP.ApellidoMaterno)), '')
        )
    END AS NombreEmpleadoAsignado,

    AP.TipoCoberturaClave,
    AP.AsignacionEstatusClave,

    SR.FechaRegistro

FROM dbo.SolicitudRelevoNoPlaneado SR

INNER JOIN dbo.ServicioEmpleado SE
    ON SE.ServicioEmpleadoId =
       SR.ServicioEmpleadoAfectadoId

INNER JOIN dbo.Servicio S
    ON S.ServicioId =
       SE.ServicioId

INNER JOIN dbo.DatosGeneralesEmpleado EA
    ON EA.ID =
       SE.EmpleadoId

INNER JOIN dbo.CAT_RelevoNoPlaneadoOrigen O
    ON O.RelevoNoPlaneadoOrigenId =
       SR.RelevoNoPlaneadoOrigenId

INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ESR
    ON ESR.RelevoNoPlaneadoEstatusId =
       SR.RelevoNoPlaneadoEstatusId

OUTER APPLY
(
    SELECT TOP (1)
        A.RelevoNoPlaneadoAsignacionId,
        A.EmpleadoIdAsignado,

        TC.Clave AS TipoCoberturaClave,

        EAS.Clave AS AsignacionEstatusClave

    FROM dbo.RelevoNoPlaneadoAsignacion A

    INNER JOIN dbo.CAT_RelevoTipoCobertura TC
        ON TC.RelevoTipoCoberturaId =
           A.RelevoTipoCoberturaId

    INNER JOIN dbo.CAT_RelevoAsignacionEstatus EAS
        ON EAS.RelevoAsignacionEstatusId =
           A.RelevoAsignacionEstatusId

    WHERE A.SolicitudRelevoNoPlaneadoId =
          SR.SolicitudRelevoNoPlaneadoId

      AND EAS.Clave IN
      (
          'PENDIENTE_SUPERVISOR',
          'PENDIENTE_FIRMA_EMPLEADO'
      )

    ORDER BY
        A.FechaAsignacion DESC,
        A.RelevoNoPlaneadoAsignacionId DESC
) AP

LEFT JOIN dbo.DatosGeneralesEmpleado EP
    ON EP.ID =
       AP.EmpleadoIdAsignado

WHERE ESR.Clave IN
(
    'PENDIENTE_ASIGNACION',
    'EN_PROCESO'
)

AND SR.FechaHoraFinCobertura >
    SYSDATETIME()

AND EXISTS
(
    SELECT 1
    FROM dbo.ServicioSupervisor SS

    WHERE SS.ServicioId =
          SE.ServicioId

      AND SS.SupervisorEmpleadoId =
          @SupervisorEmpleadoId

      AND SS.FechaInicio <=
          CAST(
              SR.FechaHoraInicioCobertura
              AS DATE
          )

      AND
      (
          SS.FechaFin IS NULL
          OR SS.FechaFin >=
             CAST(
                 SR.FechaHoraInicioCobertura
                 AS DATE
             )
      )
)

ORDER BY
    SR.FechaHoraInicioCobertura ASC,
    SR.FechaRegistro ASC;";

                var result =
                    await conn.QueryAsync<
                        RelevoPendienteSupervisorResponse>(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                SupervisorEmpleadoId =
                                    supervisor.EmpleadoId
                            },
                            cancellationToken: ct));

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Relevos pendientes del supervisor obtenidos correctamente.";
                response.desc = null;
                response.data =
                    result.ToList();

                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al obtener los relevos pendientes del supervisor.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los relevos pendientes del supervisor.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion
    }
}