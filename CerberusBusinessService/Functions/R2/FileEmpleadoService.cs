using Amazon.S3;
using Amazon.S3.Model;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.R2;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace CerberusBusinessService.Functions.R2
{

    public class FileEmpleadoService
    {
        private readonly IAmazonS3 _s3;
        private readonly R2Settings _settings;
        private readonly string _csCerberus;

        public FileEmpleadoService(IAmazonS3 s3, IOptions<R2Settings> settings, IConfiguration config)
        {
            _s3 = s3;
            _settings = settings.Value;
            _csCerberus = config.GetConnectionString("DefaultConnection")!;
        }
        public async Task<ResponseModel<List<ArchivoPorTipoDTO>>> GetFilesByEmpleadoModulo(string numeroUsuario, string modulo)
        {
            try
            {
                const string sql = @"
            SELECT 
                IdArchivo,
                FileNamed,
                FileType,
                FechaAlta
            FROM dbo.EmpleadoArchivos
            WHERE NumeroUsuario = @NumeroUsuario
              AND Modulo = @Modulo
            ORDER BY FileType, FechaAlta DESC;";

                var data = new List<(int IdArchivo, string FileName, int FileType, DateTime Fecha)>();

                using (SqlConnection conn = new SqlConnection(_csCerberus))
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@NumeroUsuario", SqlDbType.VarChar, 50).Value = numeroUsuario;
                    cmd.Parameters.Add("@Modulo", SqlDbType.VarChar, 100).Value = modulo;

                    await conn.OpenAsync();

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        data.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetInt32(2),
                            reader.GetDateTime(3)
                        ));
                    }
                }

                var result = data
                    .GroupBy(x => x.FileType)
                    .Select(g => new ArchivoPorTipoDTO
                    {
                        FileType = g.Key,
                        Archivos = g.Select(a => new ArchivoDTO
                        {
                            IdArchivo = a.IdArchivo,
                            FileName = a.FileName,
                            FechaAlta = a.Fecha
                        }).ToList()
                    })
                    .ToList();

                return new ResponseModel<List<ArchivoPorTipoDTO>>
                {
                    isSuccess = true,
                    code = 200,
                    message = "OK",
                    desc = "Archivos filtrados y agrupados correctamente",
                    data = result
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel<List<ArchivoPorTipoDTO>>
                {
                    isSuccess = false,
                    code = 500,
                    message = "Error al obtener archivos",
                    desc = ex.Message,
                    data = null
                };
            }
        }
        public async Task<ResponseModel<string>> GetFileUrlById(FileViewRequest req,bool download)
        {
            try
            {
                string rutaArchivo = null;

                const string sql = @"
            SELECT RutaArchivo
            FROM dbo.EmpleadoArchivos
            WHERE IdArchivo = @IdArchivo;";

                using (SqlConnection conn = new SqlConnection(_csCerberus))
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@IdArchivo", SqlDbType.Int).Value = req.idArchivo;

                    await conn.OpenAsync();
                    var result = await cmd.ExecuteScalarAsync();

                    if (result == null)
                    {
                        return new ResponseModel<string>
                        {
                            isSuccess = false,
                            code = 404,
                            message = "Archivo no encontrado",
                            desc = "No existe registro con el Id proporcionado",
                            data = null
                        };
                    }

                    rutaArchivo = result.ToString();
                }
                string dispotition = "";
                if (download)
                {
                    dispotition = "attachment";
                }
                else
                {
                    dispotition = "inline";
                }
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _settings.BucketName,
                    Key = rutaArchivo,
                    Expires = DateTime.UtcNow.AddMinutes(10),
                    Verb = HttpVerb.GET,
                    ResponseHeaderOverrides = new ResponseHeaderOverrides
                    {
                        ContentDisposition = dispotition
                    }
                };

                var url = _s3.GetPreSignedURL(request);

                return new ResponseModel<string>
                {
                    isSuccess = true,
                    code = 200,
                    message = "OK",
                    desc = "URL generada correctamente",
                    data = url
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel<string>
                {
                    isSuccess = false,
                    code = 500,
                    message = "Error al obtener archivo",
                    desc = ex.Message,
                    data = null
                };
            }
        }

        public async Task<ResponseModel<string>> UploadFileAsync(FileUploadRequest req)
        {
            try
            {
                if (req.file == null || req.file.Length == 0)
                {
                    return new ResponseModel<string>
                    {
                        isSuccess = false,
                        code = 400,
                        message = "Archivo inválido.",
                        desc = "No se recibió archivo o el archivo está vacío.",
                        data = null
                    };
                }
                string folder = $"{req.modulo}/{req.numeroUsuario}/{req.categoria}";
                folder = NormalizeFolder(folder);

                string uuid = Guid.NewGuid().ToString();
                var safeFileName = Path.GetFileName(req.file.FileName);
                var key = $"{folder}/{uuid}_{safeFileName}";

                using var stream = req.file.OpenReadStream();

                var request = new PutObjectRequest
                {
                    BucketName = _settings.BucketName,
                    Key = key,
                    InputStream = stream,
                    ContentType = string.IsNullOrWhiteSpace(req.file.ContentType)
                        ? "application/octet-stream"
                        : req.file.ContentType,

                    // Ajustes importantes para R2
                    DisablePayloadSigning = true,
                    DisableDefaultChecksumValidation = true,
                    AutoCloseStream = false
                };

                await _s3.PutObjectAsync(request);
                ResponseModel<string> dbResult = await SaveFileMetadataToDatabase(req, key);
                if (!dbResult.isSuccess)
                {
                    await _s3.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = _settings.BucketName,
                        Key = key
                    });
                }
                return dbResult;
            }
            catch (Exception ex)
            {
                return new ResponseModel<string>
                {
                    isSuccess = false,
                    code = 500,
                    message = "Error al cargar el archivo.",
                    desc = ex.Message,
                    data = null
                };
            }
        }

        private string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return "general";

            return folder.Trim().Replace("\\", "/").Trim('/');
        }

        public async Task<ResponseModel<string>> SaveFileMetadataToDatabase(FileUploadRequest request,string rutaArchivo)
        {
            try
            {
                const string sql = @"
            INSERT INTO dbo.EmpleadoArchivos
            (
                NumeroUsuario,
                Modulo,
                FileNamed,
                FileType,
                RutaArchivo,
                FechaVencimiento,
                FechaExpedicion
            )
            VALUES
            (
                @NumeroUsuario,
                @Modulo,
                @FileName,
                @FileType,
                @RutaArchivo,
                @FechaVencimiento,
                @FechaExpedicion
            );

            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using SqlConnection conn = new SqlConnection(_csCerberus);
                using SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.Add("@NumeroUsuario", SqlDbType.VarChar, 50).Value = request.numeroUsuario;
                cmd.Parameters.Add("@Modulo", SqlDbType.VarChar, 100).Value = request.modulo;
                cmd.Parameters.Add("@FileName", SqlDbType.VarChar, 255).Value = request.fileName;
                cmd.Parameters.Add("@FileType", SqlDbType.Int).Value = request.fileType;
                cmd.Parameters.Add("@RutaArchivo", SqlDbType.VarChar, 500).Value = rutaArchivo;
                cmd.Parameters.Add("@FechaVencimiento", SqlDbType.DateTime).Value = request.FechaVencimiento == null ? DBNull.Value : request.FechaVencimiento;
                cmd.Parameters.Add("@FechaExpedicion", SqlDbType.DateTime).Value = request.FechaExpedicion == null ? DBNull.Value : request.FechaExpedicion;

                await conn.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();

                int idArchivo = result != null ? Convert.ToInt32(result) : 0;

                return new ResponseModel<string>
                {
                    isSuccess = idArchivo > 0,
                    code = idArchivo > 0 ? 200 : 500,
                    message = idArchivo > 0 ? "OK" : "Error al insertar",
                    desc = idArchivo > 0 ? "Registro creado correctamente" : "No se obtuvo ID",
                    data = idArchivo.ToString()
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel<string>
                {
                    isSuccess = false,
                    code = 500,
                    message = "Error en base de datos",
                    desc = ex.Message,
                    data = "0"
                };
            }
        }
    }
}
