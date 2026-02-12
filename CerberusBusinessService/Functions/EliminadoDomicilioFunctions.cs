using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Empleados;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions
{
    public class EliminadoDomicilioFunctions
    {
        private readonly string _csCerberus;
        public EliminadoDomicilioFunctions(IConfiguration csCerberus)
        {
            _csCerberus = csCerberus.GetConnectionString("DefaultConnection")!;
        }
        public async Task<ResponseModel<string>> EliminarDomicilio(EliminadoDomicilioRequest data)
        {
            var response = new ResponseModel<string>
            {
                isSuccess = false,
                code = 400,
                message = "Error al eliminar domicilio",
                desc = null,
                data = null
            };

            if (data == null)
            {
                response.message = "Request vacío";
                return response;
            }

            if (string.IsNullOrWhiteSpace(data.Usuario))
            {
                response.message = "Usuario es obligatorio";
                return response;
            }

            if (data.IDdomicilio <= 0)
            {
                response.message = "IDdomicilio inválido";
                return response;
            }

            try
            {
                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync();

                using var tx = conn.BeginTransaction();

                // 1) Obtener registro
                var dom = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT Its_Principal, Its_Active
              FROM Usuarios_Domicilios
              WHERE Usuario = @Usuario AND IDdomicilio = @IDdomicilio;",
                    new { Usuario = data.Usuario.Trim(), IDdomicilio = data.IDdomicilio },
                    tx);

                if (dom == null)
                {
                    response.message = "El domicilio no existe";
                    return response;
                }

                bool itsPrincipal = (bool)dom.Its_Principal;
                bool itsActive = (bool)dom.Its_Active;

                if (!itsActive)
                {
                    response.message = "El domicilio ya se encuentra eliminado";
                    return response;
                }

                // 2) NO permitir eliminar el último activo
                var activos = await conn.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1)
              FROM Usuarios_Domicilios
              WHERE Usuario = @Usuario
                AND Its_Active = 1;",
                    new { Usuario = data.Usuario.Trim() },
                    tx);

                if (activos <= 1)
                {
                    response.message = "No es posible eliminar el último domicilio activo del usuario.";
                    return response;
                }

                // 3) Eliminación lógica
                await conn.ExecuteAsync(
                    @"UPDATE Usuarios_Domicilios
              SET Its_Active = 0,
                  Its_Principal = 0
              WHERE Usuario = @Usuario AND IDdomicilio = @IDdomicilio;",
                    new { Usuario = data.Usuario.Trim(), IDdomicilio = data.IDdomicilio },
                    tx);

                // 4) Si era principal, asignar nuevo principal a otro activo
                if (itsPrincipal)
                {
                    var nuevoPrincipalId = await conn.ExecuteScalarAsync<int?>(
                        @"SELECT TOP (1) IDdomicilio
                  FROM Usuarios_Domicilios
                  WHERE Usuario = @Usuario
                    AND Its_Active = 1
                  ORDER BY IDdomicilio ASC;",
                        new { Usuario = data.Usuario.Trim() },
                        tx);

                    if (nuevoPrincipalId.HasValue)
                    {
                        await conn.ExecuteAsync(
                            @"UPDATE Usuarios_Domicilios
                      SET Its_Principal = 1
                      WHERE Usuario = @Usuario
                        AND IDdomicilio = @IDdomicilio;",
                            new { Usuario = data.Usuario.Trim(), IDdomicilio = nuevoPrincipalId.Value },
                            tx);
                    }
                }

                tx.Commit();

                response.isSuccess = true;
                response.code = 200;
                response.message = "Domicilio eliminado correctamente";
                response.data = "OK";
                return response;
            }
            catch (Exception ex)
            {
                response.code = 500;
                response.message = "Error al eliminar domicilio";
                response.desc = ex.Message;
                return response;
            }
        }


    }
}
