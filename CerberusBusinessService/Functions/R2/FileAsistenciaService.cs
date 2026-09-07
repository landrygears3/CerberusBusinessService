using Amazon.S3;
using Amazon.S3.Model;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.R2;
using Microsoft.Extensions.Options;

namespace CerberusBusinessService.Functions.R2
{
    public class FileAsistenciaService
    {
        private readonly IAmazonS3 _s3;
        private readonly R2Settings _settings;

        public FileAsistenciaService(
            IAmazonS3 s3,
            IOptions<R2Settings> settings)
        {
            _s3 = s3;
            _settings = settings.Value;
        }

        public async Task<ResponseModel<string>> UploadFileAsync(
            IFormFile file,
            string numeroUsuario,
            DateTime fechaTurno,
            string operacionId,
            string categoria,
            CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();

            try
            {
                if (file == null || file.Length == 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "Archivo inválido.";
                    response.desc = "No se recibió archivo o está vacío.";
                    response.data = null;

                    return response;
                }

                string folder =
                    $"asistencias/" +
                    $"{numeroUsuario}/" +
                    $"{fechaTurno:yyyyMMdd}/" +
                    $"{operacionId}/" +
                    $"{categoria}";

                folder = NormalizeFolder(folder);

                string uuid =
                    Guid.NewGuid().ToString();

                string safeFileName =
                    Path.GetFileName(file.FileName);

                string key =
                    $"{folder}/{uuid}_{safeFileName}";

                using var stream =
                    file.OpenReadStream();

                var request =
                    new PutObjectRequest
                    {
                        BucketName =
                            _settings.BucketName,

                        Key =
                            key,

                        InputStream =
                            stream,

                        ContentType =
                            string.IsNullOrWhiteSpace(file.ContentType)
                                ? "application/octet-stream"
                                : file.ContentType,

                        DisablePayloadSigning = true,
                        DisableDefaultChecksumValidation = true,
                        AutoCloseStream = false
                    };

                await _s3.PutObjectAsync(
                    request,
                    ct);

                response.isSuccess = true;
                response.code = 200;
                response.message = "Archivo cargado correctamente.";
                response.desc = null;

                /*
                 * IMPORTANTE:
                 *
                 * Regresamos la KEY de R2.
                 *
                 * Ejemplo:
                 *
                 * asistencias/CER00001/20260906/xxx/firmas/xxx_firma.png
                 *
                 * Eso es lo que guardaremos en SQL.
                 */
                response.data = key;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message = "Error al cargar archivo.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }


        public async Task DeleteFileAsync(
            string key,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            var request =
                new DeleteObjectRequest
                {
                    BucketName =
                        _settings.BucketName,

                    Key =
                        key
                };

            await _s3.DeleteObjectAsync(
                request,
                ct);
        }


        private string NormalizeFolder(
            string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return "asistencias";

            return folder
                .Trim()
                .Replace("\\", "/")
                .Trim('/');
        }
    }
}