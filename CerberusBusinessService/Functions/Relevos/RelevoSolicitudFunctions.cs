using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Relevos
{
    public class RelevoSolicitudFunctions
    {
        #region CONSTANTES

        private const string ESTATUS_PENDIENTE_ASIGNACION =
            "PENDIENTE_ASIGNACION";

        private const string ESTATUS_EN_PROCESO =
            "EN_PROCESO";

        private const string ESTATUS_CANCELADO =
            "CANCELADO";

        private const string ESTATUS_ASIGNACION_CANCELADA =
            "CANCELADA";

        private static readonly HashSet<string> ORIGENES_VALIDOS =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "ASISTENCIA",
                "SUPERVISION"
            };

        #endregion


        #region PROPIEDADES

        private readonly RelevoNoPlaneadoDataService _data;
        private readonly RelevoIntegracionAsistenciaFunctions _integracion;
        private readonly FileRelevoNoPlaneadoService _fileRelevoService;

        #endregion


        #region CONSTRUCTOR

        public RelevoSolicitudFunctions(
            RelevoNoPlaneadoDataService data,
            RelevoIntegracionAsistenciaFunctions integracion,
            FileRelevoNoPlaneadoService fileRelevoService)
        {
            _data = data;
            _integracion = integracion;
            _fileRelevoService = fileRelevoService;
        }

        #endregion


        #region CREAR SOLICITUD

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudAsync(
                CrearSolicitudRelevoNoPlaneadoDto data,
                string numeroUsuario,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<SolicitudRelevoNoPlaneadoDto>();

            string? rutaFotoEvidencia = null;

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

                string? error = ValidarSolicitud(data);

                if (error != null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = error;
                    response.data = null;

                    return response;
                }

                string origenClave =
                    data.OrigenClave
                        .Trim()
                        .ToUpperInvariant();

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                // ====================================================
                // ASIGNACION AFECTADA
                // ====================================================

                ServicioEmpleadoRelevoDto? servicioEmpleadoAfectado =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        data.ServicioEmpleadoAfectadoId,
                        ct);

                if (servicioEmpleadoAfectado == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la asignación de servicio que requiere cobertura.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // ASIGNACION SALIENTE
                // ====================================================

                if (data.ServicioEmpleadoSalienteId.HasValue)
                {
                    ServicioEmpleadoRelevoDto? servicioEmpleadoSaliente =
                        await _data.ObtenerServicioEmpleadoAsync(
                            conn,
                            data.ServicioEmpleadoSalienteId.Value,
                            ct);

                    if (servicioEmpleadoSaliente == null)
                    {
                        response.isSuccess = false;
                        response.code = 404;
                        response.message =
                            "No existe la asignación del empleado saliente.";
                        response.data = null;

                        return response;
                    }

                    if (servicioEmpleadoSaliente.ServicioId !=
                        servicioEmpleadoAfectado.ServicioId)
                    {
                        response.isSuccess = false;
                        response.code = 409;
                        response.message =
                            "La asignación saliente y la asignación afectada pertenecen a servicios distintos.";
                        response.data = null;

                        return response;
                    }
                }

                // ====================================================
                // CATALOGOS
                // ====================================================

                int? origenId =
                    await _data.ObtenerOrigenIdAsync(
                        conn,
                        origenClave,
                        ct);

                if (!origenId.HasValue)
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        $"El origen '{origenClave}' no está configurado o está inactivo.";
                    response.data = null;

                    return response;
                }

                int? estatusId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_ASIGNACION,
                        ct);

                if (!estatusId.HasValue)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        "No está configurado el estatus PENDIENTE_ASIGNACION.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // SOLICITUD ACTIVA
                // ====================================================

                bool existeSolicitud =
                    await _data.ExisteSolicitudActivaAsync(
                        conn,
                        data.ServicioEmpleadoAfectadoId,
                        ct);

                if (existeSolicitud)
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Ya existe una solicitud de relevo no planeado activa para esta asignación.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // EVIDENCIA
                // ====================================================

                string operacionId =
                    Guid.NewGuid().ToString("N");

                ResponseModel<string> uploadResponse =
                    await _fileRelevoService.UploadFileAsync(
                        data.FotoEvidencia,
                        operacionId,
                        "evidencia",
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

                rutaFotoEvidencia = uploadResponse.data;

                // ====================================================
                // INSERT
                // ====================================================

                using var transaction =
                    conn.BeginTransaction();

                try
                {
                    DateTime fechaRegistro =
                        await _data.ObtenerFechaServidorAsync(
                            conn,
                            ct,
                            transaction);

                    const string sqlInsert = @"
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
    @ServicioEmpleadoAfectadoId,
    @ServicioEmpleadoSalienteId,
    @RelevoNoPlaneadoOrigenId,
    @RelevoNoPlaneadoEstatusId,
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
                                sqlInsert,
                                new
                                {
                                    data.ServicioEmpleadoAfectadoId,
                                    data.ServicioEmpleadoSalienteId,

                                    RelevoNoPlaneadoOrigenId =
                                        origenId.Value,

                                    RelevoNoPlaneadoEstatusId =
                                        estatusId.Value,

                                    data.FechaHoraInicioCobertura,
                                    data.FechaHoraFinCobertura,

                                    MotivoRelevo =
                                        data.MotivoRelevo.Trim(),

                                    MotivoNoPermanencia =
                                        string.IsNullOrWhiteSpace(
                                            data.MotivoNoPermanencia)
                                            ? null
                                            : data.MotivoNoPermanencia.Trim(),

                                    RutaFotoEvidencia =
                                        rutaFotoEvidencia,

                                    FechaRegistro =
                                        fechaRegistro,

                                    UsuarioRegistro =
                                        numeroUsuario.Trim()
                                },
                                transaction,
                                cancellationToken: ct));

                    transaction.Commit();

                    response.isSuccess = true;
                    response.code = 200;
                    response.message =
                        "Solicitud de relevo no planeado creada correctamente.";
                    response.desc =
                        "La solicitud quedó pendiente de asignación.";

                    response.data =
                        new SolicitudRelevoNoPlaneadoDto
                        {
                            SolicitudRelevoNoPlaneadoId =
                                solicitudId,

                            ServicioEmpleadoAfectadoId =
                                data.ServicioEmpleadoAfectadoId,

                            ServicioEmpleadoSalienteId =
                                data.ServicioEmpleadoSalienteId,

                            RelevoNoPlaneadoOrigenId =
                                origenId.Value,

                            RelevoNoPlaneadoEstatusId =
                                estatusId.Value,

                            FechaHoraInicioCobertura =
                                data.FechaHoraInicioCobertura,

                            FechaHoraFinCobertura =
                                data.FechaHoraFinCobertura,

                            MotivoRelevo =
                                data.MotivoRelevo.Trim(),

                            MotivoNoPermanencia =
                                string.IsNullOrWhiteSpace(
                                    data.MotivoNoPermanencia)
                                    ? null
                                    : data.MotivoNoPermanencia.Trim(),

                            RutaFotoEvidencia =
                                rutaFotoEvidencia,

                            FechaRegistro =
                                fechaRegistro,

                            UsuarioRegistro =
                                numeroUsuario.Trim()
                        };

                    rutaFotoEvidencia = null;

                    return response;
                }
                catch
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch
                    {
                    }

                    throw;
                }
            }
            catch (SqlException ex)
            {
                await EliminarArchivoSeguroAsync(
                    rutaFotoEvidencia,
                    ct);

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al crear la solicitud de relevo no planeado.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                await EliminarArchivoSeguroAsync(
                    rutaFotoEvidencia,
                    ct);

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al crear la solicitud de relevo no planeado.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region CREAR SOLICITUD DESDE ASISTENCIA

        internal async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudDesdeAsistenciaAsync(
                CrearSolicitudRelevoNoPlaneadoDto data,
                long asistenciaSalienteId,
                bool realizarCheckOut,
                string numeroUsuario,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<SolicitudRelevoNoPlaneadoDto>();

            SqlTransaction? transaction = null;
            string? rutaEvidencia = null;

            try
            {
                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El request es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message =
                        "No fue posible identificar al empleado.";
                    response.data = null;

                    return response;
                }

                if (data.ServicioEmpleadoAfectadoId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "ServicioEmpleadoAfectadoId es inválido.";
                    response.data = null;

                    return response;
                }

                if (!data.ServicioEmpleadoSalienteId.HasValue ||
                    data.ServicioEmpleadoSalienteId.Value <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "ServicioEmpleadoSalienteId es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (asistenciaSalienteId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "AsistenciaId es inválido.";
                    response.data = null;

                    return response;
                }

                if (data.FotoEvidencia == null ||
                    data.FotoEvidencia.Length == 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "La fotografía de evidencia es obligatoria.";
                    response.data = null;

                    return response;
                }

                if (data.FechaHoraFinCobertura <=
                    data.FechaHoraInicioCobertura)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "La fecha de fin de cobertura debe ser mayor a la fecha de inicio.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // EVIDENCIA
                // ====================================================

                string operacionId =
                    Guid.NewGuid().ToString("N");

                ResponseModel<string> upload =
                    await _fileRelevoService.UploadFileAsync(
                        data.FotoEvidencia,
                        operacionId,
                        "evidencia",
                        ct);

                if (!upload.isSuccess ||
                    string.IsNullOrWhiteSpace(upload.data))
                {
                    response.isSuccess = false;
                    response.code = upload.code;
                    response.message = upload.message;
                    response.desc = upload.desc;
                    response.data = null;

                    return response;
                }

                rutaEvidencia = upload.data;

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                // ====================================================
                // ASIGNACION AFECTADA
                // ====================================================

                ServicioEmpleadoRelevoDto? afectada =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        data.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (afectada == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación del empleado que debía presentarse.");
                }

                // ====================================================
                // ASIGNACION SALIENTE
                // ====================================================

                ServicioEmpleadoRelevoDto? saliente =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        data.ServicioEmpleadoSalienteId.Value,
                        ct,
                        transaction);

                if (saliente == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación del empleado saliente.");
                }

                if (afectada.ServicioId !=
                    saliente.ServicioId)
                {
                    throw new InvalidOperationException(
                        "La asignación afectada y la saliente pertenecen a servicios diferentes.");
                }

                // ====================================================
                // EVITAR SOLICITUD DUPLICADA
                // ====================================================

                bool existeSolicitud =
                    await _data.ExisteSolicitudActivaAsync(
                        conn,
                        data.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (existeSolicitud)
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaEvidencia,
                        ct);

                    rutaEvidencia = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Ya existe una solicitud de relevo activa para esta asignación.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // CATALOGOS
                // ====================================================

                const string sqlOrigen = @"
SELECT TOP (1)
    RelevoNoPlaneadoOrigenId
FROM dbo.CAT_RelevoNoPlaneadoOrigen
WHERE Clave = 'ASISTENCIA';";

                int? origenId =
                    await conn.ExecuteScalarAsync<int?>(
                        new CommandDefinition(
                            sqlOrigen,
                            transaction: transaction,
                            cancellationToken: ct));

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
                        "No están configurados los catálogos requeridos para el relevo por asistencia.");
                }

                // ====================================================
                // CREAR SOLICITUD
                // ====================================================

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
    @ServicioEmpleadoAfectadoId,
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
                                data.ServicioEmpleadoAfectadoId,
                                data.ServicioEmpleadoSalienteId,

                                OrigenId =
                                    origenId.Value,

                                EstatusId =
                                    estatusId.Value,

                                data.FechaHoraInicioCobertura,
                                data.FechaHoraFinCobertura,

                                MotivoRelevo =
                                    data.MotivoRelevo.Trim(),

                                MotivoNoPermanencia =
                                    string.IsNullOrWhiteSpace(
                                        data.MotivoNoPermanencia)
                                        ? null
                                        : data.MotivoNoPermanencia.Trim(),

                                RutaFotoEvidencia =
                                    rutaEvidencia,

                                FechaRegistro =
                                    fechaActual,

                                UsuarioRegistro =
                                    numeroUsuario.Trim()
                            },
                            transaction,
                            cancellationToken: ct));

                // ====================================================
                // INCIDENCIA DE FALTA
                // ====================================================

                await _integracion
                    .RegistrarIncidenciaFaltaRelevoAsync(
                        conn,
                        transaction,
                        afectada,
                        data.FechaHoraInicioCobertura,
                        numeroUsuario.Trim(),
                        fechaActual,
                        ct);

                // ====================================================
                // CHECK-OUT SALIENTE
                // ====================================================

                if (realizarCheckOut)
                {
                    await _integracion
                        .RealizarCheckOutSinRelevoAsync(
                            conn,
                            transaction,
                            asistenciaSalienteId,
                            data.ServicioEmpleadoSalienteId.Value,
                            numeroUsuario.Trim(),
                            fechaActual,
                            ct);
                }

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;

                response.message =
                    realizarCheckOut
                        ? "Se creó la solicitud de relevo y se registró el Check-Out."
                        : "Se creó la solicitud de relevo correctamente.";

                response.data =
                    new SolicitudRelevoNoPlaneadoDto
                    {
                        SolicitudRelevoNoPlaneadoId =
                            solicitudId,

                        ServicioEmpleadoAfectadoId =
                            data.ServicioEmpleadoAfectadoId,

                        ServicioEmpleadoSalienteId =
                            data.ServicioEmpleadoSalienteId,

                        RelevoNoPlaneadoOrigenId =
                            origenId.Value,

                        RelevoNoPlaneadoEstatusId =
                            estatusId.Value,

                        FechaHoraInicioCobertura =
                            data.FechaHoraInicioCobertura,

                        FechaHoraFinCobertura =
                            data.FechaHoraFinCobertura,

                        MotivoRelevo =
                            data.MotivoRelevo.Trim(),

                        MotivoNoPermanencia =
                            string.IsNullOrWhiteSpace(
                                data.MotivoNoPermanencia)
                                ? null
                                : data.MotivoNoPermanencia.Trim(),

                        RutaFotoEvidencia =
                            rutaEvidencia,

                        FechaRegistro =
                            fechaActual,

                        UsuarioRegistro =
                            numeroUsuario.Trim()
                    };

                rutaEvidencia = null;

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

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al crear la solicitud de relevo desde asistencia.";
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
                    rutaEvidencia,
                    ct);

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al crear la solicitud de relevo desde asistencia.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region CREAR SOLICITUD DESDE SUPERVISION

        internal async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudDesdeSupervisionAsync(
                long supervisionId,
                string motivoRelevo,
                string numeroSupervisor,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<SolicitudRelevoNoPlaneadoDto>();

            SqlTransaction? transaction = null;

            try
            {
                if (supervisionId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "SupervisionId es inválido.";
                    response.data = null;

                    return response;
                }

                if (string.IsNullOrWhiteSpace(motivoRelevo))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El motivo del retiro del elemento es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (string.IsNullOrWhiteSpace(numeroSupervisor))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message =
                        "No fue posible identificar al supervisor.";
                    response.data = null;

                    return response;
                }

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                // ====================================================
                // SUPERVISOR
                // ====================================================

                EmpleadoRelevoDto? supervisor =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroSupervisor.Trim(),
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

                // ====================================================
                // SUPERVISION
                // ====================================================

                const string sqlSupervision = @"
SELECT
    S.SupervisionId,
    S.ServicioEmpleadoId,
    S.SupervisorEmpleadoId,
    SE.ServicioId,
    SE.EmpleadoId AS EmpleadoAfectadoId,
    C.RutaFotoEmpleado
FROM dbo.Supervision S
INNER JOIN dbo.ServicioEmpleado SE
    ON SE.ServicioEmpleadoId =
       S.ServicioEmpleadoId
INNER JOIN dbo.Supervision_Comprobacion C
    ON C.SupervisionId =
       S.SupervisionId
WHERE S.SupervisionId =
      @SupervisionId;";

                RelevoSupervisionDataDto? supervision =
                    await conn.QueryFirstOrDefaultAsync<
                        RelevoSupervisionDataDto>(
                        new CommandDefinition(
                            sqlSupervision,
                            new
                            {
                                SupervisionId =
                                    supervisionId
                            },
                            transaction,
                            cancellationToken: ct));

                if (supervision == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe la supervisión indicada.";
                    response.data = null;

                    return response;
                }

                if (supervision.SupervisorEmpleadoId !=
                    supervisor.EmpleadoId)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 403;
                    response.message =
                        "La supervisión no fue realizada por el supervisor autenticado.";
                    response.data = null;

                    return response;
                }

                if (string.IsNullOrWhiteSpace(
                    supervision.RutaFotoEmpleado))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La supervisión no tiene fotografía de comprobación.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // ALCANCE SUPERVISOR
                // ====================================================

                bool puedeSupervisar =
                    await _data.EsSupervisorServicioAsync(
                        conn,
                        supervisor.EmpleadoId,
                        supervision.ServicioId,
                        fechaActual.Date,
                        ct,
                        transaction);

                if (!puedeSupervisar)
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

                // ====================================================
                // ASISTENCIA ACTIVA
                // ====================================================

                var asistencia =
                    await _integracion
                        .ObtenerAsistenciaActivaPorServicioEmpleadoForUpdateAsync(
                            conn,
                            transaction,
                            supervision.ServicioEmpleadoId,
                            ct);

                if (asistencia == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El elemento supervisado no tiene una asistencia activa.";
                    response.data = null;

                    return response;
                }

                if (asistencia.FechaHoraSalidaProgramada <=
                    fechaActual)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El turno del elemento ya terminó y no requiere cobertura.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // EVITAR SOLICITUD DUPLICADA
                //
                // SE CONSERVA LA CONSULTA DEL FLUJO ACTUAL.
                // ====================================================

                const string sqlExiste = @"
SELECT COUNT(1)
FROM dbo.SolicitudRelevoNoPlaneado SR
INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ES
    ON ES.RelevoNoPlaneadoEstatusId =
       SR.RelevoNoPlaneadoEstatusId
WHERE SR.ServicioEmpleadoAfectadoId =
      @ServicioEmpleadoId
  AND ES.Clave IN
  (
      'PENDIENTE_ASIGNACION',
      'EN_PROCESO'
  );";

                int existentes =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlExiste,
                            new
                            {
                                ServicioEmpleadoId =
                                    supervision.ServicioEmpleadoId
                            },
                            transaction,
                            cancellationToken: ct));

                if (existentes > 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Ya existe una solicitud de relevo activa para este elemento.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // CATALOGOS
                // ====================================================

                const string sqlOrigen = @"
SELECT TOP (1)
    RelevoNoPlaneadoOrigenId
FROM dbo.CAT_RelevoNoPlaneadoOrigen
WHERE Clave = 'SUPERVISION';";

                int? origenId =
                    await conn.ExecuteScalarAsync<int?>(
                        new CommandDefinition(
                            sqlOrigen,
                            transaction: transaction,
                            cancellationToken: ct));

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
                        "No están configurados los catálogos requeridos para el relevo por supervisión.");
                }

                // ====================================================
                // SOLICITUD
                // ====================================================

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
    @ServicioEmpleadoAfectadoId,
    @ServicioEmpleadoSalienteId,
    @OrigenId,
    @EstatusId,
    @FechaHoraInicioCobertura,
    @FechaHoraFinCobertura,
    @MotivoRelevo,
    NULL,
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
                                ServicioEmpleadoAfectadoId =
                                    supervision.ServicioEmpleadoId,

                                ServicioEmpleadoSalienteId =
                                    supervision.ServicioEmpleadoId,

                                OrigenId =
                                    origenId.Value,

                                EstatusId =
                                    estatusId.Value,

                                FechaHoraInicioCobertura =
                                    fechaActual,

                                FechaHoraFinCobertura =
                                    asistencia.FechaHoraSalidaProgramada,

                                MotivoRelevo =
                                    motivoRelevo.Trim(),

                                RutaFotoEvidencia =
                                    supervision.RutaFotoEmpleado,

                                FechaRegistro =
                                    fechaActual,

                                UsuarioRegistro =
                                    numeroSupervisor.Trim()
                            },
                            transaction,
                            cancellationToken: ct));

                // ====================================================
                // RETIRAR ELEMENTO
                // ====================================================

                await _integracion
                    .FinalizarAsistenciaPorSupervisionAsync(
                        conn,
                        transaction,
                        asistencia.AsistenciaId,
                        fechaActual,
                        ct);

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "El elemento fue retirado y se creó la solicitud de relevo.";
                response.desc =
                    "La solicitud quedó pendiente de asignación de cobertura.";

                response.data =
                    new SolicitudRelevoNoPlaneadoDto
                    {
                        SolicitudRelevoNoPlaneadoId =
                            solicitudId,

                        ServicioEmpleadoAfectadoId =
                            supervision.ServicioEmpleadoId,

                        ServicioEmpleadoSalienteId =
                            supervision.ServicioEmpleadoId,

                        RelevoNoPlaneadoOrigenId =
                            origenId.Value,

                        RelevoNoPlaneadoEstatusId =
                            estatusId.Value,

                        FechaHoraInicioCobertura =
                            fechaActual,

                        FechaHoraFinCobertura =
                            asistencia.FechaHoraSalidaProgramada,

                        MotivoRelevo =
                            motivoRelevo.Trim(),

                        MotivoNoPermanencia =
                            null,

                        RutaFotoEvidencia =
                            supervision.RutaFotoEmpleado,

                        FechaRegistro =
                            fechaActual,

                        UsuarioRegistro =
                            numeroSupervisor.Trim()
                    };

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
                    "Error SQL al crear el relevo desde la supervisión.";
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
                    "Error al crear el relevo desde la supervisión.";
                response.desc = ex.Message;
                response.data = null;

                return response;
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
            var response =
                new ResponseModel<SolicitudRelevoNoPlaneadoDto>();

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

                if (data == null ||
                    data.SolicitudRelevoNoPlaneadoId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "SolicitudRelevoNoPlaneadoId es inválido.";
                    response.data = null;

                    return response;
                }

                using var conn = _data.CrearConexion();
                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await _data.ObtenerFechaServidorAsync(
                        conn,
                        ct,
                        transaction);

                // ====================================================
                // SUPERVISOR
                // ====================================================

                EmpleadoRelevoDto? supervisor =
                    await _data.ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        numeroUsuario.Trim(),
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

                // ====================================================
                // SOLICITUD BLOQUEADA
                // ====================================================

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
                        "No existe la solicitud de relevo.";
                    response.data = null;

                    return response;
                }

                string? estatusActual =
                    await _data.ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct,
                        transaction);

                if (estatusActual !=
                        ESTATUS_PENDIENTE_ASIGNACION &&
                    estatusActual !=
                        ESTATUS_EN_PROCESO)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud ya no puede ser cancelada.";
                    response.desc =
                        $"Estatus actual: {estatusActual ?? "DESCONOCIDO"}.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // ASIGNACION AFECTADA
                // ====================================================

                ServicioEmpleadoRelevoDto? servicioEmpleado =
                    await _data.ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (servicioEmpleado == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación afectada por la solicitud.");
                }

                // ====================================================
                // ALCANCE SUPERVISOR
                // ====================================================

                bool tieneServicio =
                    await _data.EsSupervisorServicioAsync(
                        conn,
                        supervisor.EmpleadoId,
                        servicioEmpleado.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct,
                        transaction);

                if (!tieneServicio)
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

                // ====================================================
                // NO CANCELAR COBERTURA YA GENERADA
                // ====================================================

                bool coberturaGenerada =
                    await _data.ExisteCoberturaGeneradaAsync(
                        conn,
                        transaction,
                        solicitud.SolicitudRelevoNoPlaneadoId,
                        ct);

                if (coberturaGenerada)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud ya generó una cobertura y no puede cancelarse.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // ESTATUS CANCELADA DE ASIGNACION
                // ====================================================

                int? estatusAsignacionCanceladaId =
                    await _data.ObtenerEstatusAsignacionIdAsync(
                        conn,
                        ESTATUS_ASIGNACION_CANCELADA,
                        ct,
                        transaction);

                if (!estatusAsignacionCanceladaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus CANCELADA de asignaciones.");
                }

                // ====================================================
                // CANCELAR PROPUESTAS ABIERTAS
                // ====================================================

                const string sqlCancelarAsignaciones = @"
UPDATE A
SET
    RelevoAsignacionEstatusId = @EstatusCanceladaId,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
FROM dbo.RelevoNoPlaneadoAsignacion A
INNER JOIN dbo.CAT_RelevoAsignacionEstatus E
    ON E.RelevoAsignacionEstatusId =
       A.RelevoAsignacionEstatusId
WHERE A.SolicitudRelevoNoPlaneadoId =
      @SolicitudId
  AND E.Clave IN
  (
      'PENDIENTE_SUPERVISOR',
      'PENDIENTE_FIRMA_EMPLEADO'
  )
  AND A.ServicioEmpleadoTemporalId IS NULL;";

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlCancelarAsignaciones,
                        new
                        {
                            EstatusCanceladaId =
                                estatusAsignacionCanceladaId.Value,

                            FechaModificacion =
                                fechaActual,

                            UsuarioModificacion =
                                numeroUsuario.Trim(),

                            SolicitudId =
                                solicitud.SolicitudRelevoNoPlaneadoId
                        },
                        transaction,
                        cancellationToken: ct));

                // ====================================================
                // ESTATUS CANCELADO SOLICITUD
                // ====================================================

                int? estatusCanceladoId =
                    await _data.ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_CANCELADO,
                        ct,
                        transaction);

                if (!estatusCanceladoId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus CANCELADO.");
                }

                // ====================================================
                // CANCELAR SOLICITUD
                // ====================================================

                const string sqlCancelarSolicitud = @"
UPDATE dbo.SolicitudRelevoNoPlaneado
SET
    RelevoNoPlaneadoEstatusId = @EstatusCanceladoId,
    FechaModificacion = @FechaModificacion,
    UsuarioModificacion = @UsuarioModificacion
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND RelevoNoPlaneadoEstatusId = @EstatusAnteriorId;";

                int rows =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlCancelarSolicitud,
                            new
                            {
                                EstatusCanceladoId =
                                    estatusCanceladoId.Value,

                                FechaModificacion =
                                    fechaActual,

                                UsuarioModificacion =
                                    numeroUsuario.Trim(),

                                SolicitudId =
                                    solicitud.SolicitudRelevoNoPlaneadoId,

                                EstatusAnteriorId =
                                    solicitud.RelevoNoPlaneadoEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rows != 1)
                {
                    throw new InvalidOperationException(
                        "La solicitud cambió de estado antes de cancelarse.");
                }

                transaction.Commit();
                transaction = null;

                solicitud.RelevoNoPlaneadoEstatusId =
                    estatusCanceladoId.Value;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "La solicitud de relevo fue cancelada correctamente.";
                response.data = solicitud;

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
                    "Error SQL al cancelar la solicitud de relevo.";
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
                    "Error al cancelar la solicitud de relevo.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region VALIDACIONES

        private string? ValidarSolicitud(
            CrearSolicitudRelevoNoPlaneadoDto data)
        {
            if (data == null)
            {
                return
                    "La información de la solicitud es obligatoria.";
            }

            if (data.ServicioEmpleadoAfectadoId <= 0)
            {
                return
                    "ServicioEmpleadoAfectadoId es obligatorio.";
            }

            if (data.ServicioEmpleadoSalienteId.HasValue &&
                data.ServicioEmpleadoSalienteId.Value <= 0)
            {
                return
                    "ServicioEmpleadoSalienteId es inválido.";
            }

            if (data.ServicioEmpleadoSalienteId ==
                data.ServicioEmpleadoAfectadoId)
            {
                return
                    "La asignación saliente no puede ser la misma asignación que requiere cobertura.";
            }

            if (string.IsNullOrWhiteSpace(
                data.OrigenClave))
            {
                return
                    "El origen del relevo es obligatorio.";
            }

            string origen =
                data.OrigenClave
                    .Trim()
                    .ToUpperInvariant();

            if (!ORIGENES_VALIDOS.Contains(origen))
            {
                return
                    "El origen del relevo no es válido.";
            }

            if (data.FechaHoraInicioCobertura == default)
            {
                return
                    "La fecha y hora de inicio de cobertura es obligatoria.";
            }

            if (data.FechaHoraFinCobertura == default)
            {
                return
                    "La fecha y hora de fin de cobertura es obligatoria.";
            }

            if (data.FechaHoraFinCobertura <=
                data.FechaHoraInicioCobertura)
            {
                return
                    "La fecha de fin de cobertura debe ser posterior a la fecha de inicio.";
            }

            if (string.IsNullOrWhiteSpace(
                data.MotivoRelevo))
            {
                return
                    "El motivo del relevo es obligatorio.";
            }

            if (data.FotoEvidencia == null ||
                data.FotoEvidencia.Length == 0)
            {
                return
                    "La fotografía de evidencia es obligatoria.";
            }

            return null;
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