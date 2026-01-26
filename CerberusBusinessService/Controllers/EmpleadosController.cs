using Azure.Core;
using CerberusBusinessService.Functions;
using CerberusBusinessService.Models.DTO.Empleados;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmpleadosController : ControllerBase
    {
        private readonly ValidaAccionFunction _abac;
        private readonly AltaEmpleadoFuncions _altaEmpleadoFuncions;
        private readonly ListadoEmpleadosFunctions _listadoEmpleadosFunctions;

        public EmpleadosController(ValidaAccionFunction abac, AltaEmpleadoFuncions altaEmpleadoFuncions,ListadoEmpleadosFunctions listadoEmpleadosFunctions)
        {
            _abac = abac;
            _altaEmpleadoFuncions = altaEmpleadoFuncions;
            _listadoEmpleadosFunctions = listadoEmpleadosFunctions;
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
            if (allowed)
            {
                return Ok(await _listadoEmpleadosFunctions.ObtenerListadoEmpleadosAsync());
            }
            else
            {
                //No autorizado
                return Unauthorized("No se tiene acceso a esta función");

            }
        }

        [HttpPost("AltaEmpleadoGeneral")]
        [Authorize]
        public async Task<IActionResult> EltaEmpleado(EmpleadoAltaGeneralesRequest request, CancellationToken ct)
        {
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
                //Alta empleado
                try
                {
                    var userId = await _altaEmpleadoFuncions.AltaEmpleadoGenerales(request);

                    var response = new EmpleadoAltaGeneralesResponse
                    {
                        Success = true,
                        Message = "Empleado dado de alta correctamente",
                        UserId = userId,
                        FechaAlta = DateTime.UtcNow
                    };

                    return Ok(response);
                }
                catch (Exception ex)
                {
                    var response = new EmpleadoAltaGeneralesResponse
                    {
                        Success = false,
                        Message = ex.Message,
                        FechaAlta = DateTime.UtcNow
                    };

                    return BadRequest(response);
                }
            }
            else
            {
                //No autorizado
                return Unauthorized("No se tiene acceso a esta función");

            }

            }
        }
    }
