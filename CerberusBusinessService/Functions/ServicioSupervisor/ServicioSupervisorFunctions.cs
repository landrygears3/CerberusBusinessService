using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.ServicioSupervisor;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.ServicioSupervisor
{
    public class ServicioSupervisorFunctions
    {
        #region CONSTANTES

        private const int ESTATUS_OK = 1;
        private const int ESTATUS_SIN_RELEVO = 2;
        private const int ESTATUS_FALTA_SUPERVISION = 3;

        private const int SEGUNDOS_DIA = 86400;

        #endregion

        #region PROPIEDADES

        private readonly string _csCerberus;

        #endregion

        #region CONSTRUCTOR

        public ServicioSupervisorFunctions(
            IConfiguration config)
        {
            _csCerberus =
                config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");
        }

        #endregion

        #region OBTENER SERVICIOS ASIGNADOS

        public async Task<
            ResponseModel<List<ServicioSupervisorListadoResponse>>>
            ObtenerServiciosAsignadosAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            ResponseModel<List<ServicioSupervisorListadoResponse>> response =
                new ResponseModel<List<ServicioSupervisorListadoResponse>>();

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al supervisor autenticado.";
                response.desc = null;
                response.data = null;

                return response;
            }

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                const string sqlEmpleado = @"
SELECT TOP (1)
    ID
FROM dbo.DatosGeneralesEmpleado
WHERE LTRIM(RTRIM(UsuarioAsignado)) = @NumeroUsuario;";

                int? supervisorEmpleadoId =
                    await conn.ExecuteScalarAsync<int?>(
                        new CommandDefinition(
                            sqlEmpleado,
                            new
                            {
                                NumeroUsuario =
                                    numeroUsuario.Trim()
                            },
                            cancellationToken: ct));

                if (!supervisorEmpleadoId.HasValue)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe un empleado relacionado con el usuario autenticado.";
                    response.desc = null;
                    response.data = null;

                    return response;
                }

                const string sql = @"
DECLARE @FechaActual DATE =
    CAST(SYSDATETIME() AS DATE);

DECLARE @FechaHoraActual DATETIME2 =
    SYSDATETIME();

SELECT
    S.NombreServicio,
    S.Descripcion,
    S.Direccion,

    CASE
        WHEN I.SinRelevo = 1
            THEN @EstatusSinRelevo

        WHEN I.FaltaSupervision = 1
            THEN @EstatusFaltaSupervision

        ELSE @EstatusOk
    END AS Estatus,

    CASE
        WHEN I.SinRelevo = 1
             AND I.FaltaSupervision = 1
            THEN 'Sin relevo +1'

        WHEN I.SinRelevo = 1
            THEN 'Sin relevo'

        WHEN I.FaltaSupervision = 1
            THEN 'Falta supervisión'

        ELSE 'Ok'
    END AS EstatusDesc

FROM dbo.Servicio S

CROSS APPLY
(
    SELECT
        CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM dbo.SolicitudRelevoNoPlaneado SR

                INNER JOIN dbo.ServicioEmpleado SE
                    ON SE.ServicioEmpleadoId =
                       SR.ServicioEmpleadoAfectadoId

                INNER JOIN dbo.CAT_RelevoNoPlaneadoEstatus ER
                    ON ER.RelevoNoPlaneadoEstatusId =
                       SR.RelevoNoPlaneadoEstatusId

                WHERE SE.ServicioId =
                      S.ServicioId

                  AND ER.Clave IN
                  (
                      'PENDIENTE_ASIGNACION',
                      'EN_PROCESO'
                  )

                  AND SR.FechaHoraFinCobertura >
                      @FechaHoraActual
            )
            THEN 1
            ELSE 0
        END AS SinRelevo,

        CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM dbo.Asistencia A

                WHERE A.ServicioId =
                      S.ServicioId

                  AND A.Estatus = 3

                  AND A.FechaHoraCheckOut
                      IS NULL
            )
            THEN 1
            ELSE 0
        END AS FaltaSupervision
) I

WHERE EXISTS
(
    SELECT 1
    FROM dbo.ServicioSupervisor SS

    WHERE SS.ServicioId =
          S.ServicioId

      AND SS.SupervisorEmpleadoId =
          @SupervisorEmpleadoId

      AND SS.FechaInicio <=
          @FechaActual

      AND
      (
          SS.FechaFin IS NULL
          OR SS.FechaFin >= @FechaActual
      )
)

ORDER BY
    CASE
        WHEN I.SinRelevo = 1 THEN 1
        WHEN I.FaltaSupervision = 1 THEN 2
        ELSE 3
    END,

    S.NombreServicio;";

                var result =
                    await conn.QueryAsync<
                        ServicioSupervisorListadoResponse>(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                SupervisorEmpleadoId =
                                    supervisorEmpleadoId.Value,

                                EstatusOk =
                                    ESTATUS_OK,

                                EstatusSinRelevo =
                                    ESTATUS_SIN_RELEVO,

                                EstatusFaltaSupervision =
                                    ESTATUS_FALTA_SUPERVISION
                            },
                            cancellationToken: ct));

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Servicios asignados al supervisor obtenidos correctamente.";
                response.desc = null;
                response.data =
                    result.ToList();

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los servicios asignados al supervisor.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region ASIGNAR SERVICIOS

        public async Task<
            ResponseModel<AsignarServiciosSupervisorResponse>>
            AsignarServiciosAsync(
                AsignarServiciosSupervisorRequest request,
                string numeroUsuario,
                CancellationToken ct)
        {
            ResponseModel<AsignarServiciosSupervisorResponse> response =
                new ResponseModel<AsignarServiciosSupervisorResponse>();

            if (request == null)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    "El request es obligatorio.";
                response.data = null;

                return response;
            }

            if (request.ServicioId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    "ServicioId es obligatorio.";
                response.data = null;

                return response;
            }

            if (request.Supervisores == null ||
                request.Supervisores.Count == 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    "Debe enviar al menos un supervisor.";
                response.data = null;

                return response;
            }

            if (string.IsNullOrWhiteSpace(
                numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al usuario autenticado.";
                response.data = null;

                return response;
            }

            string? errorRequest =
                ValidarRequestAsignacion(
                    request);

            if (errorRequest != null)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    errorRequest;
                response.data = null;

                return response;
            }

            SqlTransaction? transaction =
                null;

            try
            {
                using var conn =
                    new SqlConnection(
                        _csCerberus);

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                // ================================================
                // SERVICIO
                // ================================================

                const string sqlServicio = @"
SELECT COUNT(1)
FROM dbo.Servicio
    WITH (UPDLOCK, HOLDLOCK)
WHERE ServicioId = @ServicioId;";

                int existeServicio =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlServicio,
                            new
                            {
                                request.ServicioId
                            },
                            transaction,
                            cancellationToken: ct));

                if (existeServicio == 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "No existe el servicio indicado.";
                    response.data = null;

                    return response;
                }

                // ================================================
                // SUPERVISORES
                // ================================================

                int[] supervisoresId =
                    request.Supervisores
                        .Select(x =>
                            x.SupervisorEmpleadoId)
                        .Distinct()
                        .ToArray();

                const string sqlSupervisores = @"
SELECT COUNT(DISTINCT ID)
FROM dbo.DatosGeneralesEmpleado
WHERE ID IN @SupervisoresId;";

                int supervisoresExistentes =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlSupervisores,
                            new
                            {
                                SupervisoresId =
                                    supervisoresId
                            },
                            transaction,
                            cancellationToken: ct));

                if (supervisoresExistentes !=
                    supervisoresId.Length)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "Uno o más supervisores no existen.";
                    response.data = null;

                    return response;
                }

                // ================================================
                // VIGENCIA
                // ================================================

                DateTime fechaInicio =
                    request.Supervisores[0]
                        .FechaInicio.Date;

                DateTime? fechaFin =
                    request.Supervisores[0]
                        .FechaFin?.Date;

                bool diferenteVigencia =
                    request.Supervisores.Any(x =>
                        x.FechaInicio.Date !=
                            fechaInicio ||
                        x.FechaFin?.Date !=
                            fechaFin);

                if (diferenteVigencia)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "Todos los turnos enviados deben tener la misma vigencia.";
                    response.desc =
                        "FechaInicio y FechaFin deben ser iguales para todos los supervisores de la distribución.";
                    response.data = null;

                    return response;
                }

                // ================================================
                // ASIGNACIONES YA EXISTENTES
                //
                // Ya NO se rechazan simplemente por existir.
                // Se cargan para validar el bloque acumulado.
                // ================================================

                const string sqlAsignacionesExistentes = @"
SELECT
    SupervisorEmpleadoId,
    FechaInicio,
    FechaFin,
    HoraEntrada,
    HoraSalida,
    SalidaDiaSiguiente
FROM dbo.ServicioSupervisor
    WITH (UPDLOCK, HOLDLOCK)
WHERE ServicioId = @ServicioId
  AND FechaInicio <=
      ISNULL(
          @FechaFin,
          CONVERT(DATE, '99991231')
      )
  AND
  (
      FechaFin IS NULL
      OR FechaFin >= @FechaInicio
  )
ORDER BY
    FechaInicio,
    HoraEntrada;";

                List<AsignacionServicioSupervisorRequest>
                    asignacionesExistentes =
                    (
                        await conn.QueryAsync<
                            AsignacionServicioSupervisorRequest>(
                            new CommandDefinition(
                                sqlAsignacionesExistentes,
                                new
                                {
                                    request.ServicioId,
                                    FechaInicio =
                                        fechaInicio,
                                    FechaFin =
                                        fechaFin
                                },
                                transaction,
                                cancellationToken: ct))
                    ).ToList();

                // ================================================
                // HORARIOS DEL SERVICIO
                // ================================================

                const string sqlHorarios = @"
SELECT
    DiaSemana,
    HoraInicio,
    HoraFin,
    CruzaDia,
    FechaInicioVigencia,
    FechaFinVigencia
FROM dbo.Servicios_Horarios
WHERE ServicioId = @ServicioId
  AND Estatus = 1
  AND FechaInicioVigencia <=
      ISNULL(
          @FechaFin,
          CONVERT(DATE, '99991231')
      )
  AND
  (
      FechaFinVigencia IS NULL
      OR FechaFinVigencia >=
         @FechaInicio
  )
ORDER BY
    DiaSemana,
    FechaInicioVigencia DESC;";

                List<ServicioHorarioSupervisorDto>
                    horarios =
                    (
                        await conn.QueryAsync<
                            ServicioHorarioSupervisorDto>(
                            new CommandDefinition(
                                sqlHorarios,
                                new
                                {
                                    request.ServicioId,
                                    FechaInicio =
                                        fechaInicio,
                                    FechaFin =
                                        fechaFin
                                },
                                transaction,
                                cancellationToken: ct))
                    ).ToList();

                if (horarios.Count == 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El servicio no tiene horarios operativos configurados.";
                    response.data = null;

                    return response;
                }

                // ================================================
                // FECHAS A VALIDAR
                //
                // Se valida el patrón semanal y también cualquier
                // cambio de vigencia de asignaciones existentes.
                // ================================================

                List<DateTime> fechasValidacion =
                    ObtenerFechasValidacion(
                        fechaInicio,
                        fechaFin,
                        asignacionesExistentes,
                        horarios);

                foreach (DateTime fecha
                    in fechasValidacion)
                {
                    byte diaSemana =
                        ObtenerDiaSemana(
                            fecha);

                    ServicioHorarioSupervisorDto? horario =
                        horarios
                            .Where(x =>
                                x.DiaSemana ==
                                    diaSemana &&
                                x.FechaInicioVigencia.Date <=
                                    fecha.Date &&
                                (
                                    !x.FechaFinVigencia.HasValue ||
                                    x.FechaFinVigencia.Value.Date >=
                                        fecha.Date
                                ))
                            .OrderByDescending(x =>
                                x.FechaInicioVigencia)
                            .FirstOrDefault();

                    if (horario == null)
                    {
                        transaction.Rollback();
                        transaction = null;

                        response.isSuccess = false;
                        response.code = 409;
                        response.message =
                            "La asignación contiene días fuera del horario de operación del servicio.";
                        response.desc =
                            $"El servicio no tiene horario operativo para {fecha:yyyy-MM-dd}.";
                        response.data = null;

                        return response;
                    }

                    List<AsignacionServicioSupervisorRequest>
                        existentesDelDia =
                        asignacionesExistentes
                            .Where(x =>
                                x.FechaInicio.Date <=
                                    fecha.Date &&
                                (
                                    !x.FechaFin.HasValue ||
                                    x.FechaFin.Value.Date >=
                                        fecha.Date
                                ))
                            .ToList();

                    List<AsignacionServicioSupervisorRequest>
                        nuevosDelDia =
                        request.Supervisores
                            .Where(x =>
                                x.FechaInicio.Date <=
                                    fecha.Date &&
                                (
                                    !x.FechaFin.HasValue ||
                                    x.FechaFin.Value.Date >=
                                        fecha.Date
                                ))
                            .ToList();

                    var todosLosTurnos =
                        existentesDelDia
                            .Concat(nuevosDelDia)
                            .ToList();

                    string? errorCobertura =
                        ValidarCoberturaParcialHorario(
                            horario,
                            todosLosTurnos);

                    if (errorCobertura != null)
                    {
                        transaction.Rollback();
                        transaction = null;

                        response.isSuccess = false;
                        response.code = 409;
                        response.message =
                            "La distribución de supervisores no es válida.";
                        response.desc =
                            $"{fecha:yyyy-MM-dd}: {errorCobertura}";
                        response.data = null;

                        return response;
                    }
                }

                // ================================================
                // INSERT
                // ================================================

                const string sqlInsert = @"
INSERT INTO dbo.ServicioSupervisor
(
    ServicioId,
    SupervisorEmpleadoId,
    FechaInicio,
    FechaFin,
    HoraEntrada,
    HoraSalida,
    SalidaDiaSiguiente,
    UsuarioAlta
)
VALUES
(
    @ServicioId,
    @SupervisorEmpleadoId,
    @FechaInicio,
    @FechaFin,
    @HoraEntrada,
    @HoraSalida,
    @SalidaDiaSiguiente,
    @UsuarioAlta
);";

                foreach (
                    AsignacionServicioSupervisorRequest
                    supervisor
                    in request.Supervisores)
                {
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlInsert,
                            new
                            {
                                request.ServicioId,

                                supervisor
                                    .SupervisorEmpleadoId,

                                FechaInicio =
                                    supervisor
                                        .FechaInicio.Date,

                                FechaFin =
                                    supervisor
                                        .FechaFin?.Date,

                                supervisor
                                    .HoraEntrada,

                                supervisor
                                    .HoraSalida,

                                supervisor
                                    .SalidaDiaSiguiente,

                                UsuarioAlta =
                                    numeroUsuario.Trim()
                            },
                            transaction,
                            cancellationToken: ct));
                }

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Supervisores asignados correctamente al servicio.";
                response.desc = null;

                response.data =
                    new AsignarServiciosSupervisorResponse
                    {
                        ServicioId =
                            request.ServicioId,

                        AsignacionesInsertadas =
                            request.Supervisores.Count
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
                    "Error SQL al asignar supervisores al servicio.";
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
                    "Error al asignar supervisores al servicio.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region VALIDACIONES

        private string? ValidarRequestAsignacion(
            AsignarServiciosSupervisorRequest request)
        {
            if (request.Supervisores.Any(x =>
                x.SupervisorEmpleadoId <= 0))
            {
                return
                    "SupervisorEmpleadoId debe ser mayor a cero.";
            }

            if (request.Supervisores
                .GroupBy(x =>
                    x.SupervisorEmpleadoId)
                .Any(x =>
                    x.Count() > 1))
            {
                return
                    "No se puede enviar al mismo supervisor más de una vez en la misma distribución.";
            }

            foreach (
                AsignacionServicioSupervisorRequest
                supervisor
                in request.Supervisores)
            {
                if (supervisor.FechaInicio ==
                    default)
                {
                    return
                        "FechaInicio es obligatoria para todos los supervisores.";
                }

                if (supervisor.FechaFin.HasValue &&
                    supervisor.FechaFin.Value.Date <
                    supervisor.FechaInicio.Date)
                {
                    return
                        "FechaFin no puede ser menor que FechaInicio.";
                }

                long inicio =
                    (long)supervisor
                        .HoraEntrada
                        .TotalSeconds;

                long fin =
                    ObtenerFinSegundos(
                        supervisor.HoraSalida,
                        supervisor.SalidaDiaSiguiente);

                if (fin <= inicio)
                {
                    return
                        "HoraSalida debe ser posterior a HoraEntrada. Para turnos que terminan al día siguiente debe enviarse SalidaDiaSiguiente=true.";
                }

                if (fin - inicio >
                    SEGUNDOS_DIA)
                {
                    return
                        "Un turno de supervisor no puede exceder 24 horas.";
                }
            }

            return null;
        }

        private string?
            ValidarCoberturaParcialHorario(
                ServicioHorarioSupervisorDto horario,
                List<AsignacionServicioSupervisorRequest>
                    supervisores)
        {
            long inicioOperacion =
                (long)horario
                    .HoraInicio
                    .TotalSeconds;

            long finOperacion =
                ObtenerFinSegundos(
                    horario.HoraFin,
                    horario.CruzaDia);

            if (finOperacion <=
                inicioOperacion)
            {
                return
                    "El horario operativo configurado no tiene una duración válida.";
            }

            var intervalos =
                supervisores
                    .Select(x =>
                    {
                        long inicio =
                            (long)x
                                .HoraEntrada
                                .TotalSeconds;

                        long fin =
                            ObtenerFinSegundos(
                                x.HoraSalida,
                                x.SalidaDiaSiguiente);

                        /*
                         * Ejemplo:
                         *
                         * Operación:
                         * 22:00 -> 06:00
                         *
                         * Un turno:
                         * 00:00 -> 06:00
                         *
                         * pertenece a la segunda parte
                         * del periodo operativo.
                         */
                        if (finOperacion >
                            SEGUNDOS_DIA &&
                            inicio <
                            inicioOperacion)
                        {
                            inicio +=
                                SEGUNDOS_DIA;

                            fin +=
                                SEGUNDOS_DIA;
                        }

                        return new
                        {
                            Inicio =
                                inicio,

                            Fin =
                                fin,

                            SupervisorEmpleadoId =
                                x.SupervisorEmpleadoId
                        };
                    })
                    .OrderBy(x =>
                        x.Inicio)
                    .ThenBy(x =>
                        x.Fin)
                    .ToList();

            if (intervalos.Count == 0)
            {
                return
                    "No existen turnos de supervisión.";
            }

            /*
             * La cobertura sí debe iniciar junto
             * con la operación del servicio.
             *
             * Lo que YA NO exigimos es que termine
             * cubriendo toda la operación en el
             * primer registro.
             */
            if (intervalos[0].Inicio !=
                inicioOperacion)
            {
                return
                    "Existe un desfase entre el inicio de operación y el primer turno de supervisión.";
            }

            long cursor =
                inicioOperacion;

            foreach (var intervalo
                in intervalos)
            {
                if (intervalo.Inicio <
                    inicioOperacion ||
                    intervalo.Fin >
                    finOperacion)
                {
                    return
                        "Existe un turno de supervisor fuera del horario de operación del servicio.";
                }

                if (intervalo.Inicio <
                    cursor)
                {
                    return
                        "Existen horarios de supervisores traslapados.";
                }

                if (intervalo.Inicio >
                    cursor)
                {
                    return
                        "Existe un hueco sin supervisor entre dos turnos.";
                }

                cursor =
                    intervalo.Fin;
            }

            /*
             * IMPORTANTE:
             *
             * NO validar:
             *
             * cursor != finOperacion
             *
             * porque eso impediría registrar
             * progresivamente:
             *
             * 00-08
             * 08-16
             * 16-24
             */

            return null;
        }

        private List<DateTime>
            ObtenerFechasValidacion(
                DateTime fechaInicio,
                DateTime? fechaFin,
                List<AsignacionServicioSupervisorRequest>
                    existentes,
                List<ServicioHorarioSupervisorDto>
                    horarios)
        {
            HashSet<DateTime> fechasBase =
                new HashSet<DateTime>
                {
                    fechaInicio.Date
                };

            foreach (var existente
                in existentes)
            {
                if (existente.FechaInicio.Date >=
                    fechaInicio.Date)
                {
                    fechasBase.Add(
                        existente.FechaInicio.Date);
                }

                if (existente.FechaFin.HasValue)
                {
                    DateTime siguiente =
                        existente.FechaFin.Value.Date
                            .AddDays(1);

                    if (!fechaFin.HasValue ||
                        siguiente <=
                        fechaFin.Value.Date)
                    {
                        fechasBase.Add(
                            siguiente);
                    }
                }
            }

            foreach (var horario
                in horarios)
            {
                if (horario
                    .FechaInicioVigencia.Date >=
                    fechaInicio.Date)
                {
                    fechasBase.Add(
                        horario
                            .FechaInicioVigencia
                            .Date);
                }

                if (horario
                    .FechaFinVigencia.HasValue)
                {
                    DateTime siguiente =
                        horario
                            .FechaFinVigencia
                            .Value
                            .Date
                            .AddDays(1);

                    if (!fechaFin.HasValue ||
                        siguiente <=
                        fechaFin.Value.Date)
                    {
                        fechasBase.Add(
                            siguiente);
                    }
                }
            }

            HashSet<DateTime> resultado =
                new HashSet<DateTime>();

            foreach (DateTime baseFecha
                in fechasBase)
            {
                for (int i = 0;
                    i < 7;
                    i++)
                {
                    DateTime fecha =
                        baseFecha.AddDays(i);

                    if (fecha <
                        fechaInicio.Date)
                    {
                        continue;
                    }

                    if (fechaFin.HasValue &&
                        fecha >
                        fechaFin.Value.Date)
                    {
                        break;
                    }

                    resultado.Add(
                        fecha);
                }
            }

            return resultado
                .OrderBy(x => x)
                .ToList();
        }

        private long ObtenerFinSegundos(
            TimeSpan horaFin,
            bool diaSiguiente)
        {
            long segundos =
                (long)horaFin
                    .TotalSeconds;

            if (diaSiguiente)
            {
                segundos +=
                    SEGUNDOS_DIA;
            }

            return segundos;
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

        #endregion
    }
}