using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Asistencias
{
    public static class ExclusividadOficinaFunctions
    {
        #region BLOQUEO EXCLUSIVIDAD

        private static async Task TomarBloqueoAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            string usuarioAsignado,
            CancellationToken ct)
        {
            const string sql = @"
DECLARE @Resultado INT;

EXEC @Resultado = sys.sp_getapplock
    @Resource = @Recurso,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 15000;

SELECT @Resultado;";

            string recurso =
                "Cerberus:ExclusividadOficina:" +
                usuarioAsignado.Trim().ToUpperInvariant();

            int resultado =
                await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            Recurso = recurso
                        },
                        transaction,
                        cancellationToken: ct));

            if (resultado < 0)
            {
                throw new InvalidOperationException(
                    "No fue posible obtener el bloqueo de exclusividad del empleado.");
            }
        }

        #endregion

        #region VALIDAR ASIGNACION OPERATIVA

        public static async Task<string?>
            ValidarAsignacionOperativaAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                int empleadoId,
                DateTime fechaInicio,
                DateTime? fechaFin,
                CancellationToken ct)
        {
            if (empleadoId <= 0 ||
                fechaInicio == default ||
                (fechaFin.HasValue &&
                 fechaFin.Value.Date < fechaInicio.Date))
            {
                return "Los datos de la asignación son inválidos.";
            }

            const string sqlEmpleado = @"
SELECT TOP (1)
    LTRIM(RTRIM(UsuarioAsignado))
FROM dbo.DatosGeneralesEmpleado
WHERE ID = @EmpleadoId;";

            string? usuarioAsignado =
                await conn.ExecuteScalarAsync<string?>(
                    new CommandDefinition(
                        sqlEmpleado,
                        new
                        {
                            EmpleadoId = empleadoId
                        },
                        transaction,
                        cancellationToken: ct));

            if (string.IsNullOrWhiteSpace(usuarioAsignado) ||
                usuarioAsignado.Length > 50)
            {
                return
                    "El empleado no existe o no tiene un número de empleado válido.";
            }

            await TomarBloqueoAsync(
                conn,
                transaction,
                usuarioAsignado,
                ct);

            const string sql = @"
SELECT TOP (1)
    HorarioOficinaId
FROM dbo.HorariosOficina
    WITH (UPDLOCK, HOLDLOCK)
WHERE UsuarioAsignado = @UsuarioAsignado
  AND Estatus = 1
  AND FechaInicioVigencia <=
      COALESCE(
          @FechaFin,
          CONVERT(DATE, '99991231', 112)
      )
  AND
  (
      FechaFinVigencia IS NULL
      OR FechaFinVigencia >= @FechaInicio
  );";

            long? horarioOficinaId =
                await conn.ExecuteScalarAsync<long?>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            UsuarioAsignado =
                                usuarioAsignado,

                            FechaInicio =
                                fechaInicio.Date,

                            FechaFin =
                                fechaFin?.Date
                        },
                        transaction,
                        cancellationToken: ct));

            if (horarioOficinaId.HasValue)
            {
                return
                    $"El empleado {usuarioAsignado} tiene " +
                    "un horario de oficina vigente " +
                    "que coincide con la asignación solicitada.";
            }

            return null;
        }

        #endregion

        #region VALIDAR ALTA HORARIO OFICINA

        public static async Task<string?>
            ValidarAltaHorarioOficinaAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                string usuarioAsignado,
                byte diaSemana,
                DateTime fechaInicio,
                DateTime? fechaFin,
                CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(usuarioAsignado) ||
                usuarioAsignado.Trim().Length > 50 ||
                diaSemana < 1 ||
                diaSemana > 7 ||
                fechaInicio == default ||
                (fechaFin.HasValue &&
                 fechaFin.Value.Date < fechaInicio.Date))
            {
                return
                    "Los datos del horario de oficina son inválidos.";
            }

            const string sqlEmpleado = @"
SELECT
    ID,
    LTRIM(RTRIM(UsuarioAsignado)) AS UsuarioAsignado
FROM dbo.DatosGeneralesEmpleado
WHERE LTRIM(RTRIM(UsuarioAsignado)) = @UsuarioAsignado;";

            List<EmpleadoExclusividadDto> empleados =
                (
                    await conn.QueryAsync<EmpleadoExclusividadDto>(
                        new CommandDefinition(
                            sqlEmpleado,
                            new
                            {
                                UsuarioAsignado =
                                    usuarioAsignado.Trim()
                            },
                            transaction,
                            cancellationToken: ct))
                ).ToList();

            if (empleados.Count != 1)
            {
                return
                    "No existe un único empleado con el número indicado.";
            }

            EmpleadoExclusividadDto empleado =
                empleados[0];

            await TomarBloqueoAsync(
                conn,
                transaction,
                empleado.UsuarioAsignado,
                ct);

            const string sqlOperativo = @"
SELECT CASE
    WHEN EXISTS
    (
        SELECT 1
        FROM dbo.ServicioEmpleado
            WITH (UPDLOCK, HOLDLOCK)
        WHERE EmpleadoId = @EmpleadoId
          AND FechaInicio <=
              COALESCE(
                  @FechaFin,
                  CONVERT(DATE, '99991231', 112)
              )
          AND
          (
              FechaFin IS NULL
              OR FechaFin >= @FechaInicio
          )
    )
    OR EXISTS
    (
        SELECT 1
        FROM dbo.ServicioSupervisor
            WITH (UPDLOCK, HOLDLOCK)
        WHERE SupervisorEmpleadoId = @EmpleadoId
          AND FechaInicio <=
              COALESCE(
                  @FechaFin,
                  CONVERT(DATE, '99991231', 112)
              )
          AND
          (
              FechaFin IS NULL
              OR FechaFin >= @FechaInicio
          )
    )
    THEN 1
    ELSE 0
END;";

            int tieneAsignacionOperativa =
                await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sqlOperativo,
                        new
                        {
                            EmpleadoId = empleado.ID,

                            FechaInicio =
                                fechaInicio.Date,

                            FechaFin =
                                fechaFin?.Date
                        },
                        transaction,
                        cancellationToken: ct));

            if (tieneAsignacionOperativa == 1)
            {
                return
                    $"El empleado {empleado.UsuarioAsignado} " +
                    "tiene una asignación vigente como guardia " +
                    "o supervisor que coincide con el horario solicitado.";
            }

            const string sqlHorarioExistente = @"
SELECT TOP (1)
    HorarioOficinaId
FROM dbo.HorariosOficina
    WITH (UPDLOCK, HOLDLOCK)
WHERE UsuarioAsignado = @UsuarioAsignado
  AND DiaSemana = @DiaSemana
  AND Estatus = 1
  AND FechaInicioVigencia <=
      COALESCE(
          @FechaFin,
          CONVERT(DATE, '99991231', 112)
      )
  AND
  (
      FechaFinVigencia IS NULL
      OR FechaFinVigencia >= @FechaInicio
  );";

            long? horarioExistente =
                await conn.ExecuteScalarAsync<long?>(
                    new CommandDefinition(
                        sqlHorarioExistente,
                        new
                        {
                            UsuarioAsignado =
                                empleado.UsuarioAsignado,

                            DiaSemana = diaSemana,

                            FechaInicio =
                                fechaInicio.Date,

                            FechaFin =
                                fechaFin?.Date
                        },
                        transaction,
                        cancellationToken: ct));

            if (horarioExistente.HasValue)
            {
                return
                    "El empleado ya tiene un horario activo " +
                    "para el mismo día de la semana con " +
                    "fechas de vigencia superpuestas.";
            }

            return null;
        }

        #endregion

        #region DTO INTERNO

        private sealed class EmpleadoExclusividadDto
        {
            public int ID { get; set; }

            public string UsuarioAsignado { get; set; } =
                string.Empty;
        }

        #endregion
    }
}