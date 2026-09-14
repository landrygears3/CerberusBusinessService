using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Asistencias;
using CerberusBusinessService.Models.DTO.Oficinas;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Asistencias
{
    public class AsistenciaRolFunctions
    {
        #region CONSTANTES

        private const int MINUTOS_TOLERANCIA_RETARDO = 10;

        private const int ESTATUS_EN_TURNO = 1;

        private const int ESTATUS_CANCELADA = 4;

        private const string ORIGEN_SUPERVISOR =
            "SUPERVISOR";

        private const string ORIGEN_OFICINA =
            "OFICINA";

        private const string ROL_OFICINA =
            "OFICINA";

        private const string ROL_SUPERVISOR =
            "SUPERVISOR";

        private const string ROL_SUPERVISOR_OPERATIVO =
            "SUPERVISOROPERATIVO";

        #endregion

        #region PROPIEDADES

        private readonly string _csCerberus;

        private readonly AsistenciasFunctions
            _asistenciasFunctions;

        #endregion

        #region CONSTRUCTOR

        public AsistenciaRolFunctions(
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
                IReadOnlyCollection<string> roles,
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

                DateTime fechaHoraActual =
                    await ObtenerFechaServidorAsync(
                        conn,
                        ct);

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

                bool esSupervisor =
                    TieneRol(
                        roles,
                        ROL_SUPERVISOR)
                    ||
                    TieneRol(
                        roles,
                        ROL_SUPERVISOR_OPERATIVO);

                bool esOficina =
                    TieneRol(
                        roles,
                        ROL_OFICINA);

                List<TurnoCheckInRolDto> turnos =
                    new List<TurnoCheckInRolDto>();

                #region TURNO SUPERVISOR

                if (esSupervisor)
                {
                    TurnoCheckInRolDto? turnoSupervisor =
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
                }

                #endregion

                #region TURNO OFICINA

                if (esOficina)
                {
                    TurnoCheckInRolDto? turnoOficina =
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
                }

                #endregion

                #region RESOLVER TURNO ESPECIAL

                TurnoCheckInRolDto? turnoEspecial =
                    turnos
                        .OrderByDescending(x =>
                            x.EsActivo)
                        .ThenBy(x =>
                            x.EntradaProgramada)
                        .ThenBy(x =>
                            x.TipoOrigen ==
                                ORIGEN_SUPERVISOR
                                ? 0
                                : 1)
                        .FirstOrDefault();

                if (turnoEspecial != null)
                {
                    return await ProcesarCheckInSimpleAsync(
                        conn,
                        data,
                        turnoEspecial,
                        numeroUsuario.Trim(),
                        fechaHoraActual,
                        ct);
                }

                #endregion

                #region FALLBACK EMPLEADO / GUARDIA

                return await _asistenciasFunctions
                    .ProcesarCheckIn(
                        data,
                        numeroUsuario,
                        authorization,
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
                TurnoCheckInRolDto turno,
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

                #region DUPLICADO

                const string sqlDuplicado = @"
SELECT COUNT(1)
FROM dbo.Asistencia
    WITH (UPDLOCK, HOLDLOCK)
WHERE NumeroEmpleadoEntrante =
      @NumeroEmpleadoEntrante

  AND FechaTurno =
      @FechaTurno

  AND FechaHoraEntradaProgramada =
      @FechaHoraEntradaProgramada

  AND FechaHoraSalidaProgramada =
      @FechaHoraSalidaProgramada

  AND Estatus <> @EstatusCancelada;";

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

                                FechaHoraEntradaProgramada =
                                    turno.EntradaProgramada,

                                FechaHoraSalidaProgramada =
                                    turno.SalidaProgramada,

                                EstatusCancelada =
                                    ESTATUS_CANCELADA
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

                #region RETARDO

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

        #region TURNO SUPERVISOR

        private async Task<TurnoCheckInRolDto?>
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

            IEnumerable<
                SupervisorCheckInAsignacionDto> result =
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

            List<TurnoCheckInRolDto> candidatos =
                new List<TurnoCheckInRolDto>();

            foreach (
                SupervisorCheckInAsignacionDto asignacion
                in result)
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
                        new TurnoCheckInRolDto
                        {
                            TipoOrigen =
                                ORIGEN_SUPERVISOR,

                            ServicioId =
                                asignacion.ServicioId,

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
                    x.EntradaProgramada)
                .FirstOrDefault();
        }

        #endregion

        #region TURNO OFICINA

        private async Task<TurnoCheckInRolDto?>
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
                ObtenerDiaSemana(hoy);

            byte diaAyer =
                ObtenerDiaSemana(ayer);

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

            List<TurnoCheckInRolDto> candidatos =
                new List<TurnoCheckInRolDto>();

            foreach (
                ServicioOficinaCheckInDto asignacion
                in asignaciones)
            {
                DateTime fechaTurno;

                if (asignacion.DiaSemana ==
                    diaHoy)
                {
                    fechaTurno =
                        hoy;
                }
                else
                {
                    fechaTurno =
                        ayer;
                }

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
                    new TurnoCheckInRolDto
                    {
                        TipoOrigen =
                            ORIGEN_OFICINA,

                        ServicioId =
                            null,

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
                    x.EntradaProgramada)
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

        #region ROLES

        private bool TieneRol(
            IEnumerable<string> roles,
            string rolBuscado)
        {
            string normalizadoBuscado =
                NormalizarRol(
                    rolBuscado);

            return roles.Any(x =>
                NormalizarRol(x) ==
                normalizadoBuscado);
        }

        private string NormalizarRol(
            string rol)
        {
            if (string.IsNullOrWhiteSpace(
                rol))
            {
                return string.Empty;
            }

            return new string(
                rol
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());
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