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
                
                response.Data = new List<CatalogoResponse>();
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
                    response.Data.Add(aux);
                }
                if (response.Data.Count == 0)
                {
                    response.Code = 404;
                    response.Message = "Catalogo no encontrado";
                    response.IsSuccess = false;
                }
                else
                {
                    response.Code = 200;
                    response.Message = "Ok";
                    response.IsSuccess = true;
                }

            }
            catch (Exception ex)
            {
                response.Code = 500;
                response.Desc = ex.Message;
                response.Message = "Error all obtener catalogo " + request.CatalogoNombre;
                response.IsSuccess = false;
            }
             return response;

        }
    }
}
