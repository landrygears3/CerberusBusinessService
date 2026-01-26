using CerberusBusinessService.Models.DTO.Empleados;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Net.Http;

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

        public async Task<IEnumerable<ListadoEmpleadosResponse>> ObtenerListadoEmpleadosAsync()
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
                var empleados = await connection.QueryAsync<ListadoEmpleadosResponse>(query);
                return empleados;
            }
        }
    }
}
