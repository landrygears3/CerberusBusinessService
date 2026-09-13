using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.ServicioSupervisor;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.ServicioSupervisor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicioSupervisorController : ControllerBase
    {
        #region CONSTANTES

        private const string ACTIVIDAD_VER =
            "SERVICIOS.SUPERVISOR.VER";

        private const string ACTIVIDAD_ASIGNAR =
            "SERVICIOS.SUPERVISOR.ASIGNAR";

        #endregion

        #region PROPIEDADES

        private readonly ValidaAccionFunction _abac;

        private readonly ServicioSupervisorFunctions
            _servicioSupervisorFunctions;

        #endregion

        #region CONSTRUCTOR

        public ServicioSupervisorController(
            ValidaAccionFunction abac,
            ServicioSupervisorFunctions servicioSupervisorFunctions)
        {
            _abac =
                abac;

            _servicioSupervisorFunctions =
                servicioSupervisorFunctions;
        }

        #endregion

        #region OBTENER SERVICIOS ASIGNADOS

        [HttpPost("GetServiciosAsignados")]
        [Authorize]
        public async Task<
            ResponseModel<List<ServicioSupervisorListadoResponse>>>
            GetServiciosAsignados(
                CancellationToken ct)
        {
            ResponseModel<List<ServicioSupervisorListadoResponse>> response =
                new ResponseModel<List<ServicioSupervisorListadoResponse>>();

            string auth =
                Request.Headers.Authorization.ToString();

            string token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth["Bearer ".Length..].Trim()
                    : auth.Trim();

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_VER,
                    token,
                    ct);

            if (!allowed)
            {
                response.isSuccess = false;
                response.code = 401;
                response.message = "No autorizado";
                response.desc = null;
                response.data = null;

                return response;
            }

            string? numeroUsuario =
                User.FindFirst("num")?.Value;

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al supervisor autenticado.";
                response.desc = null;
                response.data = null;

                return response;
            }

            try
            {
                response =
                    await _servicioSupervisorFunctions
                        .ObtenerServiciosAsignadosAsync(
                            numeroUsuario.Trim(),
                            ct);
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los servicios asignados al supervisor.";
                response.desc = ex.Message;
                response.data = null;
            }

            return response;
        }

        #endregion

        #region ASIGNAR SERVICIOS

        [HttpPost("AsignarServicios")]
        [Authorize]
        public async Task<
            ResponseModel<AsignarServiciosSupervisorResponse>>
            AsignarServicios(
                [FromBody] AsignarServiciosSupervisorRequest request,
                CancellationToken ct)
        {
            ResponseModel<AsignarServiciosSupervisorResponse> response =
                new ResponseModel<AsignarServiciosSupervisorResponse>();

            string auth =
                Request.Headers.Authorization.ToString();

            string token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth["Bearer ".Length..].Trim()
                    : auth.Trim();

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_ASIGNAR,
                    token,
                    ct);

            if (!allowed)
            {
                response.isSuccess = false;
                response.code = 401;
                response.message = "No autorizado";
                response.desc = null;
                response.data = null;

                return response;
            }

            string? numeroUsuario =
                User.FindFirst("num")?.Value;

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al usuario autenticado.";
                response.desc = null;
                response.data = null;

                return response;
            }

            try
            {
                response =
                    await _servicioSupervisorFunctions
                        .AsignarServiciosAsync(
                            request,
                            numeroUsuario.Trim(),
                            ct);
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al asignar supervisores al servicio.";
                response.desc = ex.Message;
                response.data = null;
            }

            return response;
        }

        #endregion
    }
}