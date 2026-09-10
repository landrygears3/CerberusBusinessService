using CerberusBusinessService.Models.DTO.Asistencias;
using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Relevos
{
    public class RelevoIntegracionAsistenciaFunctions
    {
        #region CONSTANTES

        private const int ASISTENCIA_EN_TURNO = 1;
        private const int ASISTENCIA_FINALIZADA = 2;

        #endregion


        #region OBTENER ASISTENCIA ACTIVA

        public async Task<AsistenciaActivaCheckOutDto?>
            ObtenerAsistenciaActivaPorServicioEmpleadoForUpdateAsync(
                SqlConnection conn,
                SqlTransaction transaction,
                long servicioEmpleadoId,
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
WHERE ServicioEmpleadoId = @ServicioEmpleadoId
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
                        ServicioEmpleadoId =
                            servicioEmpleadoId,

                        EstatusEnTurno =
                            ASISTENCIA_EN_TURNO
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CHECKOUT POR SUPERVISION

        public async Task FinalizarAsistenciaPorSupervisionAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            long asistenciaId,
            DateTime fechaHoraCheckOut,
            CancellationToken ct)
        {
            const string sql = @"
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
                        sql,
                        new
                        {
                            FechaHoraCheckOut =
                                fechaHoraCheckOut,

                            EstatusFinalizada =
                                ASISTENCIA_FINALIZADA,

                            AsistenciaId =
                                asistenciaId,

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
        }

        #endregion


        #region CHECKOUT SIN RELEVO

        public async Task RealizarCheckOutSinRelevoAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            long asistenciaId,
            long servicioEmpleadoSalienteId,
            string numeroEmpleado,
            DateTime fechaHoraCheckOut,
            CancellationToken ct)
        {
            const string sql = @"
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
                        sql,
                        new
                        {
                            FechaHoraCheckOut =
                                fechaHoraCheckOut,

                            EstatusFinalizada =
                                ASISTENCIA_FINALIZADA,

                            AsistenciaId =
                                asistenciaId,

                            ServicioEmpleadoSalienteId =
                                servicioEmpleadoSalienteId,

                            NumeroEmpleado =
                                numeroEmpleado.Trim(),

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

        #endregion


        #region CREAR SERVICIO EMPLEADO TEMPORAL

        public async Task<long> CrearServicioEmpleadoTemporalAsync(
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

        public async Task<long> CrearAsistenciaExtensionAsync(
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

            // ========================================================
            // CERRAR ASISTENCIA ORIGINAL
            // ========================================================

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

            // ========================================================
            // CREAR ASISTENCIA DE EXTENSION
            //
            // CONSERVA LA GEOLOCALIZACION DEL TURNO ORIGINAL.
            // ========================================================

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


        #region CHECKOUT POR RECHAZO DE EXTENSION

        public async Task CerrarAsistenciaPorRechazoExtensionAsync(
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


        #region INCIDENCIA FALTA DE RELEVO

        public async Task<long?> RegistrarIncidenciaFaltaRelevoAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            ServicioEmpleadoRelevoDto asignacionAfectada,
            DateTime fechaIncidencia,
            string usuarioRegistro,
            DateTime fechaRegistro,
            CancellationToken ct)
        {
            // ========================================================
            // TIPO DE INCIDENCIA
            // ========================================================

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

            // ========================================================
            // NUMERO DE EMPLEADO AFECTADO
            // ========================================================

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

            // ========================================================
            // EVITAR INCIDENCIA DUPLICADA
            //
            // UNA SOLA AFECTACION POR ASIGNACION Y FECHA.
            // ========================================================

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

                            FechaIncidencia =
                                fechaIncidencia
                        },
                        transaction,
                        cancellationToken: ct));

            if (incidenciaExistente.HasValue)
            {
                return incidenciaExistente.Value;
            }

            // ========================================================
            // INSERTAR INCIDENCIA
            // ========================================================

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

                        NumeroUsuario =
                            numeroEmpleado.Trim(),

                        asignacionAfectada.ServicioId,

                        AsignacionTurnoId =
                            asignacionAfectada.ServicioEmpleadoId,

                        FechaIncidencia =
                            fechaIncidencia,

                        Descripcion =
                            "Falta al turno. El empleado no se presentó para realizar el relevo programado.",

                        tipoFalta.AfectaNomina,
                        tipoFalta.TipoAfectacionNomina,
                        tipoFalta.MontoAfectacion,

                        UsuarioRegistro =
                            usuarioRegistro,

                        FechaRegistro =
                            fechaRegistro
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion
    }
}