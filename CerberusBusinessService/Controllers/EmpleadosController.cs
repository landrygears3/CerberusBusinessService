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
                    response.Data = new EditarEmpleadoResponse();
                    string estatusact = await _editar.EditarAsync(req);

                    if (estatusact != "OK")
                    {
                        response.IsSuccess = false;
                        response.Message = "Error al actualizar el empleado: " + estatusact;
                        response.Code = 400;
                        response.Data = null;
                    }
                    else
                    {
                        response.IsSuccess = true;
                        response.Message = "Empleado actualizado correctamente";
                        response.Data.UsuarioAsignado = req.UsuarioAsignado;
                        response.Data.FechaActualizacion = DateTime.UtcNow;
                    }


                }
                catch (Exception ex)
                {
                    response.IsSuccess = false;
                    response.Code = 500;
                    response.Message = "Error al actualizar el empleado";
                    response.Desc = ex.Message;
                    response.Data = null;

                }
            }
            else
            {
                //No autorizado
                response.IsSuccess = false;
                response.Code = 403;
                response.Message = "No se tiene acceso a esta función";
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
                    response.Data = await _listadoEmpleadosFunctions.ObtenerPorUsuarioAsignadoAsync(request.usuarioAsignado);

                    if (response.Data == null)
                    {
                        response.IsSuccess = false;
                        response.Code = 404;
                        response.Message = "No se encontró empleado para ese UsuarioAsignado";
                    }
                    else
                    {
                        response.IsSuccess = true;
                        response.Code = 200;
                        response.Message = "Ok";
                    }
                }
                catch (Exception ex)
                {
                    response.IsSuccess = false;
                    response.Code = 500;
                    response.Message = "Error al obtener los datos generales del empleado";
                    response.Desc = ex.Message;
                    response.Data = null;
                }

            }
            else
            {
                //No autorizado
                response.IsSuccess = false;
                response.Code = 403;
                response.Message = "No se tiene acceso a esta función";

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
                    response.IsSuccess = true;
                    response.Code = 200;
                    response.Message = "Listado de empleados obtenido correctamente";
                    response.Data = data;

                }
                catch (Exception ex)
                {
                    response.IsSuccess = false;
                    response.Code = 500;
                    response.Message = "Error al obtener el listado de empleados";
                    response.Desc = ex.Message;
                    response.Data = null;
                }
            }
            else
            {
                //No autorizado
                response.IsSuccess = false;
                response.Code = 403;
                response.Message = "No se tiene acceso a esta función";

            }
            return response;
        }

        [HttpPost("AltaEmpleadoGeneral")]
        [Authorize]
        public async Task<ResponseModel<EmpleadoAltaGeneralesResponse>> EltaEmpleado(EmpleadoAltaGeneralesRequest request, CancellationToken ct)
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
                    var userId = await _altaEmpleadoFuncions.AltaEmpleadoGenerales(request);

                    response.IsSuccess = true;
                    response.Message = "Empleado dado de alta correctamente";
                    response.Data = new EmpleadoAltaGeneralesResponse
                    {
                        UserId = userId,
                        FechaAlta = DateTime.UtcNow
                    };

                }
                catch (Exception ex)
                {
                    response.IsSuccess = false;
                    response.Message = "Error al dar de alta el empleado";
                    response.Data = null;   
                    response.Desc = ex.Message;
                    response.Code = 500;
                }
            }
            else
            {
                //No autorizado
                response.IsSuccess = false;
                response.Code = 403;
                response.Message = "No se tiene acceso a esta función";

            }
            return response;
        }
        }
    }
