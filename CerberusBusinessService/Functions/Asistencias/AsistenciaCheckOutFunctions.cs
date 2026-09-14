using CerberusBusinessService.Functions.Relevos;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Asistencias;
using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Asistencias
{
    public class AsistenciaCheckOutFunctions
    {
        #region CONSTANTES

        private const int ESTATUS_EN_TURNO = 1;
        private const int ESTATUS_PENDIENTE_AUTORIZAR = 3;

        #endregion

        #region PROPIEDADES

        private readonly string _csCerberus;
        private readonly AsistenciasFunctions _asistenciasFunctions;
        private readonly RelevoFlexibleFunctions _relevoFlexibleFunctions;

        #endregion

        #region CONSTRUCTOR

        public AsistenciaCheckOutFunctions(
            IConfiguration config,
            AsistenciasFunctions asistenciasFunctions,
            RelevoFlexibleFunctions relevoFlexibleFunctions)
        {
            _csCerberus =
                config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");

            _asistenciasFunctions =
                asistenciasFunctions;

            _relevoFlexibleFunctions =
                relevoFlexibleFunctions;
        }

        #endregion

        #region CHECK-OUT SIN RELEVO

        public async Task<ResponseModel<CheckOutRelevoResponse>>
            ProcesarCheckOutSinRelevoAsync(
                CheckOutRelevoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<CheckOutRelevoResponse>();

            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    return Error(
                        401,
                        "No fue posible identificar al empleado.");
                }

                if (data == null)
                {
                    return Error(
                        400,
                        "El request es obligatorio.");
                }

                if (data.FotoEvidencia == null ||
                    data.FotoEvidencia.Length == 0)
                {
                    return Error(
                        400,
                        "La fotografía de evidencia es obligatoria.");
                }

                numeroUsuario =
                    numeroUsuario.Trim();

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                DateTime fechaHoraActual =
                    await conn.ExecuteScalarAsync<DateTime>(
                        new CommandDefinition(
                            "SELECT SYSDATETIME();",
                            cancellationToken: ct));

                AsistenciaActivaCheckOutDto? asistencia =
                    await ObtenerAsistenciaActivaAsync(
                        conn,
                        numeroUsuario,
                        ct);

                if (asistencia == null)
                {
                    return Error(
                        404,
                        "El empleado no tiene una asistencia activa.");
                }

                // ============================================================
                // 1. SALIDA ANTICIPADA = ABANDONO DE TURNO
                // ============================================================

                if (fechaHoraActual <
                    asistencia.FechaHoraSalidaProgramada)
                {
                    if (string.IsNullOrWhiteSpace(
                        data.MotivoNoPermanencia))
                    {
                        return Error(
                            400,
                            "El motivo del abandono de turno es obligatorio.");
                    }

                    ResponseModel<SolicitudRelevoNoPlaneadoDto>
                        solicitud =
                            await _relevoFlexibleFunctions
                                .CrearSolicitudDesdeAsistenciaSinAfectadoAsync(
                                    asistencia,
                                    fechaHoraActual,
                                    asistencia.FechaHoraSalidaProgramada,
                                    "El empleado abandonó el servicio antes de finalizar su turno.",
                                    data.MotivoNoPermanencia.Trim(),
                                    data.FotoEvidencia,
                                    registrarAbandono: true,
                                    realizarCheckOut: true,
                                    numeroUsuario,
                                    accessToken,
                                    ct);

                    if (!solicitud.isSuccess ||
                        solicitud.data == null)
                    {
                        return Error(
                            solicitud.code,
                            solicitud.message,
                            solicitud.desc);
                    }

                    response.isSuccess = true;
                    response.code = 200;
                    response.message =
                        "Check-Out registrado correctamente.";
                    response.desc =
                        "Se registró la incidencia de abandono de turno y la solicitud de cobertura quedó pendiente de asignación.";

                    response.data =
                        new CheckOutRelevoResponse
                        {
                            AsistenciaId =
                                asistencia.AsistenciaId,

                            ServicioId =
                                asistencia.ServicioId,

                            ServicioEmpleadoSalienteId =
                                asistencia.ServicioEmpleadoId,

                            ServicioEmpleadoAfectadoId =
                                null,

                            SolicitudRelevoNoPlaneadoId =
                                solicitud.data
                                    .SolicitudRelevoNoPlaneadoId,

                            RelevoNoPlaneadoAsignacionId =
                                null,

                            PuedePermanecer =
                                false,

                            CheckOutRealizado =
                                true,

                            FechaHoraCheckOut =
                                solicitud.data.FechaRegistro,

                            SolicitudEstatusClave =
                                "PENDIENTE_ASIGNACION",

                            AsignacionEstatusClave =
                                null
                        };

                    return response;
                }

                // ============================================================
                // 2. TURNO FINALIZADO: BUSCAR SIGUIENTE RELEVO
                // ============================================================

                List<RelevoEsperadoCheckOutResponse>
                    relevosEsperados =
                        await ObtenerRelevosEsperadosAsync(
                            conn,
                            asistencia,
                            ct);

                if (relevosEsperados.Count > 0)
                {
                    // Existe siguiente turno. En este caso sí debe existir
                    // una asignación afectada válida y conservamos el flujo
                    // actual, incluida la incidencia al empleado ausente.
                    if (!data.ServicioEmpleadoAfectadoId.HasValue ||
                        data.ServicioEmpleadoAfectadoId.Value <= 0)
                    {
                        return Error(
                            400,
                            "Debe seleccionarse la asignación correspondiente al siguiente turno.");
                    }

                    RelevoEsperadoCheckOutResponse? seleccionado =
                        relevosEsperados.FirstOrDefault(
                            x =>
                                x.ServicioEmpleadoId ==
                                data.ServicioEmpleadoAfectadoId.Value);

                    if (seleccionado == null)
                    {
                        return Error(
                            409,
                            "La asignación seleccionada no corresponde al siguiente turno de este servicio.");
                    }

                    if (string.Equals(
                        seleccionado.NumeroUsuario,
                        numeroUsuario,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return Error(
                            409,
                            "La asignación del siguiente turno pertenece al mismo empleado.");
                    }

                    if (seleccionado.AsistenciaId.HasValue)
                    {
                        if (seleccionado.EstatusAsistencia ==
                            ESTATUS_PENDIENTE_AUTORIZAR)
                        {
                            return Error(
                                409,
                                "El empleado de relevo ya realizó Check-In y está pendiente de autorización.");
                        }

                        if (seleccionado.EstatusAsistencia ==
                            ESTATUS_EN_TURNO)
                        {
                            return Error(
                                409,
                                "El empleado de relevo ya se encuentra en turno.");
                        }
                    }

                    return await _asistenciasFunctions
                        .ProcesarCheckOutSinRelevoAsync(
                            data,
                            numeroUsuario,
                            accessToken,
                            ct);
                }

                // ============================================================
                // 3. NO EXISTE SIGUIENTE TURNO
                //
                // No hay empleado ausente, por lo tanto:
                // - ServicioEmpleadoAfectadoId = NULL
                // - no se registra FALTA_RECHAZO_TURNO
                // - el flujo continúa con extensión o cobertura.
                // ============================================================

                if (!data.PuedePermanecer &&
                    string.IsNullOrWhiteSpace(
                        data.MotivoNoPermanencia))
                {
                    return Error(
                        400,
                        "El motivo por el cual el empleado no puede permanecer es obligatorio.");
                }

                TimeSpan duracionTurno =
                    asistencia.FechaHoraSalidaProgramada -
                    asistencia.FechaHoraEntradaProgramada;

                if (duracionTurno <= TimeSpan.Zero)
                {
                    return Error(
                        409,
                        "No fue posible determinar la duración del turno para generar la cobertura.");
                }

                DateTime inicioCobertura =
                    data.PuedePermanecer
                        ? asistencia.FechaHoraSalidaProgramada
                        : fechaHoraActual;

                DateTime finCobertura =
                    inicioCobertura.Add(duracionTurno);

                ResponseModel<SolicitudRelevoNoPlaneadoDto>
                    solicitudSinAfectado =
                        await _relevoFlexibleFunctions
                            .CrearSolicitudDesdeAsistenciaSinAfectadoAsync(
                                asistencia,
                                inicioCobertura,
                                finCobertura,
                                "No existe un empleado programado para el siguiente turno del servicio.",
                                data.PuedePermanecer
                                    ? null
                                    : data.MotivoNoPermanencia!.Trim(),
                                data.FotoEvidencia,
                                registrarAbandono: false,
                                realizarCheckOut:
                                    !data.PuedePermanecer,
                                numeroUsuario,
                                accessToken,
                                ct);

                if (!solicitudSinAfectado.isSuccess ||
                    solicitudSinAfectado.data == null)
                {
                    return Error(
                        solicitudSinAfectado.code,
                        solicitudSinAfectado.message,
                        solicitudSinAfectado.desc);
                }

                long solicitudId =
                    solicitudSinAfectado.data
                        .SolicitudRelevoNoPlaneadoId;

                if (data.PuedePermanecer)
                {
                    ResponseModel<RelevoNoPlaneadoAsignacionDto>
                        extension =
                            await _relevoFlexibleFunctions
                                .CrearExtensionAsync(
                                    solicitudId,
                                    numeroUsuario,
                                    accessToken,
                                    ct);

                    if (!extension.isSuccess ||
                        extension.data == null)
                    {
                        response.isSuccess = false;
                        response.code = extension.code;
                        response.message =
                            "La solicitud de cobertura fue creada, pero no fue posible crear la propuesta de extensión.";
                        response.desc =
                            string.IsNullOrWhiteSpace(extension.desc)
                                ? extension.message
                                : extension.message + " " + extension.desc;

                        response.data =
                            new CheckOutRelevoResponse
                            {
                                AsistenciaId =
                                    asistencia.AsistenciaId,

                                ServicioId =
                                    asistencia.ServicioId,

                                ServicioEmpleadoSalienteId =
                                    asistencia.ServicioEmpleadoId,

                                ServicioEmpleadoAfectadoId =
                                    null,

                                SolicitudRelevoNoPlaneadoId =
                                    solicitudId,

                                RelevoNoPlaneadoAsignacionId =
                                    null,

                                PuedePermanecer =
                                    true,

                                CheckOutRealizado =
                                    false,

                                FechaHoraCheckOut =
                                    null,

                                SolicitudEstatusClave =
                                    "PENDIENTE_ASIGNACION",

                                AsignacionEstatusClave =
                                    null
                            };

                        return response;
                    }

                    response.isSuccess = true;
                    response.code = 200;
                    response.message =
                        "La solicitud de cobertura y la propuesta de extensión fueron creadas correctamente.";
                    response.desc =
                        string.IsNullOrWhiteSpace(extension.desc)
                            ? "La extensión quedó pendiente de autorización del supervisor."
                            : extension.desc;

                    response.data =
                        new CheckOutRelevoResponse
                        {
                            AsistenciaId =
                                asistencia.AsistenciaId,

                            ServicioId =
                                asistencia.ServicioId,

                            ServicioEmpleadoSalienteId =
                                asistencia.ServicioEmpleadoId,

                            ServicioEmpleadoAfectadoId =
                                null,

                            SolicitudRelevoNoPlaneadoId =
                                solicitudId,

                            RelevoNoPlaneadoAsignacionId =
                                extension.data
                                    .RelevoNoPlaneadoAsignacionId,

                            PuedePermanecer =
                                true,

                            CheckOutRealizado =
                                false,

                            FechaHoraCheckOut =
                                null,

                            SolicitudEstatusClave =
                                "EN_PROCESO",

                            AsignacionEstatusClave =
                                "PENDIENTE_SUPERVISOR"
                        };

                    return response;
                }

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Check-Out registrado correctamente.";
                response.desc =
                    "No existe un siguiente relevo programado. El Check-Out fue registrado y la solicitud de cobertura quedó pendiente de asignación.";

                response.data =
                    new CheckOutRelevoResponse
                    {
                        AsistenciaId =
                            asistencia.AsistenciaId,

                        ServicioId =
                            asistencia.ServicioId,

                        ServicioEmpleadoSalienteId =
                            asistencia.ServicioEmpleadoId,

                        ServicioEmpleadoAfectadoId =
                            null,

                        SolicitudRelevoNoPlaneadoId =
                            solicitudId,

                        RelevoNoPlaneadoAsignacionId =
                            null,

                        PuedePermanecer =
                            false,

                        CheckOutRealizado =
                            true,

                        FechaHoraCheckOut =
                            solicitudSinAfectado.data.FechaRegistro,

                        SolicitudEstatusClave =
                            "PENDIENTE_ASIGNACION",

                        AsignacionEstatusClave =
                            null
                    };

                return response;
            }
            catch (OperationCanceledException)
            {
                return Error(
                    408,
                    "La operación de Check-Out sin relevo fue cancelada.");
            }
            catch (SqlException ex)
            {
                return Error(
                    500,
                    "Error SQL al procesar el Check-Out sin relevo.",
                    ex.Message);
            }
            catch (Exception ex)
            {
                return Error(
                    500,
                    "Error al procesar el Check-Out sin relevo.",
                    ex.Message);
            }
        }

        #endregion

        #region CONSULTAS

        private async Task<AsistenciaActivaCheckOutDto?>
            ObtenerAsistenciaActivaAsync(
                SqlConnection conn,
                string numeroUsuario,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    AsistenciaId,
    ServicioId,
    ServicioEmpleadoId,
    NumeroEmpleadoEntrante,
    FechaTurno,
    FechaHoraEntradaProgramada,
    FechaHoraSalidaProgramada,
    FechaHoraCheckIn,
    Estatus
FROM dbo.Asistencia
WHERE NumeroEmpleadoEntrante = @NumeroUsuario
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL
ORDER BY
    FechaHoraCheckIn DESC,
    AsistenciaId DESC;";

            return await conn.QueryFirstOrDefaultAsync<
                AsistenciaActivaCheckOutDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        NumeroUsuario =
                            numeroUsuario,

                        EstatusEnTurno =
                            ESTATUS_EN_TURNO
                    },
                    cancellationToken: ct));
        }

        private async Task<List<RelevoEsperadoCheckOutResponse>>
            ObtenerRelevosEsperadosAsync(
                SqlConnection conn,
                AsistenciaActivaCheckOutDto asistencia,
                CancellationToken ct)
        {
            DateTime fechaRelevo =
                asistencia.FechaHoraSalidaProgramada.Date;

            TimeSpan horaRelevo =
                asistencia.FechaHoraSalidaProgramada.TimeOfDay;

            const string sql = @"
SELECT
    SE.ServicioEmpleadoId,
    SE.EmpleadoId,
    ISNULL(LTRIM(RTRIM(E.UsuarioAsignado)), '') AS NumeroUsuario,
    CONCAT_WS(
        ' ',
        NULLIF(LTRIM(RTRIM(E.Nombres)), ''),
        NULLIF(LTRIM(RTRIM(E.ApellidoPaterno)), ''),
        NULLIF(LTRIM(RTRIM(E.ApellidoMaterno)), '')
    ) AS NombreCompleto,
    DATEADD(
        SECOND,
        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), SE.HoraEntrada),
        CAST(@FechaRelevo AS DATETIME2)
    ) AS FechaHoraEntradaProgramada,
    DATEADD(
        DAY,
        CASE WHEN SE.SalidaDiaSiguiente = 1 THEN 1 ELSE 0 END,
        DATEADD(
            SECOND,
            DATEDIFF(SECOND, CAST('00:00:00' AS TIME), SE.HoraSalida),
            CAST(@FechaRelevo AS DATETIME2)
        )
    ) AS FechaHoraSalidaProgramada,
    A.AsistenciaId,
    A.Estatus AS EstatusAsistencia
FROM dbo.ServicioEmpleado SE
INNER JOIN dbo.DatosGeneralesEmpleado E
    ON E.ID = SE.EmpleadoId
OUTER APPLY
(
    SELECT TOP (1)
        ASI.AsistenciaId,
        ASI.Estatus
    FROM dbo.Asistencia ASI
    WHERE ASI.ServicioEmpleadoId = SE.ServicioEmpleadoId
      AND ASI.FechaTurno = @FechaRelevo
    ORDER BY ASI.AsistenciaId DESC
) A
WHERE SE.ServicioId = @ServicioId
  AND SE.ServicioEmpleadoId <> @ServicioEmpleadoActualId
  AND SE.FechaInicio <= @FechaRelevo
  AND (SE.FechaFin IS NULL OR SE.FechaFin >= @FechaRelevo)
  AND SE.HoraEntrada = @HoraRelevo
ORDER BY SE.ServicioEmpleadoId;";

            var result =
                await conn.QueryAsync<RelevoEsperadoCheckOutResponse>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            asistencia.ServicioId,
                            ServicioEmpleadoActualId =
                                asistencia.ServicioEmpleadoId,
                            FechaRelevo = fechaRelevo,
                            HoraRelevo = horaRelevo
                        },
                        cancellationToken: ct));

            return result.ToList();
        }

        #endregion

        #region RESPONSE

        private static ResponseModel<CheckOutRelevoResponse>
            Error(
                int code,
                string message,
                string? desc = null)
        {
            return new ResponseModel<CheckOutRelevoResponse>
            {
                isSuccess = false,
                code = code,
                message = message,
                desc = desc,
                data = null
            };
        }

        #endregion
    }
}
