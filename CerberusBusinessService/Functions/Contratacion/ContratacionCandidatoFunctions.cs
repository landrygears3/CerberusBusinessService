using Amazon.S3;
using Amazon.S3.Model;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Contratacion;
using CerberusBusinessService.Models.DTO.Empleados;
using CerberusBusinessService.Models.R2;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace CerberusBusinessService.Functions.Contratacion
{
    public class ContratacionCandidatoFunctions
    {
        private const int FileTypeVideoEntrevista = 16;
        private const string ModuloContratacion = "empleados";
        private const string CategoriaEntrevista = "entrevista";

        private readonly string _csCerberus;
        private readonly HttpClient _httpClient;
        private readonly AltaEmpleadoFuncions _altaEmpleadoFunctions;
        private readonly IAmazonS3 _s3;
        private readonly R2Settings _settings;

        public ContratacionCandidatoFunctions(
            IConfiguration config,
            HttpClient httpClient,
            AltaEmpleadoFuncions altaEmpleadoFunctions,
            IAmazonS3 s3,
            IOptions<R2Settings> settings)
        {
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
            _httpClient = httpClient;
            _altaEmpleadoFunctions = altaEmpleadoFunctions;
            _s3 = s3;
            _settings = settings.Value;
        }

        public async Task<ResponseModel<AuthRegisterResponse>> ContratarCandidatoAsync(
            ContratarCandidatoRequest request,
            string usuarioOperacion,
            string token)
        {
            var response = new ResponseModel<AuthRegisterResponse>
            {
                isSuccess = false,
                code = 500,
                message = "Error al contratar candidato",
                data = null
            };

            var archivosNuevosR2 = new List<string>();
            var archivosCandidatoOriginales = new List<string>();

            try
            {
                if (request == null)
                    throw new Exception("Request vacío.");

                if (request.CandidatoId <= 0)
                    throw new Exception("CandidatoId inválido.");

                if (request.VacanteId <= 0)
                    throw new Exception("VacanteId inválido.");

                if (string.IsNullOrWhiteSpace(usuarioOperacion))
                    throw new Exception("No fue posible obtener el usuario de operación.");

                if (string.IsNullOrWhiteSpace(request.PasswordConfirmacion))
                    throw new Exception("La contraseña de confirmación es obligatoria.");

                if (request.EntrevistaArchivo == null || request.EntrevistaArchivo.Length == 0)
                    throw new Exception("El video de entrevista es obligatorio.");

                var passwordOk = await ValidarPasswordAsync(usuarioOperacion, request.PasswordConfirmacion, token);

                if (!passwordOk)
                    throw new Exception("La contraseña del usuario que realiza la contratación no es correcta.");

                var candidato = await ObtenerCandidatoAsync(request.CandidatoId);

                if (candidato == null)
                    throw new Exception("El candidato no existe o ya no está activo.");

                var vacante = await ObtenerVacanteAsync(request.VacanteId);

                if (vacante == null)
                    throw new Exception("La vacante no existe.");

                if (!vacante.ACTIVO || vacante.ESTATUS_ID != 1)
                    throw new Exception("La vacante no está disponible.");

                var empleadoRequest = new EmpleadoAltaGeneralesRequest
                {
                    Nombres = candidato.Nombres,
                    ApellidoPaterno = candidato.ApellidoPaterno,
                    ApellidoMaterno = candidato.ApellidoMaterno,
                    FechaNacimiento = candidato.FechaNacimiento,
                    sexoId = candidato.SexoId,
                    Curp = candidato.Curp,
                    EscolaridadId = candidato.EscolaridadId,
                    EstadoCivilId = candidato.EstadoCivilId,
                    RFC = candidato.RFC,
                    Celular = candidato.Celular,
                    Telefono = candidato.Telefono,
                    CorreoElectronico = candidato.CorreoElectronico,
                    NacionalidadId = candidato.NacionalidadId,
                    origenVacanteId = request.VacanteId,
                    UsuarioAlta = usuarioOperacion,
                    FechaCreacion = DateTime.Now,
                    Departamento = vacante.DEPARTAMENTOID
                };

                var altaEmpleado = await _altaEmpleadoFunctions.AltaEmpleadoGenerales(empleadoRequest);

                if (altaEmpleado == null || !altaEmpleado.isSuccess || altaEmpleado.data == null)
                    throw new Exception(altaEmpleado?.message ?? "No fue posible dar de alta al empleado.");

                var numeroUsuario = altaEmpleado.data.numeroUsuario;

                if (string.IsNullOrWhiteSpace(numeroUsuario))
                    throw new Exception("El alta de empleado no regresó NumeroUsuario.");

                var videoRuta = await SubirVideoEntrevistaAsync(request.EntrevistaArchivo, numeroUsuario);
                archivosNuevosR2.Add(videoRuta);

                await MigrarInformacionCandidatoAsync(
                    request.CandidatoId,
                    request.VacanteId,
                    numeroUsuario,
                    usuarioOperacion,
                    request.EntrevistaArchivo,
                    videoRuta,
                    archivosNuevosR2,
                    archivosCandidatoOriginales);

                foreach (var oldKey in archivosCandidatoOriginales)
                {
                    await _s3.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = _settings.BucketName,
                        Key = oldKey
                    });
                }

                response.isSuccess = true;
                response.code = 200;
                response.message = "Candidato contratado correctamente.";
                response.data = altaEmpleado.data;

                return response;
            }
            catch (Exception ex)
            {
                foreach (var key in archivosNuevosR2)
                {
                    try
                    {
                        await _s3.DeleteObjectAsync(new DeleteObjectRequest
                        {
                            BucketName = _settings.BucketName,
                            Key = key
                        });
                    }
                    catch { }
                }

                response.isSuccess = false;
                response.code = 500;
                response.message = ex.Message;
                response.data = null;

                return response;
            }
        }

        private async Task<bool> ValidarPasswordAsync(string numeroUsuario, string password, string token)
        {
            var body = new
            {
                NumeroUsuario = numeroUsuario,
                Password = password
            };
            _httpClient.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Bearer",
        token);
            var result = await _httpClient.PostAsJsonAsync("api/Auth/validate-password", body);

            if (!result.IsSuccessStatusCode)
                return false;

            var response = await result.Content.ReadFromJsonAsync<ResponseModel<bool>>();

            return response != null && response.isSuccess && response.data;
        }

        private async Task<CandidatoDatos?> ObtenerCandidatoAsync(int candidatoId)
        {
            using var conn = new SqlConnection(_csCerberus);

            const string sql = @"
                SELECT TOP 1
                    ID,
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
                    NacionalidadId
                FROM dbo.DatosGeneralesCandidato
                WHERE ID = @CandidatoId
                  AND ISNULL(Its_Active, 1) = 1;";

            return await conn.QueryFirstOrDefaultAsync<CandidatoDatos>(
                sql,
                new { CandidatoId = candidatoId });
        }

        private async Task<VacanteDatos?> ObtenerVacanteAsync(int vacanteId)
        {
            using var conn = new SqlConnection(_csCerberus);

            const string sql = @"
                SELECT TOP 1
                    VACANTE_ID,
                    PUESTO_ID,
                    ESTATUS_ID,
                    ACTIVO,
                    DEPARTAMENTOID
                FROM dbo.Vacantes
                WHERE VACANTE_ID = @VacanteId;";

            return await conn.QueryFirstOrDefaultAsync<VacanteDatos>(
                sql,
                new { VacanteId = vacanteId });
        }

        private async Task MigrarInformacionCandidatoAsync(
            int candidatoId,
            int vacanteId,
            string numeroUsuario,
            string usuarioOperacion,
            IFormFile entrevistaArchivo,
            string videoRuta,
            List<string> archivosNuevosR2,
            List<string> archivosCandidatoOriginales)
        {
            using var conn = new SqlConnection(_csCerberus);
            await conn.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                await MigrarSaludAsync(conn, tx, candidatoId, numeroUsuario);
                await MigrarDomiciliosAsync(conn, tx, candidatoId, numeroUsuario);

                var archivos = await ObtenerArchivosCandidatoAsync(conn, tx, candidatoId);

                foreach (var archivo in archivos)
                {
                    var nuevaRuta = await CopiarArchivoCandidatoAEmpleadoAsync(
                        archivo.RutaArchivo,
                        numeroUsuario);

                    archivosNuevosR2.Add(nuevaRuta);
                    archivosCandidatoOriginales.Add(archivo.RutaArchivo);

                    await InsertarArchivoEmpleadoAsync(conn, tx, numeroUsuario, archivo, nuevaRuta);
                }

                await InsertarVideoEntrevistaEmpleadoAsync(
                    conn,
                    tx,
                    numeroUsuario,
                    entrevistaArchivo,
                    videoRuta);

                await EliminarArchivosCandidatoAsync(conn, tx, candidatoId);
                await ActualizarVacanteCubiertaAsync(conn, tx, vacanteId, usuarioOperacion);
                await BajaLogicaCandidatoAsync(conn, tx, candidatoId);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private async Task MigrarSaludAsync(SqlConnection conn, SqlTransaction tx, int candidatoId, string numeroUsuario)
        {
            await conn.ExecuteAsync(@"
        INSERT INTO dbo.EMP_SALUD
        (Usuario, NumeroSeguroSocial, TipoSangreId, Its_Active, CreatedAt, UpdatedAt)
        SELECT 
            @NumeroUsuario,
            NumeroSeguroSocial,
            TipoSangreId,
            ISNULL(Its_Active, 1),
            ISNULL(CreatedAt, GETDATE()),
            ISNULL(UpdatedAt, GETDATE())
        FROM dbo.CAN_SALUD
        WHERE CandidatoId = @CandidatoId;",
                new { CandidatoId = candidatoId, NumeroUsuario = numeroUsuario }, tx);

            await conn.ExecuteAsync(@"
        INSERT INTO dbo.EMP_CONTACTO_EMERGENCIA
        (Usuario, NombreCompleto, ParentescoId, Celular, Its_Active, CreatedAt, UpdatedAt)
        SELECT 
            @NumeroUsuario,
            NombreCompleto,
            ParentescoId,
            Celular,
            ISNULL(Its_Active, 1),
            ISNULL(CreatedAt, GETDATE()),
            ISNULL(UpdatedAt, GETDATE())
        FROM dbo.CAN_CONTACTO_EMERGENCIA
        WHERE CandidatoId = @CandidatoId;",
                new { CandidatoId = candidatoId, NumeroUsuario = numeroUsuario }, tx);

            await conn.ExecuteAsync(@"
        INSERT INTO dbo.EMP_ALERGIAS
        (Usuario, AlergiaId, Its_Active, CreatedAt, UpdatedAt)
        SELECT 
            @NumeroUsuario,
            AlergiaId,
            ISNULL(Its_Active, 1),
            ISNULL(CreatedAt, GETDATE()),
            ISNULL(UpdatedAt, GETDATE())
        FROM dbo.CAN_ALERGIAS
        WHERE CandidatoId = @CandidatoId;",
                new { CandidatoId = candidatoId, NumeroUsuario = numeroUsuario }, tx);

            await conn.ExecuteAsync(@"
        INSERT INTO dbo.EMP_ENFERMEDADES
        (Usuario, EnfermedadId, Its_Active, CreatedAt, UpdatedAt)
        SELECT 
            @NumeroUsuario,
            EnfermedadId,
            ISNULL(Its_Active, 1),
            ISNULL(CreatedAt, GETDATE()),
            ISNULL(UpdatedAt, GETDATE())
        FROM dbo.CAN_ENFERMEDADES
        WHERE CandidatoId = @CandidatoId;",
                new { CandidatoId = candidatoId, NumeroUsuario = numeroUsuario }, tx);

            await conn.ExecuteAsync(@"
        INSERT INTO dbo.EMP_DISCAPACIDADES
        (Usuario, DiscapacidadId, Its_Active, CreatedAt, UpdatedAt)
        SELECT 
            @NumeroUsuario,
            DiscapacidadId,
            ISNULL(Its_Active, 1),
            ISNULL(CreatedAt, GETDATE()),
            ISNULL(UpdatedAt, GETDATE())
        FROM dbo.CAN_DISCAPACIDADES
        WHERE CandidatoId = @CandidatoId;",
                new { CandidatoId = candidatoId, NumeroUsuario = numeroUsuario }, tx);

            await conn.ExecuteAsync("DELETE FROM dbo.CAN_DISCAPACIDADES WHERE CandidatoId = @CandidatoId;", new { CandidatoId = candidatoId }, tx);
            await conn.ExecuteAsync("DELETE FROM dbo.CAN_ENFERMEDADES WHERE CandidatoId = @CandidatoId;", new { CandidatoId = candidatoId }, tx);
            await conn.ExecuteAsync("DELETE FROM dbo.CAN_ALERGIAS WHERE CandidatoId = @CandidatoId;", new { CandidatoId = candidatoId }, tx);
            await conn.ExecuteAsync("DELETE FROM dbo.CAN_CONTACTO_EMERGENCIA WHERE CandidatoId = @CandidatoId;", new { CandidatoId = candidatoId }, tx);
            await conn.ExecuteAsync("DELETE FROM dbo.CAN_SALUD WHERE CandidatoId = @CandidatoId;", new { CandidatoId = candidatoId }, tx);
        }

        private async Task MigrarDomiciliosAsync(SqlConnection conn, SqlTransaction tx, int candidatoId, string numeroUsuario)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO dbo.Usuarios_Domicilios
                (
                    Usuario,
                    IDdomicilio,
                    Calle,
                    Numero_Interior,
                    Numero_Exterior,
                    Codigo_Postal,
                    EstadoID,
                    MunicipioID,
                    ColoniaID,
                    Its_Principal,
                    Its_Active
                )
                SELECT
                    @NumeroUsuario,
                    IDdomicilio,
                    Calle,
                    Numero_Interior,
                    Numero_Exterior,
                    Codigo_Postal,
                    EstadoID,
                    MunicipioID,
                    ColoniaID,
                    Its_Principal,
                    Its_Active
                FROM dbo.Candidatos_Domicilios
                WHERE CandidatoId = @CandidatoId;",
                new { CandidatoId = candidatoId, NumeroUsuario = numeroUsuario }, tx);

            await conn.ExecuteAsync(
                "DELETE FROM dbo.Candidatos_Domicilios WHERE CandidatoId = @CandidatoId;",
                new { CandidatoId = candidatoId }, tx);
        }

        private async Task<List<CandidatoArchivoData>> ObtenerArchivosCandidatoAsync(
            SqlConnection conn,
            SqlTransaction tx,
            int candidatoId)
        {
            const string sql = @"
                SELECT
                    IdArchivo,
                    CandidatoId,
                    Modulo,
                    FileNamed,
                    FileType,
                    RutaArchivo,
                    FechaAlta,
                    FechaVencimiento,
                    FechaExpedicion
                FROM dbo.CandidatoArchivos
                WHERE CandidatoId = @CandidatoId;";

            var result = await conn.QueryAsync<CandidatoArchivoData>(
                sql,
                new { CandidatoId = candidatoId },
                tx);

            return result.ToList();
        }

        private async Task<string> CopiarArchivoCandidatoAEmpleadoAsync(string rutaActual, string numeroUsuario)
        {
            var partes = rutaActual.Split('/', StringSplitOptions.RemoveEmptyEntries);

            string modulo = partes.Length >= 4 ? partes[2] : "general";
            string categoria = partes.Length >= 4 ? partes[3] : "general";
            string fileName = partes.Last();

            string nuevaRuta = $"empleados/{numeroUsuario}/{categoria}/{fileName}";

            await _s3.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = _settings.BucketName,
                SourceKey = rutaActual,
                DestinationBucket = _settings.BucketName,
                DestinationKey = nuevaRuta
            });

            return nuevaRuta;
        }

        private async Task<string> SubirVideoEntrevistaAsync(IFormFile archivo, string numeroUsuario)
        {
            string folder = $"empleados/{numeroUsuario}/{CategoriaEntrevista}";

            string uuid = Guid.NewGuid().ToString();
            string safeFileName = Path.GetFileName(archivo.FileName);
            string key = $"{folder}/{uuid}_{safeFileName}";

            using var stream = archivo.OpenReadStream();

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = string.IsNullOrWhiteSpace(archivo.ContentType)
                    ? "application/octet-stream"
                    : archivo.ContentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                AutoCloseStream = false
            };

            await _s3.PutObjectAsync(request);

            return key;
        }

        private async Task InsertarArchivoEmpleadoAsync(
            SqlConnection conn,
            SqlTransaction tx,
            string numeroUsuario,
            CandidatoArchivoData archivo,
            string nuevaRuta)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO dbo.EmpleadoArchivos
                (
                    NumeroUsuario,
                    Modulo,
                    FileNamed,
                    FileType,
                    RutaArchivo,
                    FechaAlta,
                    FechaVencimiento,
                    FechaExpedicion
                )
                VALUES
                (
                    @NumeroUsuario,
                    @Modulo,
                    @FileNamed,
                    @FileType,
                    @RutaArchivo,
                    @FechaAlta,
                    @FechaVencimiento,
                    @FechaExpedicion
                );",
                new
                {
                    NumeroUsuario = numeroUsuario,
                    archivo.Modulo,
                    archivo.FileNamed,
                    archivo.FileType,
                    RutaArchivo = nuevaRuta,
                    archivo.FechaAlta,
                    archivo.FechaVencimiento,
                    archivo.FechaExpedicion
                }, tx);
        }

        private async Task InsertarVideoEntrevistaEmpleadoAsync(
            SqlConnection conn,
            SqlTransaction tx,
            string numeroUsuario,
            IFormFile archivo,
            string rutaArchivo)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO dbo.EmpleadoArchivos
                (
                    NumeroUsuario,
                    Modulo,
                    FileNamed,
                    FileType,
                    RutaArchivo,
                    FechaAlta
                )
                VALUES
                (
                    @NumeroUsuario,
                    @Modulo,
                    @FileNamed,
                    @FileType,
                    @RutaArchivo,
                    GETDATE()
                );",
                new
                {
                    NumeroUsuario = numeroUsuario,
                    Modulo = ModuloContratacion,
                    FileNamed = Path.GetFileName(archivo.FileName),
                    FileType = FileTypeVideoEntrevista,
                    RutaArchivo = rutaArchivo
                }, tx);
        }

        private async Task EliminarArchivosCandidatoAsync(SqlConnection conn, SqlTransaction tx, int candidatoId)
        {
            await conn.ExecuteAsync(
                "DELETE FROM dbo.CandidatoArchivos WHERE CandidatoId = @CandidatoId;",
                new { CandidatoId = candidatoId }, tx);
        }

        private async Task ActualizarVacanteCubiertaAsync(
            SqlConnection conn,
            SqlTransaction tx,
            int vacanteId,
            string usuarioOperacion)
        {
            await conn.ExecuteAsync(@"
                UPDATE dbo.Vacantes
                SET
                    ESTATUS_ID = 3,
                    ACTIVO = 0,
                    FECHA_MODIFICACION = GETDATE(),
                    USUARIO_MODIFICACION = @UsuarioOperacion
                WHERE VACANTE_ID = @VacanteId;",
                new { VacanteId = vacanteId, UsuarioOperacion = usuarioOperacion }, tx);
        }

        private async Task BajaLogicaCandidatoAsync(SqlConnection conn, SqlTransaction tx, int candidatoId)
        {
            await conn.ExecuteAsync(@"
                UPDATE dbo.DatosGeneralesCandidato
                SET
                    Its_Active = 0,
                    FechaBaja = GETDATE(),
                    MotivoBaja = 'CONTRATADO'
                WHERE ID = @CandidatoId;",
                new { CandidatoId = candidatoId }, tx);
        }
    }
}
