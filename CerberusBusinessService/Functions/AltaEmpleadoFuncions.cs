using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Empleados;
using CerberusBusinessService.Models.DTO.Mail;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CerberusBusinessService.Functions
{
    public class AltaEmpleadoFuncions
    {
        private readonly string _csCerberus;
        private readonly string _csCerberusConfig;
        private readonly HttpClient _httpClient;

        public AltaEmpleadoFuncions(
            IConfiguration config,
            HttpClient httpClient)
        {
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
            _csCerberusConfig = config.GetConnectionString("CerberusConfig")!;
            _httpClient = httpClient;
        }

        public async Task<string> AltaEmpleadoGenerales(EmpleadoAltaGeneralesRequest data)
        {
            int? personaId = null;
            AuthRegisterResponse usr = null;
            try
            {
                // 1. Validaciones de formato
                ValidarFormato(data);

                // 2. Validar duplicados
                await ValidarDuplicadosAsync(data.Curp, data.RFC);

                // 3. Generar password
                var password = GenerarPassword(data.Nombres, data.ApellidoPaterno);

                // 4. Registrar usuario en Auth
                usr = await RegistrarUsuarioAuthAsync(data, password);

                // 5. Insertar Persona (CerberusConfig)
                personaId = await InsertarPersonaAsync(usr.userId, data.Departamento);

                data.UsuarioAsignado = usr.numeroUsuario;
                // 6. Insertar Datos Generales (Cerberus)
                await InsertarDatosGeneralesEmpleadoAsync(data);

                await EnviarCorreoBienvenidaAsync(
                    data.CorreoElectronico,
                    usr.numeroUsuario,
                    password);

                return $"Empleado dado de alta correctamente. Usuario: {usr.numeroUsuario}";
            }
            catch
            {
                // 🔥 ROLLBACK COMPENSATORIO 🔥
                if (personaId.HasValue)
                    await RollbackPersonaAsync(personaId.Value);

                //if (!string.IsNullOrWhiteSpace(usr.AspNetUserId))
                //    await RollbackUsuarioAuthAsync(usr.AspNetUserId);

                throw new Exception("No se pudo dar de alta el usuario");
            }
        }
        private async Task EnviarCorreoBienvenidaAsync(
    string email,
    string userName,
    string password)
        {
            var body = new SendWelcomeRequest
            {
                ToEmail = email,
                UserName = userName,
                Password = password
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/SendWelcome/enviar-bienvenida",
                body);

            if (!response.IsSuccessStatusCode)
                throw new Exception("No fue posible enviar el correo de bienvenida");
        }


        private void ValidarFormato(EmpleadoAltaGeneralesRequest data)
        {
            if (!Regex.IsMatch(data.Curp,
                @"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$"))
                throw new Exception("CURP inválida");

            if (!Regex.IsMatch(data.RFC,
                @"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$"))
                throw new Exception("RFC inválido");

            if (!Regex.IsMatch(data.CorreoElectronico,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new Exception("Correo electrónico inválido");

            if (!Regex.IsMatch(data.UsuarioAlta,
                @"^CER\d{5}$"))
                throw new Exception("UsuarioAlta inválido (CER00001)");
        }

        private async Task ValidarDuplicadosAsync(string curp, string rfc)
        {
            using var conn = new SqlConnection(_csCerberus);

            var sql = @"SELECT COUNT(1)
                        FROM DatosGeneralesEmpleado
                        WHERE Curp = @Curp OR RFC = @RFC";

            if (await conn.ExecuteScalarAsync<int>(sql, new { Curp = curp, RFC = rfc }) > 0)
                throw new Exception("La CURP o RFC ya existen");
        }

        private string GenerarPassword(string nombres, string apellidoPaterno)
        {
            var inicial = "#3CEr" + nombres.Trim()[0].ToString().ToUpper();

            return string.IsNullOrWhiteSpace(apellidoPaterno)
                ? nombres.Trim().ToUpper()
                : inicial + apellidoPaterno.Trim().ToUpper();
        }

        private async Task<AuthRegisterResponse> RegistrarUsuarioAuthAsync(
    EmpleadoAltaGeneralesRequest data,
    string password)
        {
            var body = new
            {
                email = data.CorreoElectronico,
                password,
                nombreCompleto = $"{data.Nombres} {data.ApellidoPaterno} {data.ApellidoMaterno}".Trim(),
                telefono = data.Celular
            };

            var response = await _httpClient.PostAsJsonAsync("api/Auth/register", body);
            var raw = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                ResponseModel<string> result1 = JsonSerializer.Deserialize<ResponseModel<string>>(raw);
                throw new Exception("Error al registrar usuario en Auth: " + result1?.Message);
            }

            var result = JsonSerializer.Deserialize<AuthRegisterResponse>(raw);
            //var result = await response.Content.ReadFromJsonAsync<AuthRegisterResponse>();
            if (result is null || string.IsNullOrWhiteSpace(result.userId) || string.IsNullOrWhiteSpace(result.numeroUsuario))
                throw new Exception("Auth/register no devolvió AspNetUserId y NumeroUsuario");

            return result;
        }

        private async Task<int> InsertarPersonaAsync(string aspNetUserId,int DepartamentoId)
        {
            using var conn = new SqlConnection(_csCerberusConfig);

            var sql = @"INSERT INTO Personas (AspNetUserId, DepartamentoId, Activo, CreatedAt)"+
                " VALUES (@AspNetUserId, " + DepartamentoId + ", 1, GETDATE()); SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await conn.ExecuteScalarAsync<int>(sql, new { AspNetUserId = aspNetUserId });
        }

        private async Task InsertarDatosGeneralesEmpleadoAsync(EmpleadoAltaGeneralesRequest data)
        {
            using var conn = new SqlConnection(_csCerberus);

            var sql = @"
                INSERT INTO DatosGeneralesEmpleado
                (Nombres, ApellidoPaterno, ApellidoMaterno, FechaNacimiento,
                 sexoId, NacionalidadId, Curp, EscolaridadId, EstadoCivilId,
                 RFC, Celular, Telefono, CorreoElectronico, origenVacanteId,
                 UsuarioAlta, FechaCreacion,UsuarioAsignado)
                VALUES
                (@Nombres, @ApellidoPaterno, @ApellidoMaterno, @FechaNacimiento,
                 @sexoId, @NacionalidadId, @Curp, @EscolaridadId, @EstadoCivilId,
                 @RFC, @Celular, @Telefono, @CorreoElectronico, @origenVacanteId,
                 @UsuarioAlta, GETDATE(),@UsuarioAsignado)";

            await conn.ExecuteAsync(sql, data);
        }
        private async Task RollbackPersonaAsync(int personaId)
        {
            using var conn = new SqlConnection(_csCerberusConfig);
            await conn.ExecuteAsync(
                "DELETE FROM Personas WHERE Id = @Id",
                new { Id = personaId });
        }

        private async Task RollbackUsuarioAuthAsync(string userId)
        {
            await _httpClient.DeleteAsync($"api/Auth/delete/{userId}");
        }
    }
}
