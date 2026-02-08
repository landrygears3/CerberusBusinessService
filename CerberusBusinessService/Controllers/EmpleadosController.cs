using Azure;
using Azure.Core;
using CerberusBusinessService.Functions;
using CerberusBusinessService.Models.DTO;
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
        public async Task<ResponseModel<EditarEmpleadoResponse>> EditarEmpleadoGenerales([FromBody] EditarEmpleadoRequest req, CancellationToken ct)
        {
            ResponseModel<EditarEmpleadoResponse> response = new ResponseModel<EditarEmpleadoResponse>();
            
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
                    response.data = new EditarEmpleadoResponse();
                    string estatusact = await _editar.EditarAsync(req);

                    if (estatusact != "OK")
                    {
                        response.isSuccess = false;
                        response.message = "Error al actualizar el empleado: " + estatusact;
                        response.code = 400;
                        response.data = null;
                    }
                    else
                    {
                        response.isSuccess = true;
                        response.message = "Empleado actualizado correctamente";
                        response.data.UsuarioAsignado = req.UsuarioAsignado;
                        response.data.FechaActualizacion = DateTime.UtcNow;
                    }


                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al actualizar el empleado";
                    response.desc = ex.Message;
                    response.data = null;

                }
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

        [HttpPost("ObtenerDatosGenerales")]
        [Authorize]
        public async Task<ResponseModel<EmpleadoDatosGeneralesResponse>> ObtenerDatosGenerales(EmpleadoDatosGeneralesRequest request, CancellationToken ct)
        {
            ResponseModel<EmpleadoDatosGeneralesResponse> response = new ResponseModel<EmpleadoDatosGeneralesResponse>();
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
                try
                {
                    response.data = await _listadoEmpleadosFunctions.ObtenerPorUsuarioAsignadoAsync(request.usuarioAsignado);

                    if (response.data == null)
                    {
                        response.isSuccess = false;
                        response.code = 404;
                        response.message = "No se encontró empleado para ese UsuarioAsignado";
                    }
                    else
                    {
                        response.isSuccess = true;
                        response.code = 200;
                        response.message = "Ok";
                    }
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al obtener los datos generales del empleado";
                    response.desc = ex.Message;
                    response.data = null;
                }

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

        [HttpGet("ListadoEmpleados")]
        [Authorize]
        public async Task<ResponseModel<List<ListadoEmpleadosResponse>>> ListadoEmpleados(CancellationToken ct)
        {
            ResponseModel<List<ListadoEmpleadosResponse>> response = new ResponseModel<List<ListadoEmpleadosResponse>>();
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
                try
                {
                    List<ListadoEmpleadosResponse> data = await _listadoEmpleadosFunctions.ObtenerListadoEmpleadosAsync();
                    response.isSuccess = true;
                    response.code = 200;
                    response.message = "Listado de empleados obtenido correctamente";
                    response.data = data;

                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al obtener el listado de empleados";
                    response.desc = ex.Message;
                    response.data = null;
                }
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

        [HttpPost("AltaEmpleadoGeneral")]
        [Authorize]
        public async Task<ResponseModel<EmpleadoAltaGeneralesResponse>> AltaEmpleado(EmpleadoAltaGeneralesRequest request, CancellationToken ct)
        {
            ResponseModel<EmpleadoAltaGeneralesResponse> response = new ResponseModel<EmpleadoAltaGeneralesResponse>();
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
                    ResponseModel<AuthRegisterResponse> userId = await _altaEmpleadoFuncions.AltaEmpleadoGenerales(request);

                    if (!userId.isSuccess)
                    {
                        response.isSuccess = false;
                        response.code = 400;
                        response.message = "Error al dar de alta el empleado: " + userId.message;
                        response.data = null;
                        return response;
                    }
                    else
                    {
                        response.isSuccess = true;
                        response.message = "Empleado dado de alta correctamente";
                        response.data = new EmpleadoAltaGeneralesResponse
                        {
                            UserId = userId.data.numeroUsuario,
                            FechaAlta = DateTime.UtcNow
                        };
                    }


                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.message = "Error al dar de alta el empleado";
                    response.data = null;   
                    response.desc = ex.Message;
                    response.code = 500;
                }
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
