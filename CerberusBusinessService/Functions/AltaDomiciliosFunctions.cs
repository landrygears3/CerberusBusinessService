using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Empleados;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions
{
    public class AltaDomiciliosFunctions
    {
        private readonly string _csCerberus;

        public AltaDomiciliosFunctions(IConfiguration config)
        {
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<ResponseModel<string>> AltaDomicilios(AltaDomicilioRequest data)
        {
            var response = new ResponseModel<string>
            {
                isSuccess = false,
                code = 400,
                message = "Error al registrar domicilios",
                desc = null,
                data = null
            };

            // -----------------------
            // Validaciones
            // -----------------------
            if (data == null)
            {
                response.message = "Request vacío";
                return response;
            }

            if (string.IsNullOrWhiteSpace(data.Usuario))
            {
                response.message = "El campo Usuario es obligatorio";
                return response;
            }

            if (data.domicilios == null || data.domicilios.Count == 0)
            {
                response.message = "Debe enviar al menos un domicilio";
                return response;
            }

            if (data.domicilios.Count(d => d.Its_Principal) > 1)
            {
                response.message = "Solo puede existir un domicilio principal por usuario";
                return response;
            }

            foreach (var d in data.domicilios)
            {
                if (d == null)
                {
                    response.message = "Existe un domicilio nulo en la lista";
                    return response;
                }

                if (string.IsNullOrWhiteSpace(d.Calle))
                {
                    response.message = "Calle es obligatoria en todos los domicilios";
                    return response;
                }

                if (string.IsNullOrWhiteSpace(d.Numero_Exterior))
                {
                    response.message = "Numero_Exterior es obligatorio en todos los domicilios";
                    return response;
                }

                if (d.EstadoID <= 0 || d.MunicipioID <= 0 || d.ColoniaID <= 0)
                {
                    response.message = "EstadoID, MunicipioID y ColoniaID deben ser mayores a cero";
                    return response;
                }
            }

            // -----------------------
            // Inserción
            // -----------------------
            try
            {
                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync();

                using var tx = conn.BeginTransaction();

                // Obtener siguiente IDdomicilio por usuario
                var nextId = await conn.ExecuteScalarAsync<int>(
                    @"SELECT ISNULL(MAX(IDdomicilio), 0) + 1
                      FROM Usuarios_Domicilios WITH (UPDLOCK, HOLDLOCK)
                      WHERE Usuario = @Usuario;",
                    new { Usuario = data.Usuario },
                    tx);

                // Si viene un principal, apagar el actual
                if (data.domicilios.Any(d => d.Its_Principal))
                {
                    await conn.ExecuteAsync(
                        @"UPDATE Usuarios_Domicilios
                          SET Its_Principal = 0
                          WHERE Usuario = @Usuario;",
                        new { Usuario = data.Usuario },
                        tx);
                }

                const string insertSql = @"
                    INSERT INTO Usuarios_Domicilios
                    (Usuario, IDdomicilio, Calle, Numero_Interior, Numero_Exterior,
                     Codigo_Postal, EstadoID, MunicipioID, ColoniaID, Its_Principal)
                    VALUES
                    (@Usuario, @IDdomicilio, @Calle, @Numero_Interior, @Numero_Exterior,
                     @Codigo_Postal, @EstadoID, @MunicipioID, @ColoniaID, @Its_Principal);";

                int totalInsertados = 0;

                for (int i = 0; i < data.domicilios.Count; i++)
                {
                    var d = data.domicilios[i];

                    totalInsertados += await conn.ExecuteAsync(
                        insertSql,
                        new
                        {
                            Usuario = data.Usuario.Trim(),
                            IDdomicilio = nextId + i,
                            Calle = d.Calle.Trim(),
                            Numero_Interior = string.IsNullOrWhiteSpace(d.Numero_Interior) ? null : d.Numero_Interior.Trim(),
                            Numero_Exterior = d.Numero_Exterior.Trim(),
                            Codigo_Postal = string.IsNullOrWhiteSpace(d.Codigo_Postal) ? null : d.Codigo_Postal.Trim(),
                            EstadoID = d.EstadoID,
                            MunicipioID = d.MunicipioID,
                            ColoniaID = d.ColoniaID,
                            Its_Principal = d.Its_Principal
                        },
                        tx);
                }

                tx.Commit();

                response.isSuccess = true;
                response.code = 200;
                response.message = "Domicilios registrados correctamente";
                response.data = $"Registros insertados: {totalInsertados}";
                return response;
            }
            catch (Exception ex)
            {
                response.code = 500;
                response.message = "Error al insertar domicilios";
                response.desc = ex.Message;
                return response;
            }
        }
    }
}
