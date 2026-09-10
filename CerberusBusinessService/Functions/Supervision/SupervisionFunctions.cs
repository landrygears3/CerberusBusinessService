using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Supervision;
using CerberusBusinessService.Functions.Notificaciones;
using CerberusBusinessService.Models.DTO.Notificaciones;
using CerberusBusinessService.Functions.Relevos;
using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Supervision
{
    public class SupervisionFunctions
    {
        #region CONSTANTES

        private const int ESTATUS_PENDIENTE_AUTORIZAR = 3;
        private readonly NotificationClient _notificationClient;
        #endregion


        #region PROPIEDADES
        private readonly RelevoNoPlaneadoFunctions _relevoNoPlaneadoFunctions;
        private readonly string _csCerberus;

        #endregion


        #region CONSTRUCTOR

        public SupervisionFunctions(
            IConfiguration config,
            NotificationClient notificationClient,
            RelevoNoPlaneadoFunctions relevoNoPlaneadoFunctions)
        {
            _csCerberus =
                config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");

            _notificationClient = notificationClient;
            _relevoNoPlaneadoFunctions = relevoNoPlaneadoFunctions;
        }

        #endregion


        #region PENDIENTES DE AUTORIZAR

        public async Task<List<ListadoAsistenciaPendienteAutorizarResponse>>
            ObtenerPendientesAutorizarAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                throw new ArgumentException(
                    "El NumeroUsuario es obligatorio.",
                    nameof(numeroUsuario));
            }


            using var conn =
                new SqlConnection(_csCerberus);


            await conn.OpenAsync(ct);


            int? supervisorEmpleadoId =
                await ObtenerEmpleadoIdAsync(
                    conn,
                    numeroUsuario.Trim(),
                    ct);


            /*
             * Si el usuario existe en Seguridad pero no tiene
             * empleado relacionado, simplemente no tiene
             * servicios que supervisar.
             */
            if (!supervisorEmpleadoId.HasValue)
            {
                return new List<ListadoAsistenciaPendienteAutorizarResponse>();
            }


            return await ObtenerPendientesSupervisorAsync(
                conn,
                supervisorEmpleadoId.Value,
                ct);
        }

        #endregion


        #region EMPLEADO

        private async Task<int?>
            ObtenerEmpleadoIdAsync(
                SqlConnection conn,
                string numeroUsuario,
                CancellationToken ct)
        {
            const string sql = @"
SELECT TOP (1)
    ID
FROM dbo.DatosGeneralesEmpleado
WHERE LTRIM(RTRIM(UsuarioAsignado)) = @NumeroUsuario;";


            return await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        NumeroUsuario =
                            numeroUsuario
                    },
                    cancellationToken: ct));
        }

        #endregion


        #region SERVICIOS SUPERVISADOS

        private async Task<List<ListadoAsistenciaPendienteAutorizarResponse>>
            ObtenerPendientesSupervisorAsync(
                SqlConnection conn,
                int supervisorEmpleadoId,
                CancellationToken ct)
        {
            /*
             * IMPORTANTE:
             *
             * El permiso ABAC determina si puede utilizar
             * la función.
             *
             * ServicioSupervisor determina sobre cuáles
             * servicios puede ejercer esa función.
             *
             * Un Supervisor Operativo que no esté asignado
             * directamente como supervisor de un servicio
             * no obtiene las asistencias de los supervisores
             * que tiene debajo.
             */

            const string sql = @"
SELECT
    A.AsistenciaId,
    A.ServicioEmpleadoId,
    A.ServicioId,
    A.NumeroEmpleadoEntrante,
    A.NumeroEmpleadoSaliente,
    A.FechaTurno,
    A.FechaHoraEntradaProgramada,
    A.FechaHoraSalidaProgramada,
    A.FechaHoraCheckIn,
    A.EsRetardo,
    A.MinutosRetardo,
    A.Estatus
FROM dbo.Asistencia A
WHERE A.Estatus = @EstatusPendienteAutorizar
  AND A.FechaHoraCheckOut IS NULL
  AND EXISTS
  (
      SELECT 1
      FROM dbo.ServicioSupervisor SS
      WHERE SS.ServicioId = A.ServicioId
        AND SS.SupervisorEmpleadoId = @SupervisorEmpleadoId
        AND SS.FechaInicio <= A.FechaTurno
        AND
        (
            SS.FechaFin IS NULL
            OR SS.FechaFin >= A.FechaTurno
        )
  )
ORDER BY
    A.FechaHoraCheckIn ASC,
    A.AsistenciaId ASC;";


            var result =
                await conn.QueryAsync<
                    ListadoAsistenciaPendienteAutorizarResponse>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            SupervisorEmpleadoId =
                                supervisorEmpleadoId,

                            EstatusPendienteAutorizar =
                                ESTATUS_PENDIENTE_AUTORIZAR
                        },
                        cancellationToken: ct));


            return result.ToList();
        }

        #endregion


        #region Detalle Check-In pendiente de autorización

        public async Task<ResponseModel<CheckInAutorizacionResponse>>
            ObtenerCheckInPendienteAsync(
                long asistenciaId,
                string numeroUsuario,
                CancellationToken ct)
        {
            ResponseModel<CheckInAutorizacionResponse> response =
                new ResponseModel<CheckInAutorizacionResponse>();


            if (asistenciaId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    "AsistenciaId inválido.";
                response.data = null;

                return response;
            }


            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al usuario autenticado.";
                response.data = null;

                return response;
            }


            using var conn =
                new SqlConnection(_csCerberus);


            await conn.OpenAsync(ct);


            // ================================================================
            // 1. ASISTENCIA
            //
            // Además de obtener la asistencia:
            //
            // - debe estar pendiente de autorización
            // - debe pertenecer a un servicio supervisado por
            //   el usuario autenticado
            //
            // ================================================================

            const string sqlAsistencia = @"
SELECT TOP (1)
    A.AsistenciaId,
    A.ServicioEmpleadoId,
    A.ServicioId,
    A.NumeroEmpleadoEntrante,
    A.NumeroEmpleadoSaliente,
    A.FechaTurno,
    A.FechaHoraEntradaProgramada,
    A.FechaHoraSalidaProgramada,
    A.FechaHoraCheckIn,
    A.FechaRegistro AS FechaHoraRegistro,
    A.EsRetardo,
    A.MinutosRetardo,
    10 AS ToleranciaMinutos,
    A.Estatus,
    'Pendiente autorizar' AS EstatusDescripcion
FROM dbo.Asistencia A
INNER JOIN dbo.DatosGeneralesEmpleado E
    ON LTRIM(RTRIM(E.UsuarioAsignado)) =
       LTRIM(RTRIM(@NumeroUsuario))
WHERE A.AsistenciaId = @AsistenciaId
  AND A.Estatus = 3
  AND A.FechaHoraCheckOut IS NULL
  AND EXISTS
  (
      SELECT 1
      FROM dbo.ServicioSupervisor SS
      WHERE SS.ServicioId = A.ServicioId
        AND SS.SupervisorEmpleadoId = E.ID
        AND SS.FechaInicio <= A.FechaTurno
        AND
        (
            SS.FechaFin IS NULL
            OR SS.FechaFin >= A.FechaTurno
        )
  );";


            CheckInAutorizacionResponse? detalle =
                await conn.QueryFirstOrDefaultAsync<
                    CheckInAutorizacionResponse>(
                    new CommandDefinition(
                        sqlAsistencia,
                        new
                        {
                            AsistenciaId =
                                asistenciaId,

                            NumeroUsuario =
                                numeroUsuario.Trim()
                        },
                        cancellationToken: ct));


            if (detalle == null)
            {
                response.isSuccess = false;
                response.code = 404;
                response.message =
                    "No se encontró una asistencia pendiente de autorización asignada al supervisor.";
                response.data = null;

                return response;
            }


            // ================================================================
            // 2. FORMATO DE ENTRADA / ETO6
            // ================================================================

            const string sqlFormatoEntrada = @"
SELECT
    RecepcionTurnoCompleto,
    EquipoTrabajoFuncional,
    ConocimientoConsignas,
    UniformeCompleto,
    CondicionesAptas,
    HorarioDiaDescanso
FROM dbo.Asistencia_FormatoEntrada
WHERE AsistenciaId = @AsistenciaId;";


            detalle.FormatoEntrada =
                await conn.QueryFirstOrDefaultAsync<
                    CheckInFormatoEntradaAutorizacionResponse>(
                    new CommandDefinition(
                        sqlFormatoEntrada,
                        new
                        {
                            AsistenciaId =
                                asistenciaId
                        },
                        cancellationToken: ct));


            // ================================================================
            // 3. RESGUARDOS
            //
            // Pueden no existir.
            // ================================================================

            const string sqlResguardos = @"
SELECT
    IdObjeto,
    Cantidad,
    Identificador,
    IdEstado,
    Observaciones,
    RutaFoto
FROM dbo.Asistencia_Resguardo
WHERE AsistenciaId = @AsistenciaId
ORDER BY AsistenciaResguardoId;";


            var resguardos =
                await conn.QueryAsync<
                    CheckInResguardoAutorizacionResponse>(
                    new CommandDefinition(
                        sqlResguardos,
                        new
                        {
                            AsistenciaId =
                                asistenciaId
                        },
                        cancellationToken: ct));


            detalle.Resguardos =
                resguardos.ToList();


            // ================================================================
            // 4. FORMULARIO / FIRMAS / FOTO
            // ================================================================

            const string sqlFormulario = @"
SELECT
    RutaFirmaEntrante,
    RutaFirmaSaliente,
    Observaciones,
    RutaFotoZona
FROM dbo.Asistencia_Formulario
WHERE AsistenciaId = @AsistenciaId;";


            detalle.Formulario =
                await conn.QueryFirstOrDefaultAsync<
                    CheckInFormularioAutorizacionResponse>(
                    new CommandDefinition(
                        sqlFormulario,
                        new
                        {
                            AsistenciaId =
                                asistenciaId
                        },
                        cancellationToken: ct));


            // ================================================================
            // 5. RESPONSE
            // ================================================================

            response.isSuccess = true;
            response.code = 200;
            response.message =
                "Información del Check-In obtenida correctamente.";
            response.desc = null;
            response.data =
                detalle;


            return response;
        }

        #endregion


        #region Autorizar Turno

        public async Task<ResponseModel<AutorizarTurnoResponse>>
            AutorizarTurnoAsync(
                AutorizarTurnoRequest request,
                string numeroSupervisor,
                string accessToken,
                CancellationToken ct)
        {
            ResponseModel<AutorizarTurnoResponse> response =
                new ResponseModel<AutorizarTurnoResponse>();


            const int ESTATUS_EN_TURNO = 1;
            const int ESTATUS_FINALIZADA = 2;
            const int ESTATUS_PENDIENTE_AUTORIZAR = 3;


            using var conn =
                new SqlConnection(_csCerberus);


            await conn.OpenAsync(ct);


            SqlTransaction? transaction =
                null;


            bool commitRealizado =
                false;


            try
            {
                transaction =
                    conn.BeginTransaction();


                // ============================================================
                // 1. HORA OFICIAL
                // ============================================================

                DateTime fechaHoraActual =
                    await conn.ExecuteScalarAsync<DateTime>(
                        new CommandDefinition(
                            "SELECT SYSDATETIME();",
                            transaction: transaction,
                            cancellationToken: ct));


                // ============================================================
                // 2. OBTENER Y BLOQUEAR CHECK-IN PENDIENTE
                // ============================================================

                const string sqlPendiente = @"
SELECT
    AsistenciaId,
    ServicioId,
    NumeroEmpleadoEntrante,
    NumeroEmpleadoSaliente,
    FechaTurno,
    FechaHoraCheckOut,
    Estatus
FROM dbo.Asistencia WITH (UPDLOCK, HOLDLOCK)
WHERE AsistenciaId = @AsistenciaId
  AND ServicioId = @ServicioId;";


                AutorizacionTurnoData? asistencia =
                    await conn.QueryFirstOrDefaultAsync<
                        AutorizacionTurnoData>(
                        new CommandDefinition(
                            sqlPendiente,
                            new
                            {
                                request.AsistenciaId,
                                request.ServicioId
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
                        "No se encontró la asistencia indicada.";
                    response.data = null;

                    return response;
                }


                // ============================================================
                // 3. VALIDAR ESTATUS
                // ============================================================

                if (asistencia.Estatus !=
                    ESTATUS_PENDIENTE_AUTORIZAR)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La asistencia ya no está pendiente de autorización.";
                    response.data = null;

                    return response;
                }


                if (asistencia.FechaHoraCheckOut.HasValue)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La asistencia indicada ya tiene Check-Out.";
                    response.data = null;

                    return response;
                }


                if (string.IsNullOrWhiteSpace(
                    asistencia.NumeroEmpleadoSaliente))
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "La asistencia no tiene un empleado saliente relacionado.";
                    response.data = null;

                    return response;
                }


                // ============================================================
                // 4. VALIDAR QUE EL SUPERVISOR TENGA ESE SERVICIO
                // ============================================================

                const string sqlSupervisor = @"
SELECT COUNT(1)
FROM dbo.ServicioSupervisor SS
INNER JOIN dbo.DatosGeneralesEmpleado E
    ON E.ID = SS.SupervisorEmpleadoId
WHERE SS.ServicioId = @ServicioId
  AND LTRIM(RTRIM(E.UsuarioAsignado)) = @NumeroSupervisor
  AND SS.FechaInicio <= @FechaTurno
  AND
  (
      SS.FechaFin IS NULL
      OR SS.FechaFin >= @FechaTurno
  );";


                int asignacionesSupervisor =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlSupervisor,
                            new
                            {
                                ServicioId =
                                    request.ServicioId,

                                NumeroSupervisor =
                                    numeroSupervisor.Trim(),

                                FechaTurno =
                                    asistencia.FechaTurno.Date
                            },
                            transaction,
                            cancellationToken: ct));


                if (asignacionesSupervisor == 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 403;
                    response.message =
                        "El supervisor no tiene acceso a este servicio.";
                    response.data = null;

                    return response;
                }


                // ============================================================
                // 5. OBTENER ASISTENCIA ACTIVA DEL EMPLEADO SALIENTE
                //
                // TOP 2 PARA DETECTAR INCONSISTENCIA.
                // ============================================================

                const string sqlSaliente = @"
SELECT TOP (2)
    AsistenciaId,
    NumeroEmpleadoEntrante
FROM dbo.Asistencia WITH (UPDLOCK, HOLDLOCK)
WHERE ServicioId = @ServicioId
  AND NumeroEmpleadoEntrante = @NumeroEmpleadoSaliente
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL
ORDER BY
    FechaHoraCheckIn DESC,
    AsistenciaId DESC;";


                var salientes =
                    (
                        await conn.QueryAsync<
                            AsistenciaSalienteAutorizacionDto>(
                            new CommandDefinition(
                                sqlSaliente,
                                new
                                {
                                    ServicioId =
                                        request.ServicioId,

                                    NumeroEmpleadoSaliente =
                                        asistencia
                                            .NumeroEmpleadoSaliente!
                                            .Trim(),

                                    EstatusEnTurno =
                                        ESTATUS_EN_TURNO
                                },
                                transaction,
                                cancellationToken: ct))
                    ).ToList();


                if (salientes.Count == 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El empleado saliente ya no tiene una asistencia activa en este servicio.";
                    response.data = null;

                    return response;
                }


                if (salientes.Count > 1)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El empleado saliente tiene más de una asistencia activa.";
                    response.data = null;

                    return response;
                }


                AsistenciaSalienteAutorizacionDto saliente =
                    salientes[0];


                // ============================================================
                // 6. AUTORIZAR CHECK-IN ENTRANTE
                //
                // 3 -> 1
                // ============================================================

                const string sqlAutorizarEntrante = @"
UPDATE dbo.Asistencia
SET Estatus = @EstatusEnTurno
WHERE AsistenciaId = @AsistenciaId
  AND ServicioId = @ServicioId
  AND Estatus = @EstatusPendienteAutorizar
  AND FechaHoraCheckOut IS NULL;";


                int actualizadoEntrante =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlAutorizarEntrante,
                            new
                            {
                                request.AsistenciaId,
                                request.ServicioId,

                                EstatusEnTurno =
                                    ESTATUS_EN_TURNO,

                                EstatusPendienteAutorizar =
                                    ESTATUS_PENDIENTE_AUTORIZAR
                            },
                            transaction,
                            cancellationToken: ct));


                if (actualizadoEntrante != 1)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "No fue posible autorizar el Check-In.";
                    response.data = null;

                    return response;
                }


                // ============================================================
                // 7. CHECK-OUT AUTOMÁTICO DEL SALIENTE
                //
                // 1 -> 2
                // ============================================================

                const string sqlCheckOutSaliente = @"
UPDATE dbo.Asistencia
SET
    FechaHoraCheckOut = @FechaHoraCheckOut,
    Estatus = @EstatusFinalizada
WHERE AsistenciaId = @AsistenciaSalienteId
  AND ServicioId = @ServicioId
  AND Estatus = @EstatusEnTurno
  AND FechaHoraCheckOut IS NULL;";


                int actualizadoSaliente =
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlCheckOutSaliente,
                            new
                            {
                                AsistenciaSalienteId =
                                    saliente.AsistenciaId,

                                request.ServicioId,

                                FechaHoraCheckOut =
                                    fechaHoraActual,

                                EstatusFinalizada =
                                    ESTATUS_FINALIZADA,

                                EstatusEnTurno =
                                    ESTATUS_EN_TURNO
                            },
                            transaction,
                            cancellationToken: ct));


                if (actualizadoSaliente != 1)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "No fue posible generar el Check-Out del empleado saliente.";
                    response.data = null;

                    return response;
                }


                // ============================================================
                // 8. COMMIT
                //
                // LOS DOS CAMBIOS SON UNA SOLA OPERACIÓN.
                // ============================================================

                transaction.Commit();

                transaction =
                    null;

                commitRealizado =
                    true;


                // ============================================================
                // 9. RESPONSE
                // ============================================================

                AutorizarTurnoResponse resultado =
                    new AutorizarTurnoResponse
                    {
                        ServicioId =
                            request.ServicioId,

                        AsistenciaEntranteId =
                            asistencia.AsistenciaId,

                        AsistenciaSalienteId =
                            saliente.AsistenciaId,

                        NumeroEmpleadoEntrante =
                            asistencia
                                .NumeroEmpleadoEntrante
                                .Trim(),

                        NumeroEmpleadoSaliente =
                            asistencia
                                .NumeroEmpleadoSaliente!
                                .Trim(),

                        FechaHoraAutorizacion =
                            fechaHoraActual,

                        FechaHoraCheckOut =
                            fechaHoraActual,

                        EstatusEntrante =
                            ESTATUS_EN_TURNO,

                        EstatusSaliente =
                            ESTATUS_FINALIZADA
                    };


                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Turno autorizado correctamente.";
                response.desc =
                    "El empleado entrante quedó en turno y se generó el Check-Out del empleado saliente.";
                response.data =
                    resultado;


                // ============================================================
                // 10. NOTIFICACIÓN AL ENTRANTE
                //
                // SI FALLA NO REVERTIMOS EL CAMBIO DE ASISTENCIA.
                // ============================================================

                List<string> erroresNotificacion =
                    new List<string>();


                try
                {
                    var notificacionEntrante =
                        await EnviarNotificacionEmpleadoAsync(
                            resultado.NumeroEmpleadoEntrante,
                            "ASISTENCIA_CHECKIN_AUTORIZADO",
                            "Inicio de turno autorizado",
                            "Inicio de turno autorizado, supervisión en curso",
                            resultado,
                            accessToken,
                            ct);


                    if (!notificacionEntrante.isSuccess)
                    {
                        erroresNotificacion.Add(
                            "No fue posible notificar al empleado entrante: " +
                            notificacionEntrante.message);
                    }
                }
                catch (Exception ex)
                {
                    erroresNotificacion.Add(
                        "Error al notificar al empleado entrante: " +
                        ex.Message);
                }


                // ============================================================
                // 11. NOTIFICACIÓN AL SALIENTE
                // ============================================================

                try
                {
                    var notificacionSaliente =
                        await EnviarNotificacionEmpleadoAsync(
                            resultado.NumeroEmpleadoSaliente,
                            "ASISTENCIA_CHECKOUT_AUTORIZADO",
                            "Logout aprobado",
                            "Logout aprobado, puedes abandonar tu puesto",
                            resultado,
                            accessToken,
                            ct);


                    if (!notificacionSaliente.isSuccess)
                    {
                        erroresNotificacion.Add(
                            "No fue posible notificar al empleado saliente: " +
                            notificacionSaliente.message);
                    }
                }
                catch (Exception ex)
                {
                    erroresNotificacion.Add(
                        "Error al notificar al empleado saliente: " +
                        ex.Message);
                }


                // ============================================================
                // 12. RESULTADO DE NOTIFICACIONES
                // ============================================================

                if (erroresNotificacion.Count == 0)
                {
                    response.desc =
                        "El empleado entrante quedó en turno, " +
                        "se generó el Check-Out del empleado saliente " +
                        "y ambos empleados fueron notificados.";
                }
                else
                {
                    response.desc =
                        "El turno fue autorizado correctamente, " +
                        "pero ocurrieron problemas al enviar notificaciones. " +
                        string.Join(
                            " | ",
                            erroresNotificacion);
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
                }


                throw;
            }
        }

        #endregion


        #region Notificaciones de Supervisión

        private async Task<
            ResponseModel<NotificationResponse<AutorizarTurnoResponse>>>
            EnviarNotificacionEmpleadoAsync(
                string numeroUsuario,
                string type,
                string titulo,
                string mensaje,
                AutorizarTurnoResponse data,
                string accessToken,
                CancellationToken ct)
        {
            CreateNotificationRequest<AutorizarTurnoResponse> request =
                new CreateNotificationRequest<AutorizarTurnoResponse>
                {
                    Type =
                        type,

                    Titulo =
                        titulo,

                    Mensaje =
                        mensaje,

                    Target =
                        new NotificationTarget
                        {
                            Tipo =
                                NotificationTargetTypes.Usuarios,

                            NumeroUsuarios =
                                new List<string>
                                {
                            numeroUsuario
                                },

                            RolIds =
                                new List<int>(),

                            ActividadIds =
                                new List<int>(),

                            MatchMode =
                                "ANY"
                        },

                    Data =
                        data
                };


            return await _notificationClient
                .SendAsync(
                    request,
                    accessToken,
                    ct);
        }

        #endregion


        #region RETIRAR ELEMENTO Y SOLICITAR RELEVO

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            RetirarElementoAsync(
                long supervisionId,
                string motivoRelevo,
                string numeroSupervisor,
                string accessToken,
                CancellationToken ct)
        {
            return await _relevoNoPlaneadoFunctions
                .CrearSolicitudDesdeSupervisionAsync(
                    supervisionId,
                    motivoRelevo,
                    numeroSupervisor,
                    accessToken,
                    ct);
        }

        #endregion

    }
}