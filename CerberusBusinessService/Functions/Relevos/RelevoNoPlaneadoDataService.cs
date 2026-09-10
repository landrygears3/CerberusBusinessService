using CerberusBusinessService.Models.DTO.Relevos;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Relevos
{
    public class RelevoNoPlaneadoDataService
    {
        #region PROPIEDADES

        private readonly string _connectionString;

        #endregion


        #region CONSTRUCTOR

        public RelevoNoPlaneadoDataService(
            IConfiguration config)
        {
            _connectionString =
                config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");
        }

        #endregion


        #region CONEXION

        public SqlConnection CrearConexion()
        {
            return new SqlConnection(_connectionString);
        }

        #endregion


        #region FECHA SERVIDOR

        public async Task<DateTime> ObtenerFechaServidorAsync(
            SqlConnection conn,
            CancellationToken ct,
            SqlTransaction? transaction = null)
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


        #region SERVICIO EMPLEADO

        public async Task<ServicioEmpleadoRelevoDto?>
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
                        ServicioEmpleadoId =
                            servicioEmpleadoId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region EMPLEADOS

        public async Task<EmpleadoRelevoDto?>
            ObtenerEmpleadoAsync(
                SqlConnection conn,
                int empleadoId,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
                        EmpleadoId =
                            empleadoId
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<EmpleadoRelevoDto?>
            ObtenerEmpleadoPorNumeroUsuarioAsync(
                SqlConnection conn,
                string numeroUsuario,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
WHERE LTRIM(RTRIM(D.UsuarioAsignado)) =
      @NumeroUsuario;";

            return await conn.QueryFirstOrDefaultAsync<
                EmpleadoRelevoDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        NumeroUsuario =
                            numeroUsuario.Trim()
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region SOLICITUD

        public async Task<SolicitudRelevoNoPlaneadoDto?>
            ObtenerSolicitudAsync(
                SqlConnection conn,
                long solicitudId,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
FROM dbo.SolicitudRelevoNoPlaneado
WHERE SolicitudRelevoNoPlaneadoId =
      @SolicitudId;";

            return await conn.QueryFirstOrDefaultAsync<
                SolicitudRelevoNoPlaneadoDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        SolicitudId =
                            solicitudId
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<SolicitudRelevoNoPlaneadoDto?>
            ObtenerSolicitudForUpdateAsync(
                SqlConnection conn,
                SqlTransaction transaction,
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
FROM dbo.SolicitudRelevoNoPlaneado
    WITH (UPDLOCK, HOLDLOCK)
WHERE SolicitudRelevoNoPlaneadoId =
      @SolicitudId;";

            return await conn.QueryFirstOrDefaultAsync<
                SolicitudRelevoNoPlaneadoDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        SolicitudId =
                            solicitudId
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<long?>
            ObtenerSolicitudIdPorAsignacionAsync(
                SqlConnection conn,
                long relevoNoPlaneadoAsignacionId,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<bool>
            ExisteSolicitudActivaAsync(
                SqlConnection conn,
                long servicioEmpleadoAfectadoId,
                CancellationToken ct,
                SqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT
    CASE
        WHEN EXISTS
        (
            SELECT 1
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
              AND SR.FechaHoraFinCobertura >
                  SYSDATETIME()
        )
        THEN CAST(1 AS BIT)
        ELSE CAST(0 AS BIT)
    END;";

            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ServicioEmpleadoAfectadoId =
                            servicioEmpleadoAfectadoId
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<bool>
            ExisteCoberturaGeneradaAsync(
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
            WHERE A.SolicitudRelevoNoPlaneadoId =
                  @SolicitudId
              AND
              (
                  E.Clave = 'ACEPTADA'
                  OR A.ServicioEmpleadoTemporalId
                     IS NOT NULL
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
                        SolicitudId =
                            solicitudId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region ASIGNACION RELEVO

        public async Task<RelevoNoPlaneadoAsignacionDto?>
            ObtenerAsignacionAsync(
                SqlConnection conn,
                long asignacionId,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
FROM dbo.RelevoNoPlaneadoAsignacion
WHERE RelevoNoPlaneadoAsignacionId =
      @AsignacionId;";

            return await conn.QueryFirstOrDefaultAsync<
                RelevoNoPlaneadoAsignacionDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        AsignacionId =
                            asignacionId
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<RelevoNoPlaneadoAsignacionDto?>
            ObtenerAsignacionForUpdateAsync(
                SqlConnection conn,
                SqlTransaction transaction,
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
FROM dbo.RelevoNoPlaneadoAsignacion
    WITH (UPDLOCK, HOLDLOCK)
WHERE RelevoNoPlaneadoAsignacionId =
      @AsignacionId;";

            return await conn.QueryFirstOrDefaultAsync<
                RelevoNoPlaneadoAsignacionDto>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        AsignacionId =
                            asignacionId
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<bool>
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
            WHERE A.SolicitudRelevoNoPlaneadoId =
                  @SolicitudId
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
                        SolicitudId =
                            solicitudId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CATALOGO ORIGEN

        public async Task<int?>
            ObtenerOrigenIdAsync(
                SqlConnection conn,
                string clave,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
                        Clave =
                            clave.Trim()
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CATALOGO ESTATUS SOLICITUD

        public async Task<int?>
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
                        Clave =
                            clave.Trim()
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<string?>
            ObtenerClaveEstatusSolicitudAsync(
                SqlConnection conn,
                int estatusId,
                CancellationToken ct,
                SqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT TOP (1)
    Clave
FROM dbo.CAT_RelevoNoPlaneadoEstatus
WHERE RelevoNoPlaneadoEstatusId =
      @EstatusId;";

            return await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        EstatusId =
                            estatusId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CATALOGO TIPO COBERTURA

        public async Task<int?>
            ObtenerTipoCoberturaIdAsync(
                SqlConnection conn,
                string clave,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
                        Clave =
                            clave.Trim()
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<string?>
            ObtenerClaveTipoCoberturaAsync(
                SqlConnection conn,
                int tipoCoberturaId,
                CancellationToken ct,
                SqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT TOP (1)
    Clave
FROM dbo.CAT_RelevoTipoCobertura
WHERE RelevoTipoCoberturaId =
      @TipoCoberturaId;";

            return await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TipoCoberturaId =
                            tipoCoberturaId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CATALOGO ESTATUS ASIGNACION

        public async Task<int?>
            ObtenerEstatusAsignacionIdAsync(
                SqlConnection conn,
                string clave,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
                        Clave =
                            clave.Trim()
                    },
                    transaction,
                    cancellationToken: ct));
        }


        public async Task<string?>
            ObtenerClaveEstatusAsignacionAsync(
                SqlConnection conn,
                int estatusId,
                CancellationToken ct,
                SqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT TOP (1)
    Clave
FROM dbo.CAT_RelevoAsignacionEstatus
WHERE RelevoAsignacionEstatusId =
      @EstatusId;";

            return await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        EstatusId =
                            estatusId
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region CATALOGO TIPO ASIGNACION SERVICIO

        public async Task<int?>
            ObtenerTipoAsignacionServicioIdAsync(
                SqlConnection conn,
                string clave,
                CancellationToken ct,
                SqlTransaction? transaction = null)
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
                        Clave =
                            clave.Trim()
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion


        #region SUPERVISOR SERVICIO

        public async Task<bool>
            EsSupervisorServicioAsync(
                SqlConnection conn,
                int supervisorEmpleadoId,
                int servicioId,
                DateTime fecha,
                CancellationToken ct,
                SqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.ServicioSupervisor
            WHERE ServicioId =
                  @ServicioId
              AND SupervisorEmpleadoId =
                  @SupervisorEmpleadoId
              AND FechaInicio <=
                  @Fecha
              AND
              (
                  FechaFin IS NULL
                  OR FechaFin >=
                     @Fecha
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
                        ServicioId =
                            servicioId,

                        SupervisorEmpleadoId =
                            supervisorEmpleadoId,

                        Fecha =
                            fecha.Date
                    },
                    transaction,
                    cancellationToken: ct));
        }

        #endregion
    }
}