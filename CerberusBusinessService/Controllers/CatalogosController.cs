using CerberusBusinessService.Models.DTO.Catalogs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("negocio/[controller]")]
    public class CatalogosController : ControllerBase
    {
        private readonly string _cs;

        public CatalogosController(IConfiguration config)
        {
            _cs = config.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet("ObtenerCatalogo")]
        public async Task<ActionResult<List<CatalogoResponse>>> ObtenerCatalogo(CatalogosRequest request, CancellationToken ct)
        {
            List<CatalogoResponse> response = new List<CatalogoResponse>();
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
                response.Add(aux);
            }

            return Ok(response);
        }
    }
}
