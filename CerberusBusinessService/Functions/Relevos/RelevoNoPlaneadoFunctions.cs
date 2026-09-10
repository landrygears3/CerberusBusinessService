using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Asistencias;
using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Relevos
{
    public class RelevoNoPlaneadoFunctions
    {
        #region CONSTANTES
        private const string ESTATUS_CANCELADO = "CANCELADO";
        private const string ESTATUS_ASIGNACION_CANCELADA = "CANCELADA";
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

        private const int ASISTENCIA_EN_TURNO = 1;

        private const int ASISTENCIA_FINALIZADA = 2;

        private const string ESTATUS_EN_PROCESO =
    "EN_PROCESO";

        private const string ESTATUS_PENDIENTE_SUPERVISOR =
            "PENDIENTE_SUPERVISOR";

        private const string ESTATUS_PENDIENTE_FIRMA_EMPLEADO =
            "PENDIENTE_FIRMA_EMPLEADO";

        private static readonly HashSet<string> TIPOS_COBERTURA_VALIDOS =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
        "EXTENSION",
        "SUSTITUTO",
        "SUPERVISOR"
            };
        private const string ESTATUS_PENDIENTE_ASIGNACION =
            "PENDIENTE_ASIGNACION";

        private static readonly HashSet<string>
            ORIGENES_VALIDOS =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "ASISTENCIA",
                    "SUPERVISION"
                };

        #endregion


        #region PROPIEDADES

        private readonly string _csCerberus;

        private readonly FileRelevoNoPlaneadoService
            _fileRelevoService;

        #endregion


        #region CONSTRUCTOR

        public RelevoNoPlaneadoFunctions(
            IConfiguration config,
            FileRelevoNoPlaneadoService fileRelevoService)
        {
            _csCerberus =
                config.GetConnectionString(
                    "DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");

            _fileRelevoService =
                fileRelevoService;
        }

        #endregion


        #region CREAR SOLICITUD

        public async Task<
            ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudAsync(
                CrearSolicitudRelevoNoPlaneadoDto data,
                string numeroUsuario,
                CancellationToken ct)
        {
            ResponseModel<SolicitudRelevoNoPlaneadoDto>
                response =
                    new ResponseModel<
                        SolicitudRelevoNoPlaneadoDto>();

            string? rutaFotoEvidencia =
                null;

            try
            {
                // ====================================================
                // 1. VALIDAR USUARIO
                // ====================================================

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message =
                        "No fue posible identificar al usuario.";
                    response.data = null;

                    return response;
                }


                // ====================================================
                // 2. VALIDAR REQUEST
                // ====================================================

                string? error =
                    ValidarSolicitud(data);

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


                using var conn =
                    new SqlConnection(
                        _csCerberus);


                await conn.OpenAsync(ct);


                // ====================================================
                // 3. VALIDAR SERVICIO EMPLEADO AFECTADO
                // ====================================================

                ServicioEmpleadoRelevoDto?
                    servicioEmpleadoAfectado =
                        await ObtenerServicioEmpleadoAsync(
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
                // 4. VALIDAR SERVICIO EMPLEADO SALIENTE
                // ====================================================

                if (data.ServicioEmpleadoSalienteId
                    .HasValue)
                {
                    ServicioEmpleadoRelevoDto?
                        servicioEmpleadoSaliente =
                            await ObtenerServicioEmpleadoAsync(
                                conn,
                                data
                                    .ServicioEmpleadoSalienteId
                                    .Value,
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


                    if (servicioEmpleadoSaliente
                            .ServicioId !=
                        servicioEmpleadoAfectado
                            .ServicioId)
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
                // 5. OBTENER CATÁLOGOS
                // ====================================================

                int? origenId =
                    await ObtenerOrigenIdAsync(
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
                    await ObtenerEstatusSolicitudIdAsync(
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
                // 6. EVITAR SOLICITUD ACTIVA DUPLICADA
                // ====================================================

                bool existeSolicitud =
                    await ExisteSolicitudActivaAsync(
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
                // 7. SUBIR EVIDENCIA
                // ====================================================

                string operacionId =
                    Guid.NewGuid()
                        .ToString("N");


                ResponseModel<string>
                    uploadResponse =
                        await _fileRelevoService
                            .UploadFileAsync(
                                data.FotoEvidencia,
                                operacionId,
                                "evidencia",
                                ct);


                if (!uploadResponse.isSuccess ||
                    string.IsNullOrWhiteSpace(
                        uploadResponse.data))
                {
                    response.isSuccess = false;
                    response.code =
                        uploadResponse.code;
                    response.message =
                        uploadResponse.message;
                    response.desc =
                        uploadResponse.desc;
                    response.data = null;

                    return response;
                }


                rutaFotoEvidencia =
                    uploadResponse.data;


                // ====================================================
                // 8. INSERTAR SOLICITUD
                // ====================================================

                using var transaction =
                    conn.BeginTransaction();


                try
                {
                    DateTime fechaRegistro =
                        await ObtenerFechaServidorAsync(
                            conn,
                            transaction,
                            ct);


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
                                    data
                                        .ServicioEmpleadoAfectadoId,

                                    data
                                        .ServicioEmpleadoSalienteId,

                                    RelevoNoPlaneadoOrigenId =
                                        origenId.Value,

                                    RelevoNoPlaneadoEstatusId =
                                        estatusId.Value,

                                    data
                                        .FechaHoraInicioCobertura,

                                    data
                                        .FechaHoraFinCobertura,

                                    MotivoRelevo =
                                        data.MotivoRelevo.Trim(),

                                    MotivoNoPermanencia =
                                        string.IsNullOrWhiteSpace(
                                            data
                                                .MotivoNoPermanencia)
                                            ? null
                                            : data
                                                .MotivoNoPermanencia!
                                                .Trim(),

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


                    // ================================================
                    // 9. RESPONSE
                    // ================================================

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
                                data
                                    .ServicioEmpleadoAfectadoId,

                            ServicioEmpleadoSalienteId =
                                data
                                    .ServicioEmpleadoSalienteId,

                            RelevoNoPlaneadoOrigenId =
                                origenId.Value,

                            RelevoNoPlaneadoEstatusId =
                                estatusId.Value,

                            FechaHoraInicioCobertura =
                                data
                                    .FechaHoraInicioCobertura,

                            FechaHoraFinCobertura =
                                data
                                    .FechaHoraFinCobertura,

                            MotivoRelevo =
                                data
                                    .MotivoRelevo
                                    .Trim(),

                            MotivoNoPermanencia =
                                string.IsNullOrWhiteSpace(
                                    data
                                        .MotivoNoPermanencia)
                                    ? null
                                    : data
                                        .MotivoNoPermanencia!
                                        .Trim(),

                            RutaFotoEvidencia =
                                rutaFotoEvidencia,

                            FechaRegistro =
                                fechaRegistro,

                            UsuarioRegistro =
                                numeroUsuario.Trim()
                        };


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
                response.desc =
                    ex.Message;
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
                response.desc =
                    ex.Message;
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
                return "La información de la solicitud es obligatoria.";
            }


            if (data.ServicioEmpleadoAfectadoId <= 0)
            {
                return "ServicioEmpleadoAfectadoId es obligatorio.";
            }


            if (data.ServicioEmpleadoSalienteId
                    .HasValue &&
                data.ServicioEmpleadoSalienteId.Value <= 0)
            {
                return "ServicioEmpleadoSalienteId es inválido.";
            }


            if (data.ServicioEmpleadoSalienteId ==
                data.ServicioEmpleadoAfectadoId)
            {
                return "La asignación saliente no puede ser la misma asignación que requiere cobertura.";
            }


            if (string.IsNullOrWhiteSpace(
                data.OrigenClave))
            {
                return "El origen del relevo es obligatorio.";
            }


            string origen =
                data.OrigenClave
                    .Trim()
                    .ToUpperInvariant();


            if (!ORIGENES_VALIDOS.Contains(
                origen))
            {
                return "El origen del relevo no es válido.";
            }


            if (data.FechaHoraInicioCobertura ==
                default)
            {
                return "La fecha y hora de inicio de cobertura es obligatoria.";
            }


            if (data.FechaHoraFinCobertura ==
                default)
            {
                return "La fecha y hora de fin de cobertura es obligatoria.";
            }


            if (data.FechaHoraFinCobertura <=
                data.FechaHoraInicioCobertura)
            {
                return "La fecha de fin de cobertura debe ser posterior a la fecha de inicio.";
            }


            if (string.IsNullOrWhiteSpace(
                data.MotivoRelevo))
            {
                return "El motivo del relevo es obligatorio.";
            }


            if (data.FotoEvidencia == null ||
                data.FotoEvidencia.Length == 0)
            {
                return "La fotografía de evidencia es obligatoria.";
            }


            return null;
        }

        #endregion


        #region SERVICIO EMPLEADO

        private async Task<ServicioEmpleadoRelevoDto?>
     ObtenerServicioEmpleadoAsync(
         SqlConnection conn,
         long servicioEmpleadoId,
         CancellationToken ct,
         SqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT
    SE.ServicioEmpleadoId,
    SE.ServicioId,
    SE.EmpleadoId,
    SE.TipoAsignacionServicioId,
    SE.EmpleadoCubiertoId,
    SE.FechaInicio,
    SE.FechaFin,
    SE.HoraEntrada,
    SE.HoraSalida,
    SE.SalidaDiaSiguiente,
    S.NombreServicio
FROM dbo.ServicioEmpleado SE
INNER JOIN dbo.Servicio S
    ON S.ServicioId = SE.ServicioId
WHERE SE.ServicioEmpleadoId = @ServicioEmpleadoId;";

            return await conn.QueryFirstOrDefaultAsync<
                ServicioEmpleadoRelevoDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ServicioEmpleadoId = servicioEmpleadoId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CATÁLOGOS

        private async Task<int?>
            ObtenerOrigenIdAsync(
                SqlConnection conn,
                string clave,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    RelevoNoPlaneadoOrigenId
FROM dbo.CAT_RelevoNoPlaneadoOrigen
WHERE Clave = @Clave
  AND Activo = 1;";


            return await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Clave = clave
                    },
                    cancellationToken: ct));
        }


        private async Task<int?>
         ObtenerEstatusSolicitudIdAsync(
             SqlConnection conn,
             string clave,
             CancellationToken ct,
             SqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT TOP (1)
    RelevoNoPlaneadoEstatusId
FROM dbo.CAT_RelevoNoPlaneadoEstatus
WHERE Clave = @Clave
  AND Activo = 1;";

            return await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Clave = clave
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region SOLICITUD ACTIVA

        private async Task<bool>
            ExisteSolicitudActivaAsync(
                SqlConnection conn,
                long servicioEmpleadoAfectadoId,
                CancellationToken ct)
        {
           const string sql = @"
SELECT COUNT(1)
FROM dbo.SolicitudRelevoNoPlaneado SR
INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ES
    ON ES.RelevoNoPlaneadoEstatusId = SR.RelevoNoPlaneadoEstatusId
WHERE SR.ServicioEmpleadoAfectadoId = @ServicioEmpleadoAfectadoId
  AND ES.Clave IN
  (
      'PENDIENTE_ASIGNACION',
      'EN_PROCESO'
  )
  AND SR.FechaHoraFinCobertura > SYSDATETIME();";


            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ServicioEmpleadoAfectadoId =
                            servicioEmpleadoAfectadoId
                    },
                    cancellationToken: ct));
        }

        #endregion


        #region FECHA SERVIDOR

        private async Task<DateTime>
            ObtenerFechaServidorAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                CancellationToken ct)
        {
            const string sql = @"
SELECT SYSDATETIME();";


            return await conn.ExecuteScalarAsync<DateTime>(
                new CommandDefinition(
                    sql,
                    transaction: transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region LIMPIEZA R2

        private async Task EliminarArchivoSeguroAsync(
            string? key,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(
                key))
            {
                return;
            }


            try
            {
                await _fileRelevoService
                    .DeleteFileAsync(
                        key,
                        ct);
            }
            catch
            {
                /*
                 * No sustituimos el error original por
                 * un error de limpieza de R2.
                 */
            }
        }

        #endregion


        #region CREAR ASIGNACION

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            CrearAsignacionAsync(
                CrearAsignacionRelevoNoPlaneadoDto data,
                string numeroUsuario,
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
                    response.message = "No fue posible identificar al usuario.";
                    response.data = null;

                    return response;
                }

                string? error = ValidarCrearAsignacion(data);

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
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        transaction,
                        ct);

                // ============================================================
                // SOLICITUD
                // ============================================================

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await ObtenerSolicitudParaAsignacionAsync(
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
                    await ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        transaction,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct);

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
                    await ExisteAsignacionActivaAsync(
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
                    await ObtenerServicioEmpleadoAsync(
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
                    await ObtenerEmpleadoRelevoAsync(
                        conn,
                        transaction,
                        data.EmpleadoIdAsignado,
                        ct);

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
                // REGLAS SEGUN TIPO DE COBERTURA
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
                    await ObtenerTipoCoberturaIdAsync(
                        conn,
                        transaction,
                        tipoCoberturaClave,
                        ct);

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
                    await ObtenerEstatusAsignacionIdAsync(
                        conn,
                        transaction,
                        estatusAsignacionClave,
                        ct);

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
                    await ObtenerEstatusSolicitudIdAsync(
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
                // INSERT PROPUESTA
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

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Propuesta de cobertura creada correctamente.";

                response.desc =
                    estatusAsignacionClave == ESTATUS_PENDIENTE_SUPERVISOR
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


        #region VALIDACION ASIGNACION

        private string? ValidarCrearAsignacion(
            CrearAsignacionRelevoNoPlaneadoDto data)
        {
            if (data == null)
            {
                return "La información de la asignación es obligatoria.";
            }

            if (data.SolicitudRelevoNoPlaneadoId <= 0)
            {
                return "SolicitudRelevoNoPlaneadoId es inválido.";
            }

            if (data.EmpleadoIdAsignado <= 0)
            {
                return "EmpleadoIdAsignado es inválido.";
            }

            if (string.IsNullOrWhiteSpace(
                data.TipoCoberturaClave))
            {
                return "El tipo de cobertura es obligatorio.";
            }

            string tipo =
                data.TipoCoberturaClave
                    .Trim()
                    .ToUpperInvariant();

            if (!TIPOS_COBERTURA_VALIDOS.Contains(tipo))
            {
                return "El tipo de cobertura no es válido.";
            }

            return null;
        }

        #endregion


        #region OBTENER SOLICITUD PARA ASIGNACION

        private async Task<SolicitudRelevoNoPlaneadoDto?>
            ObtenerSolicitudParaAsignacionAsync(
                SqlConnection conn,
                SqlTransaction? transaction,
                long solicitudId,
                CancellationToken ct)
        {
            const string sql = @"
SELECT
    SolicitudRelevoNoPlaneadoId,
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
FROM dbo.SolicitudRelevoNoPlaneado WITH (UPDLOCK, HOLDLOCK)
WHERE SolicitudRelevoNoPlaneadoId = @SolicitudId;";

            return await conn.QueryFirstOrDefaultAsync<
                SolicitudRelevoNoPlaneadoDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        SolicitudId = solicitudId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region EMPLEADO RELEVO

        private async Task<EmpleadoRelevoDto?>
            ObtenerEmpleadoRelevoAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                int empleadoId,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    D.ID AS EmpleadoId,
    ISNULL(
        LTRIM(RTRIM(D.UsuarioAsignado)),
        ''
    ) AS NumeroUsuario,
    CONCAT_WS(
        ' ',
        NULLIF(LTRIM(RTRIM(D.Nombres)), ''),
        NULLIF(LTRIM(RTRIM(D.ApellidoPaterno)), ''),
        NULLIF(LTRIM(RTRIM(D.ApellidoMaterno)), '')
    ) AS NombreCompleto
FROM dbo.DatosGeneralesEmpleado D
WHERE D.ID = @EmpleadoId;";

            return await conn.QueryFirstOrDefaultAsync<
                EmpleadoRelevoDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        EmpleadoId = empleadoId
                    },
                    transaction,
                    cancellationToken: ct));
        }


        private async Task<EmpleadoRelevoDto?>
    ObtenerEmpleadoPorNumeroUsuarioAsync(
        SqlConnection conn,
        SqlTransaction? transaction,
        string numeroUsuario,
        CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    D.ID AS EmpleadoId,
    ISNULL(
        LTRIM(RTRIM(D.UsuarioAsignado)),
        ''
    ) AS NumeroUsuario,
    CONCAT_WS(
        ' ',
        NULLIF(LTRIM(RTRIM(D.Nombres)), ''),
        NULLIF(LTRIM(RTRIM(D.ApellidoPaterno)), ''),
        NULLIF(LTRIM(RTRIM(D.ApellidoMaterno)), '')
    ) AS NombreCompleto
FROM dbo.DatosGeneralesEmpleado D
WHERE LTRIM(RTRIM(D.UsuarioAsignado)) = @NumeroUsuario;";

            return await conn.QueryFirstOrDefaultAsync<EmpleadoRelevoDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        NumeroUsuario = numeroUsuario
                    },
                    transaction,
                    cancellationToken: ct));
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
                    return "Una extensión requiere una asignación saliente.";
                }

                ServicioEmpleadoRelevoDto? asignacionSaliente =
                    await ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoSalienteId.Value,
                        ct,
                        transaction);

                if (asignacionSaliente == null)
                {
                    return "No existe la asignación saliente relacionada con la extensión.";
                }

                if (asignacionSaliente.EmpleadoId !=
                    empleadoAsignado.EmpleadoId)
                {
                    return "La extensión debe asignarse al empleado que actualmente se encuentra cubriendo el servicio.";
                }

                return null;
            }

            if (empleadoAsignado.EmpleadoId ==
                asignacionAfectada.EmpleadoId)
            {
                return "El empleado cuya asignación requiere relevo no puede ser propuesto como su propio sustituto.";
            }

            if (tipoCoberturaClave == "SUPERVISOR")
            {
                EmpleadoRelevoDto? empleadoActual =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        transaction,
                        numeroUsuario,
                        ct);

                if (empleadoActual == null)
                {
                    return "No existe un empleado relacionado con el supervisor autenticado.";
                }

                if (empleadoActual.EmpleadoId !=
                    empleadoAsignado.EmpleadoId)
                {
                    return "Una cobertura tipo SUPERVISOR únicamente puede asignarse al propio supervisor autenticado.";
                }
            }
            if (tipoCoberturaClave == "SUSTITUTO" ||
    tipoCoberturaClave == "SUPERVISOR")
            {
                EmpleadoRelevoDto? supervisorActual =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        transaction,
                        numeroUsuario,
                        ct);

                if (supervisorActual == null)
                {
                    return "No existe un empleado relacionado con el supervisor autenticado.";
                }

                bool puedeAdministrarServicio =
                    await EsSupervisorServicioAsync(
                        conn,
                        transaction,
                        supervisorActual.EmpleadoId,
                        asignacionAfectada.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct);

                if (!puedeAdministrarServicio)
                {
                    return "El supervisor no tiene asignado el servicio que requiere cobertura.";
                }
            }
            return null;
        }

        #endregion


        #region CATALOGOS ASIGNACION

        private async Task<int?>
            ObtenerTipoCoberturaIdAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                string clave,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    RelevoTipoCoberturaId
FROM dbo.CAT_RelevoTipoCobertura
WHERE Clave = @Clave
  AND Activo = 1;";

            return await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Clave = clave
                    },
                    transaction,
                    cancellationToken: ct));
        }


        private async Task<int?>
            ObtenerEstatusAsignacionIdAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                string clave,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    RelevoAsignacionEstatusId
FROM dbo.CAT_RelevoAsignacionEstatus
WHERE Clave = @Clave
  AND Activo = 1;";

            return await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Clave = clave
                    },
                    transaction,
                    cancellationToken: ct));
        }


        private async Task<string?> ObtenerClaveEstatusSolicitudAsync(
    SqlConnection conn,
    SqlTransaction? transaction,
    int estatusId,
    CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    Clave
FROM dbo.CAT_RelevoNoPlaneadoEstatus
WHERE RelevoNoPlaneadoEstatusId = @EstatusId;";

            return await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        EstatusId = estatusId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region ASIGNACION ACTIVA

        private async Task<bool>
            ExisteAsignacionActivaAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                long solicitudId,
                CancellationToken ct)
        {
            const string sql = @"
SELECT
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.RelevoNoPlaneadoAsignacion A
            INNER JOIN dbo.CAT_RelevoAsignacionEstatus E
                ON E.RelevoAsignacionEstatusId =
                   A.RelevoAsignacionEstatusId
            WHERE A.SolicitudRelevoNoPlaneadoId = @SolicitudId
              AND E.Clave IN
              (
                  'PENDIENTE_SUPERVISOR',
                  'PENDIENTE_FIRMA_EMPLEADO',
                  'ACEPTADA'
              )
        )
        THEN CAST(1 AS BIT)
        ELSE CAST(0 AS BIT)
    END;";

            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        SolicitudId = solicitudId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region RESPONSIVA

        private string GenerarTextoResponsiva(
            string nombreEmpleado,
            string nombreServicio,
            DateTime fechaHoraInicio,
            DateTime fechaHoraFin)
        {
            return
                $"Yo, {nombreEmpleado.Trim()}, acepto permanecer en el servicio " +
                $"{nombreServicio.Trim()} por necesidades operativas de manera " +
                $"voluntaria y eventual, sin que constituya jornada habitual. " +
                $"Cubriendo del {fechaHoraInicio:dd/MM/yyyy HH:mm} " +
                $"al {fechaHoraFin:dd/MM/yyyy HH:mm}.";
        }

        #endregion


        #region AUTORIZAR ASIGNACION

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AutorizarAsignacionAsync(
                AutorizarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            var response = new ResponseModel<RelevoNoPlaneadoAsignacionDto>();

            SqlTransaction? transaction = null;
            string? rutaFirmaSupervisor = null;

            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message = "No fue posible identificar al supervisor.";
                    response.data = null;

                    return response;
                }

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "El request es obligatorio.";
                    response.data = null;

                    return response;
                }

                if (data.RelevoNoPlaneadoAsignacionId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "RelevoNoPlaneadoAsignacionId es inválido.";
                    response.data = null;

                    return response;
                }

                if (data.FirmaSupervisor == null ||
                    data.FirmaSupervisor.Length == 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "La firma del supervisor es obligatoria.";
                    response.data = null;

                    return response;
                }

                using var conn = new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                // ============================================================
                // SUPERVISOR AUTENTICADO
                // ============================================================

                EmpleadoRelevoDto? supervisor =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        null,
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

                // ============================================================
                // PREVALIDACION DE ASIGNACION
                // ============================================================

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await ObtenerAsignacionRelevoAsync(
                        conn,
                        null,
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
                    await ObtenerClaveTipoCoberturaAsync(
                        conn,
                        null,
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
                    await ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        null,
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
                    await ObtenerSolicitudParaAsignacionAsync(
                        conn,
                        null,
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
                    await ObtenerServicioEmpleadoAsync(
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
                    await EsSupervisorServicioAsync(
                        conn,
                        null,
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
                // SUBIR FIRMA A R2
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

                rutaFirmaSupervisor = uploadResponse.data;

                // ============================================================
                // TRANSACCION
                // ============================================================

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        transaction,
                        ct);

                // ============================================================
                // VOLVER A BLOQUEAR Y VALIDAR LA PROPUESTA
                // ============================================================

                asignacion =
                    await ObtenerAsignacionRelevoAsync(
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
                    await ObtenerClaveTipoCoberturaAsync(
                        conn,
                        transaction,
                        asignacion.RelevoTipoCoberturaId,
                        ct);

                estatusAsignacion =
                    await ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        transaction,
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
                    await ObtenerSolicitudParaAsignacionAsync(
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
                    await ObtenerServicioEmpleadoAsync(
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

                // ============================================================
                // REVALIDAR ALCANCE DEL SUPERVISOR
                // ============================================================

                supervisorValido =
                    await EsSupervisorServicioAsync(
                        conn,
                        transaction,
                        supervisor.EmpleadoId,
                        servicioAfectado.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct);

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

                // ============================================================
                // NUEVO ESTATUS
                // ============================================================

                int? estatusPendienteFirmaId =
                    await ObtenerEstatusAsignacionIdAsync(
                        conn,
                        transaction,
                        ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                        ct);

                if (!estatusPendienteFirmaId.HasValue)
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaFirmaSupervisor,
                        ct);

                    rutaFirmaSupervisor = null;

                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        "No está configurado el estatus PENDIENTE_FIRMA_EMPLEADO.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // AUTORIZAR
                // ============================================================

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
                                    numeroUsuario.Trim(),

                                AsignacionId =
                                    asignacion.RelevoNoPlaneadoAsignacionId,

                                EstatusAnteriorId =
                                    asignacion.RelevoAsignacionEstatusId
                            },
                            transaction,
                            cancellationToken: ct));

                if (rows != 1)
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
                        "La propuesta cambió antes de completar la autorización.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // ARMAR RESPONSE
                // ============================================================

                asignacion.RelevoAsignacionEstatusId =
                    estatusPendienteFirmaId.Value;

                asignacion.SupervisorEmpleadoIdAutoriza =
                    supervisor.EmpleadoId;

                asignacion.RutaFirmaSupervisor =
                    rutaFirmaSupervisor;

                asignacion.FechaHoraFirmaSupervisor =
                    fechaActual;

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "La extensión fue autorizada correctamente.";
                response.desc =
                    "La propuesta quedó pendiente de firma y aceptación del empleado.";
                response.data = asignacion;

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


        #region OBTENER ASIGNACION RELEVO

        private async Task<RelevoNoPlaneadoAsignacionDto?>
            ObtenerAsignacionRelevoAsync(
                SqlConnection conn,
                SqlTransaction? transaction,
                long asignacionId,
                CancellationToken ct)
        {
            const string sql = @"
SELECT
    RelevoNoPlaneadoAsignacionId,
    SolicitudRelevoNoPlaneadoId,
    EmpleadoIdAsignado,
    RelevoTipoCoberturaId,
    RelevoAsignacionEstatusId,
    TextoResponsiva,
    SupervisorEmpleadoIdAutoriza,
    RutaFirmaSupervisor,
    FechaHoraFirmaSupervisor,
    RutaFirmaEmpleado,
    FechaHoraFirmaEmpleado,
    MotivoRechazo,
    FechaHoraRechazo,
    ServicioEmpleadoTemporalId,
    FechaAsignacion,
    UsuarioAsignacion
FROM dbo.RelevoNoPlaneadoAsignacion WITH (UPDLOCK, HOLDLOCK)
WHERE RelevoNoPlaneadoAsignacionId = @AsignacionId;";

            return await conn.QueryFirstOrDefaultAsync<
                RelevoNoPlaneadoAsignacionDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        AsignacionId = asignacionId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CLAVES CATALOGOS RELEVO

        private async Task<string?>
            ObtenerClaveTipoCoberturaAsync(
                SqlConnection conn,
                SqlTransaction? transaction,
                int tipoCoberturaId,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    Clave
FROM dbo.CAT_RelevoTipoCobertura
WHERE RelevoTipoCoberturaId = @TipoCoberturaId;";

            return await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TipoCoberturaId = tipoCoberturaId
                    },
                    transaction,
                    cancellationToken: ct));
        }


        private async Task<string?>
            ObtenerClaveEstatusAsignacionAsync(
                SqlConnection conn,
                SqlTransaction? transaction,
                int estatusId,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    Clave
FROM dbo.CAT_RelevoAsignacionEstatus
WHERE RelevoAsignacionEstatusId = @EstatusId;";

            return await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        EstatusId = estatusId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region SUPERVISOR SERVICIO

        private async Task<bool> EsSupervisorServicioAsync(
            SqlConnection conn,
            SqlTransaction? transaction,
            int supervisorEmpleadoId,
            int servicioId,
            DateTime fecha,
            CancellationToken ct)
        {
            const string sql = @"
SELECT
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.ServicioSupervisor
            WHERE ServicioId = @ServicioId
              AND SupervisorEmpleadoId = @SupervisorEmpleadoId
              AND FechaInicio <= @Fecha
              AND
              (
                  FechaFin IS NULL
                  OR FechaFin >= @Fecha
              )
        )
        THEN CAST(1 AS BIT)
        ELSE CAST(0 AS BIT)
    END;";

            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ServicioId = servicioId,
                        SupervisorEmpleadoId = supervisorEmpleadoId,
                        Fecha = fecha.Date
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region FIRMAR RESPONSIVA

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            FirmarResponsivaAsync(
                FirmarResponsivaRelevoNoPlaneadoRequest data,
                string numeroUsuario,
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
                    response.message = "No fue posible identificar al empleado.";
                    response.data = null;

                    return response;
                }

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "El request es obligatorio.";
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

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                // ============================================================
                // EMPLEADO AUTENTICADO
                // ============================================================

                EmpleadoRelevoDto? empleado =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        null,
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

                // ============================================================
                // PREVALIDAR PROPUESTA
                // ============================================================

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await ObtenerAsignacionRelevoAsync(
                        conn,
                        null,
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

                if (asignacion.EmpleadoIdAsignado != empleado.EmpleadoId)
                {
                    response.isSuccess = false;
                    response.code = 403;
                    response.message =
                        "La responsiva únicamente puede ser firmada por el empleado asignado.";
                    response.data = null;

                    return response;
                }

                string? estatusAsignacion =
                    await ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        null,
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
                    await ObtenerClaveTipoCoberturaAsync(
                        conn,
                        null,
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

                // ============================================================
                // EXTENSION REQUIERE AUTORIZACION PREVIA
                // ============================================================

                if (string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (!asignacion.SupervisorEmpleadoIdAutoriza.HasValue ||
                        string.IsNullOrWhiteSpace(asignacion.RutaFirmaSupervisor) ||
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
                    await ObtenerSolicitudParaAsignacionAsync(
                        conn,
                        null,
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
                    await ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        null,
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

                rutaFirmaAceptacion = uploadResponse.data;

                // ============================================================
                // TRANSACCION
                // ============================================================

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        transaction,
                        ct);

                // ============================================================
                // REVALIDAR CON BLOQUEO
                // ============================================================

                asignacion =
                    await ObtenerAsignacionRelevoAsync(
                        conn,
                        transaction,
                        data.RelevoNoPlaneadoAsignacionId,
                        ct);

                if (asignacion == null)
                {
                    throw new InvalidOperationException(
                        "La propuesta dejó de existir antes de completar la firma.");
                }

                if (asignacion.EmpleadoIdAsignado != empleado.EmpleadoId)
                {
                    throw new InvalidOperationException(
                        "La propuesta cambió de empleado antes de completar la firma.");
                }

                estatusAsignacion =
                    await ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        transaction,
                        asignacion.RelevoAsignacionEstatusId,
                        ct);

                if (!string.Equals(
                    estatusAsignacion,
                    ESTATUS_PENDIENTE_FIRMA_EMPLEADO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaFirmaAceptacion,
                        ct);

                    rutaFirmaAceptacion = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La propuesta cambió de estado antes de completar la firma.";
                    response.data = null;

                    return response;
                }

                solicitud =
                    await ObtenerSolicitudParaAsignacionAsync(
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
                    await ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        transaction,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct);

                if (!string.Equals(
                    estatusSolicitud,
                    ESTATUS_EN_PROCESO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(
                        rutaFirmaAceptacion,
                        ct);

                    rutaFirmaAceptacion = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La solicitud cambió de estado antes de completar la firma.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // ASIGNACION AFECTADA
                // ============================================================

                ServicioEmpleadoRelevoDto? asignacionAfectada =
                    await ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (asignacionAfectada == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación de servicio que requiere cobertura.");
                }

                // ============================================================
                // TIPO ASIGNACION FALTA
                // ============================================================

                int? tipoAsignacionFaltaId =
                    await ObtenerTipoAsignacionServicioIdAsync(
                        conn,
                        transaction,
                        TIPO_ASIGNACION_FALTA,
                        ct);

                if (!tipoAsignacionFaltaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el tipo de asignación FALTA.");
                }

                // ============================================================
                // CREAR SERVICIO EMPLEADO TEMPORAL
                // ============================================================

                long servicioEmpleadoTemporalId =
                    await CrearServicioEmpleadoTemporalAsync(
                        conn,
                        transaction,
                        solicitud,
                        asignacionAfectada,
                        empleado,
                        tipoAsignacionFaltaId.Value,
                        fechaActual,
                        numeroUsuario.Trim(),
                        ct);

                // ============================================================
                // SI ES EXTENSION, CERRAR TURNO ORIGINAL Y CREAR EL NUEVO
                // ============================================================

                if (string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await CrearAsistenciaExtensionAsync(
                        conn,
                        transaction,
                        solicitud,
                        asignacionAfectada,
                        servicioEmpleadoTemporalId,
                        empleado,
                        fechaActual,
                        numeroUsuario.Trim(),
                        ct);
                }

                // ============================================================
                // ESTATUS ACEPTADA
                // ============================================================

                int? estatusAceptadaId =
                    await ObtenerEstatusAsignacionIdAsync(
                        conn,
                        transaction,
                        ESTATUS_ACEPTADA,
                        ct);

                if (!estatusAceptadaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus ACEPTADA.");
                }

                // ============================================================
                // GUARDAR FIRMA Y SERVICIO TEMPORAL
                // ============================================================

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
                                    numeroUsuario.Trim(),

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
                    await ObtenerEstatusSolicitudIdAsync(
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
                                    numeroUsuario.Trim(),

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

                // ============================================================
                // RESPONSE
                // ============================================================

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

                response.data = asignacion;

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


        #region TIPO ASIGNACION SERVICIO

        private async Task<int?> ObtenerTipoAsignacionServicioIdAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            string clave,
            CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    TipoAsignacionServicioId
FROM dbo.CAT_TipoAsignacionServicio
WHERE Clave = @Clave
  AND Activo = 1;";

            return await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Clave = clave
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CREAR SERVICIO EMPLEADO TEMPORAL

        private async Task<long> CrearServicioEmpleadoTemporalAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            SolicitudRelevoNoPlaneadoDto solicitud,
            ServicioEmpleadoRelevoDto asignacionAfectada,
            EmpleadoRelevoDto empleadoAsignado,
            int tipoAsignacionFaltaId,
            DateTime fechaActual,
            string numeroUsuario,
            CancellationToken ct)
        {
            DateTime fechaTurno =
                solicitud.FechaHoraInicioCobertura.Date;

            TimeSpan horaEntrada =
                solicitud.FechaHoraInicioCobertura.TimeOfDay;

            TimeSpan horaSalida =
                solicitud.FechaHoraFinCobertura.TimeOfDay;

            bool salidaDiaSiguiente =
                solicitud.FechaHoraFinCobertura.Date >
                solicitud.FechaHoraInicioCobertura.Date;

            string observaciones =
                $"Relevo no planeado. Solicitud: " +
                $"{solicitud.SolicitudRelevoNoPlaneadoId}.";

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
    @EmpleadoCubiertoId,
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
                        ServicioId =
                            asignacionAfectada.ServicioId,

                        EmpleadoId =
                            empleadoAsignado.EmpleadoId,

                        TipoAsignacionServicioId =
                            tipoAsignacionFaltaId,

                        EmpleadoCubiertoId =
                            asignacionAfectada.EmpleadoId,

                        FechaInicio =
                            fechaTurno,

                        FechaFin =
                            fechaTurno,

                        HoraEntrada =
                            horaEntrada,

                        HoraSalida =
                            horaSalida,

                        SalidaDiaSiguiente =
                            salidaDiaSiguiente,

                        Observaciones =
                            observaciones,

                        FechaAlta =
                            fechaActual,

                        UsuarioAlta =
                            numeroUsuario
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CREAR ASISTENCIA EXTENSION

        private async Task<long> CrearAsistenciaExtensionAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            SolicitudRelevoNoPlaneadoDto solicitud,
            ServicioEmpleadoRelevoDto asignacionAfectada,
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

            // ================================================================
            // CERRAR ASISTENCIA ORIGINAL
            // ================================================================

            const string sqlCerrar = @"
UPDATE dbo.Asistencia
SET
    FechaHoraCheckOut = @FechaHoraCheckOut,
    Estatus = @EstatusFinalizada
OUTPUT INSERTED.AsistenciaId
WHERE ServicioEmpleadoId = @ServicioEmpleadoSalienteId
  AND NumeroEmpleadoEntrante = @NumeroEmpleado
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL;";

            long? asistenciaOriginalId =
                await conn.ExecuteScalarAsync<long?>(
                    new CommandDefinition(
                        sqlCerrar,
                        new
                        {
                            FechaHoraCheckOut =
                                solicitud.FechaHoraInicioCobertura,

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

            if (!asistenciaOriginalId.HasValue)
            {
                throw new InvalidOperationException(
                    "El empleado ya no tiene activo el turno que debía extenderse.");
            }

            // ================================================================
            // CREAR ASISTENCIA DE EXTENSION
            // ================================================================
            //
            // Se copia la geolocalización del turno que el empleado ya estaba
            // cubriendo. La extensión es continuidad del mismo elemento en sitio.
            // ================================================================

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
OUTPUT INSERTED.AsistenciaId
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
    @Estatus,
    @FechaRegistro,
    @UsuarioRegistro
FROM dbo.Asistencia A
WHERE A.AsistenciaId = @AsistenciaOriginalId;";

            long? nuevaAsistenciaId =
                await conn.ExecuteScalarAsync<long?>(
                    new CommandDefinition(
                        sqlInsert,
                        new
                        {
                            ServicioId =
                                asignacionAfectada.ServicioId,

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

                            Estatus =
                                ASISTENCIA_EN_TURNO,

                            FechaRegistro =
                                fechaActual,

                            UsuarioRegistro =
                                numeroUsuario,

                            AsistenciaOriginalId =
                                asistenciaOriginalId.Value
                        },
                        transaction,
                        cancellationToken: ct));

            if (!nuevaAsistenciaId.HasValue)
            {
                throw new InvalidOperationException(
                    "No fue posible crear la asistencia de extensión.");
            }

            return nuevaAsistenciaId.Value;
        }

        #endregion


        #region RECHAZAR ASIGNACION EMPLEADO

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionAsync(
                RechazarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
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

                if (string.IsNullOrWhiteSpace(data.MotivoRechazo))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El motivo de rechazo es obligatorio.";
                    response.data = null;

                    return response;
                }

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        transaction,
                        ct);

                // ============================================================
                // EMPLEADO AUTENTICADO
                // ============================================================

                EmpleadoRelevoDto? empleado =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        transaction,
                        numeroUsuario.Trim(),
                        ct);

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

                // ============================================================
                // PROPUESTA
                // ============================================================

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await ObtenerAsignacionRelevoAsync(
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

                if (asignacion.EmpleadoIdAsignado != empleado.EmpleadoId)
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

                // ============================================================
                // VALIDAR ESTATUS
                // ============================================================

                string? estatusAsignacion =
                    await ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        transaction,
                        asignacion.RelevoAsignacionEstatusId,
                        ct);

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

                // ============================================================
                // SOLICITUD
                // ============================================================

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await ObtenerSolicitudParaAsignacionAsync(
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
                    await ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        transaction,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct);

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

                // ============================================================
                // TIPO DE COBERTURA
                // ============================================================

                string? tipoCobertura =
                    await ObtenerClaveTipoCoberturaAsync(
                        conn,
                        transaction,
                        asignacion.RelevoTipoCoberturaId,
                        ct);

                if (string.IsNullOrWhiteSpace(tipoCobertura))
                {
                    throw new InvalidOperationException(
                        "No fue posible determinar el tipo de cobertura.");
                }

                // ============================================================
                // EXTENSION: REALIZAR CHECK-OUT
                // ============================================================

                if (string.Equals(
                    tipoCobertura,
                    "EXTENSION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await CerrarAsistenciaPorRechazoExtensionAsync(
                        conn,
                        transaction,
                        solicitud,
                        empleado,
                        fechaActual,
                        ct);
                }

                // ============================================================
                // ESTATUS RECHAZADA
                // ============================================================

                int? estatusRechazadaId =
                    await ObtenerEstatusAsignacionIdAsync(
                        conn,
                        transaction,
                        ESTATUS_RECHAZADA_EMPLEADO,
                        ct);

                if (!estatusRechazadaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus RECHAZADA_EMPLEADO.");
                }

                // ============================================================
                // ACTUALIZAR PROPUESTA
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
                                    numeroUsuario.Trim(),

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

                // ============================================================
                // SOLICITUD -> PENDIENTE_ASIGNACION
                // ============================================================

                int? estatusPendienteAsignacionId =
                    await ObtenerEstatusSolicitudIdAsync(
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
                                    numeroUsuario.Trim(),

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

                response.data = asignacion;

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


        #region CHECKOUT POR RECHAZO DE EXTENSION

        private async Task CerrarAsistenciaPorRechazoExtensionAsync(
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
    FechaHoraCheckOut = @FechaHoraCheckOut,
    Estatus = @EstatusFinalizada
OUTPUT INSERTED.AsistenciaId
WHERE ServicioEmpleadoId = @ServicioEmpleadoSalienteId
  AND NumeroEmpleadoEntrante = @NumeroEmpleado
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL;";

            long? asistenciaId =
                await conn.ExecuteScalarAsync<long?>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            FechaHoraCheckOut =
                                fechaActual,

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

            if (!asistenciaId.HasValue)
            {
                throw new InvalidOperationException(
                    "No existe una asistencia activa para realizar el Check-Out de la extensión rechazada.");
            }
        }

        #endregion


        #region RECHAZAR ASIGNACION SUPERVISOR

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionSupervisorAsync(
                RechazarAsignacionSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
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

                if (string.IsNullOrWhiteSpace(data.MotivoRechazo))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El motivo de rechazo es obligatorio.";
                    response.data = null;

                    return response;
                }

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        transaction,
                        ct);

                // ============================================================
                // SUPERVISOR AUTENTICADO
                // ============================================================

                EmpleadoRelevoDto? supervisor =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        transaction,
                        numeroUsuario.Trim(),
                        ct);

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

                // ============================================================
                // PROPUESTA
                // ============================================================

                RelevoNoPlaneadoAsignacionDto? asignacion =
                    await ObtenerAsignacionRelevoAsync(
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

                // ============================================================
                // SOLO EXTENSION
                // ============================================================

                string? tipoCobertura =
                    await ObtenerClaveTipoCoberturaAsync(
                        conn,
                        transaction,
                        asignacion.RelevoTipoCoberturaId,
                        ct);

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

                // ============================================================
                // ESTATUS ACTUAL
                // ============================================================

                string? estatusAsignacion =
                    await ObtenerClaveEstatusAsignacionAsync(
                        conn,
                        transaction,
                        asignacion.RelevoAsignacionEstatusId,
                        ct);

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

                // ============================================================
                // SOLICITUD
                // ============================================================

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await ObtenerSolicitudParaAsignacionAsync(
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
                    await ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        transaction,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct);

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

                // ============================================================
                // SERVICIO AFECTADO
                // ============================================================

                ServicioEmpleadoRelevoDto? servicioAfectado =
                    await ObtenerServicioEmpleadoAsync(
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

                // ============================================================
                // VALIDAR ALCANCE DEL SUPERVISOR
                // ============================================================

                bool puedeAdministrarServicio =
                    await EsSupervisorServicioAsync(
                        conn,
                        transaction,
                        supervisor.EmpleadoId,
                        servicioAfectado.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct);

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

                // ============================================================
                // OBTENER ESTATUS RECHAZADA_SUPERVISOR
                // ============================================================

                int? estatusRechazadaId =
                    await ObtenerEstatusAsignacionIdAsync(
                        conn,
                        transaction,
                        ESTATUS_RECHAZADA_SUPERVISOR,
                        ct);

                if (!estatusRechazadaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus RECHAZADA_SUPERVISOR.");
                }

                // ============================================================
                // RECHAZAR PROPUESTA
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
                                    numeroUsuario.Trim(),

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

                // ============================================================
                // SOLICITUD -> PENDIENTE_ASIGNACION
                // ============================================================

                int? estatusPendienteAsignacionId =
                    await ObtenerEstatusSolicitudIdAsync(
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
                                    numeroUsuario.Trim(),

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

                // ============================================================
                // RESPONSE
                // ============================================================

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
                response.desc =
                    ex.Message;
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
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
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
                    response.message = "SolicitudRelevoNoPlaneadoId es inválido.";
                    response.data = null;

                    return response;
                }

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

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
                                SolicitudId = solicitudId
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
                                SolicitudId = solicitudId
                            },
                            cancellationToken: ct));

                solicitud.Asignaciones =
                    asignaciones.ToList();

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Solicitud de relevo obtenida correctamente.";
                response.desc = null;
                response.data = solicitud;

                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al obtener la solicitud de relevo.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener la solicitud de relevo.";
                response.desc = ex.Message;
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
                new ResponseModel<List<RelevoPendienteEmpleadoResponse>>();

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
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                EmpleadoRelevoDto? empleado =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        null,
                        numeroUsuario.Trim(),
                        ct);

                if (empleado == null)
                {
                    response.isSuccess = true;
                    response.code = 200;
                    response.message =
                        "El usuario no tiene relevos pendientes.";
                    response.data =
                        new List<RelevoPendienteEmpleadoResponse>();

                    return response;
                }

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

WHERE A.EmpleadoIdAsignado = @EmpleadoId
  AND EA.Clave = @EstatusPendienteFirma
  AND ES.Clave = @EstatusEnProceso
  AND SR.FechaHoraFinCobertura > SYSDATETIME()

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
                response.data = result.ToList();

                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al obtener los relevos pendientes del empleado.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los relevos pendientes del empleado.";
                response.desc = ex.Message;
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
                new ResponseModel<List<RelevoPendienteSupervisorResponse>>();

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
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                EmpleadoRelevoDto? supervisor =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        null,
                        numeroUsuario.Trim(),
                        ct);

                if (supervisor == null)
                {
                    response.isSuccess = true;
                    response.code = 200;
                    response.message =
                        "El usuario no tiene relevos pendientes.";
                    response.data =
                        new List<RelevoPendienteSupervisorResponse>();

                    return response;
                }

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
        ELSE LTRIM(RTRIM(EP.UsuarioAsignado))
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

AND SR.FechaHoraFinCobertura > SYSDATETIME()

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
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los relevos pendientes del supervisor.";
                response.desc = ex.Message;
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
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                long? solicitudId =
                    await ObtenerSolicitudIdPorAsignacionAsync(
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

                /*
                 * El detalle central del relevo siempre se obtiene
                 * desde la solicitud.
                 *
                 * Esto permite que Asistencias, Servicios Asignados,
                 * Supervisión y Notificaciones consuman exactamente
                 * la misma estructura.
                 */
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
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener la asignación de relevo.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region OBTENER SOLICITUD POR ASIGNACION

private async Task<long?>
    ObtenerSolicitudIdPorAsignacionAsync(
        SqlConnection conn,
        long relevoNoPlaneadoAsignacionId,
        CancellationToken ct)
{
    const string sql = @"
SELECT TOP (1)
    SolicitudRelevoNoPlaneadoId
FROM dbo.RelevoNoPlaneadoAsignacion
WHERE RelevoNoPlaneadoAsignacionId =
      @RelevoNoPlaneadoAsignacionId;";

    return await conn.ExecuteScalarAsync<long?>(
        new CommandDefinition(
            sql,
            new
            {
                RelevoNoPlaneadoAsignacionId =
                    relevoNoPlaneadoAsignacionId
            },
            cancellationToken: ct));
}

        #endregion


        #region ASIGNAR SUSTITUTO

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarEmpleadoAsync(
                AsignarEmpleadoRelevoNoPlaneadoRequest data,
                string numeroUsuario,
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
                    message = "SolicitudRelevoNoPlaneadoId es inválido.",
                    data = null
                };
            }

            if (data.EmpleadoIdAsignado <= 0)
            {
                return new ResponseModel<RelevoNoPlaneadoAsignacionDto>
                {
                    isSuccess = false,
                    code = 400,
                    message = "EmpleadoIdAsignado es inválido.",
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
                ct);
        }

        #endregion


        #region SUPERVISOR SE ASIGNA

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarseSupervisorAsync(
                AsignarseSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
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

                using (var conn = new SqlConnection(_csCerberus))
                {
                    await conn.OpenAsync(ct);

                    EmpleadoRelevoDto? supervisor =
                        await ObtenerEmpleadoPorNumeroUsuarioAsync(
                            conn,
                            null,
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

                using (var conn = new SqlConnection(_csCerberus))
                {
                    await conn.OpenAsync(ct);

                    EmpleadoRelevoDto? empleado =
                        await ObtenerEmpleadoPorNumeroUsuarioAsync(
                            conn,
                            null,
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


        #region CREAR RELEVO DESDE SUPERVISION

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
                    response.message = "SupervisionId es inválido.";
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

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                DateTime fechaActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        transaction,
                        ct);

                // ============================================================
                // SUPERVISOR AUTENTICADO
                // ============================================================

                EmpleadoRelevoDto? supervisor =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        transaction,
                        numeroSupervisor.Trim(),
                        ct);

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

                // ============================================================
                // SUPERVISION REALIZADA
                // ============================================================

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
    ON SE.ServicioEmpleadoId = S.ServicioEmpleadoId
INNER JOIN dbo.Supervision_Comprobacion C
    ON C.SupervisionId = S.SupervisionId
WHERE S.SupervisionId = @SupervisionId;";

                RelevoSupervisionDataDto? supervision =
                    await conn.QueryFirstOrDefaultAsync<RelevoSupervisionDataDto>(
                        new CommandDefinition(
                            sqlSupervision,
                            new
                            {
                                SupervisionId = supervisionId
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

                if (supervision.SupervisorEmpleadoId != supervisor.EmpleadoId)
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

                if (string.IsNullOrWhiteSpace(supervision.RutaFotoEmpleado))
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

                // ============================================================
                // VALIDAR SERVICIO DEL SUPERVISOR
                // ============================================================

                bool puedeSupervisar =
                    await EsSupervisorServicioAsync(
                        conn,
                        transaction,
                        supervisor.EmpleadoId,
                        supervision.ServicioId,
                        fechaActual.Date,
                        ct);

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

                // ============================================================
                // ASISTENCIA ACTIVA DEL ELEMENTO RETIRADO
                // ============================================================

                const string sqlAsistencia = @"
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
FROM dbo.Asistencia WITH (UPDLOCK, HOLDLOCK)
WHERE ServicioEmpleadoId = @ServicioEmpleadoId
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL
ORDER BY
    FechaHoraCheckIn DESC,
    AsistenciaId DESC;";

                AsistenciaActivaCheckOutDto? asistencia =
                    await conn.QueryFirstOrDefaultAsync<AsistenciaActivaCheckOutDto>(
                        new CommandDefinition(
                            sqlAsistencia,
                            new
                            {
                                ServicioEmpleadoId =
                                    supervision.ServicioEmpleadoId,

                                EstatusEnTurno =
                                    ASISTENCIA_EN_TURNO
                            },
                            transaction,
                            cancellationToken: ct));

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

                if (asistencia.FechaHoraSalidaProgramada <= fechaActual)
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

                // ============================================================
                // EVITAR SOLICITUD DUPLICADA
                // ============================================================

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

                // ============================================================
                // CATALOGOS
                // ============================================================

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
                    await ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_ASIGNACION,
                        ct,
                        transaction);

                if (!origenId.HasValue || !estatusId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No están configurados los catálogos requeridos para el relevo por supervisión.");
                }

                // ============================================================
                // CREAR SOLICITUD
                // ============================================================

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

                // ============================================================
                // RETIRAR ELEMENTO DEL TURNO
                // ============================================================

                const string sqlCheckOut = @"
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
                            sqlCheckOut,
                            new
                            {
                                FechaHoraCheckOut =
                                    fechaActual,

                                EstatusFinalizada =
                                    ASISTENCIA_FINALIZADA,

                                AsistenciaId =
                                    asistencia.AsistenciaId,

                                EstatusEnTurno =
                                    ASISTENCIA_EN_TURNO
                            },
                            transaction,
                            cancellationToken: ct));

                if (rows != 1)
                {
                    throw new InvalidOperationException(
                        "La asistencia cambió antes de retirar al elemento.");
                }

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
            var response = new ResponseModel<SolicitudRelevoNoPlaneadoDto>();
            SqlTransaction? transaction = null;

            try
            {
                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message = "No fue posible identificar al supervisor.";
                    response.data = null;
                    return response;
                }

                if (data == null || data.SolicitudRelevoNoPlaneadoId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "SolicitudRelevoNoPlaneadoId es inválido.";
                    response.data = null;
                    return response;
                }

                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync(ct);

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await ObtenerFechaServidorAsync(conn, transaction, ct);

                EmpleadoRelevoDto? supervisor =
                    await ObtenerEmpleadoPorNumeroUsuarioAsync(
                        conn,
                        transaction,
                        numeroUsuario.Trim(),
                        ct);

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

                SolicitudRelevoNoPlaneadoDto? solicitud =
                    await ObtenerSolicitudParaAsignacionAsync(
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
                    response.message = "No existe la solicitud de relevo.";
                    response.data = null;
                    return response;
                }

                string? estatusActual =
                    await ObtenerClaveEstatusSolicitudAsync(
                        conn,
                        transaction,
                        solicitud.RelevoNoPlaneadoEstatusId,
                        ct);

                if (estatusActual != ESTATUS_PENDIENTE_ASIGNACION &&
                    estatusActual != ESTATUS_EN_PROCESO)
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

                ServicioEmpleadoRelevoDto? servicioEmpleado =
                    await ObtenerServicioEmpleadoAsync(
                        conn,
                        solicitud.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (servicioEmpleado == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación afectada por la solicitud.");
                }

                bool tieneServicio =
                    await EsSupervisorServicioAsync(
                        conn,
                        transaction,
                        supervisor.EmpleadoId,
                        servicioEmpleado.ServicioId,
                        solicitud.FechaHoraInicioCobertura.Date,
                        ct);

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

                // No permitimos cancelar algo que ya generó cobertura.
                const string sqlAceptada = @"
SELECT COUNT(1)
FROM dbo.RelevoNoPlaneadoAsignacion A
INNER JOIN dbo.CAT_RelevoAsignacionEstatus E
    ON E.RelevoAsignacionEstatusId = A.RelevoAsignacionEstatusId
WHERE A.SolicitudRelevoNoPlaneadoId = @SolicitudId
  AND
  (
      E.Clave = 'ACEPTADA'
      OR A.ServicioEmpleadoTemporalId IS NOT NULL
  );";

                int aceptadas =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlAceptada,
                            new
                            {
                                SolicitudId =
                                    solicitud.SolicitudRelevoNoPlaneadoId
                            },
                            transaction,
                            cancellationToken: ct));

                if (aceptadas > 0)
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

                int? estatusAsignacionCanceladaId =
                    await ObtenerEstatusAsignacionIdAsync(
                        conn,
                        transaction,
                        ESTATUS_ASIGNACION_CANCELADA,
                        ct);

                if (!estatusAsignacionCanceladaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus CANCELADA de asignaciones.");
                }

                // Cancela solamente propuestas todavía abiertas.
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
WHERE A.SolicitudRelevoNoPlaneadoId = @SolicitudId
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

                int? estatusCanceladoId =
                    await ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_CANCELADO,
                        ct,
                        transaction);

                if (!estatusCanceladoId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No está configurado el estatus CANCELADO.");
                }

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
                try { transaction?.Rollback(); } catch { }

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
                try { transaction?.Rollback(); } catch { }

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


        #region CREAR SOLICITUD DESDE ASISTENCIA

        internal async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudDesdeAsistenciaAsync(
                CrearSolicitudRelevoNoPlaneadoDto data,
                long asistenciaSalienteId,
                bool realizarCheckOut,
                string numeroUsuario,
                CancellationToken ct)
        {
            var response = new ResponseModel<SolicitudRelevoNoPlaneadoDto>();

            SqlTransaction? transaction = null;
            string? rutaEvidencia = null;

            try
            {
                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "El request es obligatorio.";
                    response.data = null;
                    return response;
                }

                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message = "No fue posible identificar al empleado.";
                    response.data = null;
                    return response;
                }

                if (data.ServicioEmpleadoAfectadoId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "ServicioEmpleadoAfectadoId es inválido.";
                    response.data = null;
                    return response;
                }

                if (!data.ServicioEmpleadoSalienteId.HasValue ||
                    data.ServicioEmpleadoSalienteId.Value <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "ServicioEmpleadoSalienteId es obligatorio.";
                    response.data = null;
                    return response;
                }

                if (asistenciaSalienteId <= 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "AsistenciaId es inválido.";
                    response.data = null;
                    return response;
                }

                if (data.FotoEvidencia == null ||
                    data.FotoEvidencia.Length == 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "La fotografía de evidencia es obligatoria.";
                    response.data = null;
                    return response;
                }

                if (data.FechaHoraFinCobertura <= data.FechaHoraInicioCobertura)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "La fecha de fin de cobertura debe ser mayor a la fecha de inicio.";
                    response.data = null;
                    return response;
                }

                // ============================================================
                // SUBIR EVIDENCIA
                // ============================================================

                string operacionId = Guid.NewGuid().ToString("N");

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

                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync(ct);

                transaction = conn.BeginTransaction();

                DateTime fechaActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        transaction,
                        ct);

                // ============================================================
                // ASIGNACION AFECTADA
                // ============================================================

                ServicioEmpleadoRelevoDto? afectada =
                    await ObtenerServicioEmpleadoAsync(
                        conn,
                        data.ServicioEmpleadoAfectadoId,
                        ct,
                        transaction);

                if (afectada == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación del empleado que debía presentarse.");
                }

                // ============================================================
                // ASIGNACION SALIENTE
                // ============================================================

                ServicioEmpleadoRelevoDto? saliente =
                    await ObtenerServicioEmpleadoAsync(
                        conn,
                        data.ServicioEmpleadoSalienteId.Value,
                        ct,
                        transaction);

                if (saliente == null)
                {
                    throw new InvalidOperationException(
                        "No existe la asignación del empleado saliente.");
                }

                if (afectada.ServicioId != saliente.ServicioId)
                {
                    throw new InvalidOperationException(
                        "La asignación afectada y la saliente pertenecen a servicios diferentes.");
                }

                // ============================================================
                // EVITAR SOLICITUD DUPLICADA
                // ============================================================

                const string sqlExisteSolicitud = @"
SELECT COUNT(1)
FROM dbo.SolicitudRelevoNoPlaneado SR
INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ES
    ON ES.RelevoNoPlaneadoEstatusId =
       SR.RelevoNoPlaneadoEstatusId
WHERE SR.ServicioEmpleadoAfectadoId =
      @ServicioEmpleadoAfectadoId
  AND ES.Clave IN
  (
      'PENDIENTE_ASIGNACION',
      'EN_PROCESO'
  )
  AND SR.FechaHoraFinCobertura > SYSDATETIME();";

                int solicitudesActivas =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlExisteSolicitud,
                            new
                            {
                                data.ServicioEmpleadoAfectadoId
                            },
                            transaction,
                            cancellationToken: ct));

                if (solicitudesActivas > 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    await EliminarArchivoSeguroAsync(rutaEvidencia, ct);
                    rutaEvidencia = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Ya existe una solicitud de relevo activa para esta asignación.";
                    response.data = null;
                    return response;
                }

                // ============================================================
                // CATALOGOS SOLICITUD
                // ============================================================

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
                    await ObtenerEstatusSolicitudIdAsync(
                        conn,
                        ESTATUS_PENDIENTE_ASIGNACION,
                        ct,
                        transaction);

                if (!origenId.HasValue || !estatusId.HasValue)
                {
                    throw new InvalidOperationException(
                        "No están configurados los catálogos requeridos para el relevo por asistencia.");
                }

                // ============================================================
                // CREAR SOLICITUD
                // ============================================================

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
                                OrigenId = origenId.Value,
                                EstatusId = estatusId.Value,
                                data.FechaHoraInicioCobertura,
                                data.FechaHoraFinCobertura,
                                MotivoRelevo = data.MotivoRelevo.Trim(),
                                MotivoNoPermanencia =
                                    string.IsNullOrWhiteSpace(data.MotivoNoPermanencia)
                                        ? null
                                        : data.MotivoNoPermanencia.Trim(),
                                RutaFotoEvidencia = rutaEvidencia,
                                FechaRegistro = fechaActual,
                                UsuarioRegistro = numeroUsuario.Trim()
                            },
                            transaction,
                            cancellationToken: ct));

                // ============================================================
                // REGISTRAR FALTA DEL EMPLEADO QUE NO SE PRESENTO
                // ============================================================

                await RegistrarIncidenciaFaltaRelevoAsync(
                    conn,
                    transaction,
                    afectada,
                    data.FechaHoraInicioCobertura,
                    numeroUsuario.Trim(),
                    fechaActual,
                    ct);

                // ============================================================
                // SI NO PUEDE PERMANECER, CHECK-OUT EN LA MISMA TRANSACCION
                // ============================================================

                if (realizarCheckOut)
                {
                    const string sqlCheckOut = @"
UPDATE dbo.Asistencia
SET
    FechaHoraCheckOut = @FechaHoraCheckOut,
    Estatus = @EstatusFinalizada
WHERE AsistenciaId = @AsistenciaId
  AND ServicioEmpleadoId = @ServicioEmpleadoSalienteId
  AND NumeroEmpleadoEntrante = @NumeroEmpleado
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL;";

                    int rows =
                        await conn.ExecuteAsync(
                            new CommandDefinition(
                                sqlCheckOut,
                                new
                                {
                                    FechaHoraCheckOut = fechaActual,
                                    EstatusFinalizada = ASISTENCIA_FINALIZADA,
                                    AsistenciaId = asistenciaSalienteId,
                                    ServicioEmpleadoSalienteId =
                                        data.ServicioEmpleadoSalienteId.Value,
                                    NumeroEmpleado = numeroUsuario.Trim(),
                                    EstatusEnTurno = ASISTENCIA_EN_TURNO
                                },
                                transaction,
                                cancellationToken: ct));

                    if (rows != 1)
                    {
                        throw new InvalidOperationException(
                            "La asistencia cambió antes de completar el Check-Out.");
                    }
                }

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message = realizarCheckOut
                    ? "Se creó la solicitud de relevo y se registró el Check-Out."
                    : "Se creó la solicitud de relevo correctamente.";

                response.data =
                    new SolicitudRelevoNoPlaneadoDto
                    {
                        SolicitudRelevoNoPlaneadoId = solicitudId,
                        ServicioEmpleadoAfectadoId =
                            data.ServicioEmpleadoAfectadoId,
                        ServicioEmpleadoSalienteId =
                            data.ServicioEmpleadoSalienteId,
                        RelevoNoPlaneadoOrigenId = origenId.Value,
                        RelevoNoPlaneadoEstatusId = estatusId.Value,
                        FechaHoraInicioCobertura =
                            data.FechaHoraInicioCobertura,
                        FechaHoraFinCobertura =
                            data.FechaHoraFinCobertura,
                        MotivoRelevo = data.MotivoRelevo.Trim(),
                        MotivoNoPermanencia =
                            string.IsNullOrWhiteSpace(data.MotivoNoPermanencia)
                                ? null
                                : data.MotivoNoPermanencia.Trim(),
                        RutaFotoEvidencia = rutaEvidencia,
                        FechaRegistro = fechaActual,
                        UsuarioRegistro = numeroUsuario.Trim()
                    };

                rutaEvidencia = null;

                return response;
            }
            catch (SqlException ex)
            {
                try { transaction?.Rollback(); } catch { }

                await EliminarArchivoSeguroAsync(rutaEvidencia, ct);

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
                try { transaction?.Rollback(); } catch { }

                await EliminarArchivoSeguroAsync(rutaEvidencia, ct);

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


        #region INCIDENCIA FALTA DE RELEVO

        private async Task<long?> RegistrarIncidenciaFaltaRelevoAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            ServicioEmpleadoRelevoDto asignacionAfectada,
            DateTime fechaIncidencia,
            string usuarioRegistro,
            DateTime fechaRegistro,
            CancellationToken ct)
        {
            const string sqlTipo = @"
SELECT TOP (1)
    TipoIncidenciaId,
    AfectaNomina,
    TipoAfectacionNomina,
    MontoAfectacion
FROM dbo.CAT_TIPO_INCIDENCIA
WHERE Clave = 'FALTA_RECHAZO_TURNO'
  AND Estatus = 1;";

            TipoIncidenciaDto? tipoFalta =
                await conn.QueryFirstOrDefaultAsync<TipoIncidenciaDto>(
                    new CommandDefinition(
                        sqlTipo,
                        transaction: transaction,
                        cancellationToken: ct));

            if (tipoFalta == null)
            {
                throw new InvalidOperationException(
                    "No está configurado el tipo de incidencia FALTA_RECHAZO_TURNO.");
            }

            const string sqlEmpleado = @"
SELECT TOP (1)
    LTRIM(RTRIM(UsuarioAsignado))
FROM dbo.DatosGeneralesEmpleado
WHERE ID = @EmpleadoId;";

            string? numeroEmpleado =
                await conn.ExecuteScalarAsync<string?>(
                    new CommandDefinition(
                        sqlEmpleado,
                        new
                        {
                            asignacionAfectada.EmpleadoId
                        },
                        transaction,
                        cancellationToken: ct));

            if (string.IsNullOrWhiteSpace(numeroEmpleado))
            {
                throw new InvalidOperationException(
                    "El empleado ausente no tiene un NumeroUsuario relacionado.");
            }

            // Una sola afectación por asignación y fecha de turno.
            const string sqlExiste = @"
SELECT TOP (1)
    IncidenciaId
FROM dbo.Incidencias WITH (UPDLOCK, HOLDLOCK)
WHERE TipoIncidenciaId = @TipoIncidenciaId
  AND AsignacionTurnoId = @AsignacionTurnoId
  AND CONVERT(date, FechaIncidencia) =
      CONVERT(date, @FechaIncidencia)
  AND Estatus = 1;";

            long? incidenciaExistente =
                await conn.ExecuteScalarAsync<long?>(
                    new CommandDefinition(
                        sqlExiste,
                        new
                        {
                            tipoFalta.TipoIncidenciaId,
                            AsignacionTurnoId =
                                asignacionAfectada.ServicioEmpleadoId,
                            FechaIncidencia = fechaIncidencia
                        },
                        transaction,
                        cancellationToken: ct));

            if (incidenciaExistente.HasValue)
            {
                return incidenciaExistente.Value;
            }

            const string sqlInsert = @"
INSERT INTO dbo.Incidencias
(
    TipoIncidenciaId,
    NumeroUsuario,
    ServicioId,
    AsignacionTurnoId,
    AsistenciaId,
    FechaIncidencia,
    Descripcion,
    AfectaNomina,
    TipoAfectacionNomina,
    MontoAfectacion,
    Estatus,
    UsuarioRegistro,
    FechaRegistro
)
OUTPUT INSERTED.IncidenciaId
VALUES
(
    @TipoIncidenciaId,
    @NumeroUsuario,
    @ServicioId,
    @AsignacionTurnoId,
    NULL,
    @FechaIncidencia,
    @Descripcion,
    @AfectaNomina,
    @TipoAfectacionNomina,
    @MontoAfectacion,
    1,
    @UsuarioRegistro,
    @FechaRegistro
);";

            return await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    sqlInsert,
                    new
                    {
                        tipoFalta.TipoIncidenciaId,
                        NumeroUsuario = numeroEmpleado.Trim(),
                        asignacionAfectada.ServicioId,
                        AsignacionTurnoId =
                            asignacionAfectada.ServicioEmpleadoId,
                        FechaIncidencia = fechaIncidencia,
                        Descripcion =
                            "Falta al turno. El empleado no se presentó para realizar el relevo programado.",
                        tipoFalta.AfectaNomina,
                        tipoFalta.TipoAfectacionNomina,
                        tipoFalta.MontoAfectacion,
                        UsuarioRegistro = usuarioRegistro,
                        FechaRegistro = fechaRegistro
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion
    }
}