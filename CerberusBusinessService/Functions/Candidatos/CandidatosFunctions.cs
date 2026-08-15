using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Candidatos;
using CerberusBusinessService.Models.DTO.Empleados;
using CerberusBusinessService.Models.DTO.Empleados.Items;
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

        #region Datos Generales Candidato
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
WHERE C.Its_Active = 1
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

        public async Task<ResponseModel<ObtenerDatosGeneralesCandidatoResponse>> ObtenerDatosGeneralesCandidato(
    ObtenerDatosGeneralesCandidatoRequest request)
        {
            var response = new ResponseModel<ObtenerDatosGeneralesCandidatoResponse>
            {
                isSuccess = false,
                code = 400,
                message = "Error al obtener datos generales del candidato",
                desc = null,
                data = null
            };

            if (request == null || request.Id <= 0)
            {
                response.message = "El Id del candidato es obligatorio.";
                return response;
            }

            try
            {
                using var conn = new SqlConnection(_csCerberus);

                const string sqlCandidato = @"
            SELECT TOP 1
                ID AS Id,
                Nombres,
                ApellidoPaterno,
                ApellidoMaterno,
                FechaNacimiento,
                sexoId AS SexoId,
                Curp,
                EscolaridadId,
                EstadoCivilId,
                RFC,
                Celular,
                Telefono,
                CorreoElectronico,
                NacionalidadId,
                UsuarioAlta AS UsuarioOperacion
            FROM dbo.DatosGeneralesCandidato
            WHERE ID = @Id
              AND ISNULL(Its_Active, 1) = 1;";

                var candidato = await conn.QueryFirstOrDefaultAsync<ObtenerDatosGeneralesCandidatoResponse>(
                    sqlCandidato,
                    new { request.Id });

                if (candidato == null)
                {
                    response.code = 404;
                    response.message = "Candidato no encontrado.";
                    response.desc = "No existe un candidato activo con el Id especificado.";
                    return response;
                }

                const string sqlPuestos = @"
            SELECT PuestoId
            FROM dbo.CandidatoPuesto
            WHERE CandidatoId = @Id
            ORDER BY PuestoId;";

                var puestos = await conn.QueryAsync<long>(
                    sqlPuestos,
                    new { request.Id });

                candidato.Puestos = puestos.ToList();

                response.isSuccess = true;
                response.code = 200;
                response.message = "Datos generales del candidato obtenidos correctamente.";
                response.desc = "Consulta realizada correctamente.";
                response.data = candidato;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message = "Error al consultar datos generales del candidato.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }
        #endregion

        #region Salud Candidato
        public async Task<ResponseModel<bool>> CommitSaludCandidatoAsync(
            int candidatoId,
            SaludCommitRequest req, CancellationToken ct)
        {
            var response = new ResponseModel<bool>
            {
                isSuccess = false,
                code = 500,
                message = "Error",
                desc = "No fue posible aplicar el commit.",
                data = false
            };

            if (candidatoId <= 0)
            {
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "El CandidatoId es obligatorio.";
                return response;
            }

            req.ContactosEmergencia ??= new List<ContactoEmergenciaDto>();
            req.AlergiasIds ??= new List<int>();
            req.EnfermedadesIds ??= new List<int>();
            req.DiscapacidadesIds ??= new List<int>();

            try
            {
                using var cn = new SqlConnection(_csCerberus);
                await cn.OpenAsync();

                var p = new DynamicParameters();

                p.Add("@CandidatoId", candidatoId);
                p.Add("@NumeroSeguroSocial", req.NumeroSeguroSocial);
                p.Add("@TipoSangreId", req.TipoSangreId);

                p.Add("@ContactosEmergencia",
                    BuildContactosTvp(req.ContactosEmergencia)
                        .AsTableValuedParameter("dbo.TVP_ContactoEmergencia"));

                p.Add("@Alergias",
                    BuildIdsTvp(req.AlergiasIds)
                        .AsTableValuedParameter("dbo.TVP_IdList"));

                p.Add("@Enfermedades",
                    BuildIdsTvp(req.EnfermedadesIds)
                        .AsTableValuedParameter("dbo.TVP_IdList"));

                p.Add("@Discapacidades",
                    BuildIdsTvp(req.DiscapacidadesIds)
                        .AsTableValuedParameter("dbo.TVP_IdList"));

                await cn.ExecuteAsync(
                    "dbo.SP_CAN_Salud_Commit",
                    p,
                    commandType: CommandType.StoredProcedure);

                response.isSuccess = true;
                response.code = 200;
                response.message = "Operación exitosa";
                response.desc = "Commit aplicado correctamente.";
                response.data = true;

                return response;
            }
            catch (SqlException ex)
            {
                response.message = "Error SQL";
                response.desc = ex.Message;
                return response;
            }
            catch (Exception ex)
            {
                response.message = "Error";
                response.desc = ex.Message;
                return response;
            }
        }

        private DataTable BuildIdsTvp(List<int> ids)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));

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

        public async Task<ResponseModel<SaludGetResponse>> GetSaludAsync(string CandidatoId, CancellationToken ct)
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
                using var cn = new SqlConnection(_csCerberus);
                await cn.OpenAsync(ct);

                // 1) Salud base
                const string sqlSalud = @"
                    SELECT TOP 1
                        NumeroSeguroSocial,
                        TipoSangreId
                    FROM dbo.CAN_SALUD
                    WHERE CandidatoId = @candidatoId AND Its_Active = 1;
                    ";

                var salud = await cn.QueryFirstOrDefaultAsync<SaludGetResponse>(
                    new CommandDefinition(sqlSalud, new { candidatoId = CandidatoId }, cancellationToken: ct));

                // 🔴 SI NO EXISTE → 404
                if (salud == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message = "No encontrado";
                    response.desc = $"No existe información de salud activa para el candidato {CandidatoId}.";
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
FROM dbo.CAN_CONTACTO_EMERGENCIA
WHERE CandidatoId = @CandidatoId AND Its_Active = 1
ORDER BY Id DESC;
";

                var contactos = await cn.QueryAsync<SaludContactoResponse>(
                    new CommandDefinition(sqlContactos, new { CandidatoId = CandidatoId }, cancellationToken: ct));

                data.Contactos = contactos?.ToList() ?? new List<SaludContactoResponse>();

                // 3) Alergias
                const string sqlAlergias = @"
SELECT AlergiaId
FROM dbo.CAN_ALERGIAS
WHERE CandidatoId = @CandidatoId AND Its_Active = 1;
";
                data.AlergiasIds = (await cn.QueryAsync<int>(
                    new CommandDefinition(sqlAlergias, new { CandidatoId = CandidatoId }, cancellationToken: ct)))
                    .Distinct().ToList();

                // 4) Enfermedades
                const string sqlEnfermedades = @"
SELECT EnfermedadId
FROM dbo.CAN_ENFERMEDADES
WHERE CandidatoId = @CandidatoId AND Its_Active = 1;
";
                data.EnfermedadesIds = (await cn.QueryAsync<int>(
                    new CommandDefinition(sqlEnfermedades, new { CandidatoId = CandidatoId }, cancellationToken: ct)))
                    .Distinct().ToList();

                // 5) Discapacidades
                const string sqlDiscapacidades = @"
SELECT DiscapacidadId
FROM dbo.CAN_DISCAPACIDADES
WHERE CandidatoId = @CandidatoId AND Its_Active = 1;
";
                data.DiscapacidadesIds = (await cn.QueryAsync<int>(
                    new CommandDefinition(sqlDiscapacidades, new { CandidatoId = CandidatoId }, cancellationToken: ct)))
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
        #endregion

        #region Domicilio Candidato
        public async Task<ResponseModel<string>> AltaDomiciliosCandidato(
         AltaDomicilioCandidatoRequest data)
        {
            var response = new ResponseModel<string>
            {
                isSuccess = false,
                code = 400,
                message = "Error al registrar domicilios",
                desc = null,
                data = null
            };

            if (data == null)
            {
                response.message = "Request vacío";
                return response;
            }

            if (data.CandidatoId <= 0)
            {
                response.message = "CandidatoId es obligatorio";
                return response;
            }

            if (data.domicilios == null || data.domicilios.Count == 0)
            {
                response.message = "Debe enviar al menos un domicilio";
                return response;
            }

            if (data.domicilios.Count(d => d.Its_Principal) > 1)
            {
                response.message = "Solo puede existir un domicilio principal por candidato";
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

            try
            {
                using var conn = new SqlConnection(_csCerberus);
                await conn.OpenAsync();

                using var tx = conn.BeginTransaction();

                var candidatoExiste = await conn.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1)
                      FROM dbo.DatosGeneralesCandidato
                      WHERE ID = @CandidatoId;",
                    new { data.CandidatoId },
                    tx);

                if (candidatoExiste == 0)
                {
                    tx.Rollback();
                    response.message = "El candidato no existe";
                    return response;
                }

                var nextId = await conn.ExecuteScalarAsync<int>(
                    @"SELECT ISNULL(MAX(IDdomicilio), 0) + 1
                      FROM dbo.Candidatos_Domicilios WITH (UPDLOCK, HOLDLOCK)
                      WHERE CandidatoId = @CandidatoId;",
                    new { data.CandidatoId },
                    tx);

                if (data.domicilios.Any(d => d.Its_Principal))
                {
                    await conn.ExecuteAsync(
                        @"UPDATE dbo.Candidatos_Domicilios
                          SET Its_Principal = 0
                          WHERE CandidatoId = @CandidatoId
                            AND Its_Active = 1;",
                        new { data.CandidatoId },
                        tx);
                }

                const string insertSql = @"
                    INSERT INTO dbo.Candidatos_Domicilios
                    (
                        CandidatoId,
                        IDdomicilio,
                        Calle,
                        Numero_Interior,
                        Numero_Exterior,
                        Codigo_Postal,
                        EstadoID,
                        MunicipioID,
                        ColoniaID,
                        Its_Principal
                    )
                    VALUES
                    (
                        @CandidatoId,
                        @IDdomicilio,
                        @Calle,
                        @Numero_Interior,
                        @Numero_Exterior,
                        @Codigo_Postal,
                        @EstadoID,
                        @MunicipioID,
                        @ColoniaID,
                        @Its_Principal
                    );";

                int totalInsertados = 0;

                for (int i = 0; i < data.domicilios.Count; i++)
                {
                    var d = data.domicilios[i];

                    totalInsertados += await conn.ExecuteAsync(
                        insertSql,
                        new
                        {
                            data.CandidatoId,
                            IDdomicilio = nextId + i,
                            Calle = d.Calle.Trim(),
                            Numero_Interior = string.IsNullOrWhiteSpace(d.Numero_Interior)
                                ? null
                                : d.Numero_Interior.Trim(),
                            Numero_Exterior = d.Numero_Exterior.Trim(),
                            Codigo_Postal = string.IsNullOrWhiteSpace(d.Codigo_Postal)
                                ? null
                                : d.Codigo_Postal.Trim(),
                            d.EstadoID,
                            d.MunicipioID,
                            d.ColoniaID,
                            d.Its_Principal
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

        public async Task<ResponseModel<string>> ActualizarDomicilioPrincipalCandidato(
            ActualizaDomicilioPrincipalCandidatoRequest data)
        {
            var response = new ResponseModel<string>
            {
                isSuccess = false,
                code = 400,
                message = "Error al actualizar domicilio principal",
                desc = null,
                data = null
            };

            if (data == null)
            {
                response.message = "Request vacío";
                return response;
            }

            if (data.CandidatoId <= 0)
            {
                response.message = "CandidatoId es obligatorio";
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

                var exists = await conn.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1)
                      FROM dbo.Candidatos_Domicilios
                      WHERE CandidatoId = @CandidatoId
                        AND IDdomicilio = @IDdomicilio
                        AND Its_Active = 1;",
                    new { data.CandidatoId, data.IDdomicilio },
                    tx);

                if (exists == 0)
                {
                    tx.Rollback();
                    response.message = "El domicilio no existe o está inactivo";
                    return response;
                }

                await conn.ExecuteAsync(
                    @"UPDATE dbo.Candidatos_Domicilios
                      SET Its_Principal = 0
                      WHERE CandidatoId = @CandidatoId
                        AND Its_Active = 1;",
                    new { data.CandidatoId },
                    tx);

                await conn.ExecuteAsync(
                    @"UPDATE dbo.Candidatos_Domicilios
                      SET Its_Principal = 1
                      WHERE CandidatoId = @CandidatoId
                        AND IDdomicilio = @IDdomicilio
                        AND Its_Active = 1;",
                    new { data.CandidatoId, data.IDdomicilio },
                    tx);

                tx.Commit();

                response.isSuccess = true;
                response.code = 200;
                response.message = "Domicilio principal actualizado correctamente";
                response.data = "OK";
                return response;
            }
            catch (Exception ex)
            {
                response.code = 500;
                response.message = "Error al actualizar domicilio principal";
                response.desc = ex.Message;
                return response;
            }
        }

        public async Task<ResponseModel<string>> EliminarDomicilioCandidato(
            EliminadoDomicilioCandidatoRequest data)
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

            if (data.CandidatoId <= 0)
            {
                response.message = "CandidatoId es obligatorio";
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

                var dom = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT Its_Principal, Its_Active
                      FROM dbo.Candidatos_Domicilios
                      WHERE CandidatoId = @CandidatoId
                        AND IDdomicilio = @IDdomicilio;",
                    new { data.CandidatoId, data.IDdomicilio },
                    tx);

                if (dom == null)
                {
                    tx.Rollback();
                    response.message = "El domicilio no existe";
                    return response;
                }

                bool itsPrincipal = (bool)dom.Its_Principal;
                bool itsActive = (bool)dom.Its_Active;

                if (!itsActive)
                {
                    tx.Rollback();
                    response.message = "El domicilio ya se encuentra eliminado";
                    return response;
                }

                var activos = await conn.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1)
                      FROM dbo.Candidatos_Domicilios
                      WHERE CandidatoId = @CandidatoId
                        AND Its_Active = 1;",
                    new { data.CandidatoId },
                    tx);

                if (activos <= 1)
                {
                    tx.Rollback();
                    response.message = "No es posible eliminar el último domicilio activo del candidato.";
                    return response;
                }

                await conn.ExecuteAsync(
                    @"UPDATE dbo.Candidatos_Domicilios
                      SET Its_Active = 0,
                          Its_Principal = 0
                      WHERE CandidatoId = @CandidatoId
                        AND IDdomicilio = @IDdomicilio;",
                    new { data.CandidatoId, data.IDdomicilio },
                    tx);

                if (itsPrincipal)
                {
                    var nuevoPrincipalId = await conn.ExecuteScalarAsync<int?>(
                        @"SELECT TOP (1) IDdomicilio
                          FROM dbo.Candidatos_Domicilios
                          WHERE CandidatoId = @CandidatoId
                            AND Its_Active = 1
                          ORDER BY IDdomicilio ASC;",
                        new { data.CandidatoId },
                        tx);

                    if (nuevoPrincipalId.HasValue)
                    {
                        await conn.ExecuteAsync(
                            @"UPDATE dbo.Candidatos_Domicilios
                              SET Its_Principal = 1
                              WHERE CandidatoId = @CandidatoId
                                AND IDdomicilio = @IDdomicilio;",
                            new
                            {
                                data.CandidatoId,
                                IDdomicilio = nuevoPrincipalId.Value
                            },
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

        public async Task<ResponseModel<List<ListadoDomiciliosResponse>>> ListadoDomiciliosCandidato(
            int candidatoId)
        {
            var response = new ResponseModel<List<ListadoDomiciliosResponse>>
            {
                isSuccess = false,
                code = 400,
                message = "Error al obtener domicilios",
                desc = null,
                data = null
            };

            if (candidatoId <= 0)
            {
                response.message = "El parámetro candidatoId es obligatorio";
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
                    FROM dbo.Candidatos_Domicilios
                    WHERE CandidatoId = @CandidatoId
                      AND Its_Active = 1
                    ORDER BY Its_Principal DESC, IDdomicilio ASC;";

                var result = (await conn.QueryAsync<ListadoDomiciliosResponse>(
                    sql,
                    new { CandidatoId = candidatoId }
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
        #endregion
    }
}