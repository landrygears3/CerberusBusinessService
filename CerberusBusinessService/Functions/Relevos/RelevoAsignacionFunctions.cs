using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Relevos;
using CerberusBusinessService.Functions.Notificaciones;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Relevos
{
    public class RelevoAsignacionFunctions
    {
        #region CONSTANTES

        private const string ESTATUS_RECHAZADA_SUPERVISOR =
            "RECHAZADA_SUPERVISOR";

        private const string ESTATUS_RECHAZADA_EMPLEADO =
            "RECHAZADA_EMPLEADO";

        private const string ESTATUS_ACEPTADA =
            "ACEPTADA";

        private const string ESTATUS_CUBIERTO =
            "CUBIERTO";

        private const string TIPO_ASIGNACION_FALTA =
            "FALTA";

        private const string ESTATUS_EN_PROCESO =
            "EN_PROCESO";

        private const string ESTATUS_PENDIENTE_SUPERVISOR =
            "PENDIENTE_SUPERVISOR";

        private const string ESTATUS_PENDIENTE_FIRMA_EMPLEADO =
            "PENDIENTE_FIRMA_EMPLEADO";

        private const string ESTATUS_PENDIENTE_ASIGNACION =
            "PENDIENTE_ASIGNACION";

        private static readonly HashSet<string> TIPOS_COBERTURA_VALIDOS =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "EXTENSION",
                "SUSTITUTO",
                "SUPERVISOR"
            };

        #endregion


        #region PROPIEDADES
        private readonly RelevoNotificationFunctions _relevoNotificationFunctions;
        private readonly RelevoNoPlaneadoDataService _data;
        private readonly RelevoIntegracionAsistenciaFunctions _integracion;
        private readonly FileRelevoNoPlaneadoService _fileRelevoService;

        #endregion


        #region CONSTRUCTOR

        public RelevoAsignacionFunctions(
            RelevoNoPlaneadoDataService data,
            RelevoIntegracionAsistenciaFunctions integracion,
            FileRelevoNoPlaneadoService fileRelevoService,
            RelevoNotificationFunctions relevoNotificationFunctions)
        {
            _data = data;
            _integracion = integracion;
            _fileRelevoService = fileRelevoService;
            _relevoNotificationFunctions = relevoNotificationFunctions;
        }

        #endregion


        #region CREAR ASIGNACION

        private async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            CrearAsignacionAsync(
                CrearAsignacionRelevoNoPlaneadoDto data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

            SqlTransaction? transaction = null;

            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message =
                        "No fue posible identificar al usuario.";
                    response.data = null;

                    return response;
                }

                string? error =
                    ValidarCrearAsignacion(data);

                if (error != null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = error;
                    response.data = null;

                    return response;
                }

                string tipoCoberturaClave =
                    data.TipoCoberturaClave
                        .Trim()
                        .ToUpperInvariant();

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                // ============================================================
                // SOLICITUD
                // ============================================================

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await _data.ObtenerSolicitudForUpdateAsync(
                        conn,
                        transaction,
                        data.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la solicitud de relevo no planeado.";
                    response.data = null;

                    return response;
                }

                string? estatusSolicitud =
                    await _data.ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct,
                        transaction);

                if (!string.Equals(
                    estatusSolicitud,
                    ESTATUS_PENDIENTE_ASIGNACION,
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud no se encuentra pendiente de asignación.";
                    response.desc =
                        $"Estatus actual: {estatusSolicitud ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // EVITAR PROPUESTAS ACTIVAS SIMULTANEAS
                // ============================================================

                bool existeAsignacionActiva =
                    await _data.ExisteAsignacionActivaAsync(
                        conn,
                        transaction,
                        solicitud.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (existeAsignacionActiva)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud ya tiene una propuesta de cobertura activa.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // ASIGNACION AFECTADA
                // ============================================================

                ServicioEmpleadoRelevoDto? asignacionAfectada =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (asignacionAfectada == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la asignación de servicio afectada.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // EMPLEADO PROPUESTO
                // ============================================================

                EmpleadoRelevoDto? empleadoAsignado =
                    await _data.ObtenerEmpleadoAsync(
                        conn,
                        data.EmpleadoIdAsignado,
                        ct,
                        transaction);

                if (empleadoAsignado == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe el empleado propuesto para cubrir el relevo.";
                    response.data = null;

                    return response;
                }

                if (string.IsNullOrWhiteSpace(
                    empleadoAsignado.NumeroUsuario))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El empleado propuesto no tiene un NumeroUsuario asignado.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // REGLAS DEL TIPO DE COBERTURA
                // ============================================================

                string? errorTipo =
                    await ValidarReglasTipoCoberturaAsync(
                        conn,
                        transaction,
                        solicitud,
                        asignacionAfectada,
                        empleadoAsignado,
                        tipoCoberturaClave,
                        numeroUsuario.Trim(),
                        ct);

                if (errorTipo != null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message = errorTipo;
                    response.data = null;

                    return response;
                }

                // ============================================================
                // TIPO DE COBERTURA
                // ============================================================

                int? tipoCoberturaId =
                    await _data.ObtenerTipoCoberturaIdAsync(
                        conn,
                        tipoCoberturaClave,
                        ct,
                        transaction);

                if (!tipoCoberturaId.HasValue)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        $"No está configurado el tipo de cobertura {tipoCoberturaClave}.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // ESTATUS INICIAL
                // ============================================================

                string estatusAsignacionClave =
                    tipoCoberturaClave == "EXTENSION"
                        ? ESTATUS_PENDIENTE_SUPERVISOR
                        : ESTATUS_PENDIENTE_FIRMA_EMPLEADO;

                int? estatusAsignacionId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        estatusAsignacionClave,
                        ct,
                        transaction);

                if (!estatusAsignacionId.HasValue)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        $"No está configurado el estatus {estatusAsignacionClave}.";
                    response.data = null;

                    return response;
                }

                int? estatusEnProcesoId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_EN_PROCESO,
                        ct,
                        transaction);

                if (!estatusEnProcesoId.HasValue)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        "No está configurado el estatus EN_PROCESO.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // RESPONSIVA
                // ============================================================

                string textoResponsiva =
                    GenerarTextoResponsiva(
                        empleadoAsignado.NombreCompleto,
                        asignacionAfectada.NombreServicio,
                        solicitud.FechaHoraInicioCobertura,
                        solicitud.FechaHoraFinCobertura);

                // ============================================================
                // INSERTAR PROPUESTA
                // ============================================================

                const string sqlInsert = @"
INSERT INTO dbo.RelevoNoPlaneadoAsignacion
(
    SolicitudRelevoNoPlaneadoId,
    EmpleadoIdAsignado,
    RelevoTipoCoberturaId,
    RelevoAsignacionEstatusId,
    TextoResponsiva,
    FechaAsignacion,
    UsuarioAsignacion
)
OUTPUT INSERTED.RelevoNoPlaneadoAsignacionId
VALUES
(
    @SolicitudRelevoNoPlaneadoId,
    @EmpleadoIdAsignado,
    @RelevoTipoCoberturaId,
    @RelevoAsignacionEstatusId,
    @TextoResponsiva,
    @FechaAsignacion,
    @UsuarioAsignacion
);";

                long asignacionId =
                    await conn.ExecuteScalarAsync<long>(
                        new CommandDefinition(
                            sqlInsert,
                            new
                            {
                                solicitud.SolicitudRelevoNoPlaneadoId,

                                data.EmpleadoIdAsignado,

                                RelevoTipoCoberturaId =
                                    tipoCoberturaId.Value,

                                RelevoAsignacionEstatusId =
                                    estatusAsignacionId.Value,

                                TextoResponsiva =
                                    textoResponsiva,

                                FechaAsignacion =
                                    fechaActual,

                                UsuarioAsignacion =
                                    numeroUsuario.Trim()
                            },
                            transaction,
                            cancellationToken: ct));

                // ============================================================
                // SOLICITUD -> EN_PROCESO
                // ============================================================

                const string sqlUpdateSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @EstatusEnProcesoId,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudRelevoNoPlaneadoId
  AND RelevoNoPlaneadoEstatusId = @EstatusActualId;";

                int rows =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlUpdateSolicitud,
                            new
                            {
                                EstatusEnProcesoId =
                                    estatusEnProcesoId.Value,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario.Trim(),

                                solicitud.SolicitudRelevoNoPlaneadoId,

                                EstatusActualId =
                                    solicitud.RelevoNoPlaneadoEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rows != 1)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud cambió de estado antes de crear la asignación.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // COMMIT
                // ============================================================

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Propuesta de cobertura creada correctamente.";

                response.desc =
                    estatusAsignacionClave ==
                    ESTATUS_PENDIENTE_SUPERVISOR
                        ? "La propuesta quedó pendiente de autorización del supervisor."
                        : "La propuesta quedó pendiente de firma de la persona asignada.";

                response.data =
                    new RelevoNoPlaneadoAsignacionDto
                    {
                        RelevoNoPlaneadoAsignacionId =
                            asignacionId,

                        SolicitudRelevoNoPlaneadoId =
                            solicitud.SolicitudRelevoNoPlaneadoId,

                        EmpleadoIdAsignado =
                            empleadoAsignado.EmpleadoId,

                        RelevoTipoCoberturaId =
                            tipoCoberturaId.Value,

                        RelevoAsignacionEstatusId =
                            estatusAsignacionId.Value,

                        TextoResponsiva =
                            textoResponsiva,

                        SupervisorEmpleadoIdAutoriza =
                            null,

                        RutaFirmaSupervisor =
                            null,

                        FechaHoraFirmaSupervisor =
                            null,

                        RutaFirmaEmpleado =
                            null,

                        FechaHoraFirmaEmpleado =
                            null,

                        MotivoRechazo =
                            null,

                        FechaHoraRechazo =
                            null,

                        ServicioEmpleadoTemporalId =
                            null,

                        FechaAsignacion =
                            fechaActual,

                        UsuarioAsignacion =
                            numeroUsuario.Trim()
                    };

                // ============================================================
                // NOTIFICACION
                //
                // SI FALLA, LA ASIGNACION YA ESTA COMMITTEADA.
                // ============================================================

                try
                {
                    if (tipoCoberturaClave == "EXTENSION")
                    {
                        var notificationResponse =
                            await _relevoNotificationFunctions
                                .NotificarExtensionPendienteAutorizacionAsync(
                                    asignacionAfectada.ServicioId,
                                    asignacionAfectada.NombreServicio,
                                    solicitud.SolicitudRelevoNoPlaneadoId,
                                    asignacionId,
                                    empleadoAsignado.EmpleadoId,
                                    empleadoAsignado.NumeroUsuario,
                                    empleadoAsignado.NombreCompleto,
                                    solicitud.FechaHoraInicioCobertura,
                                    solicitud.FechaHoraFinCobertura,
                                    textoResponsiva,
                                    accessToken,
                                    ct);

                        if (notificationResponse.isSuccess)
                        {
                            response.desc +=
                                " Se notificó a los supervisores del servicio.";
                        }
                        else
                        {
                            response.desc +=
                                " No fue posible notificar a los supervisores. " +
                                notificationResponse.message;
                        }
                    }
                    else
                    {
                        var notificationResponse =
                            await _relevoNotificationFunctions
                                .NotificarAsignacionPendienteFirmaEmpleadoAsync(
                                    empleadoAsignado.NumeroUsuario,
                                    asignacionAfectada.ServicioId,
                                    asignacionAfectada.NombreServicio,
                                    solicitud.SolicitudRelevoNoPlaneadoId,
                                    asignacionId,
                                    tipoCoberturaClave,
                                    solicitud.FechaHoraInicioCobertura,
                                    solicitud.FechaHoraFinCobertura,
                                    textoResponsiva,
                                    accessToken,
                                    ct);

                        if (notificationResponse.isSuccess)
                        {
                            response.desc +=
                                " Se notificó al empleado asignado.";
                        }
                        else
                        {
                            response.desc +=
                                " No fue posible notificar al empleado asignado. " +
                                notificationResponse.message;
                        }
                    }
                }
                catch (Exception ex)
                {
                    response.desc +=
                        " La propuesta fue creada, pero ocurrió un error al enviar la notificación: " +
                        ex.Message;
                }

                return response;
            }
            catch (SqlException ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al crear la propuesta de relevo no planeado.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al crear la propuesta de relevo no planeado.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region ASIGNAR SUSTITUTO

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarEmpleadoAsync(
                AsignarEmpleadoRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (data == null)
            {
                return new ResponseModel<RelevoNoPlaneadoAsignacionDto>
                {
                    isSuccess = false,
                    code = 400,
                    message = "El request es obligatorio.",
                    data = null
                };
            }

            if (data.SolicitudRelevoNoPlaneadoId <= 0)
            {
                return new ResponseModel<RelevoNoPlaneadoAsignacionDto>
                {
                    isSuccess = false,
                    code = 400,
                    message =
                        "SolicitudRelevoNoPlaneadoId es inválido.",
                    data = null
                };
            }

            if (data.EmpleadoIdAsignado <= 0)
            {
                return new ResponseModel<RelevoNoPlaneadoAsignacionDto>
                {
                    isSuccess = false,
                    code = 400,
                    message =
                        "EmpleadoIdAsignado es inválido.",
                    data = null
                };
            }

            var asignacion =
                new CrearAsignacionRelevoNoPlaneadoDto
                {
                    SolicitudRelevoNoPlaneadoId =
                        data.SolicitudRelevoNoPlaneadoId,

                    EmpleadoIdAsignado =
                        data.EmpleadoIdAsignado,

                    TipoCoberturaClave =
                        "SUSTITUTO"
                };

            return await CrearAsignacionAsync(
                asignacion,
                numeroUsuario,
                accessToken,
                ct);
        }

        #endregion


        #region SUPERVISOR SE ASIGNA

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarseSupervisorAsync(
                AsignarseSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

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

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El request es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (data.SolicitudRelevoNoPlaneadoId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "SolicitudRelevoNoPlaneadoId es inválido.";
                    response.data = null;

                    return response;
                }

                int empleadoId;

                using (var conn = _data.CrearConexion())
                {
                    await conn.OpenAsync(ct);

                    EmpleadoRelevoDto? supervisor =
                        await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                            conn,
                            numeroUsuario.Trim(),
                            ct);

                    if (supervisor == null)
                    {
                        response.isSuccess = false;
                        response.code = 404;
                        response.message =
                            "No existe un empleado relacionado con el supervisor autenticado.";
                        response.data = null;

                        return response;
                    }

                    empleadoId =
                        supervisor.EmpleadoId;
                }

                var asignacion =
                    new CrearAsignacionRelevoNoPlaneadoDto
                    {
                        SolicitudRelevoNoPlaneadoId =
                            data.SolicitudRelevoNoPlaneadoId,

                        EmpleadoIdAsignado =
                            empleadoId,

                        TipoCoberturaClave =
                            "SUPERVISOR"
                    };

                return await CrearAsignacionAsync(
                    asignacion,
                    numeroUsuario.Trim(),
                    accessToken,
                    ct);
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al asignar al supervisor como cobertura.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al asignar al supervisor como cobertura.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region CREAR PROPUESTA EXTENSION

        internal async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            CrearExtensionAsync(
                long solicitudRelevoNoPlaneadoId,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

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

                if (solicitudRelevoNoPlaneadoId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "SolicitudRelevoNoPlaneadoId es inválido.";
                    response.data = null;

                    return response;
                }

                int empleadoId;

                using (var conn = _data.CrearConexion())
                {
                    await conn.OpenAsync(ct);

                    EmpleadoRelevoDto? empleado =
                        await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                            conn,
                            numeroUsuario.Trim(),
                            ct);

                    if (empleado == null)
                    {
                        response.isSuccess = false;
                        response.code = 404;
                        response.message =
                            "No existe un empleado relacionado con el usuario autenticado.";
                        response.data = null;

                        return response;
                    }

                    empleadoId =
                        empleado.EmpleadoId;
                }

                var asignacion =
                    new CrearAsignacionRelevoNoPlaneadoDto
                    {
                        SolicitudRelevoNoPlaneadoId =
                            solicitudRelevoNoPlaneadoId,

                        EmpleadoIdAsignado =
                            empleadoId,

                        TipoCoberturaClave =
                            "EXTENSION"
                    };

                return await CrearAsignacionAsync(
                    asignacion,
                    numeroUsuario.Trim(),
                    accessToken,
                    ct);
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al crear la propuesta de extensión.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al crear la propuesta de extensión.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region AUTORIZAR ASIGNACION

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AutorizarAsignacionAsync(
                AutorizarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

            SqlTransaction? transaction = null;
            string? rutaFirmaSupervisor = null;

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

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El request es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (data.RelevoNoPlaneadoAsignacionId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "RelevoNoPlaneadoAsignacionId es inválido.";
                    response.data = null;

                    return response;
                }

                if (data.FirmaSupervisor == null ||
                    data.FirmaSupervisor.Length == 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "La firma del supervisor es obligatoria.";
                    response.data = null;

                    return response;
                }

                numeroUsuario = numeroUsuario.Trim();

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                // ============================================================
                // SUPERVISOR AUTENTICADO
                // ============================================================

                EmpleadoRelevoDto? supervisor =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct);

                if (supervisor == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe un empleado relacionado con el supervisor autenticado.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // PREVALIDACION
                // ============================================================

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await _data.ObtenerAsignacionAsync(
                        conn,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la propuesta de relevo no planeado.";
                    response.data = null;

                    return response;
                }

                string? tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct);

                if (!string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Esta propuesta no corresponde a una extensión de turno.";
                    response.data = null;

                    return response;
                }

                string? estatusAsignacion =
                    await _data.ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        asignacion.RelevoAsignacionEstatusId,
                        ct);

                if (!string.Equals(
                    estatusAsignacion,
                    ESTATUS_PENDIENTE_SUPERVISOR,
                    StringComparison.OrdinalIgnoreCase))
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La propuesta no se encuentra pendiente de autorización del supervisor.";
                    response.desc =
                        $"Estatus actual: {estatusAsignacion ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await _data.ObtenerSolicitudAsync(
                        conn,
                        asignacion.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la solicitud relacionada con la propuesta.";
                    response.data = null;

                    return response;
                }

                ServicioEmpleadoRelevoDto? servicioAfectado =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoAfectadoId,
                        ct);

                if (servicioAfectado == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la asignación de servicio relacionada con el relevo.";
                    response.data = null;

                    return response;
                }

                bool supervisorValido =
                    await _data.EsSupervisorServicioAsync(
                        conn,
                        supervisor.EmpleadoId,
                        servicioAfectado.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct);

                if (!supervisorValido)
                {
                    response.isSuccess = false;
                    response.code = 403;
                    response.message =
                        "El supervisor no tiene asignado este servicio.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // SUBIR FIRMA
                // ============================================================

                string operacionId =
                    $"asignacion-{asignacion.RelevoNoPlaneadoAsignacionId}";

                ResponseModel<string> uploadResponse =
                    await _fileRelevoService.UploadFileAsync(
                        data.FirmaSupervisor,
                        operacionId,
                        "firma-supervisor",
                        ct);

                if (!uploadResponse.isSuccess ||
                    string.IsNullOrWhiteSpace(uploadResponse.data))
                {
                    response.isSuccess = false;
                    response.code = uploadResponse.code;
                    response.message = uploadResponse.message;
                    response.desc = uploadResponse.desc;
                    response.data = null;

                    return response;
                }

                rutaFirmaSupervisor =
                    uploadResponse.data;

                // ============================================================
                // TRANSACCION
                // ============================================================

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                asignacion =
                    await _data.ObtenerAsignacionForUpdateAsync(
                        conn,
                        transaction,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaFirmaSupervisor,
                        ct);

                    rutaFirmaSupervisor = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "La propuesta de relevo ya no existe.";
                    response.data = null;

                    return response;
                }

                tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct,
                        transaction);

                estatusAsignacion =
                    await _data.ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        asignacion.RelevoAsignacionEstatusId,
                        ct,
                        transaction);

                if (!string.Equals(
                        tipoCobertura,
                        "EXTENSION",
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        estatusAsignacion,
                        ESTATUS_PENDIENTE_SUPERVISOR,
                        StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaFirmaSupervisor,
                        ct);

                    rutaFirmaSupervisor = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La propuesta cambió de estado antes de ser autorizada.";
                    response.data = null;

                    return response;
                }

                solicitud =
                    await _data.ObtenerSolicitudForUpdateAsync(
                        conn,
                        transaction,
                        asignacion.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaFirmaSupervisor,
                        ct);

                    rutaFirmaSupervisor = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "La solicitud relacionada ya no existe.";
                    response.data = null;

                    return response;
                }

                servicioAfectado =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (servicioAfectado == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaFirmaSupervisor,
                        ct);

                    rutaFirmaSupervisor = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "La asignación de servicio relacionada ya no existe.";
                    response.data = null;

                    return response;
                }

                supervisorValido =
                    await _data.EsSupervisorServicioAsync(
                        conn,
                        supervisor.EmpleadoId,
                        servicioAfectado.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct,
                        transaction);

                if (!supervisorValido)
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaFirmaSupervisor,
                        ct);

                    rutaFirmaSupervisor = null;

                    response.isSuccess = false;
                    response.code = 403;
                    response.message =
                        "El supervisor ya no tiene asignado este servicio.";
                    response.data = null;

                    return response;
                }

                int? estatusPendienteFirmaId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                        ct,
                        transaction);

                if (!estatusPendienteFirmaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus PENDIENTE_FIRMA_EMPLEADO.");
                }

                const string sqlUpdate = @"
UPDATE dbo.RelevoNoPlaneadoAsignacion
SET
    RelevoAsignacionEstatusId = @NuevoEstatusId,
    SupervisorEmpleadoIdAutoriza = @SupervisorEmpleadoId,
    RutaFirmaSupervisor = @RutaFirmaSupervisor,
    FechaHoraFirmaSupervisor = @FechaHoraFirmaSupervisor,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE RelevoNoPlaneadoAsignacionId = @AsignacionId
  AND RelevoAsignacionEstatusId = @EstatusAnteriorId
  AND SupervisorEmpleadoIdAutoriza IS NULL
  AND RutaFirmaSupervisor IS NULL
  AND FechaHoraFirmaSupervisor IS NULL;";

                int rows =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlUpdate,
                            new
                            {
                                NuevoEstatusId =
                                    estatusPendienteFirmaId.Value,

                                SupervisorEmpleadoId =
                                    supervisor.EmpleadoId,

                                RutaFirmaSupervisor =
                                    rutaFirmaSupervisor,

                                FechaHoraFirmaSupervisor =
                                    fechaActual,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario,

                                AsignacionId =
                                    asignacion.RelevoNoPlaneadoAsignacionId,

                                EstatusAnteriorId =
                                    asignacion.RelevoAsignacionEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rows != 1)
                {
                    throw new InvalidOperationException(
                        "La propuesta cambió antes de completar la autorización.");
                }

                asignacion.RelevoAsignacionEstatusId =
                    estatusPendienteFirmaId.Value;

                asignacion.SupervisorEmpleadoIdAutoriza =
                    supervisor.EmpleadoId;

                asignacion.RutaFirmaSupervisor =
                    rutaFirmaSupervisor;

                asignacion.FechaHoraFirmaSupervisor =
                    fechaActual;

                // ============================================================
                // COMMIT
                // ============================================================

                transaction.Commit();
                transaction = null;

                rutaFirmaSupervisor = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "La extensión fue autorizada correctamente.";
                response.desc =
                    "La propuesta quedó pendiente de firma y aceptación del empleado.";
                response.data =
                    asignacion;

                // ============================================================
                // NOTIFICAR EMPLEADO
                //
                // LA AUTORIZACION YA FUE COMMITTEADA.
                // ============================================================

                try
                {
                    EmpleadoRelevoDto? empleadoAsignado =
                        await _data.ObtenerEmpleadoAsync(
                            conn,
                            asignacion.EmpleadoIdAsignado,
                            ct);

                    if (empleadoAsignado == null ||
                        string.IsNullOrWhiteSpace(
                            empleadoAsignado.NumeroUsuario))
                    {
                        response.desc +=
                            " No fue posible identificar al empleado para enviar la notificación.";
                    }
                    else
                    {
                        var notificationResponse =
                            await _relevoNotificationFunctions
                                .NotificarExtensionPendienteFirmaEmpleadoAsync(
                                    empleadoAsignado.NumeroUsuario,
                                    servicioAfectado.ServicioId,
                                    servicioAfectado.NombreServicio,
                                    solicitud.SolicitudRelevoNoPlaneadoId,
                                    asignacion.RelevoNoPlaneadoAsignacionId,
                                    solicitud.FechaHoraInicioCobertura,
                                    solicitud.FechaHoraFinCobertura,
                                    asignacion.TextoResponsiva,
                                    accessToken,
                                    ct);

                        if (notificationResponse.isSuccess)
                        {
                            response.desc +=
                                " Se notificó al empleado.";
                        }
                        else
                        {
                            response.desc +=
                                " No fue posible notificar al empleado. " +
                                notificationResponse.message;
                        }
                    }
                }
                catch (Exception ex)
                {
                    response.desc +=
                        " La autorización se completó, pero ocurrió un error al enviar la notificación: " +
                        ex.Message;
                }

                return response;
            }
            catch (SqlException ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                await EliminarArchivoSeguroAsync(
                    rutaFirmaSupervisor,
                    ct);

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al autorizar la extensión.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                await EliminarArchivoSeguroAsync(
                    rutaFirmaSupervisor,
                    ct);

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al autorizar la extensión.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region FIRMAR RESPONSIVA

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            FirmarResponsivaAsync(
                FirmarResponsivaRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

            SqlTransaction? transaction = null;
            string? rutaFirmaAceptacion = null;

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

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El request es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (data.RelevoNoPlaneadoAsignacionId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "RelevoNoPlaneadoAsignacionId es inválido.";
                    response.data = null;

                    return response;
                }

                if (data.FirmaAceptacion == null ||
                    data.FirmaAceptacion.Length == 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "La firma de aceptación es obligatoria.";
                    response.data = null;

                    return response;
                }

                numeroUsuario = numeroUsuario.Trim();

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                // ============================================================
                // EMPLEADO AUTENTICADO
                // ============================================================

                EmpleadoRelevoDto? empleado =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct);

                if (empleado == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe un empleado relacionado con el usuario autenticado.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // PREVALIDACION
                // ============================================================

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await _data.ObtenerAsignacionAsync(
                        conn,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la propuesta de relevo.";
                    response.data = null;

                    return response;
                }

                if (asignacion.EmpleadoIdAsignado !=
                    empleado.EmpleadoId)
                {
                    response.isSuccess = false;
                    response.code = 403;
                    response.message =
                        "La responsiva únicamente puede ser firmada por el empleado asignado.";
                    response.data = null;

                    return response;
                }

                string? estatusAsignacion =
                    await _data.ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        asignacion.RelevoAsignacionEstatusId,
                        ct);

                if (!string.Equals(
                    estatusAsignacion,
                    ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La propuesta no se encuentra pendiente de firma.";
                    response.desc =
                        $"Estatus actual: {estatusAsignacion ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                string? tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct);

                if (string.IsNullOrWhiteSpace(tipoCobertura))
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        "No fue posible determinar el tipo de cobertura.";
                    response.data = null;

                    return response;
                }

                if (string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (!asignacion.SupervisorEmpleadoIdAutoriza.HasValue ||
                        string.IsNullOrWhiteSpace(
                            asignacion.RutaFirmaSupervisor) ||
                        !asignacion.FechaHoraFirmaSupervisor.HasValue)
                    {
                        response.isSuccess = false;
                        response.code = 409;
                        response.message =
                            "La extensión todavía no cuenta con autorización firmada del supervisor.";
                        response.data = null;

                        return response;
                    }
                }

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await _data.ObtenerSolicitudAsync(
                        conn,
                        asignacion.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la solicitud relacionada con la propuesta.";
                    response.data = null;

                    return response;
                }

                string? estatusSolicitud =
                    await _data.ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct);

                if (!string.Equals(
                    estatusSolicitud,
                    ESTATUS_EN_PROCESO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud de relevo no se encuentra en proceso.";
                    response.desc =
                        $"Estatus actual: {estatusSolicitud ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // SUBIR FIRMA
                // ============================================================

                string operacionId =
                    $"asignacion-{asignacion.RelevoNoPlaneadoAsignacionId}";

                ResponseModel<string> uploadResponse =
                    await _fileRelevoService.UploadFileAsync(
                        data.FirmaAceptacion,
                        operacionId,
                        "firma-aceptacion",
                        ct);

                if (!uploadResponse.isSuccess ||
                    string.IsNullOrWhiteSpace(uploadResponse.data))
                {
                    response.isSuccess = false;
                    response.code = uploadResponse.code;
                    response.message = uploadResponse.message;
                    response.desc = uploadResponse.desc;
                    response.data = null;

                    return response;
                }

                rutaFirmaAceptacion =
                    uploadResponse.data;

                // ============================================================
                // TRANSACCION
                // ============================================================

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                asignacion =
                    await _data.ObtenerAsignacionForUpdateAsync(
                        conn,
                        transaction,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    throw new InvalidOperationException(
                        "La propuesta dejó de existir antes de completar la firma.");
                }

                if (asignacion.EmpleadoIdAsignado !=
                    empleado.EmpleadoId)
                {
                    throw new InvalidOperationException(
                        "La propuesta cambió de empleado antes de completar la firma.");
                }

                estatusAsignacion =
                    await _data.ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        asignacion.RelevoAsignacionEstatusId,
                        ct,
                        transaction);

                if (!string.Equals(
                    estatusAsignacion,
                    ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "La propuesta cambió de estado antes de completar la firma.");
                }

                tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct,
                        transaction);

                if (string.IsNullOrWhiteSpace(tipoCobertura))
                {
                    throw new InvalidOperationException(
                        "No fue posible determinar el tipo de cobertura.");
                }

                solicitud =
                    await _data.ObtenerSolicitudForUpdateAsync(
                        conn,
                        transaction,
                        asignacion.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    throw new InvalidOperationException(
                        "No existe la solicitud relacionada con la propuesta.");
                }

                estatusSolicitud =
                    await _data.ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct,
                        transaction);

                if (!string.Equals(
                    estatusSolicitud,
                    ESTATUS_EN_PROCESO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "La solicitud cambió de estado antes de completar la firma.");
                }

                ServicioEmpleadoRelevoDto? asignacionAfectada =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (asignacionAfectada == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación de servicio que requiere cobertura.");
                }

                int? tipoAsignacionFaltaId =
                    await _data.ObtenerTipoAsignacionServicioIdAsync(
                        conn,
                        TIPO_ASIGNACION_FALTA,
                        ct,
                        transaction);

                if (!tipoAsignacionFaltaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el tipo de asignación FALTA.");
                }

                // ============================================================
                // SERVICIO EMPLEADO TEMPORAL
                // ============================================================

                long servicioEmpleadoTemporalId =
                    await _integracion
                        .CrearServicioEmpleadoTemporalAsync(
                            conn,
                            transaction,
                            solicitud,
                            asignacionAfectada,
                            empleado,
                            tipoAsignacionFaltaId.Value,
                            fechaActual,
                            numeroUsuario,
                            ct);

                // ============================================================
                // EXTENSION
                // ============================================================

                if (string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await _integracion
                        .CrearAsistenciaExtensionAsync(
                            conn,
                            transaction,
                            solicitud,
                            asignacionAfectada,
                            servicioEmpleadoTemporalId,
                            empleado,
                            fechaActual,
                            numeroUsuario,
                            ct);
                }

                // ============================================================
                // ASIGNACION -> ACEPTADA
                // ============================================================

                int? estatusAceptadaId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        ESTATUS_ACEPTADA,
                        ct,
                        transaction);

                if (!estatusAceptadaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus ACEPTADA.");
                }

                const string sqlAceptar = @"
UPDATE dbo.RelevoNoPlaneadoAsignacion
SET
    RelevoAsignacionEstatusId = @EstatusAceptadaId,
    RutaFirmaEmpleado = @RutaFirmaEmpleado,
    FechaHoraFirmaEmpleado = @FechaHoraFirmaEmpleado,
    ServicioEmpleadoTemporalId = @ServicioEmpleadoTemporalId,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE RelevoNoPlaneadoAsignacionId = @AsignacionId
  AND RelevoAsignacionEstatusId = @EstatusAnteriorId
  AND RutaFirmaEmpleado IS NULL
  AND FechaHoraFirmaEmpleado IS NULL
  AND ServicioEmpleadoTemporalId IS NULL;";

                int rowsAsignacion =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlAceptar,
                            new
                            {
                                EstatusAceptadaId =
                                    estatusAceptadaId.Value,

                                RutaFirmaEmpleado =
                                    rutaFirmaAceptacion,

                                FechaHoraFirmaEmpleado =
                                    fechaActual,

                                ServicioEmpleadoTemporalId =
                                    servicioEmpleadoTemporalId,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario,

                                AsignacionId =
                                    asignacion.RelevoNoPlaneadoAsignacionId,

                                EstatusAnteriorId =
                                    asignacion.RelevoAsignacionEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rowsAsignacion != 1)
                {
                    throw new InvalidOperationException(
                        "La propuesta cambió antes de completar la aceptación.");
                }

                // ============================================================
                // SOLICITUD -> CUBIERTO
                // ============================================================

                int? estatusCubiertoId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_CUBIERTO,
                        ct,
                        transaction);

                if (!estatusCubiertoId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus CUBIERTO.");
                }

                const string sqlCubrirSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @EstatusCubiertoId,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND RelevoNoPlaneadoEstatusId = @EstatusAnteriorId;";

                int rowsSolicitud =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlCubrirSolicitud,
                            new
                            {
                                EstatusCubiertoId =
                                    estatusCubiertoId.Value,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario,

                                SolicitudId =
                                    solicitud.SolicitudRelevoNoPlaneadoId,

                                EstatusAnteriorId =
                                    solicitud.RelevoNoPlaneadoEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rowsSolicitud != 1)
                {
                    throw new InvalidOperationException(
                        "La solicitud cambió antes de completar la cobertura.");
                }

                // ============================================================
                // COMMIT
                // ============================================================

                transaction.Commit();
                transaction = null;

                string rutaFirmaPersistida =
                    rutaFirmaAceptacion;

                rutaFirmaAceptacion = null;

                asignacion.RelevoAsignacionEstatusId =
                    estatusAceptadaId.Value;

                asignacion.RutaFirmaEmpleado =
                    rutaFirmaPersistida;

                asignacion.FechaHoraFirmaEmpleado =
                    fechaActual;

                asignacion.ServicioEmpleadoTemporalId =
                    servicioEmpleadoTemporalId;

                response.isSuccess = true;
                response.code = 200;

                if (string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    response.message =
                        "La extensión fue aceptada correctamente.";
                    response.desc =
                        "El turno original fue finalizado y se creó el turno de extensión.";
                }
                else
                {
                    response.message =
                        "La cobertura fue aceptada correctamente.";
                    response.desc =
                        "Se creó la asignación temporal. El empleado deberá realizar Check-In al presentarse en el servicio.";
                }

                response.data =
                    asignacion;

                // ============================================================
                // NOTIFICAR COBERTURA ACEPTADA
                //
                // TODO LO OPERATIVO YA ESTA COMMITTEADO.
                // ============================================================

                try
                {
                    var notificationResponse =
                        await _relevoNotificationFunctions
                            .NotificarCoberturaAceptadaAsync(
                                asignacionAfectada.ServicioId,
                                asignacionAfectada.NombreServicio,
                                solicitud.SolicitudRelevoNoPlaneadoId,
                                asignacion.RelevoNoPlaneadoAsignacionId,
                                servicioEmpleadoTemporalId,
                                tipoCobertura,
                                empleado.EmpleadoId,
                                empleado.NumeroUsuario,
                                empleado.NombreCompleto,
                                solicitud.FechaHoraInicioCobertura,
                                solicitud.FechaHoraFinCobertura,
                                accessToken,
                                ct);

                    if (notificationResponse.isSuccess)
                    {
                        response.desc +=
                            " Se notificó a los supervisores del servicio.";
                    }
                    else
                    {
                        response.desc +=
                            " No fue posible notificar a los supervisores. " +
                            notificationResponse.message;
                    }
                }
                catch (Exception ex)
                {
                    response.desc +=
                        " La cobertura fue registrada, pero ocurrió un error al enviar la notificación: " +
                        ex.Message;
                }

                return response;
            }
            catch (SqlException ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                await EliminarArchivoSeguroAsync(
                    rutaFirmaAceptacion,
                    ct);

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al aceptar la cobertura.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                await EliminarArchivoSeguroAsync(
                    rutaFirmaAceptacion,
                    ct);

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al aceptar la cobertura.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region RECHAZAR ASIGNACION EMPLEADO

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionAsync(
                RechazarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

            SqlTransaction? transaction = null;

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

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El request es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (data.RelevoNoPlaneadoAsignacionId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "RelevoNoPlaneadoAsignacionId es inválido.";
                    response.data = null;

                    return response;
                }

                if (string.IsNullOrWhiteSpace(
                    data.MotivoRechazo))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El motivo de rechazo es obligatorio.";
                    response.data = null;

                    return response;
                }

                numeroUsuario = numeroUsuario.Trim();

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                EmpleadoRelevoDto? empleado =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct,
                        transaction);

                if (empleado == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe un empleado relacionado con el usuario autenticado.";
                    response.data = null;

                    return response;
                }

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await _data.ObtenerAsignacionForUpdateAsync(
                        conn,
                        transaction,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la propuesta de relevo.";
                    response.data = null;

                    return response;
                }

                if (asignacion.EmpleadoIdAsignado !=
                    empleado.EmpleadoId)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 403;
                    response.message =
                        "La propuesta únicamente puede ser rechazada por el empleado asignado.";
                    response.data = null;

                    return response;
                }

                string? estatusAsignacion =
                    await _data.ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        asignacion.RelevoAsignacionEstatusId,
                        ct,
                        transaction);

                if (!string.Equals(
                    estatusAsignacion,
                    ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La propuesta no se encuentra pendiente de respuesta del empleado.";
                    response.desc =
                        $"Estatus actual: {estatusAsignacion ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await _data.ObtenerSolicitudForUpdateAsync(
                        conn,
                        transaction,
                        asignacion.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la solicitud relacionada con la propuesta.";
                    response.data = null;

                    return response;
                }

                string? estatusSolicitud =
                    await _data.ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct,
                        transaction);

                if (!string.Equals(
                    estatusSolicitud,
                    ESTATUS_EN_PROCESO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud de relevo ya no se encuentra en proceso.";
                    response.desc =
                        $"Estatus actual: {estatusSolicitud ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                string? tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct,
                        transaction);

                if (string.IsNullOrWhiteSpace(tipoCobertura))
                {
                    throw new InvalidOperationException(
                        "No fue posible determinar el tipo de cobertura.");
                }

                // ============================================================
                // EXTENSION RECHAZADA POR EMPLEADO -> CHECKOUT
                // ============================================================

                if (string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await _integracion
                        .CerrarAsistenciaPorRechazoExtensionAsync(
                            conn,
                            transaction,
                            solicitud,
                            empleado,
                            fechaActual,
                            ct);
                }

                int? estatusRechazadaId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        ESTATUS_RECHAZADA_EMPLEADO,
                        ct,
                        transaction);

                if (!estatusRechazadaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus RECHAZADA_EMPLEADO.");
                }

                const string sqlRechazar = @"
UPDATE dbo.RelevoNoPlaneadoAsignacion
SET
    RelevoAsignacionEstatusId = @NuevoEstatusId,
    MotivoRechazo = @MotivoRechazo,
    FechaHoraRechazo = @FechaHoraRechazo,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE RelevoNoPlaneadoAsignacionId = @AsignacionId
  AND RelevoAsignacionEstatusId = @EstatusAnteriorId
  AND ServicioEmpleadoTemporalId IS NULL;";

                int rowsAsignacion =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlRechazar,
                            new
                            {
                                NuevoEstatusId =
                                    estatusRechazadaId.Value,

                                MotivoRechazo =
                                    data.MotivoRechazo.Trim(),

                                FechaHoraRechazo =
                                    fechaActual,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario,

                                AsignacionId =
                                    asignacion.RelevoNoPlaneadoAsignacionId,

                                EstatusAnteriorId =
                                    asignacion.RelevoAsignacionEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rowsAsignacion != 1)
                {
                    throw new InvalidOperationException(
                        "La propuesta cambió antes de completar el rechazo.");
                }

                int? estatusPendienteAsignacionId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_ASIGNACION,
                        ct,
                        transaction);

                if (!estatusPendienteAsignacionId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus PENDIENTE_ASIGNACION.");
                }

                const string sqlSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @NuevoEstatusId,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND RelevoNoPlaneadoEstatusId = @EstatusAnteriorId;";

                int rowsSolicitud =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlSolicitud,
                            new
                            {
                                NuevoEstatusId =
                                    estatusPendienteAsignacionId.Value,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario,

                                SolicitudId =
                                    solicitud.SolicitudRelevoNoPlaneadoId,

                                EstatusAnteriorId =
                                    solicitud.RelevoNoPlaneadoEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rowsSolicitud != 1)
                {
                    throw new InvalidOperationException(
                        "La solicitud cambió antes de completar el rechazo.");
                }

                // ============================================================
                // COMMIT
                // ============================================================

                transaction.Commit();
                transaction = null;

                asignacion.RelevoAsignacionEstatusId =
                    estatusRechazadaId.Value;

                asignacion.MotivoRechazo =
                    data.MotivoRechazo.Trim();

                asignacion.FechaHoraRechazo =
                    fechaActual;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "La propuesta de cobertura fue rechazada.";

                response.desc =
                    string.Equals(
                        tipoCobertura,
                        "EXTENSION",
                        StringComparison.OrdinalIgnoreCase)
                        ? "Se registró el Check-Out del empleado y la solicitud quedó pendiente de una nueva asignación."
                        : "La solicitud quedó pendiente de una nueva asignación.";

                response.data =
                    asignacion;

                // ============================================================
                // NOTIFICAR NUEVA NECESIDAD DE COBERTURA
                // ============================================================

                try
                {
                    ServicioEmpleadoRelevoDto? servicioAfectado =
                        await _data.ObtenerServicioEmpleadoAsync(
                            conn,
                            solicitud.ServicioEmpleadoAfectadoId,
                            ct);

                    if (servicioAfectado == null)
                    {
                        response.desc +=
                            " No fue posible obtener el servicio para enviar la notificación.";
                    }
                    else
                    {
                        var notificationResponse =
                            await _relevoNotificationFunctions
                                .NotificarCoberturaRequeridaPorRechazoAsync(
                                    servicioAfectado.ServicioId,
                                    servicioAfectado.NombreServicio,
                                    solicitud.SolicitudRelevoNoPlaneadoId,
                                    asignacion.RelevoNoPlaneadoAsignacionId,
                                    tipoCobertura,
                                    "EMPLEADO",
                                    data.MotivoRechazo.Trim(),
                                    solicitud.FechaHoraInicioCobertura,
                                    solicitud.FechaHoraFinCobertura,
                                    accessToken,
                                    ct);

                        if (notificationResponse.isSuccess)
                        {
                            response.desc +=
                                " Se notificó a los supervisores que se requiere una nueva cobertura.";
                        }
                        else
                        {
                            response.desc +=
                                " No fue posible enviar la notificación de nueva cobertura. " +
                                notificationResponse.message;
                        }
                    }
                }
                catch (Exception ex)
                {
                    response.desc +=
                        " El rechazo fue registrado, pero ocurrió un error al enviar la notificación: " +
                        ex.Message;
                }

                return response;
            }
            catch (SqlException ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al rechazar la propuesta de cobertura.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al rechazar la propuesta de cobertura.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region RECHAZAR ASIGNACION SUPERVISOR

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionSupervisorAsync(
                RechazarAsignacionSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

            SqlTransaction? transaction = null;

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

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El request es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (data.RelevoNoPlaneadoAsignacionId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "RelevoNoPlaneadoAsignacionId es inválido.";
                    response.data = null;

                    return response;
                }

                if (string.IsNullOrWhiteSpace(
                    data.MotivoRechazo))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El motivo de rechazo es obligatorio.";
                    response.data = null;

                    return response;
                }

                numeroUsuario = numeroUsuario.Trim();

                using var conn =
                    _data.CrearConexion();

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                EmpleadoRelevoDto? supervisor =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct,
                        transaction);

                if (supervisor == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe un empleado relacionado con el supervisor autenticado.";
                    response.data = null;

                    return response;
                }

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await _data.ObtenerAsignacionForUpdateAsync(
                        conn,
                        transaction,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la propuesta de relevo.";
                    response.data = null;

                    return response;
                }

                string? tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct,
                        transaction);

                if (!string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El rechazo del supervisor únicamente aplica a propuestas de extensión.";
                    response.data = null;

                    return response;
                }

                string? estatusAsignacion =
                    await _data.ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        asignacion.RelevoAsignacionEstatusId,
                        ct,
                        transaction);

                if (!string.Equals(
                    estatusAsignacion,
                    ESTATUS_PENDIENTE_SUPERVISOR,
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La extensión no se encuentra pendiente de autorización del supervisor.";
                    response.desc =
                        $"Estatus actual: {estatusAsignacion ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await _data.ObtenerSolicitudForUpdateAsync(
                        conn,
                        transaction,
                        asignacion.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la solicitud relacionada con la propuesta.";
                    response.data = null;

                    return response;
                }

                string? estatusSolicitud =
                    await _data.ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct,
                        transaction);

                if (!string.Equals(
                    estatusSolicitud,
                    ESTATUS_EN_PROCESO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud ya no se encuentra en proceso.";
                    response.desc =
                        $"Estatus actual: {estatusSolicitud ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                ServicioEmpleadoRelevoDto? servicioAfectado =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (servicioAfectado == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la asignación de servicio relacionada con la solicitud.";
                    response.data = null;

                    return response;
                }

                bool puedeAdministrarServicio =
                    await _data.EsSupervisorServicioAsync(
                        conn,
                        supervisor.EmpleadoId,
                        servicioAfectado.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct,
                        transaction);

                if (!puedeAdministrarServicio)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 403;
                    response.message =
                        "El supervisor no tiene asignado este servicio.";
                    response.data = null;

                    return response;
                }

                int? estatusRechazadaId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        ESTATUS_RECHAZADA_SUPERVISOR,
                        ct,
                        transaction);

                if (!estatusRechazadaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus RECHAZADA_SUPERVISOR.");
                }

                // ============================================================
                // EL SUPERVISOR NO REALIZA CHECK-OUT AQUI
                // ============================================================

                const string sqlRechazar = @"
UPDATE dbo.RelevoNoPlaneadoAsignacion
SET
    RelevoAsignacionEstatusId = @NuevoEstatusId,
    MotivoRechazo = @MotivoRechazo,
    FechaHoraRechazo = @FechaHoraRechazo,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE RelevoNoPlaneadoAsignacionId = @AsignacionId
  AND RelevoAsignacionEstatusId = @EstatusAnteriorId
  AND SupervisorEmpleadoIdAutoriza IS NULL
  AND RutaFirmaSupervisor IS NULL
  AND FechaHoraFirmaSupervisor IS NULL
  AND ServicioEmpleadoTemporalId IS NULL;";

                int rowsAsignacion =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlRechazar,
                            new
                            {
                                NuevoEstatusId =
                                    estatusRechazadaId.Value,

                                MotivoRechazo =
                                    data.MotivoRechazo.Trim(),

                                FechaHoraRechazo =
                                    fechaActual,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario,

                                AsignacionId =
                                    asignacion.RelevoNoPlaneadoAsignacionId,

                                EstatusAnteriorId =
                                    asignacion.RelevoAsignacionEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rowsAsignacion != 1)
                {
                    throw new InvalidOperationException(
                        "La propuesta cambió antes de completar el rechazo.");
                }

                int? estatusPendienteAsignacionId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_ASIGNACION,
                        ct,
                        transaction);

                if (!estatusPendienteAsignacionId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus PENDIENTE_ASIGNACION.");
                }

                const string sqlSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @NuevoEstatusId,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND RelevoNoPlaneadoEstatusId = @EstatusAnteriorId;";

                int rowsSolicitud =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlSolicitud,
                            new
                            {
                                NuevoEstatusId =
                                    estatusPendienteAsignacionId.Value,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario,

                                SolicitudId =
                                    solicitud.SolicitudRelevoNoPlaneadoId,

                                EstatusAnteriorId =
                                    solicitud.RelevoNoPlaneadoEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rowsSolicitud != 1)
                {
                    throw new InvalidOperationException(
                        "La solicitud cambió antes de completar el rechazo.");
                }

                // ============================================================
                // COMMIT
                // ============================================================

                transaction.Commit();
                transaction = null;

                asignacion.RelevoAsignacionEstatusId =
                    estatusRechazadaId.Value;

                asignacion.MotivoRechazo =
                    data.MotivoRechazo.Trim();

                asignacion.FechaHoraRechazo =
                    fechaActual;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "La extensión fue rechazada por el supervisor.";
                response.desc =
                    "La solicitud quedó pendiente de una nueva asignación de cobertura.";
                response.data =
                    asignacion;

                // ============================================================
                // NOTIFICAR NUEVA NECESIDAD DE COBERTURA
                // ============================================================

                try
                {
                    var notificationResponse =
                        await _relevoNotificationFunctions
                            .NotificarCoberturaRequeridaPorRechazoAsync(
                                servicioAfectado.ServicioId,
                                servicioAfectado.NombreServicio,
                                solicitud.SolicitudRelevoNoPlaneadoId,
                                asignacion.RelevoNoPlaneadoAsignacionId,
                                tipoCobertura!,
                                "SUPERVISOR",
                                data.MotivoRechazo.Trim(),
                                solicitud.FechaHoraInicioCobertura,
                                solicitud.FechaHoraFinCobertura,
                                accessToken,
                                ct);

                    if (notificationResponse.isSuccess)
                    {
                        response.desc +=
                            " Se notificó a los supervisores que se requiere una nueva cobertura.";
                    }
                    else
                    {
                        response.desc +=
                            " No fue posible enviar la notificación de nueva cobertura. " +
                            notificationResponse.message;
                    }
                }
                catch (Exception ex)
                {
                    response.desc +=
                        " El rechazo fue registrado, pero ocurrió un error al enviar la notificación: " +
                        ex.Message;
                }

                return response;
            }
            catch (SqlException ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al rechazar la extensión.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al rechazar la extensión.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region VALIDACION ASIGNACION

        private string? ValidarCrearAsignacion(
            CrearAsignacionRelevoNoPlaneadoDto data)
        {
            if (data == null)
            {
                return
                    "La información de la asignación es obligatoria.";
            }

            if (data.SolicitudRelevoNoPlaneadoId <= 0)
            {
                return
                    "SolicitudRelevoNoPlaneadoId es inválido.";
            }

            if (data.EmpleadoIdAsignado <= 0)
            {
                return
                    "EmpleadoIdAsignado es inválido.";
            }

            if (string.IsNullOrWhiteSpace(
                data.TipoCoberturaClave))
            {
                return
                    "El tipo de cobertura es obligatorio.";
            }

            string tipo =
                data.TipoCoberturaClave
                    .Trim()
                    .ToUpperInvariant();

            if (!TIPOS_COBERTURA_VALIDOS.Contains(tipo))
            {
                return
                    "El tipo de cobertura no es válido.";
            }

            return null;
        }

        #endregion


        #region REGLAS TIPO COBERTURA

        private async Task<string?>
            ValidarReglasTipoCoberturaAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                SolicitudRelevoNoPlaneadoDto solicitud,
                ServicioEmpleadoRelevoDto asignacionAfectada,
                EmpleadoRelevoDto empleadoAsignado,
                string tipoCoberturaClave,
                string numeroUsuario,
                CancellationToken ct)
        {
            if (tipoCoberturaClave == "EXTENSION")
            {
                if (!solicitud.ServicioEmpleadoSalienteId.HasValue)
                {
                    return
                        "Una extensión requiere una asignación saliente.";
                }

                ServicioEmpleadoRelevoDto? asignacionSaliente =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoSalienteId.Value,
                        ct,
                        transaction);

                if (asignacionSaliente == null)
                {
                    return
                        "No existe la asignación saliente relacionada con la extensión.";
                }

                if (asignacionSaliente.EmpleadoId !=
                    empleadoAsignado.EmpleadoId)
                {
                    return
                        "La extensión debe asignarse al empleado que actualmente se encuentra cubriendo el servicio.";
                }

                return null;
            }

            if (empleadoAsignado.EmpleadoId ==
                asignacionAfectada.EmpleadoId)
            {
                return
                    "El empleado cuya asignación requiere relevo no puede ser propuesto como su propio sustituto.";
            }

            if (tipoCoberturaClave == "SUPERVISOR")
            {
                EmpleadoRelevoDto? empleadoActual =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct,
                        transaction);

                if (empleadoActual == null)
                {
                    return
                        "No existe un empleado relacionado con el supervisor autenticado.";
                }

                if (empleadoActual.EmpleadoId !=
                    empleadoAsignado.EmpleadoId)
                {
                    return
                        "Una cobertura tipo SUPERVISOR únicamente puede asignarse al propio supervisor autenticado.";
                }
            }

            if (tipoCoberturaClave == "SUSTITUTO" ||
                tipoCoberturaClave == "SUPERVISOR")
            {
                EmpleadoRelevoDto? supervisorActual =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct,
                        transaction);

                if (supervisorActual == null)
                {
                    return
                        "No existe un empleado relacionado con el supervisor autenticado.";
                }

                bool puedeAdministrarServicio =
                    await _data.EsSupervisorServicioAsync(
                        conn,
                        supervisorActual.EmpleadoId,
                        asignacionAfectada.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct,
                        transaction);

                if (!puedeAdministrarServicio)
                {
                    return
                        "El supervisor no tiene asignado el servicio que requiere cobertura.";
                }
            }

            return null;
        }

        #endregion


        #region RESPONSIVA

        private string GenerarTextoResponsiva(
            string nombreEmpleado,
            string nombreServicio,
            DateTime fechaHoraInicio,
            DateTime fechaHoraFin)
        {
            string inicio =
                fechaHoraInicio.ToString("dd/MM/yyyy HH:mm");

            string fin =
                fechaHoraFin.ToString("dd/MM/yyyy HH:mm");

            return
                $"Yo, {nombreEmpleado.Trim()}, acepto permanecer en el servicio " +
                $"{nombreServicio.Trim()} por necesidades operativas de manera voluntaria " +
                $"y eventual, sin que constituya jornada habitual. " +
                $"Cubriendo {inicio} a {fin}.";
        }

        #endregion


        #region LIMPIEZA R2

        private async Task EliminarArchivoSeguroAsync(
            string? key,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            try
            {
                await _fileRelevoService.DeleteFileAsync(
                    key,
                    ct);
            }
            catch
            {
                // No sustituir el error original
                // por un error de limpieza de R2.
            }
        }

        #endregion
    }
}