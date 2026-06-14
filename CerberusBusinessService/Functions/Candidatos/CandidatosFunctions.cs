using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Candidatos;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text.RegularExpressions;

namespace CerberusBusinessService.Functions.Candidatos
{
    public class CandidatosFunctions
    {
        private readonly string _csCerberus;

        public CandidatosFunctions(IConfiguration config)
        {
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<ResponseModel<CommitDatosGeneralesCandidatoResponse>> CommitDatosGeneralesCandidato(
            CommitDatosGeneralesCandidatoRequest data, CancellationToken ct)
        {
            try
            {
                ValidarFormato(data);

                if (data.Puestos == null || !data.Puestos.Any())
                    throw new Exception("Debe seleccionar al menos un puesto.");

                var result = await EjecutarCommitDatosGeneralesCandidatoAsync(data);

                return new ResponseModel<CommitDatosGeneralesCandidatoResponse>
                {
                    isSuccess = true,
                    code = 200,
                    message = result.Mensaje,
                    data = result
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel<CommitDatosGeneralesCandidatoResponse>
                {
                    isSuccess = false,
                    code = 500,
                    message = ex.Message,
                    data = null
                };
            }
        }

        private void ValidarFormato(CommitDatosGeneralesCandidatoRequest data)
        {
            if (string.IsNullOrWhiteSpace(data.Nombres))
                throw new Exception("El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(data.ApellidoPaterno))
                throw new Exception("El apellido paterno es obligatorio.");

            if (!Regex.IsMatch(data.Curp, @"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$"))
                throw new Exception("CURP inválida.");

            if (!string.IsNullOrWhiteSpace(data.RFC) &&
                !Regex.IsMatch(data.RFC, @"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$"))
                throw new Exception("RFC inválido.");

            if (!string.IsNullOrWhiteSpace(data.CorreoElectronico) &&
                !Regex.IsMatch(data.CorreoElectronico, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new Exception("Correo electrónico inválido.");

            if (!Regex.IsMatch(data.UsuarioOperacion, @"^CER\d{5}$"))
                throw new Exception("UsuarioAlta inválido.");
        }

        private async Task<CommitDatosGeneralesCandidatoResponse> EjecutarCommitDatosGeneralesCandidatoAsync(
            CommitDatosGeneralesCandidatoRequest data)
        {
            using var conn = new SqlConnection(_csCerberus);

            var parameters = new DynamicParameters();

            parameters.Add("@ID", data.Id);
            parameters.Add("@Nombres", data.Nombres);
            parameters.Add("@ApellidoPaterno", data.ApellidoPaterno);
            parameters.Add("@ApellidoMaterno", data.ApellidoMaterno);
            parameters.Add("@FechaNacimiento", data.FechaNacimiento);
            parameters.Add("@SexoId", data.SexoId);
            parameters.Add("@Curp", data.Curp);
            parameters.Add("@EscolaridadId", data.EscolaridadId);
            parameters.Add("@EstadoCivilId", data.EstadoCivilId);
            parameters.Add("@RFC", data.RFC);
            parameters.Add("@Celular", data.Celular);
            parameters.Add("@Telefono", data.Telefono);
            parameters.Add("@CorreoElectronico", data.CorreoElectronico);
            parameters.Add("@NacionalidadId", data.NacionalidadId);
            parameters.Add("@UsuarioOperacion", data.UsuarioOperacion);

            parameters.Add(
                "@Puestos",
                CrearTVPPuestos(data.Puestos)
                    .AsTableValuedParameter("dbo.TVP_CandidatoPuesto"));

            return await conn.QueryFirstAsync<CommitDatosGeneralesCandidatoResponse>(
                "dbo.SP_CANDIDATO_DATOS_GENERALES_COMMIT",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        private DataTable CrearTVPPuestos(List<long> puestos)
        {
            var dt = new DataTable();
            dt.Columns.Add("PuestoId", typeof(long));

            foreach (var puesto in puestos.Distinct())
                dt.Rows.Add(puesto);

            return dt;
        }
        public async Task<ResponseModel<List<ListadoCandidatosResponse>>> ObtenerListadoCandidatos(CancellationToken ct)
        {
            try
            {
                using var conn = new SqlConnection(_csCerberus);

                var sql = @"
           SELECT
    C.ID AS Id,
    LTRIM(RTRIM(
        ISNULL(C.ApellidoPaterno, '') + ' ' +
        ISNULL(C.ApellidoMaterno, '') + ' ' +
        ISNULL(C.Nombres, '')
    )) AS NombreCompleto,
    ISNULL(NULLIF(C.Celular, ''), C.Telefono) AS Telefono,
    'Disponible' AS Fase,
    ISNULL((
        SELECT STRING_AGG(X.NombrePuesto, ', ')
        FROM
        (
            SELECT TOP (3)
                P.NOMBRE AS NombrePuesto
            FROM dbo.CandidatoPuesto CP2
            INNER JOIN dbo.CAT_PUESTOS P
                ON P.PuestoId = CP2.PuestoId
            WHERE CP2.CandidatoId = C.ID
            ORDER BY P.NOMBRE
        ) X
    ), '') AS Areas
FROM dbo.DatosGeneralesCandidato C
ORDER BY
    C.ApellidoPaterno,
    C.ApellidoMaterno,
    C.Nombres;
        ";

                var result = await conn.QueryAsync<ListadoCandidatosResponse>(sql);

                return new ResponseModel<List<ListadoCandidatosResponse>>
                {
                    isSuccess = true,
                    code = 200,
                    message = "Listado de candidatos obtenido correctamente.",
                    data = result.ToList()
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel<List<ListadoCandidatosResponse>>
                {
                    isSuccess = false,
                    code = 500,
                    message = ex.Message,
                    data = new List<ListadoCandidatosResponse>()
                };
            }
        }
    }
}