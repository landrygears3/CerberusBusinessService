using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Contratacion;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Contratacion;
using CerberusBusinessService.Models.DTO.Empleados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContratacionController : ControllerBase
    {
        private readonly string _cs;
        private readonly ContratacionCandidatoFunctions _contratacionCandidatoFunctions;
        private readonly ValidaAccionFunction _abac;

        public ContratacionController(IConfiguration config, ContratacionCandidatoFunctions contratacionCandidatoFunctions, ValidaAccionFunction abac)
        {
            _cs = config.GetConnectionString("DefaultConnection")!;
            _contratacionCandidatoFunctions = contratacionCandidatoFunctions;
            _abac = abac;
        }

        [HttpPost("ContratarCandidato")]
        [Authorize]
        public async Task<ResponseModel<AuthRegisterResponse>> ContratarCandidato(
    [FromForm] ContratarCandidatoRequest request,
    CancellationToken ct)
        {
            ResponseModel<AuthRegisterResponse> response = new ResponseModel<AuthRegisterResponse>();
            string tarea = "MODULO.RHH.GENERALES.MODIFICACION";
            // 1) Tomar el bearer token del request actual
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                var usuarioOperacion =
                    User.Claims.FirstOrDefault(c =>
                        c.Value.StartsWith("CER", StringComparison.OrdinalIgnoreCase))?.Value;

                if (string.IsNullOrWhiteSpace(usuarioOperacion))
                {
                    return new ResponseModel<AuthRegisterResponse>
                    {
                        isSuccess = false,
                        code = 401,
                        message = "No fue posible obtener el usuario de operación desde el token.",
                        data = null
                    };
                }

                response = await _contratacionCandidatoFunctions.ContratarCandidatoAsync(
                    request,
                    usuarioOperacion, token);
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
