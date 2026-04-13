using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Empleados;
using CerberusBusinessService.Models.DTO.Empleados.Items;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CerberusBusinessService.Functions
{
    public class SaludFunctions
    {
        private readonly string _connectionString;
        public SaludFunctions(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<ResponseModel<bool>> CommitSaludAsync(SaludCommitRequest req)
        {
            var response = new ResponseModel<bool>
            {
                isSuccess = false,
                code = 500,
                message = "Error",
                desc = "No fue posible aplicar el commit.",
                data = false
            };

            if (req == null || string.IsNullOrWhiteSpace(req.Usuario))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "El campo Usuario es obligatorio.";
                response.data = false;
                return response;
            }

            req.ContactosEmergencia ??= new List<ContactoEmergenciaDto>();
            req.AlergiasIds ??= new List<int>();
            req.EnfermedadesIds ??= new List<int>();
            req.DiscapacidadesIds ??= new List<int>();

            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();

                var p = new DynamicParameters();
                p.Add("@Usuario", req.Usuario);
                p.Add("@NumeroSeguroSocial", req.NumeroSeguroSocial); // SP aplica fallback a CER00007 si viene vacío
                p.Add("@TipoSangreId", req.TipoSangreId);

                p.Add("@ContactosEmergencia",
                    BuildContactosTvp(req.ContactosEmergencia)
                    .AsTableValuedParameter("dbo.TVP_ContactoEmergencia"));

                p.Add("@Alergias",
                    CommitCatalogAsync(req.AlergiasIds)
                    .AsTableValuedParameter("dbo.TVP_IdList"));

                p.Add("@Enfermedades",
                    CommitCatalogAsync(req.EnfermedadesIds)
                    .AsTableValuedParameter("dbo.TVP_IdList"));

                p.Add("@Discapacidades",
                    CommitCatalogAsync(req.DiscapacidadesIds)
                    .AsTableValuedParameter("dbo.TVP_IdList"));

                await cn.ExecuteAsync("dbo.SP_Salud_Commit", p, commandType: CommandType.StoredProcedure);

                response.isSuccess = true;
                response.code = 200;
                response.message = "Operación exitosa";
                response.desc = "Commit aplicado correctamente.";
                response.data = true;

                return response;
            }
            catch (SqlException ex)
            {
                // ex.Message ya trae el THROW con detalle
                response.isSuccess = false;
                response.code = 500;
                response.message = "Error SQL";
                response.desc = ex.Message; // <- aquí aterriza el detalle del SP
                response.data = false;
                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message = "Error";
                response.desc = ex.Message;
                response.data = false;
                return response;
            }
        }

        private DataTable CommitCatalogAsync(List<int> ids)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));

            if (ids == null || ids.Count == 0)
                return dt;

            foreach (var id in ids.Distinct())
                dt.Rows.Add(id);

            return dt;
        }

        private DataTable BuildContactosTvp(List<ContactoEmergenciaDto> contactos)
        {
            var dt = new DataTable();
            dt.Columns.Add("IdContacto", typeof(int));
            dt.Columns.Add("NombreCompleto", typeof(string));
            dt.Columns.Add("ParentescoId", typeof(int));
            dt.Columns.Add("Celular", typeof(string));

            if (contactos == null || contactos.Count == 0)
                return dt;

            foreach (var c in contactos)
            {
                var row = dt.NewRow();
                row["IdContacto"] = (object?)c.IdContacto ?? DBNull.Value;
                row["NombreCompleto"] = (c.NombreCompleto ?? "").Trim();
                row["ParentescoId"] = c.ParentescoId;
                row["Celular"] = (c.Celular ?? "").Trim();
                dt.Rows.Add(row);
            }

            return dt;
        }

        public async Task<ResponseModel<SaludGetResponse>> GetSaludAsync(string usuario, CancellationToken ct)
        {
            var response = new ResponseModel<SaludGetResponse>
            {
                isSuccess = false,
                code = 500,
                message = "Error",
                desc = "No fue posible obtener la información.",
                data = null
            };

            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync(ct);

                // 1) Salud base
                const string sqlSalud = @"
SELECT TOP 1
    NumeroSeguroSocial,
    TipoSangreId
FROM dbo.EMP_SALUD
WHERE Usuario = @Usuario AND Its_Active = 1;
";

                var salud = await cn.QueryFirstOrDefaultAsync<SaludGetResponse>(
                    new CommandDefinition(sqlSalud, new { Usuario = usuario }, cancellationToken: ct));

                // 🔴 SI NO EXISTE → 404
                if (salud == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message = "No encontrado";
                    response.desc = $"No existe información de salud activa para el empleado {usuario}.";
                    response.data = null;
                    return response;
                }

                var data = new SaludGetResponse
                {
                    NumeroSeguroSocial = salud.NumeroSeguroSocial,
                    TipoSangreId = salud.TipoSangreId
                };

                // 2) Contactos activos
                const string sqlContactos = @"
SELECT
    NombreCompleto,
    ParentescoId,
    Celular
FROM dbo.EMP_CONTACTO_EMERGENCIA
WHERE Usuario = @Usuario AND Its_Active = 1
ORDER BY Id DESC;
";

                var contactos = await cn.QueryAsync<SaludContactoResponse>(
                    new CommandDefinition(sqlContactos, new { Usuario = usuario }, cancellationToken: ct));

                data.Contactos = contactos?.ToList() ?? new List<SaludContactoResponse>();

                // 3) Alergias
                const string sqlAlergias = @"
SELECT AlergiaId
FROM dbo.EMP_ALERGIAS
WHERE Usuario = @Usuario AND Its_Active = 1;
";
                data.AlergiasIds = (await cn.QueryAsync<int>(
                    new CommandDefinition(sqlAlergias, new { Usuario = usuario }, cancellationToken: ct)))
                    .Distinct().ToList();

                // 4) Enfermedades
                const string sqlEnfermedades = @"
SELECT EnfermedadId
FROM dbo.EMP_ENFERMEDADES
WHERE Usuario = @Usuario AND Its_Active = 1;
";
                data.EnfermedadesIds = (await cn.QueryAsync<int>(
                    new CommandDefinition(sqlEnfermedades, new { Usuario = usuario }, cancellationToken: ct)))
                    .Distinct().ToList();

                // 5) Discapacidades
                const string sqlDiscapacidades = @"
SELECT DiscapacidadId
FROM dbo.EMP_DISCAPACIDADES
WHERE Usuario = @Usuario AND Its_Active = 1;
";
                data.DiscapacidadesIds = (await cn.QueryAsync<int>(
                    new CommandDefinition(sqlDiscapacidades, new { Usuario = usuario }, cancellationToken: ct)))
                    .Distinct().ToList();

                response.isSuccess = true;
                response.code = 200;
                response.message = "Operación exitosa";
                response.desc = "Consulta realizada correctamente.";
                response.data = data;

                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message = "Error SQL";
                response.desc = ex.Message;
                response.data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message = "Error";
                response.desc = ex.Message;
                response.data = null;
                return response;
            }
        }

    }
}
