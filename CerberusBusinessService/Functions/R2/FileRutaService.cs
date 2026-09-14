using Amazon.S3;
using Amazon.S3.Model;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.R2;
using Microsoft.Extensions.Options;
using System.Net;

namespace CerberusBusinessService.Functions.R2
{
    public class FileRutaService
    {
        #region PROPIEDADES

        private readonly IAmazonS3
            _s3;

        private readonly R2Settings
            _settings;

        #endregion


        #region CONSTRUCTOR

        public FileRutaService(
            IAmazonS3 s3,
            IOptions<R2Settings> settings)
        {
            _s3 =
                s3;

            _settings =
                settings.Value;
        }

        #endregion


        #region RESOLVER ARCHIVO

        public async Task<ResponseModel<string>>
            GetFileUrlByRutaAsync(
                FileRutaRequest request,
                bool download,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            #region VALIDACIONES

            if (request == null)
            {
                response.isSuccess =
                    false;

                response.code =
                    400;

                response.message =
                    "El request es obligatorio.";

                response.desc =
                    null;

                response.data =
                    null;

                return response;
            }


            if (string.IsNullOrWhiteSpace(
                request.RutaArchivo))
            {
                response.isSuccess =
                    false;

                response.code =
                    400;

                response.message =
                    "La ruta del archivo es obligatoria.";

                response.desc =
                    null;

                response.data =
                    null;

                return response;
            }

            #endregion


            try
            {
                #region NORMALIZAR RUTA

                string rutaArchivo =
                    NormalizarRuta(
                        request.RutaArchivo);


                if (string.IsNullOrWhiteSpace(
                    rutaArchivo))
                {
                    response.isSuccess =
                        false;

                    response.code =
                        400;

                    response.message =
                        "La ruta del archivo es inválida.";

                    response.desc =
                        null;

                    response.data =
                        null;

                    return response;
                }


                /*
                 * Se espera una KEY de R2, no una URL.
                 *
                 * Ejemplo válido:
                 *
                 * asistencias/CER00001/20260914/.../foto.jpg
                 */
                if (Uri.TryCreate(
                    rutaArchivo,
                    UriKind.Absolute,
                    out _))
                {
                    response.isSuccess =
                        false;

                    response.code =
                        400;

                    response.message =
                        "La ruta debe corresponder a una key de almacenamiento.";

                    response.desc =
                        "No debe enviarse una URL absoluta.";

                    response.data =
                        null;

                    return response;
                }


                string[] segmentos =
                    rutaArchivo.Split(
                        '/',
                        StringSplitOptions
                            .RemoveEmptyEntries);


                if (segmentos.Any(
                    x =>
                        x == "." ||
                        x == ".."))
                {
                    response.isSuccess =
                        false;

                    response.code =
                        400;

                    response.message =
                        "La ruta del archivo es inválida.";

                    response.desc =
                        "La ruta contiene segmentos no permitidos.";

                    response.data =
                        null;

                    return response;
                }

                #endregion


                #region VALIDAR EXISTENCIA

                try
                {
                    await _s3
                        .GetObjectMetadataAsync(
                            new GetObjectMetadataRequest
                            {
                                BucketName =
                                    _settings.BucketName,

                                Key =
                                    rutaArchivo
                            },
                            ct);
                }
                catch (AmazonS3Exception ex)
                    when (
                        ex.StatusCode ==
                        HttpStatusCode.NotFound)
                {
                    response.isSuccess =
                        false;

                    response.code =
                        404;

                    response.message =
                        "Archivo no encontrado.";

                    response.desc =
                        "No existe un archivo para la ruta proporcionada.";

                    response.data =
                        null;

                    return response;
                }

                #endregion


                #region CONTENT DISPOSITION

                string disposition =
                    download
                        ? "attachment"
                        : "inline";

                #endregion


                #region URL PREFIRMADA

                GetPreSignedUrlRequest
                    presignedRequest =
                        new GetPreSignedUrlRequest
                        {
                            BucketName =
                                _settings.BucketName,

                            Key =
                                rutaArchivo,

                            Expires =
                                DateTime.UtcNow
                                    .AddMinutes(10),

                            Verb =
                                HttpVerb.GET,

                            ResponseHeaderOverrides =
                                new ResponseHeaderOverrides
                                {
                                    ContentDisposition =
                                        disposition
                                }
                        };


                string url =
                    _s3.GetPreSignedURL(
                        presignedRequest);

                #endregion


                #region RESPONSE

                response.isSuccess =
                    true;

                response.code =
                    200;

                response.message =
                    "OK";

                response.desc =
                    download
                        ? "URL de descarga generada correctamente."
                        : "URL de visualización generada correctamente.";

                response.data =
                    url;


                return response;

                #endregion
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                response.isSuccess =
                    false;

                response.code =
                    500;

                response.message =
                    "Error al resolver el archivo.";

                response.desc =
                    ex.Message;

                response.data =
                    null;

                return response;
            }
        }

        #endregion


        #region NORMALIZAR RUTA

        private static string
            NormalizarRuta(
                string ruta)
        {
            if (string.IsNullOrWhiteSpace(
                ruta))
            {
                return string.Empty;
            }


            string resultado =
                ruta
                    .Trim()
                    .Replace(
                        "\\",
                        "/")
                    .TrimStart('/');


            while (resultado.Contains("//"))
            {
                resultado =
                    resultado.Replace(
                        "//",
                        "/");
            }


            return resultado;
        }

        #endregion
    }
}