using CerberusBusinessService.Functions;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Empleados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmpleadosDomiciliosController : ControllerBase
    {
        private readonly ValidaAccionFunction _abac;
        private readonly AltaDomiciliosFunctions _altaDomiciliosFunctions;
        private readonly ListadoDomiciliosFunctions _listadoDomiciliosFunctions;

        public EmpleadosDomiciliosController(ValidaAccionFunction abac, AltaDomiciliosFunctions altaDomiciliosFunctions, ListadoDomiciliosFunctions listadoDomiciliosFunctions)
        {
            _abac = abac;
            _altaDomiciliosFunctions = altaDomiciliosFunctions;
            _listadoDomiciliosFunctions = listadoDomiciliosFunctions;
        }

        [HttpPost("ListadoDomicilios")]
        [Authorize]
        public async Task<ResponseModel<List<ListadoDomiciliosResponse>>> ListadoDomicilios([FromBody] ListadoDomiciliosRequest req, CancellationToken ct)
        {
            ResponseModel<List<ListadoDomiciliosResponse>> response = new ResponseModel<List<ListadoDomiciliosResponse>>();

            string tarea = "MODULO.RHH.GENERALES.VER";
            // 1) Tomar el bearer token del request actual
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _listadoDomiciliosFunctions.ListadoDomicilios(req.Usuario);
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

        [HttpPost("AltaDomicilio")]
        [Authorize]
        public async Task<ResponseModel<string>> AltaDomicilio([FromBody] AltaDomicilioRequest req, CancellationToken ct)
        {
            ResponseModel<string> response = new ResponseModel<string>();

            string tarea = "MODULO.RHH.EMPLEADOS.ALTA";
            // 1) Tomar el bearer token del request actual
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _altaDomiciliosFunctions.AltaDomicilios(req);
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
