using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Asistencias;
using CerberusBusinessService.Models.DTO.Oficinas;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Asistencias
{
    public class AsistenciaCheckInFunctions
    {
        #region CONSTANTES

        private const int MINUTOS_TOLERANCIA_RETARDO = 10;

        private const int ESTATUS_EN_TURNO = 1;

        private const int ESTATUS_CANCELADA = 4;

        private const string ORIGEN_EMPLEADO =
            "EMPLEADO";

        private const string ORIGEN_SUPERVISOR =
            "SUPERVISOR";

        private const string ORIGEN_OFICINA =
            "OFICINA";

        #endregion

        #region PROPIEDADES

        private readonly string _csCerberus;

        private readonly AsistenciasFunctions
            _asistenciasFunctions;

        #endregion

        #region CONSTRUCTOR

        public AsistenciaCheckInFunctions(
            IConfiguration config,
            AsistenciasFunctions asistenciasFunctions)
        {
            _csCerberus =
                config.GetConnectionString(
                    "DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");

            _asistenciasFunctions =
                asistenciasFunctions;
        }

        #endregion

        #region PROCESAR CHECK-IN

        public async Task<ResponseModel<CheckInResponse>>
            ProcesarCheckInAsync(
                CheckInRequest data,
                string numeroUsuario,
                string authorization,
                CancellationToken ct)
        {
            ResponseModel<CheckInResponse> response =
                new ResponseModel<CheckInResponse>();

            #region VALIDACIONES

            if (string.IsNullOrWhiteSpace(
                numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al empleado.";
                response.desc = null;
                response.data = null;

                return response;
            }

            if (data == null)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    "El request es obligatorio.";
                response.desc = null;
                response.data = null;

                return response;
            }

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
                response.desc = null;
                response.data = null;

                return response;
            }

            #endregion

            try
            {
                using var conn =
                    new SqlConnection(
                        _csCerberus);

                await conn.OpenAsync(ct);

                #region FECHA SERVIDOR

                DateTime fechaHoraActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        ct);

                #endregion

                #region EMPLEADO

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
                    response.desc = null;
                    response.data = null;

                    return response;
                }

                #endregion

                #region OBTENER TURNOS CANDIDATOS

                List<TurnoCheckInDto> turnos =
                    new List<TurnoCheckInDto>();

                TurnoCheckInDto? turnoSupervisor =
                    await ObtenerTurnoSupervisorAsync(
                        conn,
                        empleado.EmpleadoId,
                        fechaHoraActual,
                        ct);

                if (turnoSupervisor != null)
                {
                    turnos.Add(
                        turnoSupervisor);
                }

                TurnoCheckInDto? turnoOficina =
                    await ObtenerTurnoOficinaAsync(
                        conn,
                        empleado.EmpleadoId,
                        fechaHoraActual,
                        ct);

                if (turnoOficina != null)
                {
                    turnos.Add(
                        turnoOficina);
                }

                TurnoCheckInDto? turnoEmpleado =
                    await ObtenerTurnoEmpleadoAsync(
                        conn,
                        empleado.EmpleadoId,
                        fechaHoraActual,
                        ct);

                if (turnoEmpleado != null)
                {
                    turnos.Add(
                        turnoEmpleado);
                }

                #endregion

                #region SIN TURNO

                if (turnos.Count == 0)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El empleado no tiene un turno disponible para realizar Check-In.";
                    response.desc = null;
                    response.data = null;

                    return response;
                }

                #endregion

                #region SELECCIONAR TURNO

                var candidatosOrdenados =
                    turnos
                        .Select(x => new
                        {
                            Turno = x,

                            DistanciaSegundos =
                                Math.Abs(
                                    (
                                        fechaHoraActual -
                                        x.EntradaProgramada
                                    ).TotalSeconds)
                        })
                        .OrderByDescending(x =>
                            x.Turno.EsActivo)
                        .ThenBy(x =>
                            x.DistanciaSegundos)
                        .ToList();

                var primero =
                    candidatosOrdenados[0];

                if (candidatosOrdenados.Count > 1)
                {
                    var segundo =
                        candidatosOrdenados[1];

                    bool mismaPrioridad =
                        primero.Turno.EsActivo ==
                        segundo.Turno.EsActivo;

                    bool mismaDistancia =
                        Math.Abs(
                            primero.DistanciaSegundos -
                            segundo.DistanciaSegundos
                        ) < 1;

                    bool distintoOrigen =
                        !string.Equals(
                            primero.Turno.TipoOrigen,
                            segundo.Turno.TipoOrigen,
                            StringComparison.OrdinalIgnoreCase);

                    if (mismaPrioridad &&
                        mismaDistancia &&
                        distintoOrigen)
                    {
                        response.isSuccess = false;
                        response.code = 409;
                        response.message =
                            "El empleado tiene más de una asignación compatible con el horario actual.";
                        response.desc =
                            "No es posible determinar automáticamente qué asignación debe utilizarse para el Check-In.";
                        response.data = null;

                        return response;
                    }
                }

                TurnoCheckInDto turnoSeleccionado =
                    primero.Turno;

                #endregion

                #region DISPATCHER

                if (string.Equals(
                    turnoSeleccionado.TipoOrigen,
                    ORIGEN_EMPLEADO,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return await _asistenciasFunctions
                        .ProcesarCheckIn(
                            data,
                            numeroUsuario,
                            authorization,
                            ct);
                }

                return await ProcesarCheckInSimpleAsync(
                    conn,
                    data,
                    turnoSeleccionado,
                    numeroUsuario.Trim(),
                    fechaHoraActual,
                    ct);

                #endregion
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

        #region CHECK-IN SIMPLE

        private async Task<ResponseModel<CheckInResponse>>
            ProcesarCheckInSimpleAsync(
                SqlConnection conn,
                CheckInRequest data,
                TurnoCheckInDto turno,
                string numeroUsuario,
                DateTime fechaHoraActual,
                CancellationToken ct)
        {
            ResponseModel<CheckInResponse> response =
                new ResponseModel<CheckInResponse>();

            SqlTransaction? transaction = null;

            var controlHora =
                CalcularHoraCheckIn(
                    turno.EntradaProgramada,
                    fechaHoraActual);

            try
            {
                transaction =
                    conn.BeginTransaction();

                #region VALIDAR DUPLICADO

                const string sqlDuplicado = @"
SELECT COUNT(1)
FROM dbo.Asistencia
    WITH (UPDLOCK, HOLDLOCK)
WHERE NumeroEmpleadoEntrante =
      @NumeroEmpleadoEntrante

  AND FechaTurno =
      @FechaTurno

  AND Estatus <>
      @EstatusCancelada

  AND
  (
      (
          @ServicioSupervisorId IS NOT NULL
          AND ServicioSupervisorId =
              @ServicioSupervisorId
      )
      OR
      (
          @ServicioOficinaEmpleadoId IS NOT NULL
          AND ServicioOficinaEmpleadoId =
              @ServicioOficinaEmpleadoId
      )
  );";

                int duplicado =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlDuplicado,
                            new
                            {
                                NumeroEmpleadoEntrante =
                                    numeroUsuario,

                                FechaTurno =
                                    turno.FechaTurno.Date,

                                EstatusCancelada =
                                    ESTATUS_CANCELADA,

                                turno.ServicioSupervisorId,

                                turno.ServicioOficinaEmpleadoId
                            },
                            transaction,
                            cancellationToken: ct));

                if (duplicado > 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "Ya existe una asistencia para este turno.";
                    response.desc = null;
                    response.data = null;

                    return response;
                }

                #endregion

                #region INSERT ASISTENCIA

                const string sqlInsert = @"
INSERT INTO dbo.Asistencia
(
    ServicioId,
    ServicioEmpleadoId,
    ServicioSupervisorId,
    ServicioOficinaEmpleadoId,
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
    NULL,
    @ServicioSupervisorId,
    @ServicioOficinaEmpleadoId,
    @NumeroEmpleadoEntrante,
    NULL,
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

                long asistenciaId =
                    await conn.ExecuteScalarAsync<long>(
                        new CommandDefinition(
                            sqlInsert,
                            new
                            {
                                turno.ServicioId,

                                turno.ServicioSupervisorId,

                                turno.ServicioOficinaEmpleadoId,

                                NumeroEmpleadoEntrante =
                                    numeroUsuario,

                                FechaTurno =
                                    turno.FechaTurno.Date,

                                FechaHoraEntradaProgramada =
                                    turno.EntradaProgramada,

                                FechaHoraSalidaProgramada =
                                    turno.SalidaProgramada,

                                FechaHoraCheckIn =
                                    controlHora.FechaHoraCheckIn,

                                EsRetardo =
                                    controlHora.EsRetardo,

                                MinutosRetardo =
                                    controlHora.MinutosRetardo,

                                Latitud =
                                    data.Latitud!.Value,

                                Longitud =
                                    data.Longitud!.Value,

                                Estatus =
                                    ESTATUS_EN_TURNO,

                                FechaRegistro =
                                    fechaHoraActual,

                                UsuarioRegistro =
                                    numeroUsuario
                            },
                            transaction,
                            cancellationToken: ct));

                #endregion

                #region INCIDENCIA RETARDO

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
                            turno.ServicioId,
                            numeroUsuario,
                            fechaHoraActual,
                            controlHora
                                .MinutosRetardo
                                .Value,
                            ct);
                }

                #endregion

                #region COMMIT

                transaction.Commit();
                transaction = null;

                #endregion

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Check-In registrado correctamente.";

                response.desc =
                    turno.TipoOrigen ==
                    ORIGEN_OFICINA
                        ? "Check-In de oficina registrado correctamente."
                        : "Check-In de supervisor registrado correctamente.";

                response.data =
                    new CheckInResponse
                    {
                        AsistenciaId =
                            asistenciaId,

                        ServicioEmpleadoId =
                            null,

                        ServicioSupervisorId =
                            turno.ServicioSupervisorId,

                        ServicioOficinaEmpleadoId =
                            turno.ServicioOficinaEmpleadoId,

                        ServicioId =
                            turno.ServicioId,

                        NumeroEmpleadoEntrante =
                            numeroUsuario,

                        NumeroEmpleadoSaliente =
                            null,

                        FechaTurno =
                            turno.FechaTurno.Date,

                        FechaHoraEntradaProgramada =
                            turno.EntradaProgramada,

                        FechaHoraSalidaProgramada =
                            turno.SalidaProgramada,

                        FechaHoraCheckIn =
                            controlHora.FechaHoraCheckIn,

                        EsRetardo =
                            controlHora.EsRetardo,

                        MinutosRetardo =
                            controlHora.MinutosRetardo,

                        IncidenciaRetardoId =
                            incidenciaRetardoId,

                        Estatus =
                            ESTATUS_EN_TURNO,

                        EstatusDescripcion =
                            "En turno"
                    };

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

        #region TURNO EMPLEADO

        private async Task<TurnoCheckInDto?>
            ObtenerTurnoEmpleadoAsync(
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
                await conn.QueryAsync<
                    ServicioEmpleadoCheckInDto>(
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

            #region TURNO ACTIVO

            var activos =
                new List<(
                    ServicioEmpleadoCheckInDto Asignacion,
                    DateTime FechaTurno,
                    DateTime Entrada,
                    DateTime Salida)>();

            foreach (
                ServicioEmpleadoCheckInDto asignacion
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
                        .OrderByDescending(x =>
                            x.Entrada)
                        .First();

                return new TurnoCheckInDto
                {
                    TipoOrigen =
                        ORIGEN_EMPLEADO,

                    ServicioId =
                        turnoActivo
                            .Asignacion
                            .ServicioId,

                    ServicioEmpleadoId =
                        turnoActivo
                            .Asignacion
                            .ServicioEmpleadoId,

                    FechaTurno =
                        turnoActivo.FechaTurno,

                    EntradaProgramada =
                        turnoActivo.Entrada,

                    SalidaProgramada =
                        turnoActivo.Salida,

                    EsActivo =
                        true
                };
            }

            #endregion

            #region PROXIMO TURNO

            var proximos =
                new List<(
                    ServicioEmpleadoCheckInDto Asignacion,
                    DateTime FechaTurno,
                    DateTime Entrada,
                    DateTime Salida)>();

            foreach (
                ServicioEmpleadoCheckInDto asignacion
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
                    .OrderBy(x =>
                        x.Entrada)
                    .First();

            return new TurnoCheckInDto
            {
                TipoOrigen =
                    ORIGEN_EMPLEADO,

                ServicioId =
                    proximo.Asignacion.ServicioId,

                ServicioEmpleadoId =
                    proximo.Asignacion.ServicioEmpleadoId,

                FechaTurno =
                    proximo.FechaTurno,

                EntradaProgramada =
                    proximo.Entrada,

                SalidaProgramada =
                    proximo.Salida,

                EsActivo =
                    false
            };

            #endregion
        }

        #endregion

        #region TURNO SUPERVISOR

        private async Task<TurnoCheckInDto?>
            ObtenerTurnoSupervisorAsync(
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
    ServicioSupervisorId,
    ServicioId,
    SupervisorEmpleadoId,
    FechaInicio,
    FechaFin,
    HoraEntrada,
    HoraSalida,
    SalidaDiaSiguiente
FROM dbo.ServicioSupervisor
WHERE SupervisorEmpleadoId =
      @EmpleadoId
  AND FechaInicio <= @Hoy
  AND
  (
      FechaFin IS NULL
      OR FechaFin >= @Ayer
  )
ORDER BY
    FechaInicio DESC,
    ServicioSupervisorId DESC;";

            IEnumerable<SupervisorCheckInAsignacionDto> result =
                await conn.QueryAsync<
                    SupervisorCheckInAsignacionDto>(
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

            List<SupervisorCheckInAsignacionDto> asignaciones =
                result.ToList();

            var candidatos =
                new List<TurnoCheckInDto>();

            foreach (
                SupervisorCheckInAsignacionDto asignacion
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
                    if (fecha.Date <
                        asignacion.FechaInicio.Date)
                    {
                        continue;
                    }

                    if (asignacion.FechaFin.HasValue &&
                        fecha.Date >
                        asignacion.FechaFin.Value.Date)
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

                    bool activo =
                        fechaHoraActual >= entrada &&
                        fechaHoraActual <= salida;

                    bool proximoHoy =
                        fecha.Date == hoy &&
                        entrada > fechaHoraActual;

                    if (!activo &&
                        !proximoHoy)
                    {
                        continue;
                    }

                    candidatos.Add(
                        new TurnoCheckInDto
                        {
                            TipoOrigen =
                                ORIGEN_SUPERVISOR,

                            ServicioId =
                                asignacion.ServicioId,

                            ServicioSupervisorId =
                                asignacion
                                    .ServicioSupervisorId,

                            FechaTurno =
                                fecha.Date,

                            EntradaProgramada =
                                entrada,

                            SalidaProgramada =
                                salida,

                            EsActivo =
                                activo
                        });
                }
            }

            return candidatos
                .OrderByDescending(x =>
                    x.EsActivo)
                .ThenBy(x =>
                    Math.Abs(
                        (
                            fechaHoraActual -
                            x.EntradaProgramada
                        ).TotalSeconds))
                .FirstOrDefault();
        }

        #endregion

        #region TURNO OFICINA

        private async Task<TurnoCheckInDto?>
            ObtenerTurnoOficinaAsync(
                SqlConnection conn,
                int empleadoId,
                DateTime fechaHoraActual,
                CancellationToken ct)
        {
            DateTime hoy =
                fechaHoraActual.Date;

            DateTime ayer =
                hoy.AddDays(-1);

            byte diaHoy =
                ObtenerDiaSemana(
                    hoy);

            byte diaAyer =
                ObtenerDiaSemana(
                    ayer);

            const string sql = @"
SELECT
    SOE.ServicioOficinaEmpleadoId,
    SO.ServicioOficinaId,
    SO.OficinaId,
    SOE.EmpleadoId,
    SOH.DiaSemana,
    SOH.HoraEntrada,
    SOH.HoraSalida,
    SOH.SalidaDiaSiguiente

FROM dbo.ServicioOficinaEmpleado SOE

INNER JOIN dbo.ServicioOficina SO
    ON SO.ServicioOficinaId =
       SOE.ServicioOficinaId

INNER JOIN dbo.Oficinas O
    ON O.OficinaId =
       SO.OficinaId

INNER JOIN dbo.ServicioOficinaHorario SOH
    ON SOH.ServicioOficinaId =
       SO.ServicioOficinaId

WHERE SOE.EmpleadoId =
      @EmpleadoId

  AND SOE.Estatus = 1
  AND SO.Estatus = 1
  AND O.Estatus = 1
  AND SOH.Estatus = 1

  AND SOH.DiaSemana IN
  (
      @DiaHoy,
      @DiaAyer
  );";

            IEnumerable<ServicioOficinaCheckInDto>
                asignaciones =
                await conn.QueryAsync<
                    ServicioOficinaCheckInDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            EmpleadoId =
                                empleadoId,

                            DiaHoy =
                                diaHoy,

                            DiaAyer =
                                diaAyer
                        },
                        cancellationToken: ct));

            List<TurnoCheckInDto> candidatos =
                new List<TurnoCheckInDto>();

            foreach (
                ServicioOficinaCheckInDto asignacion
                in asignaciones)
            {
                DateTime fechaTurno =
                    asignacion.DiaSemana == diaHoy
                        ? hoy
                        : ayer;

                DateTime entrada =
                    fechaTurno.Add(
                        asignacion.HoraEntrada);

                DateTime salida =
                    fechaTurno.Add(
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

                bool activo =
                    fechaHoraActual >= entrada &&
                    fechaHoraActual <= salida;

                bool proximoHoy =
                    fechaTurno == hoy &&
                    entrada > fechaHoraActual;

                if (!activo &&
                    !proximoHoy)
                {
                    continue;
                }

                candidatos.Add(
                    new TurnoCheckInDto
                    {
                        TipoOrigen =
                            ORIGEN_OFICINA,

                        ServicioId =
                            null,

                        ServicioOficinaEmpleadoId =
                            asignacion
                                .ServicioOficinaEmpleadoId,

                        FechaTurno =
                            fechaTurno,

                        EntradaProgramada =
                            entrada,

                        SalidaProgramada =
                            salida,

                        EsActivo =
                            activo
                    });
            }

            return candidatos
                .OrderByDescending(x =>
                    x.EsActivo)
                .ThenBy(x =>
                    Math.Abs(
                        (
                            fechaHoraActual -
                            x.EntradaProgramada
                        ).TotalSeconds))
                .FirstOrDefault();
        }

        #endregion

        #region EMPLEADO

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
WHERE LTRIM(RTRIM(UsuarioAsignado)) =
      @NumeroUsuario;";

            return await conn
                .QueryFirstOrDefaultAsync<
                    EmpleadoAsistenciaDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            NumeroUsuario =
                                numeroUsuario.Trim()
                        },
                        cancellationToken: ct));
        }

        #endregion

        #region INCIDENCIA RETARDO

        private async Task<long>
            RegistrarIncidenciaRetardoAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                long asistenciaId,
                int? servicioId,
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

            TipoIncidenciaDto? tipo =
                await conn
                    .QueryFirstOrDefaultAsync<
                        TipoIncidenciaDto>(
                        new CommandDefinition(
                            sqlTipo,
                            transaction:
                                transaction,
                            cancellationToken:
                                ct));

            if (tipo == null)
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
                        tipo.TipoIncidenciaId,

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

                        tipo.AfectaNomina,

                        tipo.TipoAfectacionNomina,

                        tipo.MontoAfectacion,

                        UsuarioRegistro =
                            numeroUsuario,

                        FechaRegistro =
                            fechaHoraActual
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion

        #region GEOLOCALIZACION

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

        #endregion

        #region FECHA Y HORARIO

        private async Task<DateTime>
            ObtenerFechaServidorAsync(
                SqlConnection conn,
                CancellationToken ct)
        {
            return await conn
                .ExecuteScalarAsync<DateTime>(
                    new CommandDefinition(
                        "SELECT SYSDATETIME();",
                        cancellationToken: ct));
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

        #endregion
    }
}