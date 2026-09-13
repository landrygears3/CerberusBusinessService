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

            try
            {
                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync(ct);

                if (request.ServicioId == 0)
                {
                    const string sql = @"
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
                        Descripcion = string.IsNullOrWhiteSpace(request.Descripcion)
                            ? null
                            : request.Descripcion.Trim(),
                        request.TipoServicioId,
                        request.CantidadEmpleadosRequeridos,
                        request.IdActividadServ,
                        Direccion = string.IsNullOrWhiteSpace(request.Direccion)
                            ? null
                            : request.Direccion.Trim()
                    };

                    int servicioId = await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sql,
                            parameters,
                            cancellationToken: ct));

                    response.isSuccess = true;
                    response.code = 200;
                    response.message = "Servicio registrado correctamente.";
                    response.desc = null;
                    response.data = new CommitServicioResponse
                    {
                        ServicioId = servicioId,
                        EsNuevo = true
                    };

                    return response;
                }

                const string sqlExiste = @"
SELECT COUNT(1)
FROM dbo.Servicio
WHERE ServicioId = @ServicioId;";

                int existe = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sqlExiste,
                        new
                        {
                            request.ServicioId
                        },
                        cancellationToken: ct));

                if (existe == 0)
                {
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
                    Descripcion = string.IsNullOrWhiteSpace(request.Descripcion)
                        ? null
                        : request.Descripcion.Trim(),
                    request.TipoServicioId,
                    request.CantidadEmpleadosRequeridos,
                    request.IdActividadServ,
                    Direccion = string.IsNullOrWhiteSpace(request.Direccion)
                        ? null
                        : request.Direccion.Trim()
                };

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlUpdate,
                        updateParameters,
                        cancellationToken: ct));

                response.isSuccess = true;
                response.code = 200;
                response.message = "Servicio actualizado correctamente.";
                response.desc = null;
                response.data = new CommitServicioResponse
                {
                    ServicioId = request.ServicioId,
                    EsNuevo = false
                };

                return response;
            }
            catch (Exception ex)
            {
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

        public async Task<ResponseModel<List<ServicioResponse>>> ObtenerServiciosAsync(CancellationToken ct)
        {
            ResponseModel<List<ServicioResponse>> response = new ResponseModel<List<ServicioResponse>>();

            try
            {
                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync(ct);

                const string sql = @"
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
    S.Direccion
FROM dbo.Servicio S
LEFT JOIN dbo.CAT_TipoServicio TS
    ON TS.TipoServicioId = S.TipoServicioId
ORDER BY
    S.NombreServicio,
    S.ServicioId;";

                var result = await conn.QueryAsync<ServicioResponse>(
                    new CommandDefinition(
                        sql,
                        cancellationToken: ct));

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

                const string sql = @"
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
                        sql,
                        new
                        {
                            request.ServicioId
                        },
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