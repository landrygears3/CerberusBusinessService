using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Servicios;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Servicios
{
    public class ServiciosFunctions
    {
        #region PROPIEDADES

        private readonly string _csCerberus;

        #endregion

        #region CONSTRUCTOR

        public ServiciosFunctions(IConfiguration config)
        {
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
        }

        #endregion

        #region COMMIT SERVICIO

        public async Task<ResponseModel<CommitServicioResponse>> CommitServicioAsync(
            CommitServicioRequest request,
            CancellationToken ct)
        {
            ResponseModel<CommitServicioResponse> response = new ResponseModel<CommitServicioResponse>();
            SqlTransaction? transaction = null;

            if (request == null)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "El request es obligatorio.";
                response.data = null;
                return response;
            }

            if (request.ServicioId < 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "ServicioId no es válido.";
                response.data = null;
                return response;
            }

            if (request.ClienteId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "ClienteId es obligatorio.";
                response.data = null;
                return response;
            }

            if (string.IsNullOrWhiteSpace(request.NombreServicio))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "NombreServicio es obligatorio.";
                response.data = null;
                return response;
            }

            if (request.TipoServicioId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "TipoServicioId es obligatorio.";
                response.data = null;
                return response;
            }

            if (request.CantidadEmpleadosRequeridos < 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "CantidadEmpleadosRequeridos no puede ser negativa.";
                response.data = null;
                return response;
            }

            if (request.IdActividadServ <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "IdActividadServ debe ser mayor a cero.";
                response.data = null;
                return response;
            }

            if (request.Horarios == null || request.Horarios.Count == 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "Debe registrar al menos un horario operativo para el servicio.";
                response.data = null;
                return response;
            }

            if (request.Horarios.Any(x => x.DiaSemana < 1 || x.DiaSemana > 7))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "DiaSemana debe tener un valor entre 1 y 7.";
                response.data = null;
                return response;
            }

            if (request.Horarios.GroupBy(x => x.DiaSemana).Any(x => x.Count() > 1))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "No se puede enviar más de un horario para el mismo día de la semana.";
                response.data = null;
                return response;
            }

            if (request.Horarios.Any(x => x.FechaInicioVigencia == default))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "FechaInicioVigencia es obligatoria para todos los horarios.";
                response.data = null;
                return response;
            }

            try
            {
                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync(ct);

                transaction = conn.BeginTransaction();

                int servicioId;
                bool esNuevo;

                if (request.ServicioId == 0)
                {
                    const string sqlAlta = @"
INSERT INTO dbo.Servicio
(
    ClienteId,
    NombreServicio,
    Descripcion,
    Estatus,
    FechaAlta,
    TipoServicioId,
    CantidadEmpleadosRequeridos,
    IdActividadServ,
    Direccion
)
OUTPUT INSERTED.ServicioId
VALUES
(
    @ClienteId,
    @NombreServicio,
    @Descripcion,
    1,
    GETDATE(),
    @TipoServicioId,
    @CantidadEmpleadosRequeridos,
    @IdActividadServ,
    @Direccion
);";

                    var parameters = new
                    {
                        request.ClienteId,
                        NombreServicio = request.NombreServicio.Trim(),
                        Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
                        request.TipoServicioId,
                        request.CantidadEmpleadosRequeridos,
                        request.IdActividadServ,
                        Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim()
                    };

                    servicioId = await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(sqlAlta, parameters, transaction, cancellationToken: ct));

                    esNuevo = true;
                }
                else
                {
                    const string sqlExiste = @"
SELECT COUNT(1)
FROM dbo.Servicio
WHERE ServicioId = @ServicioId;";

                    int existe = await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlExiste,
                            new { request.ServicioId },
                            transaction,
                            cancellationToken: ct));

                    if (existe == 0)
                    {
                        transaction.Rollback();
                        transaction = null;

                        response.isSuccess = false;
                        response.code = 404;
                        response.message = "Servicio no encontrado.";
                        response.desc = null;
                        response.data = null;
                        return response;
                    }

                    const string sqlUpdate = @"
UPDATE dbo.Servicio
SET
    ClienteId = @ClienteId,
    NombreServicio = @NombreServicio,
    Descripcion = @Descripcion,
    TipoServicioId = @TipoServicioId,
    CantidadEmpleadosRequeridos = @CantidadEmpleadosRequeridos,
    IdActividadServ = @IdActividadServ,
    Direccion = @Direccion
WHERE ServicioId = @ServicioId;";

                    var updateParameters = new
                    {
                        request.ServicioId,
                        request.ClienteId,
                        NombreServicio = request.NombreServicio.Trim(),
                        Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
                        request.TipoServicioId,
                        request.CantidadEmpleadosRequeridos,
                        request.IdActividadServ,
                        Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim()
                    };

                    await conn.ExecuteAsync(
                        new CommandDefinition(sqlUpdate, updateParameters, transaction, cancellationToken: ct));

                    servicioId = request.ServicioId;
                    esNuevo = false;
                }

                foreach (ServicioHorarioRequest horario in request.Horarios)
                {
                    const string sqlHorarioExistente = @"
SELECT TOP (1)
    ServicioHorarioId
FROM dbo.Servicios_Horarios
WHERE ServicioId = @ServicioId
  AND DiaSemana = @DiaSemana
  AND Estatus = 1
  AND FechaFinVigencia IS NULL
ORDER BY ServicioHorarioId DESC;";

                    int? servicioHorarioId = await conn.QueryFirstOrDefaultAsync<int?>(
                        new CommandDefinition(
                            sqlHorarioExistente,
                            new
                            {
                                ServicioId = servicioId,
                                horario.DiaSemana
                            },
                            transaction,
                            cancellationToken: ct));

                    if (servicioHorarioId.HasValue)
                    {
                        const string sqlUpdateHorario = @"
UPDATE dbo.Servicios_Horarios
SET
    HoraInicio = @HoraInicio,
    HoraFin = @HoraFin,
    CruzaDia = @CruzaDia,
    FechaInicioVigencia = @FechaInicioVigencia
WHERE ServicioHorarioId = @ServicioHorarioId;";

                        await conn.ExecuteAsync(
                            new CommandDefinition(
                                sqlUpdateHorario,
                                new
                                {
                                    ServicioHorarioId = servicioHorarioId.Value,
                                    horario.HoraInicio,
                                    horario.HoraFin,
                                    horario.CruzaDia,
                                    FechaInicioVigencia = horario.FechaInicioVigencia.Date
                                },
                                transaction,
                                cancellationToken: ct));
                    }
                    else
                    {
                        const string sqlInsertHorario = @"
INSERT INTO dbo.Servicios_Horarios
(
    ServicioId,
    DiaSemana,
    HoraInicio,
    HoraFin,
    CruzaDia,
    FechaInicioVigencia
)
VALUES
(
    @ServicioId,
    @DiaSemana,
    @HoraInicio,
    @HoraFin,
    @CruzaDia,
    @FechaInicioVigencia
);";

                        await conn.ExecuteAsync(
                            new CommandDefinition(
                                sqlInsertHorario,
                                new
                                {
                                    ServicioId = servicioId,
                                    horario.DiaSemana,
                                    horario.HoraInicio,
                                    horario.HoraFin,
                                    horario.CruzaDia,
                                    FechaInicioVigencia = horario.FechaInicioVigencia.Date
                                },
                                transaction,
                                cancellationToken: ct));
                    }
                }

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message = esNuevo
                    ? "Servicio registrado correctamente."
                    : "Servicio actualizado correctamente.";
                response.desc = null;
                response.data = new CommitServicioResponse
                {
                    ServicioId = servicioId,
                    EsNuevo = esNuevo
                };

                return response;
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    transaction.Rollback();
                    transaction = null;
                }

                response.isSuccess = false;
                response.code = 500;
                response.message = "Error al registrar o actualizar el servicio";
                response.desc = ex.Message;
                response.data = null;
                return response;
            }
        }

        #endregion

        #region OBTENER SERVICIOS

        public async Task<ResponseModel<List<ListadoServicioResponse>>> ObtenerServiciosAsync(CancellationToken ct)
        {
            ResponseModel<List<ListadoServicioResponse>> response = new ResponseModel<List<ListadoServicioResponse>>();

            try
            {
                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync(ct);

                const string sql = @"
DECLARE @FechaActual DATE = CAST(SYSDATETIME() AS DATE);

WITH ServiciosBase AS
(
    SELECT
        S.ServicioId,
        S.ClienteId,
        S.NombreServicio,
        S.Descripcion,
        S.Estatus,
        S.FechaAlta,
        S.TipoServicioId,
        S.CantidadEmpleadosRequeridos,
        S.IdActividadServ,
        S.Direccion
    FROM dbo.Servicio S
),
HorariosOperativos AS
(
    SELECT
        SH.ServicioHorarioId,
        SH.ServicioId,
        SH.DiaSemana,
        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), SH.HoraInicio) AS InicioSegundo,
        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), SH.HoraFin)
            + CASE WHEN SH.CruzaDia = 1 THEN 86400 ELSE 0 END AS FinSegundo
    FROM dbo.Servicios_Horarios SH
    WHERE SH.Estatus = 1
      AND SH.FechaInicioVigencia <= @FechaActual
      AND
      (
          SH.FechaFinVigencia IS NULL
          OR SH.FechaFinVigencia >= @FechaActual
      )
),
EmpleadoBase AS
(
    SELECT
        SE.ServicioId,
        ISNULL(SE.EmpleadoCubiertoId, SE.EmpleadoId) AS EmpleadoCoberturaId,
        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), SE.HoraEntrada) AS InicioSegundo,
        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), SE.HoraSalida)
            + CASE WHEN SE.SalidaDiaSiguiente = 1 THEN 86400 ELSE 0 END AS FinSegundo
    FROM dbo.ServicioEmpleado SE
    WHERE SE.FechaInicio <= @FechaActual
      AND
      (
          SE.FechaFin IS NULL
          OR SE.FechaFin >= @FechaActual
      )
),
EmpleadoIntervalos AS
(
    SELECT
        EB.ServicioId,
        EB.EmpleadoCoberturaId,
        EB.InicioSegundo + D.Desplazamiento AS InicioSegundo,
        EB.FinSegundo + D.Desplazamiento AS FinSegundo
    FROM EmpleadoBase EB
    CROSS JOIN
    (
        VALUES
            (-86400),
            (0),
            (86400)
    ) D(Desplazamiento)
    WHERE EB.FinSegundo > EB.InicioSegundo
),
SupervisorBase AS
(
    SELECT
        SS.ServicioId,
        SS.SupervisorEmpleadoId,
        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), SS.HoraEntrada) AS InicioSegundo,
        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), SS.HoraSalida)
            + CASE WHEN SS.SalidaDiaSiguiente = 1 THEN 86400 ELSE 0 END AS FinSegundo
    FROM dbo.ServicioSupervisor SS
    WHERE SS.FechaInicio <= @FechaActual
      AND
      (
          SS.FechaFin IS NULL
          OR SS.FechaFin >= @FechaActual
      )
),
SupervisorIntervalos AS
(
    SELECT
        SB.ServicioId,
        SB.SupervisorEmpleadoId,
        SB.InicioSegundo + D.Desplazamiento AS InicioSegundo,
        SB.FinSegundo + D.Desplazamiento AS FinSegundo
    FROM SupervisorBase SB
    CROSS JOIN
    (
        VALUES
            (-86400),
            (0),
            (86400)
    ) D(Desplazamiento)
    WHERE SB.FinSegundo > SB.InicioSegundo
),
Puntos AS
(
    SELECT
        H.ServicioHorarioId,
        H.ServicioId,
        H.DiaSemana,
        H.InicioSegundo AS Punto
    FROM HorariosOperativos H

    UNION

    SELECT
        H.ServicioHorarioId,
        H.ServicioId,
        H.DiaSemana,
        H.FinSegundo
    FROM HorariosOperativos H

    UNION

    SELECT
        H.ServicioHorarioId,
        H.ServicioId,
        H.DiaSemana,
        EI.InicioSegundo
    FROM HorariosOperativos H
    INNER JOIN EmpleadoIntervalos EI
        ON EI.ServicioId = H.ServicioId
       AND EI.InicioSegundo > H.InicioSegundo
       AND EI.InicioSegundo < H.FinSegundo

    UNION

    SELECT
        H.ServicioHorarioId,
        H.ServicioId,
        H.DiaSemana,
        EI.FinSegundo
    FROM HorariosOperativos H
    INNER JOIN EmpleadoIntervalos EI
        ON EI.ServicioId = H.ServicioId
       AND EI.FinSegundo > H.InicioSegundo
       AND EI.FinSegundo < H.FinSegundo

    UNION

    SELECT
        H.ServicioHorarioId,
        H.ServicioId,
        H.DiaSemana,
        SI.InicioSegundo
    FROM HorariosOperativos H
    INNER JOIN SupervisorIntervalos SI
        ON SI.ServicioId = H.ServicioId
       AND SI.InicioSegundo > H.InicioSegundo
       AND SI.InicioSegundo < H.FinSegundo

    UNION

    SELECT
        H.ServicioHorarioId,
        H.ServicioId,
        H.DiaSemana,
        SI.FinSegundo
    FROM HorariosOperativos H
    INNER JOIN SupervisorIntervalos SI
        ON SI.ServicioId = H.ServicioId
       AND SI.FinSegundo > H.InicioSegundo
       AND SI.FinSegundo < H.FinSegundo
),
Segmentos AS
(
    SELECT
        P.ServicioHorarioId,
        P.ServicioId,
        P.DiaSemana,
        P.Punto AS InicioSegmento,
        LEAD(P.Punto) OVER
        (
            PARTITION BY P.ServicioHorarioId
            ORDER BY P.Punto
        ) AS FinSegmento
    FROM Puntos P
),
Evaluacion AS
(
    SELECT
        SEG.ServicioId,
        CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM SupervisorIntervalos SI
                WHERE SI.ServicioId = SEG.ServicioId
                  AND SI.InicioSegundo <= SEG.InicioSegmento
                  AND SI.FinSegundo >= SEG.FinSegmento
            )
            THEN 0
            ELSE 1
        END AS FaltaSupervisor,
        (
            SELECT COUNT(DISTINCT EI.EmpleadoCoberturaId)
            FROM EmpleadoIntervalos EI
            WHERE EI.ServicioId = SEG.ServicioId
              AND EI.InicioSegundo <= SEG.InicioSegmento
              AND EI.FinSegundo >= SEG.FinSegmento
        ) AS CantidadCobertura
    FROM Segmentos SEG
    WHERE SEG.FinSegmento IS NOT NULL
      AND SEG.FinSegmento > SEG.InicioSegmento
),
Incidencias AS
(
    SELECT
        SB.ServicioId,
        ISNULL(MAX(E.FaltaSupervisor), 0) AS FaltaSupervisor,
        ISNULL(MAX(
            CASE
                WHEN E.CantidadCobertura < SB.CantidadEmpleadosRequeridos THEN 1
                ELSE 0
            END
        ), 0) AS FaltaCobertura,
        ISNULL(MAX(
            CASE
                WHEN E.CantidadCobertura > SB.CantidadEmpleadosRequeridos THEN 1
                ELSE 0
            END
        ), 0) AS ExcesoCobertura
    FROM ServiciosBase SB
    LEFT JOIN Evaluacion E
        ON E.ServicioId = SB.ServicioId
    GROUP BY
        SB.ServicioId,
        SB.CantidadEmpleadosRequeridos
),
EstatusOperativo AS
(
    SELECT
        I.ServicioId,
        CASE
            WHEN I.FaltaSupervisor + I.FaltaCobertura + I.ExcesoCobertura > 1 THEN 5
            WHEN I.FaltaSupervisor = 1 THEN 2
            WHEN I.FaltaCobertura = 1 THEN 3
            WHEN I.ExcesoCobertura = 1 THEN 4
            ELSE 1
        END AS EstatusOperativo
    FROM Incidencias I
)
SELECT
    S.ServicioId,
    S.ClienteId,
    S.NombreServicio,
    S.Descripcion,
    S.Estatus,
    S.FechaAlta,
    S.TipoServicioId,
    ISNULL(TS.Clave, '') AS TipoServicioClave,
    ISNULL(TS.Nombre, '') AS TipoServicioNombre,
    S.CantidadEmpleadosRequeridos,
    S.IdActividadServ,
    S.Direccion,
    EO.EstatusOperativo,
    CASE EO.EstatusOperativo
        WHEN 1 THEN 'Ok'
        WHEN 2 THEN 'Falta Supervisor'
        WHEN 3 THEN 'Falta cobertura'
        WHEN 4 THEN 'Exceso de cobertura'
        WHEN 5 THEN 'Varias incidencias'
    END AS DesEstatusOp
FROM ServiciosBase S
LEFT JOIN dbo.CAT_TipoServicio TS
    ON TS.TipoServicioId = S.TipoServicioId
INNER JOIN EstatusOperativo EO
    ON EO.ServicioId = S.ServicioId
ORDER BY
    S.NombreServicio,
    S.ServicioId;";

                var result = await conn.QueryAsync<ListadoServicioResponse>(
                    new CommandDefinition(sql, cancellationToken: ct));

                response.isSuccess = true;
                response.code = 200;
                response.message = "Listado de servicios obtenido correctamente.";
                response.desc = null;
                response.data = result.ToList();
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message = "Error al obtener el listado de servicios";
                response.desc = ex.Message;
                response.data = null;
            }

            return response;
        }

        #endregion

        #region OBTENER SERVICIO

        public async Task<ResponseModel<ServicioResponse>> ObtenerServicioAsync(
            GetServicioRequest request,
            CancellationToken ct)
        {
            ResponseModel<ServicioResponse> response = new ResponseModel<ServicioResponse>();

            if (request == null || request.ServicioId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "El campo ServicioId es obligatorio.";
                response.data = null;
                return response;
            }

            try
            {
                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync(ct);

                const string sqlServicio = @"
SELECT TOP (1)
    S.ServicioId,
    S.ClienteId,
    S.NombreServicio,
    S.Descripcion,
    S.Estatus,
    S.FechaAlta,
    S.TipoServicioId,
    ISNULL(TS.Clave, '') AS TipoServicioClave,
    ISNULL(TS.Nombre, '') AS TipoServicioNombre,
    S.CantidadEmpleadosRequeridos,
    S.IdActividadServ,
    S.Direccion
FROM dbo.Servicio S
LEFT JOIN dbo.CAT_TipoServicio TS
    ON TS.TipoServicioId = S.TipoServicioId
WHERE S.ServicioId = @ServicioId;";

                ServicioResponse? servicio = await conn.QueryFirstOrDefaultAsync<ServicioResponse>(
                    new CommandDefinition(
                        sqlServicio,
                        new { request.ServicioId },
                        cancellationToken: ct));

                if (servicio == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message = "Servicio no encontrado.";
                    response.desc = null;
                    response.data = null;
                    return response;
                }

                const string sqlHorarios = @"
SELECT
    ServicioHorarioId,
    ServicioId,
    DiaSemana,
    HoraInicio,
    HoraFin,
    CruzaDia,
    FechaInicioVigencia,
    FechaFinVigencia,
    Estatus,
    FechaAlta
FROM dbo.Servicios_Horarios
WHERE ServicioId = @ServicioId
  AND Estatus = 1
  AND FechaFinVigencia IS NULL
ORDER BY
    DiaSemana,
    ServicioHorarioId;";

                var horarios = await conn.QueryAsync<ServicioHorarioResponse>(
                    new CommandDefinition(
                        sqlHorarios,
                        new { request.ServicioId },
                        cancellationToken: ct));

                servicio.Horarios = horarios.ToList();

                response.isSuccess = true;
                response.code = 200;
                response.message = "Información del servicio obtenida correctamente.";
                response.desc = null;
                response.data = servicio;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message = "Error al obtener la información del servicio";
                response.desc = ex.Message;
                response.data = null;
            }

            return response;
        }

        #endregion
    }
}