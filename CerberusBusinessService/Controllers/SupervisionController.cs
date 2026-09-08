using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Supervision;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Supervision;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SupervisionController : ControllerBase
    {
        private readonly ValidaAccionFunction _abac;
        private readonly SupervisionFunctions _supervisionFunctions;


        public SupervisionController(
            ValidaAccionFunction abac,
            SupervisionFunctions supervisionFunctions)
        {
            _abac =
                abac;

            _supervisionFunctions =
                supervisionFunctions;
        }


        #region ASISTENCIAS PENDIENTES DE AUTORIZAR

        [HttpGet("PendientesAutorizar")]
        [Authorize]
        public async Task<
            ResponseModel<
                List<ListadoAsistenciaPendienteAutorizarResponse>>>
            PendientesAutorizar(
                CancellationToken ct)
        {
            ResponseModel<
                List<ListadoAsistenciaPendienteAutorizarResponse>>
                response =
                    new ResponseModel<
                        List<ListadoAsistenciaPendienteAutorizarResponse>>();


            string tarea =
                "ASISTENCIAS.AUTORIZAR_TURNO";


            // ========================================================
            // 1. TOMAR BEARER TOKEN
            // ========================================================

            var auth =
                Request.Headers.Authorization
                    .ToString();


            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth["Bearer ".Length..].Trim()
                    : auth.Trim();


            if (string.IsNullOrWhiteSpace(token))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible obtener el token de autorización.";
                response.desc = null;
                response.data = null;

                return response;
            }


            // ========================================================
            // 2. VALIDAR ABAC
            // ========================================================

            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (!allowed)
            {
                response.isSuccess = false;
                response.code = 403;
                response.message =
                    "No se tiene acceso a esta función";
                response.desc = null;
                response.data = null;

                return response;
            }


            // ========================================================
            // 3. OBTENER NUMEROUSUARIO DESDE JWT
            // ========================================================

            string? numeroUsuario =
                User.FindFirst("num")?.Value;


            if (string.IsNullOrWhiteSpace(
                numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al usuario autenticado.";
                response.desc = null;
                response.data = null;

                return response;
            }


            // ========================================================
            // 4. OBTENER PENDIENTES DEL SUPERVISOR
            // ========================================================

            try
            {
                response.data =
                    await _supervisionFunctions
                        .ObtenerPendientesAutorizarAsync(
                            numeroUsuario,
                            ct);


                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Listado de asistencias pendientes de autorización obtenido correctamente.";
                response.desc = null;
            }
            catch (OperationCanceledException)
            {
                response.isSuccess = false;
                response.code = 408;
                response.message =
                    "La consulta fue cancelada.";
                response.desc = null;
                response.data = null;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener las asistencias pendientes de autorización.";
                response.desc =
                    ex.Message;
                response.data = null;
            }


            return response;
        }

        #endregion

        #region Asistencias

        [HttpPost("GetCheckInPendiente")]
        [Authorize]
        public async Task<ResponseModel<CheckInAutorizacionResponse>>
            GetCheckInPendiente(
                [FromBody] ObtenerCheckInPendienteRequest request,
                CancellationToken ct)
        {
            ResponseModel<CheckInAutorizacionResponse> response =
                new ResponseModel<CheckInAutorizacionResponse>();


            string tarea =
                "ASISTENCIAS.AUTORIZAR_TURNO";


            // 1) Tomar el bearer token del request actual
            var auth =
                Request.Headers.Authorization.ToString();

            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth["Bearer ".Length..].Trim()
                    : auth.Trim();


            // 2) Llamar ABAC
            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                try
                {
                    if (request == null ||
                        request.AsistenciaId <= 0)
                    {
                        response.isSuccess = false;
                        response.code = 400;
                        response.message =
                            "Request inválido";
                        response.desc =
                            "El campo AsistenciaId es obligatorio.";
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
                        response.data = null;

                        return response;
                    }


                    response =
                        await _supervisionFunctions
                            .ObtenerCheckInPendienteAsync(
                                request.AsistenciaId,
                                numeroUsuario,
                                ct);
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        "Error al obtener la información del Check-In";
                    response.desc =
                        ex.Message;
                    response.data = null;
                }
            }
            else
            {
                response.isSuccess = false;
                response.code = 403;
                response.message =
                    "No se tiene acceso a esta función";
                response.data = null;
            }


            return response;
        }

        #endregion

        #region Autorizar Turno

        [HttpPost("AutorizarTurno")]
        [Authorize]
        public async Task<ResponseModel<AutorizarTurnoResponse>>
            AutorizarTurno(
                [FromBody] AutorizarTurnoRequest request,
                CancellationToken ct)
        {
            ResponseModel<AutorizarTurnoResponse> response =
                new ResponseModel<AutorizarTurnoResponse>();


            string tarea =
                "ASISTENCIAS.AUTORIZAR_TURNO";


            // 1) Tomar el bearer token del request actual
            var auth =
                Request.Headers.Authorization.ToString();

            var token =
                auth.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? auth["Bearer ".Length..].Trim()
                    : auth.Trim();


            // 2) Llamar ABAC
            var allowed =
                await _abac.CheckAsync(
                    tarea,
                    token,
                    ct);


            if (allowed)
            {
                try
                {
                    if (request == null ||
                        request.ServicioId <= 0 ||
                        request.AsistenciaId <= 0)
                    {
                        response.isSuccess = false;
                        response.code = 400;
                        response.message =
                            "Request inválido";
                        response.desc =
                            "ServicioId y AsistenciaId son obligatorios.";
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
                        response.data = null;

                        return response;
                    }


                    response =
                        await _supervisionFunctions
                            .AutorizarTurnoAsync(
                                request,
                                numeroUsuario,
                                token,
                                ct);
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        "Error al autorizar el turno";
                    response.desc =
                        ex.Message;
                    response.data = null;
                }
            }
            else
            {
                response.isSuccess = false;
                response.code = 403;
                response.message =
                    "No se tiene acceso a esta función";
                response.data = null;
            }


            return response;
        }

        #endregion
    }
}