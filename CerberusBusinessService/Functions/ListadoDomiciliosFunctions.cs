using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Empleados;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions
{
    public class ListadoDomiciliosFunctions
    {
        private readonly string _csCerberus;

        public ListadoDomiciliosFunctions(IConfiguration config)
        {
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
        }
        public async Task<ResponseModel<List<ListadoDomiciliosResponse>>> ListadoDomicilios(string usuario)
        {
            var response = new ResponseModel<List<ListadoDomiciliosResponse>>
            {
                isSuccess = false,
                code = 400,
                message = "Error al obtener domicilios",
                desc = null,
                data = null
            };

            // -----------------------
            // Validaciones
            // -----------------------
            if (string.IsNullOrWhiteSpace(usuario))
            {
                response.message = "El parámetro usuario es obligatorio";
                return response;
            }

            try
            {
                using var conn = new SqlConnection(_csCerberus);

                var sql = @"
            SELECT
                IDdomicilio,
                Calle,
                Numero_Exterior,
                Numero_Interior,
                Codigo_Postal,
                EstadoID,
                MunicipioID,
                ColoniaID,
                Its_Principal
            FROM Usuarios_Domicilios
            WHERE Usuario = @Usuario and Its_Active = 1
            ORDER BY Its_Principal DESC, IDdomicilio ASC;";

                var result = (await conn.QueryAsync<ListadoDomiciliosResponse>(
                    sql,
                    new { Usuario = usuario.Trim() }
                )).ToList();

                response.isSuccess = true;
                response.code = 200;
                response.message = "Domicilios obtenidos correctamente";
                response.data = result;

                return response;
            }
            catch (Exception ex)
            {
                response.code = 500;
                response.message = "Error al consultar domicilios";
                response.desc = ex.Message;
                return response;
            }
        }

    }
}
