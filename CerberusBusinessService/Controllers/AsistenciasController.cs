using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Asistencias;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Asistencias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AsistenciasController : ControllerBase
    {
        private readonly AsistenciasFunctions _asistenciasFunctions;
        private readonly ValidaAccionFunction _abac;

        public AsistenciasController(ValidaAccionFunction abac,
            AsistenciasFunctions asistenciasFunctions)
        {
            _asistenciasFunctions =
                asistenciasFunctions;
            _abac = abac;
        }


        [HttpPost("CheckIn")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<ResponseModel<CheckInResponse>> CheckIn(
            [FromForm] CheckInRequest request,
            CancellationToken ct)
        {
            ResponseModel<CheckInResponse> response =
                new ResponseModel<CheckInResponse>();
            string tarea = "ASISTENCIAS.GESTIONAR_RELEVO";
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
                    // ====================================================
                    // 1. OBTENER NUMERO DE USUARIO DESDE JWT
                    // ====================================================
                    //
                    // El NumeroUsuario NO viene del front.
                    //
                    // CerberusWebService lo coloca en:
                    //
                    // claim "num"
                    // ====================================================

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


                    // ====================================================
                    // 2. OBTENER TOKEN ORIGINAL
                    // ====================================================
                    //
                    // Se necesita para que BusinessService pueda
                    // comunicarse con CerberusNotificaciones.
                    //
                    // El token se pasa completo:
                    //
                    // Bearer eyJ...
                    //
                    // NotificationClient se encarga de normalizarlo.
                    // ====================================================

                    string accessToken =
                        Request.Headers.Authorization
                            .ToString();


                    if (string.IsNullOrWhiteSpace(
                        accessToken))
                    {
                        response.isSuccess = false;
                        response.code = 401;
                        response.message =
                            "No fue posible obtener el token de autorización.";
                        response.desc = null;
                        response.data = null;

                        return response;
                    }


                    if (!accessToken.StartsWith(
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


                    // ====================================================
                    // 3. PROCESAR CHECK-IN
                    // ====================================================

                    response =
                        await _asistenciasFunctions
                            .ProcesarCheckIn(
                                request,
                                numeroUsuario,
                                accessToken,
                                ct);
                }
                catch (OperationCanceledException)
                {
                    response.isSuccess = false;
                    response.code = 408;
                    response.message =
                        "La operación de Check-In fue cancelada.";
                    response.desc = null;
                    response.data = null;
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message =
                        "Error al registrar el Check-In.";
                    response.desc =
                        ex.Message;
                    response.data =
                        null;
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