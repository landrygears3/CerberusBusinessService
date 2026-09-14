using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.R2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        #region CONSTANTES

        private const string
            ACTIVIDAD_SERVICIOS_DOCUMENTOS_VER =
                "SERVICIOS.DOCUMENTOS.VER";

        #endregion


        #region PROPIEDADES

        private readonly FileEmpleadoService
            _fileService;

        private readonly ValidaAccionFunction
            _abac;

        private readonly FileCandidatoService
            _fileCandidatoService;

        private readonly FileRutaService
            _fileRutaService;

        #endregion


        #region CONSTRUCTOR

        public FilesController(
            ValidaAccionFunction abac,
            FileEmpleadoService fileService,
            FileCandidatoService fileCandidatoService,
            FileRutaService fileRutaService)
        {
            _abac =
                abac;

            _fileService =
                fileService;

            _fileCandidatoService =
                fileCandidatoService;

            _fileRutaService =
                fileRutaService;
        }

        #endregion


        #region EMPLEADOS

        [HttpPost("listFiles")]
        [Authorize]
        public async Task<
            ResponseModel<List<ArchivoPorTipoDTO>>>
            GetFiles(
                [FromBody]
                FileListRequest request,
                CancellationToken ct)
        {
            ResponseModel<List<ArchivoPorTipoDTO>>
                response =
                    new ResponseModel<
                        List<ArchivoPorTipoDTO>>();


            string tarea =
                "MODULO.RHH.ARCHIVOS.VER";


            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();


            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                response =
                    await _fileService
                        .GetFilesByEmpleadoModulo(
                            request.NumeroUsuario,
                            request.Modulo);
            }
            else
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";
            }


            return response;
        }


        [HttpPost("viewFile")]
        [Authorize]
        public async Task<ResponseModel<string>>
            View(
                [FromBody]
                FileViewRequest request,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            string tarea =
                "MODULO.RHH.ARCHIVOS.VER";


            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();


            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                response =
                    await _fileService
                        .GetFileUrlById(
                            request,
                            false);
            }
            else
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";
            }


            return response;
        }


        [HttpPost("downloadFile")]
        [Authorize]
        public async Task<ResponseModel<string>>
            Download(
                [FromBody]
                FileViewRequest request,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            string tarea =
                "MODULO.RHH.ARCHIVOS.DESCARGAR";


            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();


            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                response =
                    await _fileService
                        .GetFileUrlById(
                            request,
                            true);
            }
            else
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";
            }


            return response;
        }


        [HttpPost("uploadFile")]
        [Authorize]
        public async Task<ResponseModel<string>>
            Upload(
                FileUploadRequest req,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            string tarea =
                "MODULO.RHH.ARCHIVOS.SUBIR";


            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();


            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                response =
                    await _fileService
                        .UploadFileAsync(
                            req);
            }
            else
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";
            }


            return response;
        }

        #endregion


        #region CANDIDATOS

        [HttpPost("candidatos/listFiles")]
        [Authorize]
        public async Task<
            ResponseModel<List<ArchivoPorTipoDTO>>>
            GetFilesCandidato(
                [FromBody]
                CandidatoFileListRequest request,
                CancellationToken ct)
        {
            ResponseModel<List<ArchivoPorTipoDTO>>
                response =
                    new ResponseModel<
                        List<ArchivoPorTipoDTO>>();


            string tarea =
                "MODULO.RHH.ARCHIVOS.VER";


            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();


            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                response =
                    await _fileCandidatoService
                        .GetFilesByCandidatoModulo(
                            request.CandidatoId,
                            request.Modulo);
            }
            else
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";
            }


            return response;
        }


        [HttpPost("candidatos/viewFile")]
        [Authorize]
        public async Task<ResponseModel<string>>
            ViewCandidato(
                [FromBody]
                FileViewRequest request,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            string tarea =
                "MODULO.RHH.ARCHIVOS.VER";


            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();


            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                response =
                    await _fileCandidatoService
                        .GetFileUrlById(
                            request,
                            false);
            }
            else
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";
            }


            return response;
        }


        [HttpPost("candidatos/downloadFile")]
        [Authorize]
        public async Task<ResponseModel<string>>
            DownloadCandidato(
                [FromBody]
                FileViewRequest request,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            string tarea =
                "MODULO.RHH.ARCHIVOS.DESCARGAR";


            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();


            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                response =
                    await _fileCandidatoService
                        .GetFileUrlById(
                            request,
                            true);
            }
            else
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";
            }


            return response;
        }


        [HttpPost("candidatos/uploadFile")]
        [Authorize]
        public async Task<ResponseModel<string>>
            UploadCandidato(
                CandidatoFileUploadRequest req,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            string tarea =
                "MODULO.RHH.ARCHIVOS.SUBIR";


            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();


            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                response =
                    await _fileCandidatoService
                        .UploadFileAsync(
                            req);
            }
            else
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";
            }


            return response;
        }

        #endregion


        #region SERVICIOS DOCUMENTOS

        [HttpPost("servicios/viewFile")]
        [Authorize]
        public async Task<ResponseModel<string>>
            ViewServicioDocumento(
                [FromBody]
                FileRutaRequest request,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            #region TOKEN

            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();

            #endregion


            #region ABAC

            var allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_SERVICIOS_DOCUMENTOS_VER,
                    token,
                    ct);


            if (!allowed)
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";

                response.desc =
                    null;

                response.data =
                    null;

                return response;
            }

            #endregion


            #region RESOLVER ARCHIVO

            response =
                await _fileRutaService
                    .GetFileUrlByRutaAsync(
                        request,
                        false,
                        ct);

            #endregion


            return response;
        }


        [HttpPost("servicios/downloadFile")]
        [Authorize]
        public async Task<ResponseModel<string>>
            DownloadServicioDocumento(
                [FromBody]
                FileRutaRequest request,
                CancellationToken ct)
        {
            ResponseModel<string> response =
                new ResponseModel<string>();


            #region TOKEN

            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth[
                        "Bearer ".Length..
                    ].Trim()
                    : auth.Trim();

            #endregion


            #region ABAC

            /*
             * Por definición solicitada,
             * tanto visualizar como descargar
             * utilizan:
             *
             * SERVICIOS.DOCUMENTOS.VER
             */
            var allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_SERVICIOS_DOCUMENTOS_VER,
                    token,
                    ct);


            if (!allowed)
            {
                response.isSuccess =
                    false;

                response.code =
                    403;

                response.message =
                    "No se tiene acceso a esta función";

                response.desc =
                    null;

                response.data =
                    null;

                return response;
            }

            #endregion


            #region RESOLVER ARCHIVO

            response =
                await _fileRutaService
                    .GetFileUrlByRutaAsync(
                        request,
                        true,
                        ct);

            #endregion


            return response;
        }

        #endregion
    }
}