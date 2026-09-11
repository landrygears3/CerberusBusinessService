using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Supervision;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Relevos;
using CerberusBusinessService.Models.DTO.Supervision;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SupervisionController : ControllerBase
    {
        #region CONSTANTES

        private const string ACTIVIDAD_AUTORIZAR_TURNO =
            "ASISTENCIAS.AUTORIZAR_TURNO";

        #endregion


        #region PROPIEDADES

        private readonly ValidaAccionFunction _abac;

        private readonly SupervisionFunctions
            _supervisionFunctions;

        #endregion


        #region CONSTRUCTOR

        public SupervisionController(
            ValidaAccionFunction abac,
            SupervisionFunctions supervisionFunctions)
        {
            _abac =
                abac;

            _supervisionFunctions =
                supervisionFunctions;
        }

        #endregion


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


            // ========================================================
            // 1. OBTENER TOKEN
            // ========================================================

            string authorization =
                Request.Headers.Authorization
                    .ToString();


            if (string.IsNullOrWhiteSpace(
                authorization))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible obtener el token de autorización.";
                response.desc = null;
                response.data = null;

                return response;
            }


            string token =
                authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? authorization["Bearer ".Length..]
                        .Trim()
                    : authorization.Trim();


            if (string.IsNullOrWhiteSpace(token))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "El token de autorización no tiene un formato válido.";
                response.desc = null;
                response.data = null;

                return response;
            }


            // ========================================================
            // 2. VALIDAR ABAC
            // ========================================================

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_AUTORIZAR_TURNO,
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
            // 3. OBTENER NUMEROUSUARIO
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
            // 4. OBTENER PENDIENTES
            // ========================================================

            try
            {
                response.data =
                    await _supervisionFunctions
                        .ObtenerPendientesAutorizarAsync(
                            numeroUsuario.Trim(),
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


        #region DETALLE CHECK-IN PENDIENTE

        [HttpPost("GetCheckInPendiente")]
        [Authorize]
        public async Task<ResponseModel<CheckInAutorizacionResponse>>
            GetCheckInPendiente(
                [FromBody] ObtenerCheckInPendienteRequest request,
                CancellationToken ct)
        {
            ResponseModel<CheckInAutorizacionResponse> response =
                new ResponseModel<CheckInAutorizacionResponse>();


            // ========================================================
            // 1. OBTENER TOKEN
            // ========================================================

            string authorization =
                Request.Headers.Authorization
                    .ToString();


            if (string.IsNullOrWhiteSpace(
                authorization))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible obtener el token de autorización.";
                response.desc = null;
                response.data = null;

                return response;
            }


            string token =
                authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? authorization["Bearer ".Length..]
                        .Trim()
                    : authorization.Trim();


            if (string.IsNullOrWhiteSpace(token))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "El token de autorización no tiene un formato válido.";
                response.desc = null;
                response.data = null;

                return response;
            }


            // ========================================================
            // 2. VALIDAR ABAC
            // ========================================================

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_AUTORIZAR_TURNO,
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
            // 3. VALIDAR REQUEST
            // ========================================================

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


            // ========================================================
            // 4. OBTENER NUMEROUSUARIO
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
            // 5. OBTENER DETALLE
            // ========================================================

            try
            {
                response =
                    await _supervisionFunctions
                        .ObtenerCheckInPendienteAsync(
                            request.AsistenciaId,
                            numeroUsuario.Trim(),
                            ct);
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
                    "Error al obtener la información del Check-In";
                response.desc =
                    ex.Message;
                response.data = null;
            }


            return response;
        }

        #endregion


        #region AUTORIZAR TURNO

        [HttpPost("AutorizarTurno")]
        [Authorize]
        public async Task<ResponseModel<AutorizarTurnoResponse>>
            AutorizarTurno(
                [FromBody] AutorizarTurnoRequest request,
                CancellationToken ct)
        {
            ResponseModel<AutorizarTurnoResponse> response =
                new ResponseModel<AutorizarTurnoResponse>();


            // ========================================================
            // 1. OBTENER TOKEN
            // ========================================================

            string authorization =
                Request.Headers.Authorization
                    .ToString();


            if (string.IsNullOrWhiteSpace(
                authorization))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible obtener el token de autorización.";
                response.desc = null;
                response.data = null;

                return response;
            }


            string token =
                authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? authorization["Bearer ".Length..]
                        .Trim()
                    : authorization.Trim();


            if (string.IsNullOrWhiteSpace(token))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "El token de autorización no tiene un formato válido.";
                response.desc = null;
                response.data = null;

                return response;
            }


            // ========================================================
            // 2. VALIDAR ABAC
            // ========================================================

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_AUTORIZAR_TURNO,
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
            // 3. VALIDAR REQUEST
            // ========================================================

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


            // ========================================================
            // 4. OBTENER NUMEROUSUARIO
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
            // 5. AUTORIZAR TURNO
            // ========================================================

            try
            {
                response =
                    await _supervisionFunctions
                        .AutorizarTurnoAsync(
                            request,
                            numeroUsuario.Trim(),
                            token,
                            ct);
            }
            catch (OperationCanceledException)
            {
                response.isSuccess = false;
                response.code = 408;
                response.message =
                    "La operación fue cancelada.";
                response.desc = null;
                response.data = null;
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


            return response;
        }

        #endregion


        #region RETIRAR ELEMENTO Y SOLICITAR RELEVO

        [HttpPost("RetirarElemento")]
        [Authorize]
        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            RetirarElemento(
                [FromBody] RetirarElementoSupervisionRequest request,
                CancellationToken ct)
        {
            ResponseModel<SolicitudRelevoNoPlaneadoDto> response =
                new ResponseModel<SolicitudRelevoNoPlaneadoDto>();


            // ========================================================
            // 1. OBTENER TOKEN
            // ========================================================

            string authorization =
                Request.Headers.Authorization
                    .ToString();


            if (string.IsNullOrWhiteSpace(
                authorization))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible obtener el token de autorización.";
                response.desc = null;
                response.data = null;

                return response;
            }


            if (!authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "El token de autorización no tiene un formato válido.";
                response.desc = null;
                response.data = null;

                return response;
            }


            string token =
                authorization["Bearer ".Length..]
                    .Trim();


            if (string.IsNullOrWhiteSpace(
                token))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "El token de autorización no tiene un formato válido.";
                response.desc = null;
                response.data = null;

                return response;
            }


            // ========================================================
            // 2. VALIDAR ABAC
            // ========================================================

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_AUTORIZAR_TURNO,
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
            // 3. OBTENER NUMEROUSUARIO
            // ========================================================

            string? numeroSupervisor =
                User.FindFirst("num")?.Value;


            if (string.IsNullOrWhiteSpace(
                numeroSupervisor))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al supervisor autenticado.";
                response.desc = null;
                response.data = null;

                return response;
            }


            numeroSupervisor =
                numeroSupervisor.Trim();


            // ========================================================
            // 4. VALIDAR REQUEST
            // ========================================================

            if (request == null)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    "El request es obligatorio.";
                response.desc = null;
                response.data = null;

                return response;
            }


            if (request.SupervisionId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    "SupervisionId es inválido.";
                response.desc = null;
                response.data = null;

                return response;
            }


            if (string.IsNullOrWhiteSpace(
                request.MotivoRelevo))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message =
                    "El motivo del retiro es obligatorio.";
                response.desc = null;
                response.data = null;

                return response;
            }


            // ========================================================
            // 5. RETIRAR ELEMENTO
            // ========================================================

            try
            {
                response =
                    await _supervisionFunctions
                        .RetirarElementoAsync(
                            request.SupervisionId,
                            request.MotivoRelevo.Trim(),
                            numeroSupervisor,
                            authorization,
                            ct);
            }
            catch (OperationCanceledException)
            {
                response.isSuccess = false;
                response.code = 408;
                response.message =
                    "La operación de retiro fue cancelada.";
                response.desc = null;
                response.data = null;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al retirar al elemento del servicio.";
                response.desc =
                    ex.Message;
                response.data = null;
            }


            return response;
        }

        #endregion
    }
}