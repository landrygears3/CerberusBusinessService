using CerberusBusinessService.Functions.Notificaciones;
using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Asistencias;
using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Relevos
{
    public class RelevoFlexibleFunctions
    {
        #region CONSTANTES

        private const int ASISTENCIA_EN_TURNO = 1;
        private const int ASISTENCIA_FINALIZADA = 2;

        private const string ESTATUS_PENDIENTE_ASIGNACION =
            "PENDIENTE_ASIGNACION";

        private const string ESTATUS_EN_PROCESO =
            "EN_PROCESO";

        private const string ESTATUS_CUBIERTO =
            "CUBIERTO";

        private const string ESTATUS_CANCELADO =
            "CANCELADO";

        private const string ESTATUS_PENDIENTE_SUPERVISOR =
            "PENDIENTE_SUPERVISOR";

        private const string ESTATUS_PENDIENTE_FIRMA_EMPLEADO =
            "PENDIENTE_FIRMA_EMPLEADO";

        private const string ESTATUS_ACEPTADA =
            "ACEPTADA";

        private const string ESTATUS_RECHAZADA_SUPERVISOR =
            "RECHAZADA_SUPERVISOR";

        private const string ESTATUS_RECHAZADA_EMPLEADO =
            "RECHAZADA_EMPLEADO";

        private const string ESTATUS_ASIGNACION_CANCELADA =
            "CANCELADA";

        private const string TIPO_ASIGNACION_FALTA =
            "FALTA";

        #endregion

        #region PROPIEDADES

        private readonly RelevoNoPlaneadoDataService _data;
        private readonly FileRelevoNoPlaneadoService _fileRelevoService;
        private readonly RelevoNotificationFunctions _relevoNotificationFunctions;
        private readonly ServicioNotificationFunctions _servicioNotificationFunctions;

        #endregion

        #region CONSTRUCTOR

        public RelevoFlexibleFunctions(
            RelevoNoPlaneadoDataService data,
            FileRelevoNoPlaneadoService fileRelevoService,
            RelevoNotificationFunctions relevoNotificationFunctions,
            ServicioNotificationFunctions servicioNotificationFunctions)
        {
            _data = data;
            _fileRelevoService = fileRelevoService;
            _relevoNotificationFunctions = relevoNotificationFunctions;
            _servicioNotificationFunctions = servicioNotificationFunctions;
        }

        #endregion

        #region IDENTIFICAR FLUJO FLEXIBLE

        public async Task<bool> EsSolicitudSinAfectadoAsync(
            long solicitudId,
            CancellationToken ct)
        {
            if (solicitudId <= 0)
            {
                return false;
            }

            using var conn = _data.CrearConexion();
            await conn.OpenAsync(ct);

            const string sql = @"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM dbo.SolicitudRelevoNoPlaneado
    WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
      AND ServicioEmpleadoAfectadoId IS NULL
)
THEN CAST(1 AS BIT)
ELSE CAST(0 AS BIT)
END;";

            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new { SolicitudId = solicitudId },
                    cancellationToken: ct));
        }

        public async Task<bool> EsAsignacionSinAfectadoAsync(
            long asignacionId,
            CancellationToken ct)
        {
            if (asignacionId <= 0)
            {
                return false;
            }

            using var conn = _data.CrearConexion();
            await conn.OpenAsync(ct);

            const string sql = @"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM dbo.RelevoNoPlaneadoAsignacion A
    INNER JOIN dbo.SolicitudRelevoNoPlaneado SR
        ON SR.SolicitudRelevoNoPlaneadoId =
           A.SolicitudRelevoNoPlaneadoId
    WHERE A.RelevoNoPlaneadoAsignacionId = @AsignacionId
      AND SR.ServicioEmpleadoAfectadoId IS NULL
)
THEN CAST(1 AS BIT)
ELSE CAST(0 AS BIT)
END;";

            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new { AsignacionId = asignacionId },
                    cancellationToken: ct));
        }

        #endregion

        #region CREAR SOLICITUD DESDE ASISTENCIA SIN AFECTADO

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudDesdeAsistenciaSinAfectadoAsync(
                AsistenciaActivaCheckOutDto asistencia,
                DateTime fechaHoraInicioCobertura,
                DateTime fechaHoraFinCobertura,
                string motivoRelevo,
                string? motivoNoPermanencia,
                IFormFile fotoEvidencia,
                bool registrarAbandono,
                bool realizarCheckOut,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<SolicitudRelevoNoPlaneadoDto>();

            SqlTransaction? transaction = null;
            string? rutaEvidencia = null;

            try
            {
                if (asistencia == null ||
                    asistencia.AsistenciaId <= 0 ||
                    asistencia.ServicioEmpleadoId <= 0)
                {
                    return ErrorSolicitud(
                        400,
                        "La asistencia activa es inválida.");
                }

                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    return ErrorSolicitud(
                        401,
                        "No fue posible identificar al empleado.");
                }

                if (fotoEvidencia == null ||
                    fotoEvidencia.Length == 0)
                {
                    return ErrorSolicitud(
                        400,
                        "La fotografía de evidencia es obligatoria.");
                }

                if (fechaHoraFinCobertura <=
                    fechaHoraInicioCobertura)
                {
                    return ErrorSolicitud(
                        400,
                        "La fecha de fin de cobertura debe ser mayor a la fecha de inicio.");
                }

                if (string.IsNullOrWhiteSpace(motivoRelevo))
                {
                    return ErrorSolicitud(
                        400,
                        "El motivo del relevo es obligatorio.");
                }

                numeroUsuario = numeroUsuario.Trim();

                string operacionId =
                    Guid.NewGuid().ToString("N");

                ResponseModel<string> upload =
                    await _fileRelevoService.UploadFileAsync(
                        fotoEvidencia,
                        operacionId,
                        "evidencia",
                        ct);

                if (!upload.isSuccess ||
                    string.IsNullOrWhiteSpace(upload.data))
                {
                    return ErrorSolicitud(
                        upload.code,
                        upload.message,
                        upload.desc);
                }

                rutaEvidencia = upload.data;

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                ServicioEmpleadoRelevoDto? saliente =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        asistencia.ServicioEmpleadoId,
                        ct,
                        transaction);

                if (saliente == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación del empleado saliente.");
                }

                if (saliente.ServicioId != asistencia.ServicioId)
                {
                    throw new InvalidOperationException(
                        "La asistencia y la asignación saliente pertenecen a servicios distintos.");
                }

                bool existeSolicitud =
                    await ExisteSolicitudFlexibleActivaAsync(
                        conn,
                        transaction,
                        asistencia.ServicioEmpleadoId,
                        fechaActual,
                        ct);

                if (existeSolicitud)
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaEvidencia,
                        ct);

                    rutaEvidencia = null;

                    return ErrorSolicitud(
                        409,
                        "Ya existe una solicitud de cobertura activa para esta asistencia.");
                }

                int? origenId =
                    await _data.ObtenerOrigenIdAsync(
                        conn,
                        "ASISTENCIA",
                        ct,
                        transaction);

                int? estatusId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_ASIGNACION,
                        ct,
                        transaction);

                if (!origenId.HasValue ||
                    !estatusId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No están configurados los catálogos requeridos para crear la cobertura.");
                }

                const string sqlSolicitud = @"
INSERT INTO dbo.SolicitudRelevoNoPlaneado
(
    ServicioEmpleadoAfectadoId,
    ServicioEmpleadoSalienteId,
    RelevoNoPlaneadoOrigenId,
    RelevoNoPlaneadoEstatusId,
    FechaHoraInicioCobertura,
    FechaHoraFinCobertura,
    MotivoRelevo,
    MotivoNoPermanencia,
    RutaFotoEvidencia,
    FechaRegistro,
    UsuarioRegistro
)
OUTPUT INSERTED.SolicitudRelevoNoPlaneadoId
VALUES
(
    NULL,
    @ServicioEmpleadoSalienteId,
    @OrigenId,
    @EstatusId,
    @FechaHoraInicioCobertura,
    @FechaHoraFinCobertura,
    @MotivoRelevo,
    @MotivoNoPermanencia,
    @RutaFotoEvidencia,
    @FechaRegistro,
    @UsuarioRegistro
);";

                long solicitudId =
                    await conn.ExecuteScalarAsync<long>(
                        new CommandDefinition(
                            sqlSolicitud,
                            new
                            {
                                ServicioEmpleadoSalienteId =
                                    asistencia.ServicioEmpleadoId,
                                OrigenId = origenId.Value,
                                EstatusId = estatusId.Value,
                                FechaHoraInicioCobertura =
                                    fechaHoraInicioCobertura,
                                FechaHoraFinCobertura =
                                    fechaHoraFinCobertura,
                                MotivoRelevo =
                                    motivoRelevo.Trim(),
                                MotivoNoPermanencia =
                                    string.IsNullOrWhiteSpace(
                                        motivoNoPermanencia)
                                        ? null
                                        : motivoNoPermanencia.Trim(),
                                RutaFotoEvidencia =
                                    rutaEvidencia,
                                FechaRegistro = fechaActual,
                                UsuarioRegistro = numeroUsuario
                            },
                            transaction,
                            cancellationToken: ct));

                if (registrarAbandono)
                {
                    if (string.IsNullOrWhiteSpace(
                        motivoNoPermanencia))
                    {
                        throw new InvalidOperationException(
                            "El motivo del abandono es obligatorio.");
                    }

                    await RegistrarIncidenciaAbandonoAsync(
                        conn,
                        transaction,
                        asistencia,
                        numeroUsuario,
                        motivoNoPermanencia.Trim(),
                        fechaActual,
                        ct);
                }

                if (realizarCheckOut)
                {
                    await RealizarCheckOutAsync(
                        conn,
                        transaction,
                        asistencia,
                        numeroUsuario,
                        fechaActual,
                        ct);
                }

                transaction.Commit();
                transaction = null;

                string rutaPersistida = rutaEvidencia;
                rutaEvidencia = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    realizarCheckOut
                        ? "Se creó la solicitud de cobertura y se registró el Check-Out."
                        : "Se creó la solicitud de cobertura correctamente.";
                response.desc =
                    "La cobertura no tiene una asignación de relevo afectada.";

                response.data =
                    new SolicitudRelevoNoPlaneadoDto
                    {
                        SolicitudRelevoNoPlaneadoId =
                            solicitudId,

                        ServicioEmpleadoAfectadoId =
                            0,

                        ServicioEmpleadoSalienteId =
                            asistencia.ServicioEmpleadoId,

                        RelevoNoPlaneadoOrigenId =
                            origenId.Value,

                        RelevoNoPlaneadoEstatusId =
                            estatusId.Value,

                        FechaHoraInicioCobertura =
                            fechaHoraInicioCobertura,

                        FechaHoraFinCobertura =
                            fechaHoraFinCobertura,

                        MotivoRelevo =
                            motivoRelevo.Trim(),

                        MotivoNoPermanencia =
                            string.IsNullOrWhiteSpace(
                                motivoNoPermanencia)
                                ? null
                                : motivoNoPermanencia.Trim(),

                        RutaFotoEvidencia =
                            rutaPersistida,

                        FechaRegistro =
                            fechaActual,

                        UsuarioRegistro =
                            numeroUsuario
                    };

                if (realizarCheckOut)
                {
                    await NotificarCoberturaRequeridaAsync(
                        saliente,
                        solicitudId,
                        fechaHoraInicioCobertura,
                        fechaHoraFinCobertura,
                        motivoRelevo,
                        accessToken,
                        ct,
                        response);
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
                    rutaEvidencia,
                    ct);

                return ErrorSolicitud(
                    500,
                    "Error SQL al crear la solicitud de cobertura.",
                    ex.Message);
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
                    rutaEvidencia,
                    ct);

                return ErrorSolicitud(
                    500,
                    "Error al crear la solicitud de cobertura.",
                    ex.Message);
            }
        }

        #endregion

        #region CREAR EXTENSION

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            CrearExtensionAsync(
                long solicitudId,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                return ErrorAsignacion(
                    401,
                    "No fue posible identificar al empleado.");
            }

            using var conn = _data.CrearConexion();
            await conn.OpenAsync(ct);

            EmpleadoRelevoDto? empleado =
                await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                    conn,
                    numeroUsuario.Trim(),
                    ct);

            if (empleado == null)
            {
                return ErrorAsignacion(
                    404,
                    "No existe un empleado relacionado con el usuario autenticado.");
            }

            return await CrearAsignacionFlexibleAsync(
                solicitudId,
                empleado.EmpleadoId,
                "EXTENSION",
                numeroUsuario.Trim(),
                accessToken,
                ct);
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
            if (data == null ||
                data.SolicitudRelevoNoPlaneadoId <= 0 ||
                data.EmpleadoIdAsignado <= 0)
            {
                return ErrorAsignacion(
                    400,
                    "La solicitud y el empleado asignado son obligatorios.");
            }

            return await CrearAsignacionFlexibleAsync(
                data.SolicitudRelevoNoPlaneadoId,
                data.EmpleadoIdAsignado,
                "SUSTITUTO",
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
            if (data == null ||
                data.SolicitudRelevoNoPlaneadoId <= 0)
            {
                return ErrorAsignacion(
                    400,
                    "SolicitudRelevoNoPlaneadoId es inválido.");
            }

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                return ErrorAsignacion(
                    401,
                    "No fue posible identificar al supervisor.");
            }

            using var conn = _data.CrearConexion();
            await conn.OpenAsync(ct);

            EmpleadoRelevoDto? supervisor =
                await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                    conn,
                    numeroUsuario.Trim(),
                    ct);

            if (supervisor == null)
            {
                return ErrorAsignacion(
                    404,
                    "No existe un empleado relacionado con el supervisor autenticado.");
            }

            return await CrearAsignacionFlexibleAsync(
                data.SolicitudRelevoNoPlaneadoId,
                supervisor.EmpleadoId,
                "SUPERVISOR",
                numeroUsuario.Trim(),
                accessToken,
                ct);
        }

        #endregion

        #region CREAR ASIGNACION FLEXIBLE

        private async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            CrearAsignacionFlexibleAsync(
                long solicitudId,
                int empleadoIdAsignado,
                string tipoCoberturaClave,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

            SqlTransaction? transaction = null;

            try
            {
                if (solicitudId <= 0 ||
                    empleadoIdAsignado <= 0)
                {
                    return ErrorAsignacion(
                        400,
                        "La solicitud y el empleado asignado son obligatorios.");
                }

                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    return ErrorAsignacion(
                        401,
                        "No fue posible identificar al usuario.");
                }

                numeroUsuario = numeroUsuario.Trim();
                tipoCoberturaClave =
                    tipoCoberturaClave.Trim().ToUpperInvariant();

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await _data.ObtenerSolicitudForUpdateAsync(
                        conn,
                        transaction,
                        solicitudId,
                        ct);

                if (solicitud == null ||
                    !await EsSolicitudSinAfectadoAsync(
                        conn,
                        transaction,
                        solicitudId,
                        ct))
                {
                    transaction.Rollback();
                    transaction = null;

                    return ErrorAsignacion(
                        404,
                        "No existe la solicitud de cobertura sin relevo afectado.");
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

                    return ErrorAsignacion(
                        409,
                        "La solicitud no se encuentra pendiente de asignación.",
                        $"Estatus actual: {estatusSolicitud ?? "DESCONOCIDO"}.");
                }

                bool existeAsignacion =
                    await _data.ExisteAsignacionActivaAsync(
                        conn,
                        transaction,
                        solicitudId,
                        ct);

                if (existeAsignacion)
                {
                    transaction.Rollback();
                    transaction = null;

                    return ErrorAsignacion(
                        409,
                        "La solicitud ya tiene una propuesta de cobertura activa.");
                }

                ServicioEmpleadoRelevoDto? contexto =
                    await ObtenerContextoServicioAsync(
                        conn,
                        transaction,
                        solicitud,
                        ct);

                if (contexto == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación saliente que define el servicio de la cobertura.");
                }

                EmpleadoRelevoDto? empleadoAsignado =
                    await _data.ObtenerEmpleadoAsync(
                        conn,
                        empleadoIdAsignado,
                        ct,
                        transaction);

                if (empleadoAsignado == null ||
                    string.IsNullOrWhiteSpace(
                        empleadoAsignado.NumeroUsuario))
                {
                    throw new InvalidOperationException(
                        "El empleado propuesto no existe o no tiene NumeroUsuario.");
                }

                if (tipoCoberturaClave == "EXTENSION")
                {
                    if (contexto.EmpleadoId !=
                        empleadoAsignado.EmpleadoId)
                    {
                        transaction.Rollback();
                        transaction = null;

                        return ErrorAsignacion(
                            409,
                            "La extensión debe asignarse al empleado que actualmente cubre el servicio.");
                    }
                }
                else
                {
                    if (contexto.EmpleadoId ==
                        empleadoAsignado.EmpleadoId)
                    {
                        transaction.Rollback();
                        transaction = null;

                        return ErrorAsignacion(
                            409,
                            "El empleado saliente no puede ser propuesto como su propio sustituto.");
                    }

                    EmpleadoRelevoDto? supervisorActual =
                        await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                            conn,
                            numeroUsuario,
                            ct,
                            transaction);

                    if (supervisorActual == null)
                    {
                        transaction.Rollback();
                        transaction = null;

                        return ErrorAsignacion(
                            404,
                            "No existe un empleado relacionado con el supervisor autenticado.");
                    }

                    if (tipoCoberturaClave == "SUPERVISOR" &&
                        supervisorActual.EmpleadoId !=
                        empleadoAsignado.EmpleadoId)
                    {
                        transaction.Rollback();
                        transaction = null;

                        return ErrorAsignacion(
                            409,
                            "Una cobertura tipo SUPERVISOR únicamente puede asignarse al propio supervisor autenticado.");
                    }

                    bool supervisorValido =
                        await _data.EsSupervisorServicioAsync(
                            conn,
                            supervisorActual.EmpleadoId,
                            contexto.ServicioId,
                            solicitud.FechaHoraInicioCobertura.Date,
                            ct,
                            transaction);

                    if (!supervisorValido)
                    {
                        transaction.Rollback();
                        transaction = null;

                        return ErrorAsignacion(
                            403,
                            "El supervisor no tiene asignado el servicio que requiere cobertura.");
                    }
                }

                int? tipoCoberturaId =
                    await _data.ObtenerTipoCoberturaIdAsync(
                        conn,
                        tipoCoberturaClave,
                        ct,
                        transaction);

                string estatusInicial =
                    tipoCoberturaClave == "EXTENSION"
                        ? ESTATUS_PENDIENTE_SUPERVISOR
                        : ESTATUS_PENDIENTE_FIRMA_EMPLEADO;

                int? estatusAsignacionId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        estatusInicial,
                        ct,
                        transaction);

                int? estatusEnProcesoId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_EN_PROCESO,
                        ct,
                        transaction);

                if (!tipoCoberturaId.HasValue ||
                    !estatusAsignacionId.HasValue ||
                    !estatusEnProcesoId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No están configurados los catálogos requeridos para la cobertura.");
                }

                string textoResponsiva =
                    GenerarTextoResponsiva(
                        empleadoAsignado.NombreCompleto,
                        contexto.NombreServicio,
                        solicitud.FechaHoraInicioCobertura,
                        solicitud.FechaHoraFinCobertura);

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
    @SolicitudId,
    @EmpleadoIdAsignado,
    @TipoCoberturaId,
    @EstatusAsignacionId,
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
                                SolicitudId = solicitudId,
                                EmpleadoIdAsignado = empleadoIdAsignado,
                                TipoCoberturaId = tipoCoberturaId.Value,
                                EstatusAsignacionId =
                                    estatusAsignacionId.Value,
                                TextoResponsiva = textoResponsiva,
                                FechaAsignacion = fechaActual,
                                UsuarioAsignacion = numeroUsuario
                            },
                            transaction,
                            cancellationToken: ct));

                const string sqlSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @NuevoEstatusId,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND RelevoNoPlaneadoEstatusId = @EstatusAnteriorId;";

                int rows =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlSolicitud,
                            new
                            {
                                NuevoEstatusId =
                                    estatusEnProcesoId.Value,
                                FechaModificacion = fechaActual,
                                UsuarioModificacion = numeroUsuario,
                                SolicitudId = solicitudId,
                                EstatusAnteriorId =
                                    solicitud.RelevoNoPlaneadoEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rows != 1)
                {
                    throw new InvalidOperationException(
                        "La solicitud cambió antes de crear la propuesta de cobertura.");
                }

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Propuesta de cobertura creada correctamente.";
                response.desc =
                    tipoCoberturaClave == "EXTENSION"
                        ? "La propuesta quedó pendiente de autorización del supervisor."
                        : "La propuesta quedó pendiente de firma del empleado asignado.";

                response.data =
                    new RelevoNoPlaneadoAsignacionDto
                    {
                        RelevoNoPlaneadoAsignacionId =
                            asignacionId,
                        SolicitudRelevoNoPlaneadoId =
                            solicitudId,
                        EmpleadoIdAsignado =
                            empleadoAsignado.EmpleadoId,
                        RelevoTipoCoberturaId =
                            tipoCoberturaId.Value,
                        RelevoAsignacionEstatusId =
                            estatusAsignacionId.Value,
                        TextoResponsiva =
                            textoResponsiva,
                        FechaAsignacion =
                            fechaActual,
                        UsuarioAsignacion =
                            numeroUsuario
                    };

                try
                {
                    if (tipoCoberturaClave == "EXTENSION")
                    {
                        var notification =
                            await _relevoNotificationFunctions
                                .NotificarExtensionPendienteAutorizacionAsync(
                                    contexto.ServicioId,
                                    contexto.NombreServicio,
                                    solicitudId,
                                    asignacionId,
                                    empleadoAsignado.EmpleadoId,
                                    empleadoAsignado.NumeroUsuario,
                                    empleadoAsignado.NombreCompleto,
                                    solicitud.FechaHoraInicioCobertura,
                                    solicitud.FechaHoraFinCobertura,
                                    textoResponsiva,
                                    accessToken,
                                    ct);

                        if (!notification.isSuccess)
                        {
                            response.desc +=
                                " No fue posible notificar a los supervisores. " +
                                notification.message;
                        }
                    }
                    else
                    {
                        var notification =
                            await _relevoNotificationFunctions
                                .NotificarAsignacionPendienteFirmaEmpleadoAsync(
                                    empleadoAsignado.NumeroUsuario,
                                    contexto.ServicioId,
                                    contexto.NombreServicio,
                                    solicitudId,
                                    asignacionId,
                                    tipoCoberturaClave,
                                    solicitud.FechaHoraInicioCobertura,
                                    solicitud.FechaHoraFinCobertura,
                                    textoResponsiva,
                                    accessToken,
                                    ct);

                        if (!notification.isSuccess)
                        {
                            response.desc +=
                                " No fue posible notificar al empleado. " +
                                notification.message;
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

                return ErrorAsignacion(
                    500,
                    "Error SQL al crear la propuesta de cobertura.",
                    ex.Message);
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

                return ErrorAsignacion(
                    500,
                    "Error al crear la propuesta de cobertura.",
                    ex.Message);
            }
        }

        #endregion

        #region AUTORIZAR EXTENSION

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
                    return ErrorAsignacion(
                        401,
                        "No fue posible identificar al supervisor.");
                }

                if (data == null ||
                    data.RelevoNoPlaneadoAsignacionId <= 0)
                {
                    return ErrorAsignacion(
                        400,
                        "RelevoNoPlaneadoAsignacionId es inválido.");
                }

                if (data.FirmaSupervisor == null ||
                    data.FirmaSupervisor.Length == 0)
                {
                    return ErrorAsignacion(
                        400,
                        "La firma del supervisor es obligatoria.");
                }

                numeroUsuario = numeroUsuario.Trim();

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                EmpleadoRelevoDto? supervisor =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct);

                if (supervisor == null)
                {
                    return ErrorAsignacion(
                        404,
                        "No existe un empleado relacionado con el supervisor autenticado.");
                }

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await _data.ObtenerAsignacionAsync(
                        conn,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    return ErrorAsignacion(
                        404,
                        "No existe la propuesta de cobertura.");
                }

                string? tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct);

                string? estatusAsignacion =
                    await _data.ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        asignacion.RelevoAsignacionEstatusId,
                        ct);

                if (!string.Equals(
                        tipoCobertura,
                        "EXTENSION",
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        estatusAsignacion,
                        ESTATUS_PENDIENTE_SUPERVISOR,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ErrorAsignacion(
                        409,
                        "La propuesta no corresponde a una extensión pendiente de autorización.");
                }

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await _data.ObtenerSolicitudAsync(
                        conn,
                        asignacion.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    return ErrorAsignacion(
                        404,
                        "No existe la solicitud relacionada con la propuesta.");
                }

                ServicioEmpleadoRelevoDto? contexto =
                    await ObtenerContextoServicioAsync(
                        conn,
                        null,
                        solicitud,
                        ct);

                if (contexto == null)
                {
                    return ErrorAsignacion(
                        404,
                        "No existe la asignación saliente relacionada con la cobertura.");
                }

                bool supervisorValido =
                    await _data.EsSupervisorServicioAsync(
                        conn,
                        supervisor.EmpleadoId,
                        contexto.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct);

                if (!supervisorValido)
                {
                    return ErrorAsignacion(
                        403,
                        "El supervisor no tiene asignado este servicio.");
                }

                string operacionId =
                    $"asignacion-{asignacion.RelevoNoPlaneadoAsignacionId}";

                ResponseModel<string> upload =
                    await _fileRelevoService.UploadFileAsync(
                        data.FirmaSupervisor,
                        operacionId,
                        "firma-supervisor",
                        ct);

                if (!upload.isSuccess ||
                    string.IsNullOrWhiteSpace(upload.data))
                {
                    return ErrorAsignacion(
                        upload.code,
                        upload.message,
                        upload.desc);
                }

                rutaFirmaSupervisor = upload.data;

                transaction = conn.BeginTransaction();

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
                        "La propuesta dejó de existir antes de completar la autorización.");
                }

                estatusAsignacion =
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
                    throw new InvalidOperationException(
                        "La propuesta cambió de estado antes de completar la autorización.");
                }

                int? nuevoEstatusId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                        ct,
                        transaction);

                if (!nuevoEstatusId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus PENDIENTE_FIRMA_EMPLEADO.");
                }

                const string sql = @"
UPDATE dbo.RelevoNoPlaneadoAsignacion
SET
    RelevoAsignacionEstatusId = @NuevoEstatusId,
    SupervisorEmpleadoIdAutoriza = @SupervisorEmpleadoId,
    RutaFirmaSupervisor = @RutaFirmaSupervisor,
    FechaHoraFirmaSupervisor = @FechaActual,
    FechaModificacion = @FechaActual,
    UsuarioModificacion = @NumeroUsuario
WHERE RelevoNoPlaneadoAsignacionId = @AsignacionId
  AND RelevoAsignacionEstatusId = @EstatusAnteriorId
  AND SupervisorEmpleadoIdAutoriza IS NULL;";

                int rows =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                NuevoEstatusId = nuevoEstatusId.Value,
                                SupervisorEmpleadoId =
                                    supervisor.EmpleadoId,
                                RutaFirmaSupervisor =
                                    rutaFirmaSupervisor,
                                FechaActual = fechaActual,
                                NumeroUsuario = numeroUsuario,
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

                transaction.Commit();
                transaction = null;

                asignacion.RelevoAsignacionEstatusId =
                    nuevoEstatusId.Value;
                asignacion.SupervisorEmpleadoIdAutoriza =
                    supervisor.EmpleadoId;
                asignacion.RutaFirmaSupervisor =
                    rutaFirmaSupervisor;
                asignacion.FechaHoraFirmaSupervisor =
                    fechaActual;

                rutaFirmaSupervisor = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "La extensión fue autorizada correctamente.";
                response.desc =
                    "La propuesta quedó pendiente de firma y aceptación del empleado.";
                response.data = asignacion;

                try
                {
                    EmpleadoRelevoDto? empleadoAsignado =
                        await _data.ObtenerEmpleadoAsync(
                            conn,
                            asignacion.EmpleadoIdAsignado,
                            ct);

                    if (empleadoAsignado != null &&
                        !string.IsNullOrWhiteSpace(
                            empleadoAsignado.NumeroUsuario))
                    {
                        var notification =
                            await _relevoNotificationFunctions
                                .NotificarExtensionPendienteFirmaEmpleadoAsync(
                                    empleadoAsignado.NumeroUsuario,
                                    contexto.ServicioId,
                                    contexto.NombreServicio,
                                    solicitud.SolicitudRelevoNoPlaneadoId,
                                    asignacion.RelevoNoPlaneadoAsignacionId,
                                    solicitud.FechaHoraInicioCobertura,
                                    solicitud.FechaHoraFinCobertura,
                                    asignacion.TextoResponsiva,
                                    accessToken,
                                    ct);

                        if (!notification.isSuccess)
                        {
                            response.desc +=
                                " No fue posible notificar al empleado. " +
                                notification.message;
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

                return ErrorAsignacion(
                    500,
                    "Error SQL al autorizar la extensión.",
                    ex.Message);
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

                return ErrorAsignacion(
                    500,
                    "Error al autorizar la extensión.",
                    ex.Message);
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
            string? rutaFirmaEmpleado = null;

            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    return ErrorAsignacion(
                        401,
                        "No fue posible identificar al empleado.");
                }

                if (data == null ||
                    data.RelevoNoPlaneadoAsignacionId <= 0)
                {
                    return ErrorAsignacion(
                        400,
                        "RelevoNoPlaneadoAsignacionId es inválido.");
                }

                if (data.FirmaAceptacion == null ||
                    data.FirmaAceptacion.Length == 0)
                {
                    return ErrorAsignacion(
                        400,
                        "La firma de aceptación es obligatoria.");
                }

                numeroUsuario = numeroUsuario.Trim();

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                EmpleadoRelevoDto? empleado =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct);

                if (empleado == null)
                {
                    return ErrorAsignacion(
                        404,
                        "No existe un empleado relacionado con el usuario autenticado.");
                }

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await _data.ObtenerAsignacionAsync(
                        conn,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    return ErrorAsignacion(
                        404,
                        "No existe la propuesta de cobertura.");
                }

                if (asignacion.EmpleadoIdAsignado !=
                    empleado.EmpleadoId)
                {
                    return ErrorAsignacion(
                        403,
                        "La responsiva únicamente puede ser firmada por el empleado asignado.");
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
                    return ErrorAsignacion(
                        409,
                        "La propuesta no se encuentra pendiente de firma.");
                }

                string? tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct);

                if (string.IsNullOrWhiteSpace(tipoCobertura))
                {
                    return ErrorAsignacion(
                        500,
                        "No fue posible determinar el tipo de cobertura.");
                }

                if (string.Equals(
                        tipoCobertura,
                        "EXTENSION",
                        StringComparison.OrdinalIgnoreCase) &&
                    (!asignacion.SupervisorEmpleadoIdAutoriza.HasValue ||
                     string.IsNullOrWhiteSpace(
                         asignacion.RutaFirmaSupervisor) ||
                     !asignacion.FechaHoraFirmaSupervisor.HasValue))
                {
                    return ErrorAsignacion(
                        409,
                        "La extensión todavía no cuenta con autorización firmada del supervisor.");
                }

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await _data.ObtenerSolicitudAsync(
                        conn,
                        asignacion.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (solicitud == null)
                {
                    return ErrorAsignacion(
                        404,
                        "No existe la solicitud relacionada con la propuesta.");
                }

                string operacionId =
                    $"asignacion-{asignacion.RelevoNoPlaneadoAsignacionId}";

                ResponseModel<string> upload =
                    await _fileRelevoService.UploadFileAsync(
                        data.FirmaAceptacion,
                        operacionId,
                        "firma-aceptacion",
                        ct);

                if (!upload.isSuccess ||
                    string.IsNullOrWhiteSpace(upload.data))
                {
                    return ErrorAsignacion(
                        upload.code,
                        upload.message,
                        upload.desc);
                }

                rutaFirmaEmpleado = upload.data;

                transaction = conn.BeginTransaction();

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

                ServicioEmpleadoRelevoDto? contexto =
                    await ObtenerContextoServicioAsync(
                        conn,
                        transaction,
                        solicitud,
                        ct);

                if (contexto == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación saliente relacionada con la cobertura.");
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

                long servicioEmpleadoTemporalId =
                    await CrearServicioEmpleadoTemporalAsync(
                        conn,
                        transaction,
                        solicitud,
                        contexto,
                        empleado,
                        tipoAsignacionFaltaId.Value,
                        fechaActual,
                        numeroUsuario,
                        ct);

                if (string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await CrearAsistenciaExtensionAsync(
                        conn,
                        transaction,
                        solicitud,
                        contexto,
                        servicioEmpleadoTemporalId,
                        empleado,
                        fechaActual,
                        numeroUsuario,
                        ct);
                }

                int? estatusAceptadaId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        ESTATUS_ACEPTADA,
                        ct,
                        transaction);

                int? estatusCubiertoId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_CUBIERTO,
                        ct,
                        transaction);

                if (!estatusAceptadaId.HasValue ||
                    !estatusCubiertoId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No están configurados los estatus de aceptación de cobertura.");
                }

                const string sqlAsignacion = @"
UPDATE dbo.RelevoNoPlaneadoAsignacion
SET
    RelevoAsignacionEstatusId = @NuevoEstatusId,
    RutaFirmaEmpleado = @RutaFirmaEmpleado,
    FechaHoraFirmaEmpleado = @FechaActual,
    ServicioEmpleadoTemporalId = @ServicioEmpleadoTemporalId,
    FechaModificacion = @FechaActual,
    UsuarioModificacion = @NumeroUsuario
WHERE RelevoNoPlaneadoAsignacionId = @AsignacionId
  AND RelevoAsignacionEstatusId = @EstatusAnteriorId
  AND RutaFirmaEmpleado IS NULL
  AND ServicioEmpleadoTemporalId IS NULL;";

                int rowsAsignacion =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlAsignacion,
                            new
                            {
                                NuevoEstatusId =
                                    estatusAceptadaId.Value,
                                RutaFirmaEmpleado =
                                    rutaFirmaEmpleado,
                                FechaActual = fechaActual,
                                ServicioEmpleadoTemporalId =
                                    servicioEmpleadoTemporalId,
                                NumeroUsuario = numeroUsuario,
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

                const string sqlSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @NuevoEstatusId,
    FechaModificacion = @FechaActual,
    UsuarioModificacion = @NumeroUsuario
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND RelevoNoPlaneadoEstatusId = @EstatusAnteriorId;";

                int rowsSolicitud =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlSolicitud,
                            new
                            {
                                NuevoEstatusId =
                                    estatusCubiertoId.Value,
                                FechaActual = fechaActual,
                                NumeroUsuario = numeroUsuario,
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

                transaction.Commit();
                transaction = null;

                string rutaPersistida = rutaFirmaEmpleado;
                rutaFirmaEmpleado = null;

                asignacion.RelevoAsignacionEstatusId =
                    estatusAceptadaId.Value;
                asignacion.RutaFirmaEmpleado =
                    rutaPersistida;
                asignacion.FechaHoraFirmaEmpleado =
                    fechaActual;
                asignacion.ServicioEmpleadoTemporalId =
                    servicioEmpleadoTemporalId;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    string.Equals(
                        tipoCobertura,
                        "EXTENSION",
                        StringComparison.OrdinalIgnoreCase)
                        ? "La extensión fue aceptada correctamente."
                        : "La cobertura fue aceptada correctamente.";
                response.desc =
                    string.Equals(
                        tipoCobertura,
                        "EXTENSION",
                        StringComparison.OrdinalIgnoreCase)
                        ? "El turno original fue finalizado y se creó el turno de extensión."
                        : "Se creó la asignación temporal. El empleado deberá realizar Check-In al presentarse en el servicio.";
                response.data = asignacion;

                try
                {
                    var notification =
                        await _relevoNotificationFunctions
                            .NotificarCoberturaAceptadaAsync(
                                contexto.ServicioId,
                                contexto.NombreServicio,
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

                    if (!notification.isSuccess)
                    {
                        response.desc +=
                            " No fue posible notificar a los supervisores. " +
                            notification.message;
                    }
                }
                catch (Exception ex)
                {
                    response.desc +=
                        " La cobertura se registró, pero ocurrió un error al enviar la notificación: " +
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
                    rutaFirmaEmpleado,
                    ct);

                return ErrorAsignacion(
                    500,
                    "Error SQL al aceptar la cobertura.",
                    ex.Message);
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
                    rutaFirmaEmpleado,
                    ct);

                return ErrorAsignacion(
                    500,
                    "Error al aceptar la cobertura.",
                    ex.Message);
            }
        }

        #endregion

        #region RECHAZAR EMPLEADO

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionAsync(
                RechazarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (data == null ||
                data.RelevoNoPlaneadoAsignacionId <= 0 ||
                string.IsNullOrWhiteSpace(data.MotivoRechazo))
            {
                return ErrorAsignacion(
                    400,
                    "La asignación y el motivo de rechazo son obligatorios.");
            }

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                return ErrorAsignacion(
                    401,
                    "No fue posible identificar al empleado.");
            }

            return await RechazarAsignacionFlexibleAsync(
                data.RelevoNoPlaneadoAsignacionId,
                data.MotivoRechazo.Trim(),
                numeroUsuario.Trim(),
                rechazadoPorSupervisor: false,
                accessToken,
                ct);
        }

        #endregion

        #region RECHAZAR SUPERVISOR

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionSupervisorAsync(
                RechazarAsignacionSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (data == null ||
                data.RelevoNoPlaneadoAsignacionId <= 0 ||
                string.IsNullOrWhiteSpace(data.MotivoRechazo))
            {
                return ErrorAsignacion(
                    400,
                    "La asignación y el motivo de rechazo son obligatorios.");
            }

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                return ErrorAsignacion(
                    401,
                    "No fue posible identificar al supervisor.");
            }

            return await RechazarAsignacionFlexibleAsync(
                data.RelevoNoPlaneadoAsignacionId,
                data.MotivoRechazo.Trim(),
                numeroUsuario.Trim(),
                rechazadoPorSupervisor: true,
                accessToken,
                ct);
        }

        #endregion

        #region RECHAZO FLEXIBLE

        private async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionFlexibleAsync(
                long asignacionId,
                string motivo,
                string numeroUsuario,
                bool rechazadoPorSupervisor,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

            SqlTransaction? transaction = null;

            try
            {
                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await _data.ObtenerAsignacionForUpdateAsync(
                        conn,
                        transaction,
                        asignacionId,
                        ct);

                if (asignacion == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    return ErrorAsignacion(
                        404,
                        "No existe la propuesta de cobertura.");
                }

                SolicitudRelevoNoPlaneadoDto? solicitud =
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

                ServicioEmpleadoRelevoDto? contexto =
                    await ObtenerContextoServicioAsync(
                        conn,
                        transaction,
                        solicitud,
                        ct);

                if (contexto == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación saliente relacionada con la cobertura.");
                }

                string? tipoCobertura =
                    await _data.ObtenerClaveTipoCoberturaAsync(
                        conn,
                        asignacion.RelevoTipoCoberturaId,
                        ct,
                        transaction);

                string? estatusAsignacion =
                    await _data.ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        asignacion.RelevoAsignacionEstatusId,
                        ct,
                        transaction);

                EmpleadoRelevoDto? usuario =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario,
                        ct,
                        transaction);

                if (usuario == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    return ErrorAsignacion(
                        404,
                        "No existe un empleado relacionado con el usuario autenticado.");
                }

                if (rechazadoPorSupervisor)
                {
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

                        return ErrorAsignacion(
                            409,
                            "La propuesta no se encuentra pendiente de autorización del supervisor.");
                    }

                    bool supervisorValido =
                        await _data.EsSupervisorServicioAsync(
                            conn,
                            usuario.EmpleadoId,
                            contexto.ServicioId,
                            solicitud.FechaHoraInicioCobertura.Date,
                            ct,
                            transaction);

                    if (!supervisorValido)
                    {
                        transaction.Rollback();
                        transaction = null;

                        return ErrorAsignacion(
                            403,
                            "El supervisor no tiene asignado este servicio.");
                    }
                }
                else
                {
                    if (asignacion.EmpleadoIdAsignado !=
                        usuario.EmpleadoId)
                    {
                        transaction.Rollback();
                        transaction = null;

                        return ErrorAsignacion(
                            403,
                            "La propuesta únicamente puede ser rechazada por el empleado asignado.");
                    }

                    if (!string.Equals(
                        estatusAsignacion,
                        ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        transaction.Rollback();
                        transaction = null;

                        return ErrorAsignacion(
                            409,
                            "La propuesta no se encuentra pendiente de respuesta del empleado.");
                    }

                    if (string.Equals(
                        tipoCobertura,
                        "EXTENSION",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        await CerrarAsistenciaExtensionRechazadaAsync(
                            conn,
                            transaction,
                            solicitud,
                            usuario,
                            fechaActual,
                            ct);
                    }
                }

                string claveRechazo =
                    rechazadoPorSupervisor
                        ? ESTATUS_RECHAZADA_SUPERVISOR
                        : ESTATUS_RECHAZADA_EMPLEADO;

                int? estatusRechazoId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        claveRechazo,
                        ct,
                        transaction);

                int? pendienteAsignacionId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_ASIGNACION,
                        ct,
                        transaction);

                if (!estatusRechazoId.HasValue ||
                    !pendienteAsignacionId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No están configurados los estatus requeridos para rechazar la cobertura.");
                }

                const string sqlAsignacion = @"
UPDATE dbo.RelevoNoPlaneadoAsignacion
SET
    RelevoAsignacionEstatusId = @NuevoEstatusId,
    MotivoRechazo = @Motivo,
    FechaHoraRechazo = @FechaActual,
    FechaModificacion = @FechaActual,
    UsuarioModificacion = @NumeroUsuario
WHERE RelevoNoPlaneadoAsignacionId = @AsignacionId
  AND RelevoAsignacionEstatusId = @EstatusAnteriorId
  AND ServicioEmpleadoTemporalId IS NULL;";

                int rowsAsignacion =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlAsignacion,
                            new
                            {
                                NuevoEstatusId =
                                    estatusRechazoId.Value,
                                Motivo = motivo,
                                FechaActual = fechaActual,
                                NumeroUsuario = numeroUsuario,
                                AsignacionId = asignacionId,
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

                const string sqlSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @NuevoEstatusId,
    FechaModificacion = @FechaActual,
    UsuarioModificacion = @NumeroUsuario
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND RelevoNoPlaneadoEstatusId = @EstatusAnteriorId;";

                int rowsSolicitud =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlSolicitud,
                            new
                            {
                                NuevoEstatusId =
                                    pendienteAsignacionId.Value,
                                FechaActual = fechaActual,
                                NumeroUsuario = numeroUsuario,
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

                transaction.Commit();
                transaction = null;

                asignacion.RelevoAsignacionEstatusId =
                    estatusRechazoId.Value;
                asignacion.MotivoRechazo = motivo;
                asignacion.FechaHoraRechazo = fechaActual;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    rechazadoPorSupervisor
                        ? "La extensión fue rechazada por el supervisor."
                        : "La propuesta de cobertura fue rechazada.";
                response.desc =
                    "La solicitud quedó pendiente de una nueva asignación de cobertura.";
                response.data = asignacion;

                try
                {
                    var notification =
                        await _relevoNotificationFunctions
                            .NotificarCoberturaRequeridaPorRechazoAsync(
                                contexto.ServicioId,
                                contexto.NombreServicio,
                                solicitud.SolicitudRelevoNoPlaneadoId,
                                asignacion.RelevoNoPlaneadoAsignacionId,
                                tipoCobertura ?? "COBERTURA",
                                rechazadoPorSupervisor
                                    ? "SUPERVISOR"
                                    : "EMPLEADO",
                                motivo,
                                solicitud.FechaHoraInicioCobertura,
                                solicitud.FechaHoraFinCobertura,
                                accessToken,
                                ct);

                    if (!notification.isSuccess)
                    {
                        response.desc +=
                            " No fue posible notificar la nueva necesidad de cobertura. " +
                            notification.message;
                    }
                }
                catch (Exception ex)
                {
                    response.desc +=
                        " El rechazo fue registrado, pero ocurrió un error al notificar: " +
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

                return ErrorAsignacion(
                    500,
                    "Error SQL al rechazar la cobertura.",
                    ex.Message);
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

                return ErrorAsignacion(
                    500,
                    "Error al rechazar la cobertura.",
                    ex.Message);
            }
        }

        #endregion

        #region CANCELAR SOLICITUD

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CancelarSolicitudAsync(
                CancelarSolicitudRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            SqlTransaction? transaction = null;

            try
            {
                if (data == null ||
                    data.SolicitudRelevoNoPlaneadoId <= 0)
                {
                    return ErrorSolicitud(
                        400,
                        "SolicitudRelevoNoPlaneadoId es inválido.");
                }

                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    return ErrorSolicitud(
                        401,
                        "No fue posible identificar al usuario.");
                }

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

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

                    return ErrorSolicitud(
                        404,
                        "No existe la solicitud de cobertura.");
                }

                int? canceladoId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_CANCELADO,
                        ct,
                        transaction);

                int? asignacionCanceladaId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        ESTATUS_ASIGNACION_CANCELADA,
                        ct,
                        transaction);

                if (!canceladoId.HasValue ||
                    !asignacionCanceladaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No están configurados los estatus de cancelación.");
                }

                const string sqlAsignaciones = @"
UPDATE A
SET
    A.RelevoAsignacionEstatusId = @CanceladaId,
    A.FechaModificacion = @FechaActual,
    A.UsuarioModificacion = @NumeroUsuario
FROM dbo.RelevoNoPlaneadoAsignacion A
INNER JOIN dbo.CAT_RelevoAsignacionEstatus E
    ON E.RelevoAsignacionEstatusId = A.RelevoAsignacionEstatusId
WHERE A.SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND E.Clave IN
  (
      'PENDIENTE_SUPERVISOR',
      'PENDIENTE_FIRMA_EMPLEADO'
  );";

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlAsignaciones,
                        new
                        {
                            CanceladaId =
                                asignacionCanceladaId.Value,
                            FechaActual = fechaActual,
                            NumeroUsuario = numeroUsuario.Trim(),
                            SolicitudId =
                                solicitud.SolicitudRelevoNoPlaneadoId
                        },
                        transaction,
                        cancellationToken: ct));

                const string sqlSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @CanceladoId,
    FechaModificacion = @FechaActual,
    UsuarioModificacion = @NumeroUsuario
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId;";

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlSolicitud,
                        new
                        {
                            CanceladoId = canceladoId.Value,
                            FechaActual = fechaActual,
                            NumeroUsuario = numeroUsuario.Trim(),
                            SolicitudId =
                                solicitud.SolicitudRelevoNoPlaneadoId
                        },
                        transaction,
                        cancellationToken: ct));

                transaction.Commit();
                transaction = null;

                solicitud.RelevoNoPlaneadoEstatusId =
                    canceladoId.Value;

                return new ResponseModel<SolicitudRelevoNoPlaneadoDto>
                {
                    isSuccess = true,
                    code = 200,
                    message = "Solicitud cancelada correctamente.",
                    desc = null,
                    data = solicitud
                };
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

                return ErrorSolicitud(
                    500,
                    "Error SQL al cancelar la solicitud.",
                    ex.Message);
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

                return ErrorSolicitud(
                    500,
                    "Error al cancelar la solicitud.",
                    ex.Message);
            }
        }

        #endregion

        #region CONSULTAR SOLICITUD

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerSolicitudAsync(
                long solicitudId,
                CancellationToken ct)
        {
            if (solicitudId <= 0)
            {
                return ErrorSolicitudResponse(
                    400,
                    "SolicitudRelevoNoPlaneadoId es inválido.");
            }

            try
            {
                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                const string sqlSolicitud = @"
SELECT
    SR.SolicitudRelevoNoPlaneadoId,
    CAST(NULL AS BIGINT) AS ServicioEmpleadoAfectadoId,
    SR.ServicioEmpleadoSalienteId,
    SE.ServicioId,
    S.NombreServicio,
    CAST(NULL AS INT) AS EmpleadoAfectadoId,
    CAST(NULL AS VARCHAR(50)) AS NumeroUsuarioAfectado,
    CAST(NULL AS VARCHAR(250)) AS NombreEmpleadoAfectado,
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
    ON SE.ServicioEmpleadoId = SR.ServicioEmpleadoSalienteId
INNER JOIN dbo.Servicio S
    ON S.ServicioId = SE.ServicioId
INNER JOIN dbo.CAT_RelevoNoPlaneadoOrigen O
    ON O.RelevoNoPlaneadoOrigenId = SR.RelevoNoPlaneadoOrigenId
INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ES
    ON ES.RelevoNoPlaneadoEstatusId = SR.RelevoNoPlaneadoEstatusId
WHERE SR.SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND SR.ServicioEmpleadoAfectadoId IS NULL;";

                SolicitudRelevoNoPlaneadoResponse? solicitud =
                    await conn.QueryFirstOrDefaultAsync<
                        SolicitudRelevoNoPlaneadoResponse>(
                        new CommandDefinition(
                            sqlSolicitud,
                            new { SolicitudId = solicitudId },
                            cancellationToken: ct));

                if (solicitud == null)
                {
                    return ErrorSolicitudResponse(
                        404,
                        "No existe la solicitud de cobertura.");
                }

                const string sqlAsignaciones = @"
SELECT
    A.RelevoNoPlaneadoAsignacionId,
    A.SolicitudRelevoNoPlaneadoId,
    A.EmpleadoIdAsignado,
    ISNULL(LTRIM(RTRIM(E.UsuarioAsignado)), '') AS NumeroUsuarioAsignado,
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
    A.FechaHoraFirmaEmpleado AS FechaHoraFirmaAceptacion,
    A.MotivoRechazo,
    A.FechaHoraRechazo,
    A.ServicioEmpleadoTemporalId,
    A.FechaAsignacion
FROM dbo.RelevoNoPlaneadoAsignacion A
INNER JOIN dbo.DatosGeneralesEmpleado E
    ON E.ID = A.EmpleadoIdAsignado
INNER JOIN dbo.CAT_RelevoTipoCobertura TC
    ON TC.RelevoTipoCoberturaId = A.RelevoTipoCoberturaId
INNER JOIN dbo.CAT_RelevoAsignacionEstatus EA
    ON EA.RelevoAsignacionEstatusId = A.RelevoAsignacionEstatusId
WHERE A.SolicitudRelevoNoPlaneadoId = @SolicitudId
ORDER BY A.FechaAsignacion ASC,
         A.RelevoNoPlaneadoAsignacionId ASC;";

                var asignaciones =
                    await conn.QueryAsync<
                        RelevoNoPlaneadoAsignacionResponse>(
                        new CommandDefinition(
                            sqlAsignaciones,
                            new { SolicitudId = solicitudId },
                            cancellationToken: ct));

                solicitud.Asignaciones = asignaciones.ToList();

                return new ResponseModel<SolicitudRelevoNoPlaneadoResponse>
                {
                    isSuccess = true,
                    code = 200,
                    message = "Solicitud de cobertura obtenida correctamente.",
                    desc = null,
                    data = solicitud
                };
            }
            catch (SqlException ex)
            {
                return ErrorSolicitudResponse(
                    500,
                    "Error SQL al obtener la solicitud de cobertura.",
                    ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorSolicitudResponse(
                    500,
                    "Error al obtener la solicitud de cobertura.",
                    ex.Message);
            }
        }

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerAsignacionAsync(
                long asignacionId,
                CancellationToken ct)
        {
            if (asignacionId <= 0)
            {
                return ErrorSolicitudResponse(
                    400,
                    "RelevoNoPlaneadoAsignacionId es inválido.");
            }

            using var conn = _data.CrearConexion();
            await conn.OpenAsync(ct);

            long? solicitudId =
                await _data.ObtenerSolicitudIdPorAsignacionAsync(
                    conn,
                    asignacionId,
                    ct);

            if (!solicitudId.HasValue)
            {
                return ErrorSolicitudResponse(
                    404,
                    "No existe la asignación de cobertura.");
            }

            return await ObtenerSolicitudAsync(
                solicitudId.Value,
                ct);
        }

        #endregion

        #region PENDIENTES EMPLEADO

        public async Task<ResponseModel<List<RelevoPendienteEmpleadoResponse>>>
            ObtenerPendientesEmpleadoAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    return ErrorListaEmpleado(
                        401,
                        "No fue posible identificar al empleado.");
                }

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                EmpleadoRelevoDto? empleado =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario.Trim(),
                        ct);

                if (empleado == null)
                {
                    return ExitoListaEmpleado(
                        new List<RelevoPendienteEmpleadoResponse>());
                }

                const string sql = @"
SELECT
    SR.SolicitudRelevoNoPlaneadoId,
    A.RelevoNoPlaneadoAsignacionId,
    SE.ServicioId,
    S.NombreServicio,
    CAST(NULL AS BIGINT) AS ServicioEmpleadoAfectadoId,
    TC.Clave AS TipoCoberturaClave,
    EA.Clave AS EstatusClave,
    SR.FechaHoraInicioCobertura,
    SR.FechaHoraFinCobertura,
    A.TextoResponsiva,
    A.FechaAsignacion
FROM dbo.RelevoNoPlaneadoAsignacion A
INNER JOIN dbo.SolicitudRelevoNoPlaneado SR
    ON SR.SolicitudRelevoNoPlaneadoId = A.SolicitudRelevoNoPlaneadoId
INNER JOIN dbo.ServicioEmpleado SE
    ON SE.ServicioEmpleadoId = SR.ServicioEmpleadoSalienteId
INNER JOIN dbo.Servicio S
    ON S.ServicioId = SE.ServicioId
INNER JOIN dbo.CAT_RelevoTipoCobertura TC
    ON TC.RelevoTipoCoberturaId = A.RelevoTipoCoberturaId
INNER JOIN dbo.CAT_RelevoAsignacionEstatus EA
    ON EA.RelevoAsignacionEstatusId = A.RelevoAsignacionEstatusId
INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ES
    ON ES.RelevoNoPlaneadoEstatusId = SR.RelevoNoPlaneadoEstatusId
WHERE SR.ServicioEmpleadoAfectadoId IS NULL
  AND A.EmpleadoIdAsignado = @EmpleadoId
  AND EA.Clave = @EstatusPendienteFirma
  AND ES.Clave = @EstatusEnProceso
  AND SR.FechaHoraFinCobertura > SYSDATETIME()
ORDER BY SR.FechaHoraInicioCobertura ASC,
         A.FechaAsignacion ASC;";

                var result =
                    await conn.QueryAsync<RelevoPendienteEmpleadoResponse>(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                EmpleadoId = empleado.EmpleadoId,
                                EstatusPendienteFirma =
                                    ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                                EstatusEnProceso =
                                    ESTATUS_EN_PROCESO
                            },
                            cancellationToken: ct));

                return ExitoListaEmpleado(
                    result.ToList());
            }
            catch (SqlException ex)
            {
                return ErrorListaEmpleado(
                    500,
                    "Error SQL al obtener los relevos pendientes del empleado.",
                    ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorListaEmpleado(
                    500,
                    "Error al obtener los relevos pendientes del empleado.",
                    ex.Message);
            }
        }

        #endregion

        #region PENDIENTES SUPERVISOR

        public async Task<ResponseModel<List<RelevoPendienteSupervisorResponse>>>
            ObtenerPendientesSupervisorAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    return ErrorListaSupervisor(
                        401,
                        "No fue posible identificar al supervisor.");
                }

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                EmpleadoRelevoDto? supervisor =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario.Trim(),
                        ct);

                if (supervisor == null)
                {
                    return ExitoListaSupervisor(
                        new List<RelevoPendienteSupervisorResponse>());
                }

                const string sql = @"
SELECT
    SR.SolicitudRelevoNoPlaneadoId,
    AP.RelevoNoPlaneadoAsignacionId,
    SE.ServicioId,
    S.NombreServicio,
    CAST(NULL AS BIGINT) AS ServicioEmpleadoAfectadoId,
    CAST(NULL AS INT) AS EmpleadoAfectadoId,
    CAST(NULL AS VARCHAR(50)) AS NumeroUsuarioAfectado,
    CAST(NULL AS VARCHAR(250)) AS NombreEmpleadoAfectado,
    O.Clave AS OrigenClave,
    ESR.Clave AS SolicitudEstatusClave,
    SR.FechaHoraInicioCobertura,
    SR.FechaHoraFinCobertura,
    SR.MotivoRelevo,
    SR.RutaFotoEvidencia,
    AP.EmpleadoIdAsignado,
    CASE
        WHEN AP.EmpleadoIdAsignado IS NULL THEN NULL
        ELSE LTRIM(RTRIM(EP.UsuarioAsignado))
    END AS NumeroUsuarioAsignado,
    CASE
        WHEN AP.EmpleadoIdAsignado IS NULL THEN NULL
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
    ON SE.ServicioEmpleadoId = SR.ServicioEmpleadoSalienteId
INNER JOIN dbo.Servicio S
    ON S.ServicioId = SE.ServicioId
INNER JOIN dbo.CAT_RelevoNoPlaneadoOrigen O
    ON O.RelevoNoPlaneadoOrigenId = SR.RelevoNoPlaneadoOrigenId
INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ESR
    ON ESR.RelevoNoPlaneadoEstatusId = SR.RelevoNoPlaneadoEstatusId
OUTER APPLY
(
    SELECT TOP (1)
        A.RelevoNoPlaneadoAsignacionId,
        A.EmpleadoIdAsignado,
        TC.Clave AS TipoCoberturaClave,
        EAS.Clave AS AsignacionEstatusClave
    FROM dbo.RelevoNoPlaneadoAsignacion A
    INNER JOIN dbo.CAT_RelevoTipoCobertura TC
        ON TC.RelevoTipoCoberturaId = A.RelevoTipoCoberturaId
    INNER JOIN dbo.CAT_RelevoAsignacionEstatus EAS
        ON EAS.RelevoAsignacionEstatusId = A.RelevoAsignacionEstatusId
    WHERE A.SolicitudRelevoNoPlaneadoId = SR.SolicitudRelevoNoPlaneadoId
      AND EAS.Clave IN
      (
          'PENDIENTE_SUPERVISOR',
          'PENDIENTE_FIRMA_EMPLEADO'
      )
    ORDER BY A.FechaAsignacion DESC,
             A.RelevoNoPlaneadoAsignacionId DESC
) AP
LEFT JOIN dbo.DatosGeneralesEmpleado EP
    ON EP.ID = AP.EmpleadoIdAsignado
WHERE SR.ServicioEmpleadoAfectadoId IS NULL
  AND ESR.Clave IN
  (
      'PENDIENTE_ASIGNACION',
      'EN_PROCESO'
  )
  AND SR.FechaHoraFinCobertura > SYSDATETIME()
  AND EXISTS
  (
      SELECT 1
      FROM dbo.ServicioSupervisor SS
      WHERE SS.ServicioId = SE.ServicioId
        AND SS.SupervisorEmpleadoId = @SupervisorEmpleadoId
        AND SS.FechaInicio <= CAST(SR.FechaHoraInicioCobertura AS DATE)
        AND
        (
            SS.FechaFin IS NULL
            OR SS.FechaFin >= CAST(SR.FechaHoraInicioCobertura AS DATE)
        )
  )
ORDER BY SR.FechaHoraInicioCobertura ASC,
         SR.FechaRegistro ASC;";

                var result =
                    await conn.QueryAsync<RelevoPendienteSupervisorResponse>(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                SupervisorEmpleadoId =
                                    supervisor.EmpleadoId
                            },
                            cancellationToken: ct));

                return ExitoListaSupervisor(
                    result.ToList());
            }
            catch (SqlException ex)
            {
                return ErrorListaSupervisor(
                    500,
                    "Error SQL al obtener los relevos pendientes del supervisor.",
                    ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorListaSupervisor(
                    500,
                    "Error al obtener los relevos pendientes del supervisor.",
                    ex.Message);
            }
        }

        #endregion

        #region HELPERS ASISTENCIA / COBERTURA

        private async Task RegistrarIncidenciaAbandonoAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            AsistenciaActivaCheckOutDto asistencia,
            string numeroUsuario,
            string motivo,
            DateTime fechaActual,
            CancellationToken ct)
        {
            const string sqlTipo = @"
SELECT TOP (1)
    TipoIncidenciaId,
    AfectaNomina,
    TipoAfectacionNomina,
    MontoAfectacion
FROM dbo.CAT_TIPO_INCIDENCIA
WHERE Clave = 'ABANDONO_TURNO'
  AND Estatus = 1;";

            TipoIncidenciaDto? tipo =
                await conn.QueryFirstOrDefaultAsync<TipoIncidenciaDto>(
                    new CommandDefinition(
                        sqlTipo,
                        transaction: transaction,
                        cancellationToken: ct));

            if (tipo == null)
            {
                throw new InvalidOperationException(
                    "No está configurado el tipo de incidencia ABANDONO_TURNO.");
            }

            int minutosFaltantes =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        (
                            asistencia.FechaHoraSalidaProgramada -
                            fechaActual
                        ).TotalMinutes));

            const string sql = @"
INSERT INTO dbo.Incidencias
(
    TipoIncidenciaId,
    NumeroUsuario,
    ServicioId,
    AsistenciaId,
    AsignacionTurnoId,
    FechaIncidencia,
    Descripcion,
    AfectaNomina,
    TipoAfectacionNomina,
    MontoAfectacion,
    Estatus,
    UsuarioRegistro,
    FechaRegistro
)
VALUES
(
    @TipoIncidenciaId,
    @NumeroUsuario,
    @ServicioId,
    @AsistenciaId,
    @AsignacionTurnoId,
    @FechaIncidencia,
    @Descripcion,
    @AfectaNomina,
    @TipoAfectacionNomina,
    @MontoAfectacion,
    1,
    @UsuarioRegistro,
    @FechaRegistro
);";

            await conn.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        tipo.TipoIncidenciaId,
                        NumeroUsuario = numeroUsuario,
                        ServicioId = asistencia.ServicioId,
                        AsistenciaId = asistencia.AsistenciaId,
                        AsignacionTurnoId =
                            asistencia.ServicioEmpleadoId,
                        FechaIncidencia = fechaActual,
                        Descripcion =
                            $"Abandono de turno {minutosFaltantes} minuto(s) antes de la salida programada. Motivo: {motivo}",
                        tipo.AfectaNomina,
                        tipo.TipoAfectacionNomina,
                        tipo.MontoAfectacion,
                        UsuarioRegistro = numeroUsuario,
                        FechaRegistro = fechaActual
                    },
                    transaction,
                    cancellationToken: ct));
        }

        private async Task RealizarCheckOutAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            AsistenciaActivaCheckOutDto asistencia,
            string numeroUsuario,
            DateTime fechaActual,
            CancellationToken ct)
        {
            const string sql = @"
UPDATE dbo.Asistencia
SET
    FechaHoraCheckOut = @FechaActual,
    Estatus = @EstatusFinalizada
WHERE AsistenciaId = @AsistenciaId
  AND ServicioEmpleadoId = @ServicioEmpleadoId
  AND NumeroEmpleadoEntrante = @NumeroUsuario
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL;";

            int rows =
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            FechaActual = fechaActual,
                            EstatusFinalizada =
                                ASISTENCIA_FINALIZADA,
                            asistencia.AsistenciaId,
                            asistencia.ServicioEmpleadoId,
                            NumeroUsuario = numeroUsuario,
                            EstatusEnTurno =
                                ASISTENCIA_EN_TURNO
                        },
                        transaction,
                        cancellationToken: ct));

            if (rows != 1)
            {
                throw new InvalidOperationException(
                    "La asistencia cambió antes de completar el Check-Out.");
            }
        }

        private async Task<long> CrearServicioEmpleadoTemporalAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            SolicitudRelevoNoPlaneadoDto solicitud,
            ServicioEmpleadoRelevoDto contexto,
            EmpleadoRelevoDto empleado,
            int tipoAsignacionId,
            DateTime fechaActual,
            string numeroUsuario,
            CancellationToken ct)
        {
            DateTime fechaInicio =
                solicitud.FechaHoraInicioCobertura.Date;

            DateTime fechaFin =
                solicitud.FechaHoraFinCobertura.Date;

            TimeSpan horaEntrada =
                solicitud.FechaHoraInicioCobertura.TimeOfDay;

            TimeSpan horaSalida =
                solicitud.FechaHoraFinCobertura.TimeOfDay;

            bool salidaDiaSiguiente =
                fechaFin > fechaInicio;

            const string sql = @"
INSERT INTO dbo.ServicioEmpleado
(
    ServicioId,
    EmpleadoId,
    TipoAsignacionServicioId,
    EmpleadoCubiertoId,
    FechaInicio,
    FechaFin,
    HoraEntrada,
    HoraSalida,
    SalidaDiaSiguiente,
    Observaciones,
    FechaAlta,
    UsuarioAlta
)
OUTPUT INSERTED.ServicioEmpleadoId
VALUES
(
    @ServicioId,
    @EmpleadoId,
    @TipoAsignacionServicioId,
    NULL,
    @FechaInicio,
    @FechaFin,
    @HoraEntrada,
    @HoraSalida,
    @SalidaDiaSiguiente,
    @Observaciones,
    @FechaAlta,
    @UsuarioAlta
);";

            return await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ServicioId = contexto.ServicioId,
                        EmpleadoId = empleado.EmpleadoId,
                        TipoAsignacionServicioId =
                            tipoAsignacionId,
                        FechaInicio = fechaInicio,
                        FechaFin = fechaFin,
                        HoraEntrada = horaEntrada,
                        HoraSalida = horaSalida,
                        SalidaDiaSiguiente =
                            salidaDiaSiguiente,
                        Observaciones =
                            $"Cobertura sin relevo afectado. Solicitud: {solicitud.SolicitudRelevoNoPlaneadoId}.",
                        FechaAlta = fechaActual,
                        UsuarioAlta = numeroUsuario
                    },
                    transaction,
                    cancellationToken: ct));
        }

        private async Task CrearAsistenciaExtensionAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            SolicitudRelevoNoPlaneadoDto solicitud,
            ServicioEmpleadoRelevoDto contexto,
            long servicioEmpleadoTemporalId,
            EmpleadoRelevoDto empleado,
            DateTime fechaActual,
            string numeroUsuario,
            CancellationToken ct)
        {
            if (!solicitud.ServicioEmpleadoSalienteId.HasValue)
            {
                throw new InvalidOperationException(
                    "La extensión no tiene una asignación saliente relacionada.");
            }

            const string sqlOriginal = @"
SELECT TOP (1)
    AsistenciaId
FROM dbo.Asistencia WITH (UPDLOCK, HOLDLOCK)
WHERE ServicioEmpleadoId = @ServicioEmpleadoSalienteId
  AND NumeroEmpleadoEntrante = @NumeroEmpleado
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL
ORDER BY AsistenciaId DESC;";

            long? asistenciaOriginalId =
                await conn.ExecuteScalarAsync<long?>(
                    new CommandDefinition(
                        sqlOriginal,
                        new
                        {
                            ServicioEmpleadoSalienteId =
                                solicitud.ServicioEmpleadoSalienteId.Value,
                            NumeroEmpleado =
                                empleado.NumeroUsuario,
                            EstatusEnTurno =
                                ASISTENCIA_EN_TURNO
                        },
                        transaction,
                        cancellationToken: ct));

            if (!asistenciaOriginalId.HasValue)
            {
                throw new InvalidOperationException(
                    "El empleado ya no tiene activo el turno que debía extenderse.");
            }

            const string sqlCerrar = @"
UPDATE dbo.Asistencia
SET
    FechaHoraCheckOut = @FechaHoraCheckOut,
    Estatus = @EstatusFinalizada
WHERE AsistenciaId = @AsistenciaId
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL;";

            int rows =
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlCerrar,
                        new
                        {
                            FechaHoraCheckOut =
                                solicitud.FechaHoraInicioCobertura,
                            EstatusFinalizada =
                                ASISTENCIA_FINALIZADA,
                            AsistenciaId =
                                asistenciaOriginalId.Value,
                            EstatusEnTurno =
                                ASISTENCIA_EN_TURNO
                        },
                        transaction,
                        cancellationToken: ct));

            if (rows != 1)
            {
                throw new InvalidOperationException(
                    "El turno original cambió antes de crear la extensión.");
            }

            const string sqlInsert = @"
INSERT INTO dbo.Asistencia
(
    ServicioId,
    ServicioEmpleadoId,
    NumeroEmpleadoEntrante,
    NumeroEmpleadoSaliente,
    FechaTurno,
    FechaHoraEntradaProgramada,
    FechaHoraSalidaProgramada,
    FechaHoraCheckIn,
    FechaHoraCheckOut,
    EsRetardo,
    MinutosRetardo,
    Geolocalizacion,
    Estatus,
    FechaRegistro,
    UsuarioRegistro
)
SELECT
    @ServicioId,
    @ServicioEmpleadoId,
    @NumeroEmpleado,
    @NumeroEmpleado,
    @FechaTurno,
    @FechaHoraEntradaProgramada,
    @FechaHoraSalidaProgramada,
    @FechaHoraCheckIn,
    NULL,
    0,
    NULL,
    A.Geolocalizacion,
    @EstatusEnTurno,
    @FechaRegistro,
    @UsuarioRegistro
FROM dbo.Asistencia A
WHERE A.AsistenciaId = @AsistenciaOriginalId;";

            int insertados =
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlInsert,
                        new
                        {
                            ServicioId = contexto.ServicioId,
                            ServicioEmpleadoId =
                                servicioEmpleadoTemporalId,
                            NumeroEmpleado =
                                empleado.NumeroUsuario,
                            FechaTurno =
                                solicitud.FechaHoraInicioCobertura.Date,
                            FechaHoraEntradaProgramada =
                                solicitud.FechaHoraInicioCobertura,
                            FechaHoraSalidaProgramada =
                                solicitud.FechaHoraFinCobertura,
                            FechaHoraCheckIn =
                                solicitud.FechaHoraInicioCobertura,
                            EstatusEnTurno =
                                ASISTENCIA_EN_TURNO,
                            FechaRegistro = fechaActual,
                            UsuarioRegistro = numeroUsuario,
                            AsistenciaOriginalId =
                                asistenciaOriginalId.Value
                        },
                        transaction,
                        cancellationToken: ct));

            if (insertados != 1)
            {
                throw new InvalidOperationException(
                    "No fue posible crear la asistencia de extensión.");
            }
        }

        private async Task CerrarAsistenciaExtensionRechazadaAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            SolicitudRelevoNoPlaneadoDto solicitud,
            EmpleadoRelevoDto empleado,
            DateTime fechaActual,
            CancellationToken ct)
        {
            if (!solicitud.ServicioEmpleadoSalienteId.HasValue)
            {
                throw new InvalidOperationException(
                    "La extensión no tiene una asignación saliente relacionada.");
            }

            const string sql = @"
UPDATE dbo.Asistencia
SET
    FechaHoraCheckOut = @FechaActual,
    Estatus = @EstatusFinalizada
WHERE ServicioEmpleadoId = @ServicioEmpleadoSalienteId
  AND NumeroEmpleadoEntrante = @NumeroEmpleado
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL;";

            int rows =
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            FechaActual = fechaActual,
                            EstatusFinalizada =
                                ASISTENCIA_FINALIZADA,
                            ServicioEmpleadoSalienteId =
                                solicitud.ServicioEmpleadoSalienteId.Value,
                            NumeroEmpleado =
                                empleado.NumeroUsuario,
                            EstatusEnTurno =
                                ASISTENCIA_EN_TURNO
                        },
                        transaction,
                        cancellationToken: ct));

            if (rows != 1)
            {
                throw new InvalidOperationException(
                    "No existe una asistencia activa para realizar el Check-Out de la extensión rechazada.");
            }
        }

        #endregion

        #region HELPERS SOLICITUD / CONTEXTO

        private async Task<bool> ExisteSolicitudFlexibleActivaAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            long servicioEmpleadoSalienteId,
            DateTime fechaActual,
            CancellationToken ct)
        {
            const string sql = @"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM dbo.SolicitudRelevoNoPlaneado SR WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ES
        ON ES.RelevoNoPlaneadoEstatusId =
           SR.RelevoNoPlaneadoEstatusId
    WHERE SR.ServicioEmpleadoAfectadoId IS NULL
      AND SR.ServicioEmpleadoSalienteId = @ServicioEmpleadoSalienteId
      AND ES.Clave IN ('PENDIENTE_ASIGNACION', 'EN_PROCESO')
      AND SR.FechaHoraFinCobertura > @FechaActual
)
THEN CAST(1 AS BIT)
ELSE CAST(0 AS BIT)
END;";

            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ServicioEmpleadoSalienteId =
                            servicioEmpleadoSalienteId,
                        FechaActual = fechaActual
                    },
                    transaction,
                    cancellationToken: ct));
        }

        private async Task<bool> EsSolicitudSinAfectadoAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            long solicitudId,
            CancellationToken ct)
        {
            const string sql = @"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM dbo.SolicitudRelevoNoPlaneado
    WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
      AND ServicioEmpleadoAfectadoId IS NULL
)
THEN CAST(1 AS BIT)
ELSE CAST(0 AS BIT)
END;";

            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new { SolicitudId = solicitudId },
                    transaction,
                    cancellationToken: ct));
        }

        private async Task<ServicioEmpleadoRelevoDto?>
            ObtenerContextoServicioAsync(
                SqlConnection conn,
                SqlTransaction? transaction,
                SolicitudRelevoNoPlaneadoDto solicitud,
                CancellationToken ct)
        {
            if (!solicitud.ServicioEmpleadoSalienteId.HasValue ||
                solicitud.ServicioEmpleadoSalienteId.Value <= 0)
            {
                return null;
            }

            return await _data.ObtenerServicioEmpleadoAsync(
                conn,
                solicitud.ServicioEmpleadoSalienteId.Value,
                ct,
                transaction);
        }

        #endregion

        #region NOTIFICACION COBERTURA REQUERIDA

        private async Task NotificarCoberturaRequeridaAsync(
            ServicioEmpleadoRelevoDto contexto,
            long solicitudId,
            DateTime inicio,
            DateTime fin,
            string motivo,
            string accessToken,
            CancellationToken ct,
            ResponseModel<SolicitudRelevoNoPlaneadoDto> response)
        {
            try
            {
                object payload =
                    new
                    {
                        Evento =
                            RelevoNotificationFunctions
                                .RELEVO_COBERTURA_REQUERIDA,
                        Accion =
                            "GESTIONAR_COBERTURA",
                        SolicitudRelevoNoPlaneadoId =
                            solicitudId,
                        ServicioId =
                            contexto.ServicioId,
                        ServicioEmpleadoAfectadoId =
                            (long?)null,
                        ServicioEmpleadoSalienteId =
                            contexto.ServicioEmpleadoId,
                        NombreServicio =
                            contexto.NombreServicio,
                        OrigenClave =
                            "ASISTENCIA",
                        FechaHoraInicioCobertura =
                            inicio,
                        FechaHoraFinCobertura =
                            fin,
                        MotivoRelevo =
                            motivo
                    };

                var notification =
                    await _servicioNotificationFunctions
                        .EnviarASupervisoresAsync(
                            contexto.ServicioId,
                            inicio.Date,
                            RelevoNotificationFunctions
                                .RELEVO_COBERTURA_REQUERIDA,
                            "Cobertura requerida",
                            $"Se requiere una cobertura no planeada para el servicio {contexto.NombreServicio}.",
                            payload,
                            accessToken,
                            ct);

                if (!notification.isSuccess)
                {
                    response.desc +=
                        " No fue posible notificar a los supervisores. " +
                        notification.message;
                }
            }
            catch (Exception ex)
            {
                response.desc +=
                    " La solicitud fue registrada, pero ocurrió un error al enviar la notificación: " +
                    ex.Message;
            }
        }

        #endregion

        #region RESPONSIVA

        private static string GenerarTextoResponsiva(
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
            }
        }

        #endregion

        #region RESPUESTAS

        private static ResponseModel<SolicitudRelevoNoPlaneadoDto>
            ErrorSolicitud(
                int code,
                string message,
                string? desc = null)
        {
            return new ResponseModel<SolicitudRelevoNoPlaneadoDto>
            {
                isSuccess = false,
                code = code,
                message = message,
                desc = desc,
                data = null
            };
        }

        private static ResponseModel<RelevoNoPlaneadoAsignacionDto>
            ErrorAsignacion(
                int code,
                string message,
                string? desc = null)
        {
            return new ResponseModel<RelevoNoPlaneadoAsignacionDto>
            {
                isSuccess = false,
                code = code,
                message = message,
                desc = desc,
                data = null
            };
        }

        private static ResponseModel<SolicitudRelevoNoPlaneadoResponse>
            ErrorSolicitudResponse(
                int code,
                string message,
                string? desc = null)
        {
            return new ResponseModel<SolicitudRelevoNoPlaneadoResponse>
            {
                isSuccess = false,
                code = code,
                message = message,
                desc = desc,
                data = null
            };
        }

        private static ResponseModel<List<RelevoPendienteEmpleadoResponse>>
            ErrorListaEmpleado(
                int code,
                string message,
                string? desc = null)
        {
            return new ResponseModel<List<RelevoPendienteEmpleadoResponse>>
            {
                isSuccess = false,
                code = code,
                message = message,
                desc = desc,
                data = null
            };
        }

        private static ResponseModel<List<RelevoPendienteSupervisorResponse>>
            ErrorListaSupervisor(
                int code,
                string message,
                string? desc = null)
        {
            return new ResponseModel<List<RelevoPendienteSupervisorResponse>>
            {
                isSuccess = false,
                code = code,
                message = message,
                desc = desc,
                data = null
            };
        }

        private static ResponseModel<List<RelevoPendienteEmpleadoResponse>>
            ExitoListaEmpleado(
                List<RelevoPendienteEmpleadoResponse> data)
        {
            return new ResponseModel<List<RelevoPendienteEmpleadoResponse>>
            {
                isSuccess = true,
                code = 200,
                message =
                    "Relevos pendientes del empleado obtenidos correctamente.",
                desc = null,
                data = data
            };
        }

        private static ResponseModel<List<RelevoPendienteSupervisorResponse>>
            ExitoListaSupervisor(
                List<RelevoPendienteSupervisorResponse> data)
        {
            return new ResponseModel<List<RelevoPendienteSupervisorResponse>>
            {
                isSuccess = true,
                code = 200,
                message =
                    "Relevos pendientes del supervisor obtenidos correctamente.",
                desc = null,
                data = data
            };
        }

        #endregion
    }
}
