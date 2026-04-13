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
        private readonly FileService _fileService;
        private readonly ValidaAccionFunction _abac;

        public FilesController(ValidaAccionFunction abac, FileService fileService)
        {
            _abac  = abac;
            _fileService = fileService;
        }

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
    }
}
