using CerberusBusinessService.Functions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("negocio/[controller]")]
    public class EmpleadosController : ControllerBase
    {
        private readonly ValidaAccionFunction _abac;

        public EmpleadosController(ValidaAccionFunction abac)
        {
            _abac = abac;
        }

        [HttpPost("ListadoEmpleados")]
        [Authorize]
        public async Task<IActionResult> ListadoEmpleados(CancellationToken ct)
        {
            string tarea = "MODULO.RHH.EMPLEADOS.VER";
            // 1) Tomar el bearer token del request actual
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);

            return Ok(new { allowed });
        }
    }
}
