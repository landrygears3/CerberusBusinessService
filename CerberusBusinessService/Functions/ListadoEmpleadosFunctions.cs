using CerberusBusinessService.Models.DTO.Empleados;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CerberusBusinessService.Functions
{
    public class ListadoEmpleadosFunctions
    {
        private readonly string _csCerberus;
        public ListadoEmpleadosFunctions(
            IConfiguration config)
        {
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<List<ListadoEmpleadosResponse>> ObtenerListadoEmpleadosAsync()
        {
            using (var connection = new SqlConnection(_csCerberus))
            {
                var query = @"
                    SELECT 
                        IdEmpleado,
                        NombreCompleto,
                        Departamento,
                        Estatus,
                        Expediente
                    FROM 
                        View_ListadoEmpleados"; // Ajusta la consulta según tu esquema de base de datos
                List<ListadoEmpleadosResponse> empleados = (await connection.QueryAsync<ListadoEmpleadosResponse>(query)).ToList();
                return empleados;
            }
        }
        public async Task<EmpleadoDatosGeneralesResponse?> ObtenerPorUsuarioAsignadoAsync(string usuarioAsignado)
        {
            if (string.IsNullOrWhiteSpace(usuarioAsignado) || !Regex.IsMatch(usuarioAsignado, @"^CER\d{5}$"))
                throw new Exception("UsuarioAsignado inválido (ej. CER00004)");

            using var conn = new SqlConnection(_csCerberus);

            var sql = @"
                        SELECT TOP 1
                            *
                        FROM View_DatosGenerales
                        WHERE UsuarioAsignado = @UsuarioAsignado
                        ORDER BY Id DESC;";

            return await conn.QueryFirstOrDefaultAsync<EmpleadoDatosGeneralesResponse>(
                sql,
                new { UsuarioAsignado = usuarioAsignado });
        }
    }
}
