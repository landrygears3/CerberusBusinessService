using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Catalogs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogosController : ControllerBase
    {
        private readonly string _cs;

        public CatalogosController(IConfiguration config)
        {
            _cs = config.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost("ObtenerCatalogo")]
        public async Task<ResponseModel<List<CatalogoResponse>>> ObtenerCatalogo(CatalogosRequest request, CancellationToken ct)
        {
            ResponseModel<List<CatalogoResponse>> response = new ResponseModel<List<CatalogoResponse>>();
            try
            {

                response.data = new List<CatalogoResponse>();
                await using var conn = new SqlConnection(_cs);
                await conn.OpenAsync(ct);

                await using var cmd = new SqlCommand("sp_GetCatalog", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                cmd.Parameters.Add(new SqlParameter("@NOMBRECAT", System.Data.SqlDbType.VarChar, 200) { Value = request.CatalogoNombre });

                var reader = await cmd.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    CatalogoResponse aux = new CatalogoResponse();
                    aux.Id = reader.GetInt32(reader.GetOrdinal("Id"));
                    aux.Nombre = reader.GetString(reader.GetOrdinal("Nombre"));
                    response.data.Add(aux);
                }
                if (response.data.Count == 0)
                {
                    response.code = 404;
                    response.message = "Catalogo no encontrado";
                    response.isSuccess = false;
                }
                else
                {
                    response.code = 200;
                    response.message = "Ok";
                    response.isSuccess = true;
                }

            }
            catch (Exception ex)
            {
                response.code = 500;
                response.desc = ex.Message;
                response.message = "Error all obtener catalogo " + request.CatalogoNombre;
                response.isSuccess = false;
            }
            return response;

        }

        [HttpPost("ObtenerSubCatalogo")]
        public async Task<ResponseModel<List<CatalogoResponse>>> ObtenerSubCatalogo(SubCatalogosRequest request, CancellationToken ct)
        {
            ResponseModel<List<CatalogoResponse>> response = new ResponseModel<List<CatalogoResponse>>();
            try
            {

                response.data = new List<CatalogoResponse>();
                await using var conn = new SqlConnection(_cs);
                await conn.OpenAsync(ct);

                await using var cmd = new SqlCommand("sp_GetSubCatalog", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                cmd.Parameters.Add(new SqlParameter("@NOMBRECATPADRE", System.Data.SqlDbType.VarChar, 200) { Value = request.CatalogoNombre });
                cmd.Parameters.Add(new SqlParameter("@IDCATPADRE", System.Data.SqlDbType.VarChar, 200) { Value = request.idPadre });

                var reader = await cmd.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    CatalogoResponse aux = new CatalogoResponse();
                    aux.Id = reader.GetInt32(reader.GetOrdinal("Id"));
                    aux.Nombre = reader.GetString(reader.GetOrdinal("Nombre"));
                    response.data.Add(aux);
                }
                if (response.data.Count == 0)
                {
                    response.code = 404;
                    response.message = "Catalogo no encontrado";
                    response.isSuccess = false;
                }
                else
                {
                    response.code = 200;
                    response.message = "Ok";
                    response.isSuccess = true;
                }

            }
            catch (Exception ex)
            {
                response.code = 500;
                response.desc = ex.Message;
                response.message = "Error all obtener catalogo " + request.CatalogoNombre;
                response.isSuccess = false;
            }
            return response;

        }
    }
}
