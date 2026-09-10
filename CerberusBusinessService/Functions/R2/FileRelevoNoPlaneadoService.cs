using Amazon.S3;
using Amazon.S3.Model;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.R2;
using Microsoft.Extensions.Options;

namespace CerberusBusinessService.Functions.R2
{
    public class FileRelevoNoPlaneadoService
    {
        #region PROPIEDADES

        private readonly IAmazonS3 _s3;

        private readonly R2Settings _settings;

        #endregion


        #region CONSTRUCTOR

        public FileRelevoNoPlaneadoService(
            IAmazonS3 s3,
            IOptions<R2Settings> settings)
        {
            _s3 = s3;

            _settings = settings.Value;
        }

        #endregion


        #region SUBIR ARCHIVO

        public async Task<ResponseModel<string>>
            UploadFileAsync(
                IFormFile file,
                string operacionId,
                string categoria,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();

            try
            {
                if (file == null ||
                    file.Length == 0)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "Archivo inválido.";
                    response.desc =
                        "No se recibió archivo o está vacío.";
                    response.data = null;

                    return response;
                }


                if (string.IsNullOrWhiteSpace(
                    operacionId))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "Operación inválida.";
                    response.desc =
                        "OperacionId es obligatorio.";
                    response.data = null;

                    return response;
                }


                if (string.IsNullOrWhiteSpace(
                    categoria))
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message =
                        "Categoría inválida.";
                    response.desc =
                        "La categoría del archivo es obligatoria.";
                    response.data = null;

                    return response;
                }


                string folder =
                    $"relevos-no-planeados/" +
                    $"{operacionId}/" +
                    $"{categoria}";


                folder =
                    NormalizeFolder(
                        folder);


                string uuid =
                    Guid.NewGuid()
                        .ToString();


                string safeFileName =
                    Path.GetFileName(
                        file.FileName);


                string key =
                    $"{folder}/" +
                    $"{uuid}_" +
                    $"{safeFileName}";


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
                            string.IsNullOrWhiteSpace(
                                file.ContentType)
                                ? "application/octet-stream"
                                : file.ContentType,

                        DisablePayloadSigning = true,

                        DisableDefaultChecksumValidation =
                            true,

                        AutoCloseStream = false
                    };


                await _s3.PutObjectAsync(
                    request,
                    ct);


                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Archivo cargado correctamente.";
                response.desc = null;
                response.data = key;


                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al cargar archivo.";
                response.desc =
                    ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion


        #region ELIMINAR ARCHIVO

        public async Task DeleteFileAsync(
            string key,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(
                key))
            {
                return;
            }


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

        #endregion


        #region NORMALIZAR CARPETA

        private string NormalizeFolder(
            string folder)
        {
            if (string.IsNullOrWhiteSpace(
                folder))
            {
                return "relevos-no-planeados";
            }


            return folder
                .Trim()
                .Replace("\\", "/")
                .Trim('/');
        }

        #endregion
    }
}