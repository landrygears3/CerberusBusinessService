using CerberusBusinessService.Functions.Notificaciones;
using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Functions.Relevos;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Asistencias;
using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Asistencias
{
    public class AsistenciasFunctions
    {
        #region CONSTANTES

        private const int MINUTOS_TOLERANCIA_RETARDO = 10;

        private const int ESTATUS_EN_TURNO = 1;
        private const int ESTATUS_FINALIZADA = 2;
        private const int ESTATUS_PENDIENTE_AUTORIZAR = 3;
        private const int ESTATUS_CANCELADA = 4;

        #endregion


        #region PROPIEDADES

        private readonly string _csCerberus;

        private readonly FileAsistenciaService _fileAsistenciaService;

        private readonly ServicioNotificationFunctions
            _servicioNotificationFunctions;

        private readonly RelevoNoPlaneadoFunctions
            _relevoNoPlaneadoFunctions;

        #endregion


        #region CONSTRUCTOR

        public AsistenciasFunctions(
            IConfiguration config,
            FileAsistenciaService fileAsistenciaService,
            ServicioNotificationFunctions servicioNotificationFunctions,
            RelevoNoPlaneadoFunctions relevoNoPlaneadoFunctions)
        {
            _csCerberus =
                config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");

            _fileAsistenciaService =
                fileAsistenciaService;

            _servicioNotificationFunctions =
                servicioNotificationFunctions;

            _relevoNoPlaneadoFunctions =
                relevoNoPlaneadoFunctions;
        }

        #endregion


        #region CHECK-IN

        public async Task<ResponseModel<CheckInResponse>>
            ProcesarCheckIn(
                CheckInRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            ResponseModel<CheckInResponse> response =
                new ResponseModel<CheckInResponse>();

            try
            {
                // ====================================================
                // 1. VALIDAR USUARIO
                // ====================================================

                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message =
                        "No fue posible identificar al empleado.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // 2. VALIDAR REQUEST
                // ====================================================

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El request es obligatorio.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // 3. VALIDAR GEOLOCALIZACION
                // ====================================================

                string? errorGeolocalizacion =
                    ValidarGeolocalizacion(
                        data.Latitud,
                        data.Longitud);

                if (errorGeolocalizacion != null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        errorGeolocalizacion;
                    response.data = null;

                    return response;
                }

                double latitud =
                    data.Latitud!.Value;

                double longitud =
                    data.Longitud!.Value;

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                // ====================================================
                // 4. FECHA ACTUAL DEL SERVIDOR
                // ====================================================

                DateTime fechaHoraActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        ct);

                // ====================================================
                // 5. EMPLEADO DEL TOKEN
                // ====================================================

                EmpleadoAsistenciaDto? empleado =
                    await ObtenerEmpleadoAsync(
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

                // ====================================================
                // 6. TURNO ASIGNADO
                // ====================================================

                var turno =
                    await ObtenerTurnoProximoAsync(
                        conn,
                        empleado.EmpleadoId,
                        fechaHoraActual,
                        ct);

                if (turno == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El empleado no tiene un turno disponible para realizar Check-In.";
                    response.data = null;

                    return response;
                }

                ServicioEmpleadoCheckInDto asignacion =
                    turno.Value.Asignacion;

                DateTime fechaTurno =
                    turno.Value.FechaTurno;

                DateTime entradaProgramada =
                    turno.Value.EntradaProgramada;

                DateTime salidaProgramada =
                    turno.Value.SalidaProgramada;

                // ====================================================
                // 7. HORARIO DEL SERVICIO
                // ====================================================

                ServicioHorarioCheckInDto? horarioServicio =
                    await ObtenerHorarioServicioAsync(
                        conn,
                        asignacion.ServicioId,
                        fechaTurno,
                        ct);

                if (horarioServicio == null)
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El servicio no tiene un horario operativo configurado para la fecha del turno.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // 8. DETERMINAR FLUJO
                // ====================================================

                bool esRelevoContinuo =
                    EsServicioAtencionContinua(
                        horarioServicio);

                // ====================================================
                // 9. DISPATCHER
                // ====================================================

                if (esRelevoContinuo)
                {
                    return await ProcesarCheckInRelevoContinuoAsync(
                        conn,
                        data,
                        asignacion,
                        fechaTurno,
                        entradaProgramada,
                        salidaProgramada,
                        fechaHoraActual,
                        numeroUsuario.Trim(),
                        latitud,
                        longitud,
                        accessToken,
                        ct);
                }

                return await ProcesarCheckInAperturaAsync(
                    conn,
                    asignacion,
                    fechaTurno,
                    entradaProgramada,
                    salidaProgramada,
                    fechaHoraActual,
                    numeroUsuario.Trim(),
                    latitud,
                    longitud,
                    ct);
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al procesar el Check-In.";
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
                    "Error al procesar el Check-In.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region CHECK-OUT DIRECTO

        public async Task<ResponseModel<CheckOutResponse>>
            ProcesarCheckOut(
                string numeroUsuario,
                bool esOficina,
                CancellationToken ct)
        {
            ResponseModel<CheckOutResponse> response =
                new ResponseModel<CheckOutResponse>();

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

                // ====================================================
                // OFICINA
                // ====================================================

                if (!esOficina)
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El Check-Out de este empleado requiere el flujo de relevo.";
                    response.desc =
                        "El Check-Out directo únicamente aplica al rol Oficina.";
                    response.data = null;

                    return response;
                }

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                DateTime fechaHoraActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        ct);

                transaction =
                    conn.BeginTransaction();

                // ====================================================
                // BUSCAR ASISTENCIA ACTIVA
                // ====================================================

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
WHERE NumeroEmpleadoEntrante = @NumeroUsuario
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL
ORDER BY
    FechaHoraCheckIn DESC,
    AsistenciaId DESC;";

                AsistenciaActivaCheckOutDto? asistencia =
                    await conn.QueryFirstOrDefaultAsync<
                        AsistenciaActivaCheckOutDto>(
                        new CommandDefinition(
                            sqlAsistencia,
                            new
                            {
                                NumeroUsuario =
                                    numeroUsuario.Trim(),

                                EstatusEnTurno =
                                    ESTATUS_EN_TURNO
                            },
                            transaction,
                            cancellationToken: ct));

                if (asistencia == null)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El empleado no tiene una asistencia activa para realizar Check-Out.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // CHECK-OUT DIRECTO
                // ====================================================

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
                                    fechaHoraActual,

                                EstatusFinalizada =
                                    ESTATUS_FINALIZADA,

                                asistencia.AsistenciaId,

                                EstatusEnTurno =
                                    ESTATUS_EN_TURNO
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
                        "La asistencia cambió de estado antes de completar el Check-Out.";
                    response.data = null;

                    return response;
                }

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Check-Out registrado correctamente.";
                response.desc =
                    "El Check-Out se realizó directamente por pertenecer al rol Oficina.";

                response.data =
                    new CheckOutResponse
                    {
                        AsistenciaId =
                            asistencia.AsistenciaId,

                        ServicioId =
                            asistencia.ServicioId,

                        ServicioEmpleadoId =
                            asistencia.ServicioEmpleadoId,

                        NumeroEmpleado =
                            numeroUsuario.Trim(),

                        FechaTurno =
                            asistencia.FechaTurno,

                        FechaHoraEntradaProgramada =
                            asistencia.FechaHoraEntradaProgramada,

                        FechaHoraSalidaProgramada =
                            asistencia.FechaHoraSalidaProgramada,

                        FechaHoraCheckIn =
                            asistencia.FechaHoraCheckIn,

                        FechaHoraCheckOut =
                            fechaHoraActual,

                        Estatus =
                            ESTATUS_FINALIZADA,

                        EstatusDescripcion =
                            "Finalizada"
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
                    "Error SQL al registrar el Check-Out.";
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
                    "Error al registrar el Check-Out.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region CHECK-IN APERTURA

        private async Task<ResponseModel<CheckInResponse>>
            ProcesarCheckInAperturaAsync(
                SqlConnection conn,
                ServicioEmpleadoCheckInDto asignacion,
                DateTime fechaTurno,
                DateTime entradaProgramada,
                DateTime salidaProgramada,
                DateTime fechaHoraActual,
                string numeroUsuario,
                double latitud,
                double longitud,
                CancellationToken ct)
        {
            ResponseModel<CheckInResponse> response =
                new ResponseModel<CheckInResponse>();

            var controlHora =
                CalcularHoraCheckIn(
                    entradaProgramada,
                    fechaHoraActual);

            SqlTransaction? transaction = null;

            try
            {
                transaction =
                    conn.BeginTransaction();

                bool existe =
                    await ExisteAsistenciaAsync(
                        conn,
                        transaction,
                        asignacion.ServicioEmpleadoId,
                        fechaTurno,
                        true,
                        ct);

                if (existe)
                {
                    transaction.Rollback();

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Ya existe una asistencia para este turno.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // INSERT ASISTENCIA
                // ====================================================

                long asistenciaId =
                    await InsertarAsistenciaAsync(
                        conn,
                        transaction,
                        asignacion,
                        numeroUsuario,
                        null,
                        fechaTurno,
                        entradaProgramada,
                        salidaProgramada,
                        controlHora.FechaHoraCheckIn,
                        controlHora.EsRetardo,
                        controlHora.MinutosRetardo,
                        latitud,
                        longitud,
                        ESTATUS_EN_TURNO,
                        fechaHoraActual,
                        ct);

                // ====================================================
                // RETARDO
                // ====================================================

                long? incidenciaRetardoId = null;

                if (controlHora.EsRetardo &&
                    controlHora.MinutosRetardo.HasValue)
                {
                    incidenciaRetardoId =
                        await RegistrarIncidenciaRetardoAsync(
                            conn,
                            transaction,
                            asistenciaId,
                            asignacion.ServicioId,
                            numeroUsuario,
                            fechaHoraActual,
                            controlHora.MinutosRetardo.Value,
                            ct);
                }

                transaction.Commit();

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Check-In registrado correctamente.";
                response.desc =
                    "El empleado quedó registrado en turno.";

                response.data =
                    CrearCheckInResponse(
                        asistenciaId,
                        asignacion,
                        numeroUsuario,
                        null,
                        fechaTurno,
                        entradaProgramada,
                        salidaProgramada,
                        controlHora.FechaHoraCheckIn,
                        controlHora.EsRetardo,
                        controlHora.MinutosRetardo,
                        incidenciaRetardoId,
                        ESTATUS_EN_TURNO,
                        "En turno");

                return response;
            }
            catch
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                throw;
            }
        }

        #endregion


        #region CHECK-IN RELEVO CONTINUO

        private async Task<ResponseModel<CheckInResponse>>
            ProcesarCheckInRelevoContinuoAsync(
                SqlConnection conn,
                CheckInRequest data,
                ServicioEmpleadoCheckInDto asignacion,
                DateTime fechaTurno,
                DateTime entradaProgramada,
                DateTime salidaProgramada,
                DateTime fechaHoraActual,
                string numeroUsuario,
                double latitud,
                double longitud,
                string accessToken,
                CancellationToken ct)
        {
            ResponseModel<CheckInResponse> response =
                new ResponseModel<CheckInResponse>();

            List<string> archivosR2 =
                new List<string>();

            SqlTransaction? transaction = null;

            bool commitRealizado = false;

            try
            {
                // ====================================================
                // VALIDACIONES DEL RELEVO
                // ====================================================

                string? error =
                    ValidarRequestRelevo(data);

                if (error != null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = error;
                    response.data = null;

                    return response;
                }

                string numeroEmpleadoSaliente =
                    data.Formulario!
                        .IdEmpleadoSaliente!
                        .Trim();

                if (string.Equals(
                    numeroEmpleadoSaliente,
                    numeroUsuario,
                    StringComparison.OrdinalIgnoreCase))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El empleado entrante no puede ser el mismo empleado saliente.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // EMPLEADO SALIENTE
                // ====================================================

                bool salienteExiste =
                    await ExisteEmpleadoAsync(
                        conn,
                        numeroEmpleadoSaliente,
                        ct);

                if (!salienteExiste)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El empleado saliente no existe.";
                    response.data = null;

                    return response;
                }

                bool salienteEnTurno =
                    await ExisteEmpleadoSalienteEnTurnoAsync(
                        conn,
                        asignacion.ServicioId,
                        numeroEmpleadoSaliente,
                        ct);

                if (!salienteEnTurno)
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El empleado saliente no tiene una asistencia activa en este servicio.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // HORA / RETARDO
                // ====================================================

                var controlHora =
                    CalcularHoraCheckIn(
                        entradaProgramada,
                        fechaHoraActual);

                // ====================================================
                // DUPLICADO PREVIO
                // ====================================================

                bool duplicadoPrevio =
                    await ExisteAsistenciaAsync(
                        conn,
                        null,
                        asignacion.ServicioEmpleadoId,
                        fechaTurno,
                        false,
                        ct);

                if (duplicadoPrevio)
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Ya existe una asistencia para este turno.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // ARCHIVOS
                // ====================================================

                string operacionId =
                    Guid.NewGuid().ToString("N");

                var firmaEntrante =
                    await SubirArchivoAsync(
                        data.Formulario!
                            .ImagenFirmaEntrante!,
                        numeroUsuario,
                        fechaTurno,
                        operacionId,
                        "firmas",
                        archivosR2,
                        ct);

                if (!firmaEntrante.isSuccess)
                {
                    return ErrorArchivo(
                        firmaEntrante);
                }

                var firmaSaliente =
                    await SubirArchivoAsync(
                        data.Formulario
                            .ImagenFirmaSaliente!,
                        numeroUsuario,
                        fechaTurno,
                        operacionId,
                        "firmas",
                        archivosR2,
                        ct);

                if (!firmaSaliente.isSuccess)
                {
                    await LimpiarArchivosAsync(
                        archivosR2,
                        ct);

                    return ErrorArchivo(
                        firmaSaliente);
                }

                var fotoZona =
                    await SubirArchivoAsync(
                        data.Formulario.Foto!,
                        numeroUsuario,
                        fechaTurno,
                        operacionId,
                        "zona",
                        archivosR2,
                        ct);

                if (!fotoZona.isSuccess)
                {
                    await LimpiarArchivosAsync(
                        archivosR2,
                        ct);

                    return ErrorArchivo(
                        fotoZona);
                }

                // ====================================================
                // RESGUARDOS
                // ====================================================

                List<ResguardoCheckInRequest> resguardos =
                    data.Resguardo
                    ?? new List<ResguardoCheckInRequest>();

                List<string?> fotosResguardo =
                    new List<string?>();

                foreach (var item in resguardos)
                {
                    if (item.Foto != null &&
                        item.Foto.Length > 0)
                    {
                        var upload =
                            await SubirArchivoAsync(
                                item.Foto,
                                numeroUsuario,
                                fechaTurno,
                                operacionId,
                                "resguardos",
                                archivosR2,
                                ct);

                        if (!upload.isSuccess)
                        {
                            await LimpiarArchivosAsync(
                                archivosR2,
                                ct);

                            return ErrorArchivo(
                                upload);
                        }

                        fotosResguardo.Add(
                            upload.data);
                    }
                    else
                    {
                        fotosResguardo.Add(
                            null);
                    }
                }

                // ====================================================
                // TRANSACCION
                // ====================================================

                transaction =
                    conn.BeginTransaction();

                bool duplicado =
                    await ExisteAsistenciaAsync(
                        conn,
                        transaction,
                        asignacion.ServicioEmpleadoId,
                        fechaTurno,
                        true,
                        ct);

                if (duplicado)
                {
                    transaction.Rollback();
                    transaction = null;

                    await LimpiarArchivosAsync(
                        archivosR2,
                        ct);

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Ya existe una asistencia para este turno.";
                    response.data = null;

                    return response;
                }

                // ====================================================
                // ASISTENCIA
                // ====================================================

                long asistenciaId =
                    await InsertarAsistenciaAsync(
                        conn,
                        transaction,
                        asignacion,
                        numeroUsuario,
                        numeroEmpleadoSaliente,
                        fechaTurno,
                        entradaProgramada,
                        salidaProgramada,
                        controlHora.FechaHoraCheckIn,
                        controlHora.EsRetardo,
                        controlHora.MinutosRetardo,
                        latitud,
                        longitud,
                        ESTATUS_PENDIENTE_AUTORIZAR,
                        fechaHoraActual,
                        ct);

                // ====================================================
                // ETO6
                // ====================================================

                await InsertarFormatoEntradaAsync(
                    conn,
                    transaction,
                    asistenciaId,
                    data,
                    fechaHoraActual,
                    ct);

                // ====================================================
                // FORMULARIO
                // ====================================================

                await InsertarFormularioAsync(
                    conn,
                    transaction,
                    asistenciaId,
                    data,
                    firmaEntrante.data!,
                    firmaSaliente.data!,
                    fotoZona.data!,
                    fechaHoraActual,
                    ct);

                // ====================================================
                // RESGUARDOS
                // ====================================================

                if (resguardos.Count > 0)
                {
                    await InsertarResguardosAsync(
                        conn,
                        transaction,
                        asistenciaId,
                        resguardos,
                        fotosResguardo,
                        fechaHoraActual,
                        ct);
                }

                // ====================================================
                // RETARDO
                // ====================================================

                long? incidenciaRetardoId = null;

                if (controlHora.EsRetardo &&
                    controlHora.MinutosRetardo.HasValue)
                {
                    incidenciaRetardoId =
                        await RegistrarIncidenciaRetardoAsync(
                            conn,
                            transaction,
                            asistenciaId,
                            asignacion.ServicioId,
                            numeroUsuario,
                            fechaHoraActual,
                            controlHora.MinutosRetardo.Value,
                            ct);
                }

                // ====================================================
                // COMMIT
                // ====================================================

                transaction.Commit();
                transaction = null;

                commitRealizado = true;

                // ====================================================
                // RESPONSE
                // ====================================================

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Check-In registrado correctamente.";
                response.desc =
                    "La asistencia quedó pendiente de autorización.";

                response.data =
                    CrearCheckInResponse(
                        asistenciaId,
                        asignacion,
                        numeroUsuario,
                        numeroEmpleadoSaliente,
                        fechaTurno,
                        entradaProgramada,
                        salidaProgramada,
                        controlHora.FechaHoraCheckIn,
                        controlHora.EsRetardo,
                        controlHora.MinutosRetardo,
                        incidenciaRetardoId,
                        ESTATUS_PENDIENTE_AUTORIZAR,
                        "Pendiente autorizar");

                // ====================================================
                // PAYLOAD NOTIFICACION
                // ====================================================

                var resguardosNotificacion =
                    resguardos
                        .Select(
                            (item, index) =>
                                new
                                {
                                    item.IdObjeto,
                                    item.Cantidad,
                                    item.Identificador,
                                    item.IdEstado,
                                    item.Observaciones,

                                    RutaFoto =
                                        fotosResguardo[index]
                                })
                        .ToList();

                var notificationData =
                    new
                    {
                        AsistenciaId =
                            asistenciaId,

                        ServicioEmpleadoId =
                            asignacion.ServicioEmpleadoId,

                        ServicioId =
                            asignacion.ServicioId,

                        NumeroEmpleadoEntrante =
                            numeroUsuario,

                        NumeroEmpleadoSaliente =
                            numeroEmpleadoSaliente,

                        FechaTurno =
                            fechaTurno.Date,

                        FechaHoraEntradaProgramada =
                            entradaProgramada,

                        FechaHoraSalidaProgramada =
                            salidaProgramada,

                        FechaHoraCheckIn =
                            controlHora.FechaHoraCheckIn,

                        FechaHoraRegistro =
                            fechaHoraActual,

                        Latitud =
                            latitud,

                        Longitud =
                            longitud,

                        EsRetardo =
                            controlHora.EsRetardo,

                        MinutosRetardo =
                            controlHora.MinutosRetardo,

                        ToleranciaMinutos =
                            MINUTOS_TOLERANCIA_RETARDO,

                        Estatus =
                            ESTATUS_PENDIENTE_AUTORIZAR,

                        EstatusDescripcion =
                            "Pendiente autorizar",

                        FormatoEntrada =
                            new
                            {
                                data.FormatoEntrada!
                                    .EmpleadoEntrante!
                                    .RecepcionTurnoCompleto,

                                data.FormatoEntrada
                                    .EmpleadoEntrante
                                    .EquipoTrabajoFuncional,

                                data.FormatoEntrada
                                    .EmpleadoEntrante
                                    .ConocimientoConsignas,

                                data.FormatoEntrada
                                    .EmpleadoEntrante
                                    .UniformeCompleto,

                                data.FormatoEntrada
                                    .EmpleadoEntrante
                                    .CondicionesAptas,

                                data.FormatoEntrada
                                    .EmpleadoEntrante
                                    .HorarioDiaDescanso
                            },

                        Resguardos =
                            resguardosNotificacion,

                        Formulario =
                            new
                            {
                                RutaFirmaEntrante =
                                    firmaEntrante.data,

                                RutaFirmaSaliente =
                                    firmaSaliente.data,

                                Observaciones =
                                    data.Formulario
                                        .Observaciones,

                                RutaFotoZona =
                                    fotoZona.data
                            }
                    };

                // ====================================================
                // NOTIFICAR SUPERVISORES
                //
                // SIEMPRE DESPUES DEL COMMIT.
                // ====================================================

                try
                {
                    var notificationResponse =
                        await _servicioNotificationFunctions
                            .EnviarASupervisoresAsync(
                                asignacion.ServicioId,
                                fechaTurno,
                                "ASISTENCIA_CHECKIN_AUTORIZAR",
                                "Check-In pendiente de autorización",
                                $"El empleado {numeroUsuario} realizó un Check-In pendiente de revisión.",
                                notificationData,
                                accessToken,
                                ct);

                    if (notificationResponse.isSuccess)
                    {
                        response.desc =
                            "La asistencia quedó pendiente de autorización " +
                            "y se notificó a los supervisores del servicio.";
                    }
                    else
                    {
                        response.desc =
                            "La asistencia quedó pendiente de autorización, " +
                            "pero no fue posible enviar la notificación a los supervisores. " +
                            notificationResponse.message;
                    }
                }
                catch (Exception ex)
                {
                    response.desc =
                        "La asistencia quedó pendiente de autorización, " +
                        "pero ocurrió un error al enviar la notificación: " +
                        ex.Message;
                }

                return response;
            }
            catch
            {
                if (!commitRealizado)
                {
                    try
                    {
                        transaction?.Rollback();
                    }
                    catch
                    {
                    }

                    await LimpiarArchivosAsync(
                        archivosR2,
                        ct);
                }

                throw;
            }
        }

        #endregion


        #region VALIDACIONES CHECK-IN

        private string? ValidarGeolocalizacion(
            double? latitud,
            double? longitud)
        {
            if (!latitud.HasValue)
            {
                return
                    "La latitud es obligatoria para registrar el Check-In.";
            }

            if (!longitud.HasValue)
            {
                return
                    "La longitud es obligatoria para registrar el Check-In.";
            }

            if (latitud.Value < -90 ||
                latitud.Value > 90)
            {
                return
                    "La latitud debe encontrarse entre -90 y 90.";
            }

            if (longitud.Value < -180 ||
                longitud.Value > 180)
            {
                return
                    "La longitud debe encontrarse entre -180 y 180.";
            }

            return null;
        }


        private string? ValidarRequestRelevo(
            CheckInRequest data)
        {
            if (data == null)
            {
                return
                    "El request es obligatorio.";
            }

            if (data.FormatoEntrada == null)
            {
                return
                    "FormatoEntrada es obligatorio para un Check-In con relevo.";
            }

            if (data.FormatoEntrada
                    .EmpleadoEntrante == null)
            {
                return
                    "EmpleadoEntrante es obligatorio para un Check-In con relevo.";
            }

            if (data.Formulario == null)
            {
                return
                    "Formulario es obligatorio para un Check-In con relevo.";
            }

            if (string.IsNullOrWhiteSpace(
                data.Formulario.IdEmpleadoSaliente))
            {
                return
                    "El empleado saliente es obligatorio para un Check-In con relevo.";
            }

            if (data.Formulario
                    .ImagenFirmaEntrante == null ||
                data.Formulario
                    .ImagenFirmaEntrante
                    .Length == 0)
            {
                return
                    "La firma del empleado entrante es obligatoria.";
            }

            if (data.Formulario
                    .ImagenFirmaSaliente == null ||
                data.Formulario
                    .ImagenFirmaSaliente
                    .Length == 0)
            {
                return
                    "La firma del empleado saliente es obligatoria.";
            }

            if (data.Formulario.Foto == null ||
                data.Formulario.Foto.Length == 0)
            {
                return
                    "La fotografía de la zona es obligatoria.";
            }

            // Resguardo es opcional:
            // null, [], o N elementos son válidos.

            return null;
        }

        #endregion


        #region FECHA SERVIDOR

        private async Task<DateTime>
            ObtenerFechaServidorAsync(
                SqlConnection conn,
                CancellationToken ct)
        {
            return await conn.ExecuteScalarAsync<DateTime>(
                new CommandDefinition(
                    "SELECT SYSDATETIME();",
                    cancellationToken: ct));
        }

        #endregion


        #region EMPLEADOS

        private async Task<EmpleadoAsistenciaDto?>
            ObtenerEmpleadoAsync(
                SqlConnection conn,
                string numeroUsuario,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    ID AS EmpleadoId,
    UsuarioAsignado AS NumeroUsuario
FROM dbo.DatosGeneralesEmpleado
WHERE UsuarioAsignado = @NumeroUsuario;";

            return await conn
                .QueryFirstOrDefaultAsync<EmpleadoAsistenciaDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            NumeroUsuario =
                                numeroUsuario.Trim()
                        },
                        cancellationToken: ct));
        }


        private async Task<bool>
            ExisteEmpleadoAsync(
                SqlConnection conn,
                string numeroUsuario,
                CancellationToken ct)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.DatosGeneralesEmpleado
WHERE UsuarioAsignado = @NumeroUsuario;";

            int cantidad =
                await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            NumeroUsuario =
                                numeroUsuario
                        },
                        cancellationToken: ct));

            return cantidad > 0;
        }


        private async Task<bool>
            ExisteEmpleadoSalienteEnTurnoAsync(
                SqlConnection conn,
                int servicioId,
                string numeroEmpleadoSaliente,
                CancellationToken ct)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.Asistencia
WHERE ServicioId = @ServicioId
  AND NumeroEmpleadoEntrante = @NumeroEmpleadoSaliente
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL;";

            int cantidad =
                await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            ServicioId =
                                servicioId,

                            NumeroEmpleadoSaliente =
                                numeroEmpleadoSaliente,

                            EstatusEnTurno =
                                ESTATUS_EN_TURNO
                        },
                        cancellationToken: ct));

            return cantidad > 0;
        }

        #endregion


        #region TURNOS Y HORARIOS

        private async Task<(
            ServicioEmpleadoCheckInDto Asignacion,
            DateTime FechaTurno,
            DateTime EntradaProgramada,
            DateTime SalidaProgramada)?>
            ObtenerTurnoProximoAsync(
                SqlConnection conn,
                int empleadoId,
                DateTime fechaHoraActual,
                CancellationToken ct)
        {
            DateTime hoy =
                fechaHoraActual.Date;

            DateTime ayer =
                hoy.AddDays(-1);

            const string sql = @"
SELECT
    ServicioEmpleadoId,
    ServicioId,
    EmpleadoId,
    TipoAsignacionServicioId,
    EmpleadoCubiertoId,
    FechaInicio,
    FechaFin,
    HoraEntrada,
    HoraSalida,
    SalidaDiaSiguiente
FROM dbo.ServicioEmpleado
WHERE EmpleadoId = @EmpleadoId
  AND FechaInicio <= @Hoy
  AND
  (
      FechaFin IS NULL
      OR FechaFin >= @Ayer
  )
ORDER BY
    FechaInicio DESC,
    ServicioEmpleadoId DESC;";

            IEnumerable<ServicioEmpleadoCheckInDto> result =
                await conn.QueryAsync<ServicioEmpleadoCheckInDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            EmpleadoId =
                                empleadoId,

                            Hoy =
                                hoy,

                            Ayer =
                                ayer
                        },
                        cancellationToken: ct));

            List<ServicioEmpleadoCheckInDto> asignaciones =
                result.ToList();

            // ========================================================
            // TURNO ACTIVO
            // ========================================================

            var activos =
                new List<(
                    ServicioEmpleadoCheckInDto Asignacion,
                    DateTime FechaTurno,
                    DateTime Entrada,
                    DateTime Salida)>();

            foreach (ServicioEmpleadoCheckInDto asignacion
                     in asignaciones)
            {
                DateTime[] fechas =
                {
                    ayer,
                    hoy
                };

                foreach (DateTime fecha in fechas)
                {
                    if (!FechaDentroDeAsignacion(
                        fecha,
                        asignacion))
                    {
                        continue;
                    }

                    DateTime entrada =
                        fecha.Date.Add(
                            asignacion.HoraEntrada);

                    DateTime salida =
                        fecha.Date.Add(
                            asignacion.HoraSalida);

                    if (asignacion.SalidaDiaSiguiente)
                    {
                        salida =
                            salida.AddDays(1);
                    }

                    if (salida <= entrada)
                    {
                        continue;
                    }

                    if (fechaHoraActual >= entrada &&
                        fechaHoraActual <= salida)
                    {
                        activos.Add(
                            (
                                asignacion,
                                fecha.Date,
                                entrada,
                                salida
                            ));
                    }
                }
            }

            if (activos.Count > 0)
            {
                var turnoActivo =
                    activos
                        .OrderByDescending(
                            x => x.Entrada)
                        .First();

                return (
                    turnoActivo.Asignacion,
                    turnoActivo.FechaTurno,
                    turnoActivo.Entrada,
                    turnoActivo.Salida
                );
            }

            // ========================================================
            // PROXIMO TURNO DE HOY
            // ========================================================

            var proximos =
                new List<(
                    ServicioEmpleadoCheckInDto Asignacion,
                    DateTime FechaTurno,
                    DateTime Entrada,
                    DateTime Salida)>();

            foreach (ServicioEmpleadoCheckInDto asignacion
                     in asignaciones)
            {
                if (!FechaDentroDeAsignacion(
                    hoy,
                    asignacion))
                {
                    continue;
                }

                DateTime entrada =
                    hoy.Add(
                        asignacion.HoraEntrada);

                DateTime salida =
                    hoy.Add(
                        asignacion.HoraSalida);

                if (asignacion.SalidaDiaSiguiente)
                {
                    salida =
                        salida.AddDays(1);
                }

                if (salida <= entrada)
                {
                    continue;
                }

                if (entrada > fechaHoraActual)
                {
                    proximos.Add(
                        (
                            asignacion,
                            hoy,
                            entrada,
                            salida
                        ));
                }
            }

            if (proximos.Count == 0)
            {
                return null;
            }

            var proximo =
                proximos
                    .OrderBy(
                        x => x.Entrada)
                    .First();

            return (
                proximo.Asignacion,
                proximo.FechaTurno,
                proximo.Entrada,
                proximo.Salida
            );
        }


        private async Task<ServicioHorarioCheckInDto?>
           ObtenerHorarioServicioAsync(
               SqlConnection conn,
               int servicioId,
               DateTime fechaTurno,
               CancellationToken ct)
        {
            byte diaSemana =
                ObtenerDiaSemana(
                    fechaTurno);

            const string sql = @"
SELECT TOP (1)
    ServicioHorarioId AS IdServicioHorario,
    ServicioId AS IdServicio,
    DiaSemana,
    HoraInicio,
    HoraFin,
    CruzaDia,
    Estatus AS Activo,
    FechaInicioVigencia AS VigenteDesde,
    FechaFinVigencia AS VigenteHasta
FROM dbo.Servicios_Horarios
WHERE ServicioId = @ServicioId
  AND DiaSemana = @DiaSemana
  AND Estatus = 1
  AND FechaInicioVigencia <= @Fecha
  AND
  (
      FechaFinVigencia IS NULL
      OR FechaFinVigencia >= @Fecha
  )
ORDER BY
    FechaInicioVigencia DESC,
    ServicioHorarioId DESC;";

            return await conn
                .QueryFirstOrDefaultAsync<ServicioHorarioCheckInDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            ServicioId =
                                servicioId,

                            DiaSemana =
                                diaSemana,

                            Fecha =
                                fechaTurno.Date
                        },
                        cancellationToken: ct));
        }

        private bool EsServicioAtencionContinua(
            ServicioHorarioCheckInDto horario)
        {
            return
                horario.HoraInicio == TimeSpan.Zero &&
                horario.HoraFin == TimeSpan.Zero &&
                horario.CruzaDia;
        }


        private byte ObtenerDiaSemana(
            DateTime fecha)
        {
            return fecha.DayOfWeek switch
            {
                DayOfWeek.Monday => 1,
                DayOfWeek.Tuesday => 2,
                DayOfWeek.Wednesday => 3,
                DayOfWeek.Thursday => 4,
                DayOfWeek.Friday => 5,
                DayOfWeek.Saturday => 6,
                DayOfWeek.Sunday => 7,

                _ => throw new InvalidOperationException(
                    "Día de semana inválido.")
            };
        }


        private (
            DateTime FechaHoraCheckIn,
            bool EsRetardo,
            int? MinutosRetardo)
            CalcularHoraCheckIn(
                DateTime entradaProgramada,
                DateTime fechaHoraActual)
        {
            if (fechaHoraActual <
                entradaProgramada)
            {
                return (
                    entradaProgramada,
                    false,
                    null);
            }

            DateTime limiteTolerancia =
                entradaProgramada.AddMinutes(
                    MINUTOS_TOLERANCIA_RETARDO);

            if (fechaHoraActual <=
                limiteTolerancia)
            {
                return (
                    fechaHoraActual,
                    false,
                    null);
            }

            int minutosRetardo =
                (int)Math.Ceiling(
                    (
                        fechaHoraActual -
                        entradaProgramada
                    ).TotalMinutes);

            return (
                fechaHoraActual,
                true,
                minutosRetardo);
        }


        private bool FechaDentroDeAsignacion(
            DateTime fecha,
            ServicioEmpleadoCheckInDto asignacion)
        {
            if (fecha.Date <
                asignacion.FechaInicio.Date)
            {
                return false;
            }

            if (asignacion.FechaFin.HasValue &&
                fecha.Date >
                asignacion.FechaFin.Value.Date)
            {
                return false;
            }

            return true;
        }

        #endregion


        #region ASISTENCIA - EXISTENCIA E INSERCION

        private async Task<bool>
            ExisteAsistenciaAsync(
                SqlConnection conn,
                SqlTransaction? transaction,
                long servicioEmpleadoId,
                DateTime fechaTurno,
                bool bloquear,
                CancellationToken ct)
        {
            string lockSql =
                bloquear
                    ? " WITH (UPDLOCK, HOLDLOCK)"
                    : string.Empty;

            string sql = $@"
SELECT COUNT(1)
FROM dbo.Asistencia{lockSql}
WHERE ServicioEmpleadoId = @ServicioEmpleadoId
  AND FechaTurno = @FechaTurno
  AND Estatus <> @EstatusCancelada;";

            int cantidad =
                await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            ServicioEmpleadoId =
                                servicioEmpleadoId,

                            FechaTurno =
                                fechaTurno.Date,

                            EstatusCancelada =
                                ESTATUS_CANCELADA
                        },
                        transaction,
                        cancellationToken: ct));

            return cantidad > 0;
        }


        private async Task<long>
            InsertarAsistenciaAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                ServicioEmpleadoCheckInDto asignacion,
                string numeroEmpleadoEntrante,
                string? numeroEmpleadoSaliente,
                DateTime fechaTurno,
                DateTime entradaProgramada,
                DateTime salidaProgramada,
                DateTime fechaHoraCheckIn,
                bool esRetardo,
                int? minutosRetardo,
                double latitud,
                double longitud,
                int estatus,
                DateTime fechaRegistro,
                CancellationToken ct)
        {
            const string sql = @"
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
VALUES
(
    @ServicioId,
    @ServicioEmpleadoId,
    @NumeroEmpleadoEntrante,
    @NumeroEmpleadoSaliente,
    @FechaTurno,
    @FechaHoraEntradaProgramada,
    @FechaHoraSalidaProgramada,
    @FechaHoraCheckIn,
    NULL,
    @EsRetardo,
    @MinutosRetardo,

    geography::Point(
        @Latitud,
        @Longitud,
        4326
    ),

    @Estatus,
    @FechaRegistro,
    @UsuarioRegistro
);";

            return await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        asignacion.ServicioId,
                        asignacion.ServicioEmpleadoId,

                        NumeroEmpleadoEntrante =
                            numeroEmpleadoEntrante,

                        NumeroEmpleadoSaliente =
                            numeroEmpleadoSaliente,

                        FechaTurno =
                            fechaTurno.Date,

                        FechaHoraEntradaProgramada =
                            entradaProgramada,

                        FechaHoraSalidaProgramada =
                            salidaProgramada,

                        FechaHoraCheckIn =
                            fechaHoraCheckIn,

                        EsRetardo =
                            esRetardo,

                        MinutosRetardo =
                            minutosRetardo,

                        Latitud =
                            latitud,

                        Longitud =
                            longitud,

                        Estatus =
                            estatus,

                        FechaRegistro =
                            fechaRegistro,

                        UsuarioRegistro =
                            numeroEmpleadoEntrante
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region FORMATO ENTRADA

        private async Task InsertarFormatoEntradaAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            long asistenciaId,
            CheckInRequest data,
            DateTime fechaRegistro,
            CancellationToken ct)
        {
            const string sql = @"
INSERT INTO dbo.Asistencia_FormatoEntrada
(
    AsistenciaId,
    RecepcionTurnoCompleto,
    EquipoTrabajoFuncional,
    ConocimientoConsignas,
    UniformeCompleto,
    CondicionesAptas,
    HorarioDiaDescanso,
    FechaRegistro
)
VALUES
(
    @AsistenciaId,
    @RecepcionTurnoCompleto,
    @EquipoTrabajoFuncional,
    @ConocimientoConsignas,
    @UniformeCompleto,
    @CondicionesAptas,
    @HorarioDiaDescanso,
    @FechaRegistro
);";

            await conn.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        AsistenciaId =
                            asistenciaId,

                        data.FormatoEntrada!
                            .EmpleadoEntrante!
                            .RecepcionTurnoCompleto,

                        data.FormatoEntrada
                            .EmpleadoEntrante
                            .EquipoTrabajoFuncional,

                        data.FormatoEntrada
                            .EmpleadoEntrante
                            .ConocimientoConsignas,

                        data.FormatoEntrada
                            .EmpleadoEntrante
                            .UniformeCompleto,

                        data.FormatoEntrada
                            .EmpleadoEntrante
                            .CondicionesAptas,

                        data.FormatoEntrada
                            .EmpleadoEntrante
                            .HorarioDiaDescanso,

                        FechaRegistro =
                            fechaRegistro
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region FORMULARIO

        private async Task InsertarFormularioAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            long asistenciaId,
            CheckInRequest data,
            string rutaFirmaEntrante,
            string rutaFirmaSaliente,
            string rutaFotoZona,
            DateTime fechaRegistro,
            CancellationToken ct)
        {
            const string sql = @"
INSERT INTO dbo.Asistencia_Formulario
(
    AsistenciaId,
    RutaFirmaEntrante,
    RutaFirmaSaliente,
    Observaciones,
    RutaFotoZona,
    FechaRegistro
)
VALUES
(
    @AsistenciaId,
    @RutaFirmaEntrante,
    @RutaFirmaSaliente,
    @Observaciones,
    @RutaFotoZona,
    @FechaRegistro
);";

            await conn.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        AsistenciaId =
                            asistenciaId,

                        RutaFirmaEntrante =
                            rutaFirmaEntrante,

                        RutaFirmaSaliente =
                            rutaFirmaSaliente,

                        Observaciones =
                            string.IsNullOrWhiteSpace(
                                data.Formulario!
                                    .Observaciones)
                                ? null
                                : data.Formulario
                                    .Observaciones
                                    .Trim(),

                        RutaFotoZona =
                            rutaFotoZona,

                        FechaRegistro =
                            fechaRegistro
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region RESGUARDOS

        private async Task InsertarResguardosAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            long asistenciaId,
            IReadOnlyList<ResguardoCheckInRequest> resguardos,
            IReadOnlyList<string?> fotosResguardo,
            DateTime fechaRegistro,
            CancellationToken ct)
        {
            const string sql = @"
INSERT INTO dbo.Asistencia_Resguardo
(
    AsistenciaId,
    IdObjeto,
    Cantidad,
    Identificador,
    IdEstado,
    Observaciones,
    RutaFoto,
    FechaRegistro
)
VALUES
(
    @AsistenciaId,
    @IdObjeto,
    @Cantidad,
    @Identificador,
    @IdEstado,
    @Observaciones,
    @RutaFoto,
    @FechaRegistro
);";

            for (int i = 0;
                 i < resguardos.Count;
                 i++)
            {
                ResguardoCheckInRequest item =
                    resguardos[i];

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            AsistenciaId =
                                asistenciaId,

                            item.IdObjeto,
                            item.Cantidad,
                            item.Identificador,
                            item.IdEstado,
                            item.Observaciones,

                            RutaFoto =
                                fotosResguardo[i],

                            FechaRegistro =
                                fechaRegistro
                        },
                        transaction,
                        cancellationToken: ct));
            }
        }

        #endregion


        #region INCIDENCIAS

        private async Task<long>
            RegistrarIncidenciaRetardoAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                long asistenciaId,
                int servicioId,
                string numeroUsuario,
                DateTime fechaHoraActual,
                int minutosRetardo,
                CancellationToken ct)
        {
            const string sqlTipo = @"
SELECT TOP (1)
    TipoIncidenciaId,
    AfectaNomina,
    TipoAfectacionNomina,
    MontoAfectacion
FROM dbo.CAT_TIPO_INCIDENCIA
WHERE Clave = 'RETARDO'
  AND Estatus = 1;";

            TipoIncidenciaDto? tipoRetardo =
                await conn
                    .QueryFirstOrDefaultAsync<TipoIncidenciaDto>(
                        new CommandDefinition(
                            sqlTipo,
                            transaction: transaction,
                            cancellationToken: ct));

            if (tipoRetardo == null)
            {
                throw new InvalidOperationException(
                    "No existe una configuración activa para la incidencia RETARDO.");
            }

            const string sql = @"
INSERT INTO dbo.Incidencias
(
    TipoIncidenciaId,
    NumeroUsuario,
    ServicioId,
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
    @AsistenciaId,
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
                    sql,
                    new
                    {
                        tipoRetardo.TipoIncidenciaId,

                        NumeroUsuario =
                            numeroUsuario,

                        ServicioId =
                            servicioId,

                        AsistenciaId =
                            asistenciaId,

                        FechaIncidencia =
                            fechaHoraActual,

                        Descripcion =
                            $"Retardo de {minutosRetardo} minuto(s). " +
                            $"Tolerancia permitida: {MINUTOS_TOLERANCIA_RETARDO} minutos.",

                        tipoRetardo.AfectaNomina,
                        tipoRetardo.TipoAfectacionNomina,
                        tipoRetardo.MontoAfectacion,

                        UsuarioRegistro =
                            numeroUsuario,

                        FechaRegistro =
                            fechaHoraActual
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region R2

        private async Task<ResponseModel<string>>
            SubirArchivoAsync(
                IFormFile file,
                string numeroUsuario,
                DateTime fechaTurno,
                string operacionId,
                string categoria,
                List<string> archivosR2,
                CancellationToken ct)
        {
            ResponseModel<string> upload =
                await _fileAsistenciaService
                    .UploadFileAsync(
                        file,
                        numeroUsuario,
                        fechaTurno,
                        operacionId,
                        categoria,
                        ct);

            if (upload.isSuccess &&
                !string.IsNullOrWhiteSpace(
                    upload.data))
            {
                archivosR2.Add(
                    upload.data);
            }

            return upload;
        }


        private async Task LimpiarArchivosAsync(
            IEnumerable<string> archivos,
            CancellationToken ct)
        {
            foreach (string key in archivos)
            {
                try
                {
                    await _fileAsistenciaService
                        .DeleteFileAsync(
                            key,
                            ct);
                }
                catch
                {
                }
            }
        }


        private ResponseModel<CheckInResponse>
            ErrorArchivo(
                ResponseModel<string> upload)
        {
            return new ResponseModel<CheckInResponse>
            {
                isSuccess =
                    false,

                code =
                    upload.code,

                message =
                    upload.message,

                desc =
                    upload.desc,

                data =
                    null
            };
        }

        #endregion


        #region RESPONSE CHECK-IN

        private CheckInResponse CrearCheckInResponse(
            long asistenciaId,
            ServicioEmpleadoCheckInDto asignacion,
            string numeroEmpleadoEntrante,
            string? numeroEmpleadoSaliente,
            DateTime fechaTurno,
            DateTime entradaProgramada,
            DateTime salidaProgramada,
            DateTime fechaHoraCheckIn,
            bool esRetardo,
            int? minutosRetardo,
            long? incidenciaRetardoId,
            int estatus,
            string estatusDescripcion)
        {
            return new CheckInResponse
            {
                AsistenciaId =
                    asistenciaId,

                ServicioEmpleadoId =
                    asignacion.ServicioEmpleadoId,

                ServicioId =
                    asignacion.ServicioId,

                NumeroEmpleadoEntrante =
                    numeroEmpleadoEntrante,

                NumeroEmpleadoSaliente =
                    numeroEmpleadoSaliente,

                FechaTurno =
                    fechaTurno.Date,

                FechaHoraEntradaProgramada =
                    entradaProgramada,

                FechaHoraSalidaProgramada =
                    salidaProgramada,

                FechaHoraCheckIn =
                    fechaHoraCheckIn,

                EsRetardo =
                    esRetardo,

                MinutosRetardo =
                    minutosRetardo,

                IncidenciaRetardoId =
                    incidenciaRetardoId,

                Estatus =
                    estatus,

                EstatusDescripcion =
                    estatusDescripcion
            };
        }

        #endregion


        #region RELEVOS ESPERADOS CHECK-OUT

        public async Task<ResponseModel<List<RelevoEsperadoCheckOutResponse>>>
            ObtenerRelevosEsperadosCheckOutAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            var response =
                new ResponseModel<
                    List<RelevoEsperadoCheckOutResponse>>();

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

                AsistenciaActivaCheckOutDto? asistencia =
                    await ObtenerAsistenciaActivaCheckOutAsync(
                        conn,
                        numeroUsuario.Trim(),
                        null,
                        ct);

                if (asistencia == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El empleado no tiene una asistencia activa.";
                    response.data = null;

                    return response;
                }

                List<RelevoEsperadoCheckOutResponse> relevos =
                    await ObtenerRelevosEsperadosAsync(
                        conn,
                        asistencia,
                        ct);

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Relevos esperados obtenidos correctamente.";
                response.desc = null;
                response.data = relevos;

                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al obtener los relevos esperados.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los relevos esperados.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region CONSULTAR RELEVOS ESPERADOS

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

    ISNULL(
        LTRIM(RTRIM(E.UsuarioAsignado)),
        ''
    ) AS NumeroUsuario,

    CONCAT_WS(
        ' ',
        NULLIF(LTRIM(RTRIM(E.Nombres)), ''),
        NULLIF(LTRIM(RTRIM(E.ApellidoPaterno)), ''),
        NULLIF(LTRIM(RTRIM(E.ApellidoMaterno)), '')
    ) AS NombreCompleto,

    DATEADD(
        SECOND,
        DATEDIFF(
            SECOND,
            CAST('00:00:00' AS TIME),
            SE.HoraEntrada
        ),
        CAST(@FechaRelevo AS DATETIME2)
    ) AS FechaHoraEntradaProgramada,

    DATEADD(
        DAY,
        CASE
            WHEN SE.SalidaDiaSiguiente = 1 THEN 1
            ELSE 0
        END,
        DATEADD(
            SECOND,
            DATEDIFF(
                SECOND,
                CAST('00:00:00' AS TIME),
                SE.HoraSalida
            ),
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
    WHERE ASI.ServicioEmpleadoId =
          SE.ServicioEmpleadoId
      AND ASI.FechaTurno =
          @FechaRelevo
    ORDER BY
        ASI.AsistenciaId DESC
) A

WHERE SE.ServicioId =
      @ServicioId

  AND SE.ServicioEmpleadoId <>
      @ServicioEmpleadoActualId

  AND SE.FechaInicio <=
      @FechaRelevo

  AND
  (
      SE.FechaFin IS NULL
      OR SE.FechaFin >= @FechaRelevo
  )

  AND SE.HoraEntrada =
      @HoraRelevo

ORDER BY
    SE.ServicioEmpleadoId;";

            var result =
                await conn.QueryAsync<
                    RelevoEsperadoCheckOutResponse>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            ServicioId =
                                asistencia.ServicioId,

                            ServicioEmpleadoActualId =
                                asistencia.ServicioEmpleadoId,

                            FechaRelevo =
                                fechaRelevo,

                            HoraRelevo =
                                horaRelevo
                        },
                        cancellationToken: ct));

            return result.ToList();
        }

        #endregion


        #region ASISTENCIA ACTIVA CHECK-OUT

        private async Task<AsistenciaActivaCheckOutDto?>
            ObtenerAsistenciaActivaCheckOutAsync(
                SqlConnection conn,
                string numeroUsuario,
                SqlTransaction? transaction,
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
FROM dbo.Asistencia WITH (UPDLOCK, HOLDLOCK)
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
                        transaction,
                        cancellationToken: ct));
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
                // ============================================================
                // VALIDAR USUARIO
                // ============================================================

                if (string.IsNullOrWhiteSpace(numeroUsuario))
                {
                    response.isSuccess = false;
                    response.code = 401;
                    response.message =
                        "No fue posible identificar al empleado.";
                    response.data = null;

                    return response;
                }

                numeroUsuario =
                    numeroUsuario.Trim();

                // ============================================================
                // VALIDAR REQUEST
                // ============================================================

                if (data == null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El request es obligatorio.";
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

                if (!data.PuedePermanecer &&
                    string.IsNullOrWhiteSpace(
                        data.MotivoNoPermanencia))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "El motivo por el cual el empleado no puede permanecer es obligatorio.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // CONEXION
                // ============================================================

                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                // ============================================================
                // ASISTENCIA ACTIVA DEL EMPLEADO SALIENTE
                // ============================================================

                AsistenciaActivaCheckOutDto? asistencia =
                    await ObtenerAsistenciaActivaCheckOutAsync(
                        conn,
                        numeroUsuario,
                        null,
                        ct);

                if (asistencia == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El empleado no tiene una asistencia activa.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // RELEVOS ESPERADOS DEL SIGUIENTE TURNO
                // ============================================================

                List<RelevoEsperadoCheckOutResponse>
                    relevosEsperados =
                        await ObtenerRelevosEsperadosAsync(
                            conn,
                            asistencia,
                            ct);

                RelevoEsperadoCheckOutResponse? relevoAfectado =
                    relevosEsperados.FirstOrDefault(
                        x =>
                            x.ServicioEmpleadoId ==
                            data.ServicioEmpleadoAfectadoId);

                if (relevoAfectado == null)
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La asignación seleccionada no corresponde al siguiente turno de este servicio.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // EL SALIENTE NO PUEDE SER EL AUSENTE
                // ============================================================

                if (string.Equals(
                    relevoAfectado.NumeroUsuario,
                    numeroUsuario,
                    StringComparison.OrdinalIgnoreCase))
                {
                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La asignación del siguiente turno pertenece al mismo empleado.";
                    response.data = null;

                    return response;
                }

                // ============================================================
                // VALIDAR CHECK-IN DEL RELEVO
                // ============================================================

                if (relevoAfectado.AsistenciaId.HasValue)
                {
                    if (relevoAfectado.EstatusAsistencia ==
                        ESTATUS_PENDIENTE_AUTORIZAR)
                    {
                        response.isSuccess = false;
                        response.code = 409;
                        response.message =
                            "El empleado de relevo ya realizó Check-In y está pendiente de autorización.";
                        response.data = null;

                        return response;
                    }

                    if (relevoAfectado.EstatusAsistencia ==
                        ESTATUS_EN_TURNO)
                    {
                        response.isSuccess = false;
                        response.code = 409;
                        response.message =
                            "El empleado de relevo ya se encuentra en turno.";
                        response.data = null;

                        return response;
                    }
                }

                // ============================================================
                // ARMAR SOLICITUD
                // ============================================================

                var solicitudRequest =
                    new CrearSolicitudRelevoNoPlaneadoDto
                    {
                        ServicioEmpleadoAfectadoId =
                            relevoAfectado.ServicioEmpleadoId,

                        ServicioEmpleadoSalienteId =
                            asistencia.ServicioEmpleadoId,

                        OrigenClave =
                            "ASISTENCIA",

                        FechaHoraInicioCobertura =
                            relevoAfectado
                                .FechaHoraEntradaProgramada,

                        FechaHoraFinCobertura =
                            relevoAfectado
                                .FechaHoraSalidaProgramada,

                        MotivoRelevo =
                            "El empleado programado para el siguiente turno no se presentó al relevo.",

                        MotivoNoPermanencia =
                            data.PuedePermanecer
                                ? null
                                : data.MotivoNoPermanencia!
                                    .Trim(),

                        FotoEvidencia =
                            data.FotoEvidencia
                    };

                // ============================================================
                // CREAR SOLICITUD
                //
                // MISMA TRANSACCION:
                // - SOLICITUD
                // - INCIDENCIA
                // - CHECK-OUT SI NO PUEDE PERMANECER
                //
                // DESPUES DEL COMMIT:
                // - NOTIFICACION SI QUEDA SIN COBERTURA
                // ============================================================

                ResponseModel<SolicitudRelevoNoPlaneadoDto>
                    solicitudResponse =
                        await _relevoNoPlaneadoFunctions
                            .CrearSolicitudDesdeAsistenciaAsync(
                                solicitudRequest,
                                asistencia.AsistenciaId,
                                realizarCheckOut:
                                    !data.PuedePermanecer,
                                numeroUsuario,
                                accessToken,
                                ct);

                if (!solicitudResponse.isSuccess ||
                    solicitudResponse.data == null)
                {
                    response.isSuccess = false;
                    response.code =
                        solicitudResponse.code;
                    response.message =
                        solicitudResponse.message;
                    response.desc =
                        solicitudResponse.desc;
                    response.data = null;

                    return response;
                }

                long solicitudId =
                    solicitudResponse.data
                        .SolicitudRelevoNoPlaneadoId;

                // ============================================================
                // PUEDE PERMANECER -> PROPUESTA EXTENSION
                // ============================================================

                if (data.PuedePermanecer)
                {
                    ResponseModel<RelevoNoPlaneadoAsignacionDto>
                        extensionResponse =
                            await _relevoNoPlaneadoFunctions
                                .CrearExtensionAsync(
                                    solicitudId,
                                    numeroUsuario,
                                    accessToken,
                                    ct);

                    if (!extensionResponse.isSuccess ||
                        extensionResponse.data == null)
                    {
                        response.isSuccess = false;
                        response.code =
                            extensionResponse.code;

                        response.message =
                            "La solicitud de relevo fue creada, pero no fue posible crear la propuesta de extensión.";

                        response.desc =
                            string.IsNullOrWhiteSpace(
                                extensionResponse.desc)
                                ? extensionResponse.message
                                : extensionResponse.message +
                                  " " +
                                  extensionResponse.desc;

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
                                    relevoAfectado.ServicioEmpleadoId,

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
                        "La solicitud de relevo y la propuesta de extensión fueron creadas correctamente.";

                    response.desc =
                        string.IsNullOrWhiteSpace(
                            extensionResponse.desc)
                            ? "La extensión quedó pendiente de autorización del supervisor."
                            : extensionResponse.desc;

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
                                relevoAfectado.ServicioEmpleadoId,

                            SolicitudRelevoNoPlaneadoId =
                                solicitudId,

                            RelevoNoPlaneadoAsignacionId =
                                extensionResponse.data
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

                // ============================================================
                // NO PUEDE PERMANECER
                // ============================================================

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Check-Out registrado correctamente.";

                response.desc =
                    "Se registró la falta del empleado entrante y " +
                    "la solicitud de relevo quedó pendiente de asignación.";

                if (!string.IsNullOrWhiteSpace(
                    solicitudResponse.desc))
                {
                    response.desc +=
                        " " +
                        solicitudResponse.desc;
                }

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
                            relevoAfectado.ServicioEmpleadoId,

                        SolicitudRelevoNoPlaneadoId =
                            solicitudId,

                        RelevoNoPlaneadoAsignacionId =
                            null,

                        PuedePermanecer =
                            false,

                        CheckOutRealizado =
                            true,

                        FechaHoraCheckOut =
                            solicitudResponse.data
                                .FechaRegistro,

                        SolicitudEstatusClave =
                            "PENDIENTE_ASIGNACION",

                        AsignacionEstatusClave =
                            null
                    };

                return response;
            }
            catch (OperationCanceledException)
            {
                response.isSuccess = false;
                response.code = 408;
                response.message =
                    "La operación de Check-Out sin relevo fue cancelada.";
                response.data = null;

                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al procesar el Check-Out sin relevo.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al procesar el Check-Out sin relevo.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion
    }
}