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
        private readonly EditarEmpleadoFunctions _editar;

        public EmpleadosController(ValidaAccionFunction abac, AltaEmpleadoFuncions altaEmpleadoFuncions,ListadoEmpleadosFunctions listadoEmpleadosFunctions, 
            EditarEmpleadoFunctions editar)
        {
            _abac = abac;
            _altaEmpleadoFuncions = altaEmpleadoFuncions;
            _listadoEmpleadosFunctions = listadoEmpleadosFunctions;
            _editar = editar;
        }

        [HttpPost("EditarEmpleadoGenerales")]
        [Authorize]
        public async Task<IActionResult> EditarEmpleadoGenerales([FromBody] EditarEmpleadoRequest req, CancellationToken ct)
        {

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
                try
                {
                    await _editar.EditarAsync(req);

                    return Ok(new EditarEmpleadoResponse
                    {
                        Success = true,
                        Message = "Empleado actualizado correctamente",
                        UsuarioAsignado = req.UsuarioAsignado,
                        FechaActualizacion = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    return BadRequest(new EditarEmpleadoResponse
                    {
                        Success = false,
                        Message = ex.Message,
                        UsuarioAsignado = req.UsuarioAsignado ?? string.Empty,
                        FechaActualizacion = DateTime.UtcNow
                    });
                }
            }
            else
            {
                //No autorizado
                return Unauthorized("No se tiene acceso a esta función");
            }
             
        }

        [HttpPost("ObtenerDatosGenerales")]
        [Authorize]
        public async Task<IActionResult> ObtenerDatosGenerales(EmpleadoDatosGeneralesRequest request, CancellationToken ct)
        {
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
                var data = await _listadoEmpleadosFunctions.ObtenerPorUsuarioAsignadoAsync(request.usuarioAsignado);

                if (data == null)
                    return NotFound(new { success = false, message = "No se encontró empleado para ese UsuarioAsignado" });

                return Ok(new { success = true, data });
            }
            else
            {
                //No autorizado
                return Unauthorized("No se tiene acceso a esta función");

            }
        }

        [HttpGet("ListadoEmpleados")]
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
