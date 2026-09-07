using CerberusBusinessService.Functions.Notificaciones;
using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Asistencias;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Asistencias
{
    public class AsistenciasFunctions
    {
        private const int MINUTOS_TOLERANCIA_RETARDO = 10;

        private const int ESTATUS_EN_TURNO = 1;
        private const int ESTATUS_PENDIENTE_AUTORIZAR = 3;
        private const int ESTATUS_CANCELADA = 4;

        private readonly string _csCerberus;

        private readonly FileAsistenciaService _fileAsistenciaService;

        private readonly ServicioNotificationFunctions
            _servicioNotificationFunctions;


        public AsistenciasFunctions(
            IConfiguration config,
            FileAsistenciaService fileAsistenciaService,
            ServicioNotificationFunctions servicioNotificationFunctions)
        {
            _csCerberus =
                config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");

            _fileAsistenciaService =
                fileAsistenciaService;

            _servicioNotificationFunctions =
                servicioNotificationFunctions;
        }


        // ============================================================
        // CHECK-IN
        // ============================================================

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
                // 1. VALIDAR IDENTIDAD
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


                using var conn =
                    new SqlConnection(_csCerberus);


                await conn.OpenAsync(ct);


                // ====================================================
                // 2. HORA ACTUAL DEL SERVIDOR SQL
                // ====================================================

                DateTime fechaHoraActual =
                    await conn.ExecuteScalarAsync<DateTime>(
                        new CommandDefinition(
                            "SELECT SYSDATETIME();",
                            cancellationToken: ct));


                // ====================================================
                // 3. OBTENER EMPLEADO DESDE EL TOKEN
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
                // 4. OBTENER TURNO ASIGNADO
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
                // 5. OBTENER HORARIO OPERATIVO DEL SERVICIO
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
                // 6. DETERMINAR TIPO DE CHECK-IN
                // ====================================================
                //
                // 00:00 -> 00:00 T+1
                //
                // = servicio continuo
                // = requiere relevo
                // = requiere autorización
                //
                //
                // Ejemplo:
                //
                // 07:00 -> 21:00
                //
                // = apertura
                // = no requiere relevo
                // = entra directamente EN TURNO
                // ====================================================

                bool esRelevoContinuo =
                    EsServicioAtencionContinua(
                        horarioServicio);


                // ====================================================
                // 7. DISPATCHER
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


        // ============================================================
        // CHECK-IN DE APERTURA
        // ============================================================
        //
        // Servicio que NO es atención continua.
        //
        // Ejemplo:
        //
        // 07:00 -> 21:00
        //
        // No requiere:
        //
        // - empleado saliente
        // - relevo
        // - firmas de relevo
        // - ETO6
        // - resguardos
        // - autorización
        // - notificación
        //
        // Queda directamente EN TURNO.
        // ============================================================

        private async Task<ResponseModel<CheckInResponse>>
            ProcesarCheckInAperturaAsync(
                SqlConnection conn,
                ServicioEmpleadoCheckInDto asignacion,
                DateTime fechaTurno,
                DateTime entradaProgramada,
                DateTime salidaProgramada,
                DateTime fechaHoraActual,
                string numeroUsuario,
                CancellationToken ct)
        {
            ResponseModel<CheckInResponse> response =
                new ResponseModel<CheckInResponse>();


            var controlHora =
                CalcularHoraCheckIn(
                    entradaProgramada,
                    fechaHoraActual);


            SqlTransaction? transaction =
                null;


            try
            {
                transaction =
                    conn.BeginTransaction();


                // ====================================================
                // DUPLICADO
                // ====================================================

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
                // REGISTRAR ASISTENCIA
                //
                // ESTATUS 1 = EN TURNO
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
                        ESTATUS_EN_TURNO,
                        fechaHoraActual,
                        ct);


                // ====================================================
                // RETARDO
                // ====================================================

                long? incidenciaRetardoId =
                    null;


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


        // ============================================================
        // CHECK-IN CON RELEVO CONTINUO
        // ============================================================
        //
        // Servicio:
        //
        // 00:00 -> 00:00 T+1
        //
        // Requiere:
        //
        // - empleado saliente
        // - relevo
        // - ETO6
        // - firmas
        // - fotografía
        //
        // Resguardo:
        //
        // - PUEDE VENIR NULL
        // - PUEDE VENIR VACÍO
        //
        // Al finalizar:
        //
        // Estatus 3 = Pendiente autorizar
        //
        // y se notifica a los supervisores del servicio.
        // ============================================================

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
                string accessToken,
                CancellationToken ct)
        {
            ResponseModel<CheckInResponse> response =
                new ResponseModel<CheckInResponse>();


            List<string> archivosR2 =
                new List<string>();


            SqlTransaction? transaction =
                null;


            bool commitRealizado =
                false;


            try
            {
                // ====================================================
                // 1. VALIDAR CAMPOS DE RELEVO
                //
                // RESGUARDO NO SE VALIDA COMO OBLIGATORIO.
                // ====================================================

                string? error =
                    ValidarRequestRelevo(data);


                if (error != null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        error;
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
                // 2. VALIDAR QUE EL SALIENTE EXISTA
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


                // ====================================================
                // 3. VALIDAR QUE EL SALIENTE ESTÉ EN TURNO
                //    EN ESTE MISMO SERVICIO
                // ====================================================

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
                // 4. CONTROL DE HORA / RETARDO
                // ====================================================

                var controlHora =
                    CalcularHoraCheckIn(
                        entradaProgramada,
                        fechaHoraActual);


                // ====================================================
                // 5. DUPLICADO ANTES DE SUBIR ARCHIVOS
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
                // 6. SUBIR FIRMA ENTRANTE
                // ====================================================

                string operacionId =
                    Guid.NewGuid()
                        .ToString("N");


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


                // ====================================================
                // 7. SUBIR FIRMA SALIENTE
                // ====================================================

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


                // ====================================================
                // 8. FOTO DE ZONA
                // ====================================================

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
                // 9. RESGUARDOS OPCIONALES
                // ====================================================
                //
                // No se valida que existan.
                //
                // null   -> válido
                // []     -> válido
                //
                // Si llegan elementos, se procesan.
                // ====================================================

                List<ResguardoCheckInRequest> resguardos =
                    data.Resguardo
                    ?? new List<ResguardoCheckInRequest>();


                List<string?> fotosResguardo =
                    new List<string?>();


                foreach (var item
                         in resguardos)
                {
                    /*
                     * Tampoco obligamos a que el resguardo
                     * tenga fotografía desde este método.
                     *
                     * Si viene foto, se almacena.
                     */
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
                // 10. TRANSACCIÓN
                // ====================================================

                transaction =
                    conn.BeginTransaction();


                // ====================================================
                // 11. VALIDACIÓN DUPLICADO CON LOCK
                // ====================================================

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

                    transaction =
                        null;


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
                // 12. ASISTENCIA
                //
                // ESTATUS 3 = PENDIENTE AUTORIZAR
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
                        ESTATUS_PENDIENTE_AUTORIZAR,
                        fechaHoraActual,
                        ct);


                // ====================================================
                // 13. ETO6 / FORMATO DE ENTRADA
                // ====================================================

                await InsertarFormatoEntradaAsync(
                    conn,
                    transaction,
                    asistenciaId,
                    data,
                    fechaHoraActual,
                    ct);


                // ====================================================
                // 14. FORMULARIO
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
                // 15. RESGUARDOS OPCIONALES
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
                // 16. INCIDENCIA RETARDO
                // ====================================================

                long? incidenciaRetardoId =
                    null;


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
                // 17. COMMIT
                // ====================================================

                transaction.Commit();

                transaction =
                    null;

                commitRealizado =
                    true;


                // ====================================================
                // 18. RESPONSE
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
                // 19. PAYLOAD DE NOTIFICACIÓN
                // ====================================================

                var resguardosNotificacion =
                    resguardos
                        .Select(
                            (item, index) =>
                                new
                                {
                                    item.IdObjeto,

                                    item.Cantidad,

                                    Identificador =
                                        item.Identificador,

                                    item.IdEstado,

                                    Observaciones =
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
                // 20. NOTIFICACIÓN A SUPERVISORES
                //
                // SIEMPRE DESPUÉS DEL COMMIT
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
                    /*
                     * NO se revierte la asistencia.
                     *
                     * El COMMIT ya ocurrió.
                     */
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


        // ============================================================
        // OBTENER EMPLEADO
        // ============================================================

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


        // ============================================================
        // EXISTE EMPLEADO
        // ============================================================

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


        // ============================================================
        // VALIDAR EMPLEADO SALIENTE EN TURNO
        // ============================================================

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


        // ============================================================
        // OBTENER TURNO DEL EMPLEADO
        // ============================================================

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


            /*
             * No filtramos TipoAsignacionServicioId.
             *
             * Un empleado puede llegar por:
             *
             * 1 regular
             * 2 falta
             * 3 cobertura
             *
             * Si la asignación pertenece al empleado,
             * debe poder realizar Check-In.
             */

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
            // 1. TURNO ACTIVO
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


                foreach (DateTime fecha
                         in fechas)
                {
                    if (!FechaDentroDeAsignacion(
                        fecha,
                        asignacion))
                    {
                        continue;
                    }


                    DateTime entrada =
                        fecha.Date
                            .Add(asignacion.HoraEntrada);


                    DateTime salida =
                        fecha.Date
                            .Add(asignacion.HoraSalida);


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
            // 2. PRÓXIMO TURNO DE HOY
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


        // ============================================================
        // HORARIO OPERATIVO DEL SERVICIO
        // ============================================================

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
    IdServicioHorario,
    IdServicio,
    DiaSemana,
    HoraInicio,
    HoraFin,
    CruzaDia,
    Activo,
    VigenteDesde,
    VigenteHasta
FROM dbo.Servicio_Horarios
WHERE IdServicio = @ServicioId
  AND DiaSemana = @DiaSemana
  AND Activo = 1
  AND
  (
      VigenteDesde IS NULL
      OR VigenteDesde <= @Fecha
  )
  AND
  (
      VigenteHasta IS NULL
      OR VigenteHasta >= @Fecha
  )
ORDER BY
    VigenteDesde DESC,
    IdServicioHorario DESC;";


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


        // ============================================================
        // ATENCIÓN CONTINUA
        // ============================================================

        private bool EsServicioAtencionContinua(
            ServicioHorarioCheckInDto horario)
        {
            return
                horario.HoraInicio == TimeSpan.Zero &&
                horario.HoraFin == TimeSpan.Zero &&
                horario.CruzaDia;
        }


        // ============================================================
        // DÍA DE SEMANA
        // ============================================================

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


        // ============================================================
        // CONTROL HORA CHECK-IN
        // ============================================================

        private (
            DateTime FechaHoraCheckIn,
            bool EsRetardo,
            int? MinutosRetardo)
            CalcularHoraCheckIn(
                DateTime entradaProgramada,
                DateTime fechaHoraActual)
        {
            // ========================================================
            // LLEGA ANTES
            // ========================================================

            if (fechaHoraActual <
                entradaProgramada)
            {
                return (
                    entradaProgramada,
                    false,
                    null);
            }


            // ========================================================
            // TOLERANCIA 10 MINUTOS
            // ========================================================

            DateTime limiteTolerancia =
                entradaProgramada
                    .AddMinutes(
                        MINUTOS_TOLERANCIA_RETARDO);


            if (fechaHoraActual <=
                limiteTolerancia)
            {
                return (
                    fechaHoraActual,
                    false,
                    null);
            }


            // ========================================================
            // RETARDO
            // ========================================================

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


        // ============================================================
        // VALIDAR FECHA ASIGNACIÓN
        // ============================================================

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


        // ============================================================
        // DUPLICADO
        // ============================================================

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


        // ============================================================
        // INSERT ASISTENCIA
        // ============================================================

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


        // ============================================================
        // INSERT FORMATO ENTRADA
        // ============================================================

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


        // ============================================================
        // INSERT FORMULARIO
        // ============================================================

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


        // ============================================================
        // INSERT RESGUARDOS
        // ============================================================

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


        // ============================================================
        // INCIDENCIA DE RETARDO
        // ============================================================

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


        // ============================================================
        // R2 - SUBIR
        // ============================================================

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


        // ============================================================
        // R2 - LIMPIAR
        // ============================================================

        private async Task LimpiarArchivosAsync(
            IEnumerable<string> archivos,
            CancellationToken ct)
        {
            foreach (string key
                     in archivos)
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
                    /*
                     * Nunca sustituir el error original.
                     */
                }
            }
        }


        // ============================================================
        // ERROR ARCHIVO
        // ============================================================

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


        // ============================================================
        // VALIDAR RELEVO
        // ============================================================
        //
        // IMPORTANTE:
        //
        // RESGUARDO NO ES OBLIGATORIO.
        //
        // No existe ninguna validación:
        //
        // data.Resguardo == null  -> válido
        // data.Resguardo.Count=0  -> válido
        // ============================================================

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
                data.Formulario
                    .IdEmpleadoSaliente))
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


            /*
             * DELIBERADAMENTE NO SE VALIDA RESGUARDO.
             */


            return null;
        }


        // ============================================================
        // RESPONSE
        // ============================================================

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
    }
}