using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Servicios;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiciosController : ControllerBase
    {
        #region PROPIEDADES

        private readonly ValidaAccionFunction _abac;
        private readonly ServiciosFunctions _serviciosFunctions;

        #endregion

        #region CONSTRUCTOR

        public ServiciosController(
            ValidaAccionFunction abac,
            ServiciosFunctions serviciosFunctions)
        {
            _abac = abac;
            _serviciosFunctions = serviciosFunctions;
        }

        #endregion

        #region COMMIT SERVICIO

        [HttpPost("CommitServicio")]
        [Authorize]
        public async Task<ResponseModel<CommitServicioResponse>> CommitServicio(
            CommitServicioRequest request,
            CancellationToken ct)
        {
            ResponseModel<CommitServicioResponse> response = new ResponseModel<CommitServicioResponse>();

            string tarea = "SERVICIOS.COMMIT";

            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            var allowed = await _abac.CheckAsync(tarea, token, ct);

            if (allowed)
            {
                try
                {
                    response = await _serviciosFunctions.CommitServicioAsync(request, ct);
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al registrar o actualizar el servicio";
                    response.desc = ex.Message;
                    response.data = null;
                }
            }
            else
            {
                response.isSuccess = false;
                response.code = 401;
                response.message = "No autorizado";
                response.desc = null;
                response.data = null;
            }

            return response;
        }

        #endregion

        #region OBTENER SERVICIOS

        [HttpPost("GetServicios")]
        [Authorize]
        public async Task<ResponseModel<List<ServicioResponse>>> GetServicios(CancellationToken ct)
        {
            ResponseModel<List<ServicioResponse>> response = new ResponseModel<List<ServicioResponse>>();

            string tarea = "SERVICIOS.VER";

            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            var allowed = await _abac.CheckAsync(tarea, token, ct);

            if (allowed)
            {
                try
                {
                    response = await _serviciosFunctions.ObtenerServiciosAsync(ct);
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al obtener el listado de servicios";
                    response.desc = ex.Message;
                    response.data = null;
                }
            }
            else
            {
                response.isSuccess = false;
                response.code = 401;
                response.message = "No autorizado";
                response.desc = null;
                response.data = null;
            }

            return response;
        }

        #endregion

        #region OBTENER SERVICIO

        [HttpPost("GetServicio")]
        [Authorize]
        public async Task<ResponseModel<ServicioResponse>> GetServicio(
            GetServicioRequest request,
            CancellationToken ct)
        {
            ResponseModel<ServicioResponse> response = new ResponseModel<ServicioResponse>();

            string tarea = "SERVICIOS.VER";

            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            var allowed = await _abac.CheckAsync(tarea, token, ct);

            if (allowed)
            {
                try
                {
                    if (request == null || request.ServicioId <= 0)
                    {
                        response.isSuccess = false;
                        response.code = 400;
                        response.message = "Request inválido";
                        response.desc = "El campo ServicioId es obligatorio.";
                        response.data = null;

                        return response;
                    }

                    response = await _serviciosFunctions.ObtenerServicioAsync(request, ct);
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al obtener la información del servicio";
                    response.desc = ex.Message;
                    response.data = null;
                }
            }
            else
            {
                response.isSuccess = false;
                response.code = 401;
                response.message = "No autorizado";
                response.desc = null;
                response.data = null;
            }

            return response;
        }

        #endregion
    }
}