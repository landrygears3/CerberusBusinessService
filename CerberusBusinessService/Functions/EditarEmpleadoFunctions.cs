using CerberusBusinessService.Models.DTO.Empleados;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace CerberusBusinessService.Functions
{
    public class EditarEmpleadoFunctions
    {
        private readonly string _csCerberus;

        public EditarEmpleadoFunctions(IConfiguration config)
        {
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<string> EditarAsync(EditarEmpleadoRequest req)
        {
            

            if (!string.IsNullOrEmpty(ValidarFormato(req)))
                return ValidarFormato(req);

            using var conn = new SqlConnection(_csCerberus);

            // 1) Validar que exista el empleado por UsuarioAsignado
            var existe = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.DatosGeneralesEmpleado WHERE UsuarioAsignado = @UsuarioAsignado",
                new { req.UsuarioAsignado });

            if (existe == 0)
                throw new Exception("No existe empleado con ese UsuarioAsignado");

            // 2) Validar duplicados Curp/RFC sin chocar consigo mismo
            var dup = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1)
                FROM dbo.DatosGeneralesEmpleado
                WHERE (Curp = @Curp OR RFC = @RFC)
                  AND UsuarioAsignado <> @UsuarioAsignado;",
                new { req.Curp, req.RFC, req.UsuarioAsignado });

            if (dup > 0)
                throw new Exception("La CURP o RFC ya están registrados en otro empleado");

            // 3) Update
            var sql = @"
UPDATE dbo.DatosGeneralesEmpleado
SET
    Nombres = @Nombres,
    ApellidoPaterno = @ApellidoPaterno,
    ApellidoMaterno = @ApellidoMaterno,
    FechaNacimiento = @FechaNacimiento,
    sexoId = @SexoId,
    Curp = @Curp,
    RFC = @RFC,
    EscolaridadId = @EscolaridadId,
    EstadoCivilId = @EstadoCivilId,
    Celular = @Celular,
    Telefono = @Telefono,
    CorreoElectronico = @CorreoElectronico,
    NacionalidadId = @NacionalidadId,
    origenVacanteId = @OrigenVacanteId
WHERE UsuarioAsignado = @UsuarioAsignado;";

            await conn.ExecuteAsync(sql, req);
            return "OK";
        }

        private string ValidarFormato(EditarEmpleadoRequest req)
        {
            if (!Regex.IsMatch(req.UsuarioAsignado, @"^CER\d{5}$"))
                return "UsuarioAsignado inválido (ej. CER00001)";

            if (!Regex.IsMatch(req.Curp, @"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$"))
                return "CURP inválida";

            if (!Regex.IsMatch(req.RFC, @"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$"))
                return "RFC inválido";

            if (!Regex.IsMatch(req.CorreoElectronico, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return "Correo electrónico inválido";
            return string.Empty;
        }
    }
}
