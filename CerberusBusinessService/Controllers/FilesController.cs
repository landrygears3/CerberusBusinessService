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
        private readonly FileEmpleadoService _fileService;
        private readonly ValidaAccionFunction _abac;
        private readonly FileCandidatoService _fileCandidatoService;

        public FilesController(ValidaAccionFunction abac, FileEmpleadoService fileService, FileCandidatoService fileCandidatoService)
        {
            _abac  = abac;
            _fileService = fileService;
            _fileCandidatoService = fileCandidatoService;
        }

        #region Empleados
        [HttpPost("listFiles")]
        [Authorize]
        public async Task<ResponseModel<List<ArchivoPorTipoDTO>>> GetFiles([FromBody] FileListRequest request, CancellationToken ct)
        {

            ResponseModel<List<ArchivoPorTipoDTO>> response = new ResponseModel<List<ArchivoPorTipoDTO>>();
            string tarea = "MODULO.RHH.ARCHIVOS.VER";
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _fileService.GetFilesByEmpleadoModulo(request.NumeroUsuario, request.Modulo);
            }
            else
            {
                //No autorizado
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
            }
            return response;
        }

        [HttpPost("viewFile")]
        [Authorize]
        public async Task<ResponseModel<string>> View([FromBody] FileViewRequest request, CancellationToken ct)
        {
            ResponseModel<string> response = new ResponseModel<string>();
            string tarea = "MODULO.RHH.ARCHIVOS.VER";
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response  = await _fileService.GetFileUrlById(request,false);
            }
            else
            {
                //No autorizado
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
            }
            return response;
        }

        [HttpPost("downloadFile")]
        [Authorize]
        public async Task<ResponseModel<string>> Download([FromBody] FileViewRequest request, CancellationToken ct)
        {
            ResponseModel<string> response = new ResponseModel<string>();
            string tarea = "MODULO.RHH.ARCHIVOS.DESCARGAR";
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _fileService.GetFileUrlById(request, true);
            }
            else
            {
                //No autorizado
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
            }
            return response;
        }

        [HttpPost("uploadFile")]
        [Authorize]
        public async Task<ResponseModel<string>> Upload(FileUploadRequest req, CancellationToken ct)
        {
            ResponseModel<string> response = new ResponseModel<string>();
            string tarea = "MODULO.RHH.ARCHIVOS.SUBIR";
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _fileService.UploadFileAsync(req);
            }
            else
            {
                //No autorizado
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
            }
            return response;
           

        }
        #endregion

        #region Candidatos
        [HttpPost("candidatos/listFiles")]
        [Authorize]
        public async Task<ResponseModel<List<ArchivoPorTipoDTO>>> GetFilesCandidato(
    [FromBody] CandidatoFileListRequest request,
    CancellationToken ct)
        {
            ResponseModel<List<ArchivoPorTipoDTO>> response = new();

            string tarea = "MODULO.RHH.ARCHIVOS.VER";

            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            var allowed = await _abac.CheckAsync(tarea, token, ct);

            if (allowed)
            {
                response = await _fileCandidatoService.GetFilesByCandidatoModulo(
                    request.CandidatoId,
                    request.Modulo);
            }
            else
            {
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
            }

            return response;
        }

        [HttpPost("candidatos/viewFile")]
        [Authorize]
        public async Task<ResponseModel<string>> ViewCandidato(
            [FromBody] FileViewRequest request,
            CancellationToken ct)
        {
            ResponseModel<string> response = new();

            string tarea = "MODULO.RHH.ARCHIVOS.VER";

            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            var allowed = await _abac.CheckAsync(tarea, token, ct);

            if (allowed)
            {
                response = await _fileCandidatoService.GetFileUrlById(request, false);
            }
            else
            {
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
            }

            return response;
        }

        [HttpPost("candidatos/downloadFile")]
        [Authorize]
        public async Task<ResponseModel<string>> DownloadCandidato(
            [FromBody] FileViewRequest request,
            CancellationToken ct)
        {
            ResponseModel<string> response = new();

            string tarea = "MODULO.RHH.ARCHIVOS.DESCARGAR";

            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            var allowed = await _abac.CheckAsync(tarea, token, ct);

            if (allowed)
            {
                response = await _fileCandidatoService.GetFileUrlById(request, true);
            }
            else
            {
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
            }

            return response;
        }

        [HttpPost("candidatos/uploadFile")]
        [Authorize]
        public async Task<ResponseModel<string>> UploadCandidato(
            CandidatoFileUploadRequest req,
            CancellationToken ct)
        {
            ResponseModel<string> response = new();

            string tarea = "MODULO.RHH.ARCHIVOS.SUBIR";

            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            var allowed = await _abac.CheckAsync(tarea, token, ct);

            if (allowed)
            {
                response = await _fileCandidatoService.UploadFileAsync(req);
            }
            else
            {
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
            }

            return response;
        }
        #endregion
    }
}
