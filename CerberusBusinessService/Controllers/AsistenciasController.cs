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
        #region CONSTANTES

        private const string ACTIVIDAD_GESTIONAR_RELEVO =
            "ASISTENCIAS.GESTIONAR_RELEVO";

        #endregion


        #region PROPIEDADES

        private readonly AsistenciasFunctions _asistenciasFunctions;
        private readonly ValidaAccionFunction _abac;

        #endregion


        #region CONSTRUCTOR

        public AsistenciasController(
            ValidaAccionFunction abac,
            AsistenciasFunctions asistenciasFunctions)
        {
            _abac = abac;
            _asistenciasFunctions = asistenciasFunctions;
        }

        #endregion


        #region CHECK-IN

        [HttpPost("CheckIn")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<ResponseModel<CheckInResponse>> CheckIn(
            [FromForm] CheckInRequest request,
            CancellationToken ct)
        {
            if (!TryGetBearerToken(out string authorization, out string token))
            {
                return CrearError<CheckInResponse>(
                    401,
                    "No fue posible obtener un token de autorización válido.");
            }

            bool allowed = await _abac.CheckAsync(
                ACTIVIDAD_GESTIONAR_RELEVO,
                token,
                ct);

            if (!allowed)
            {
                return CrearError<CheckInResponse>(
                    403,
                    "No se tiene acceso a esta función");
            }

            string? numeroUsuario = ObtenerNumeroUsuario();

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                return CrearError<CheckInResponse>(
                    401,
                    "No fue posible identificar al usuario autenticado.");
            }

            try
            {
                return await _asistenciasFunctions.ProcesarCheckIn(
                    request,
                    numeroUsuario,
                    authorization,
                    ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<CheckInResponse>(
                    408,
                    "La operación de Check-In fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<CheckInResponse>(
                    500,
                    "Error al registrar el Check-In.",
                    ex.Message);
            }
        }

        #endregion


        #region RELEVOS ESPERADOS CHECK-OUT

        [HttpGet("RelevosEsperadosCheckOut")]
        [Authorize]
        public async Task<ResponseModel<List<RelevoEsperadoCheckOutResponse>>>
            RelevosEsperadosCheckOut(
                CancellationToken ct)
        {
            if (!TryGetBearerToken(out _, out string token))
            {
                return CrearError<List<RelevoEsperadoCheckOutResponse>>(
                    401,
                    "No fue posible obtener un token de autorización válido.");
            }

            bool allowed = await _abac.CheckAsync(
                ACTIVIDAD_GESTIONAR_RELEVO,
                token,
                ct);

            if (!allowed)
            {
                return CrearError<List<RelevoEsperadoCheckOutResponse>>(
                    403,
                    "No se tiene acceso a esta función");
            }

            string? numeroUsuario = ObtenerNumeroUsuario();

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                return CrearError<List<RelevoEsperadoCheckOutResponse>>(
                    401,
                    "No fue posible identificar al usuario autenticado.");
            }

            try
            {
                return await _asistenciasFunctions
                    .ObtenerRelevosEsperadosCheckOutAsync(
                        numeroUsuario,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<List<RelevoEsperadoCheckOutResponse>>(
                    408,
                    "La consulta de relevos esperados fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<List<RelevoEsperadoCheckOutResponse>>(
                    500,
                    "Error al obtener los relevos esperados.",
                    ex.Message);
            }
        }

        #endregion


        #region CHECK-OUT SIN RELEVO

        [HttpPost("CheckOutSinRelevo")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<ResponseModel<CheckOutRelevoResponse>>
            CheckOutSinRelevo(
                [FromForm] CheckOutRelevoRequest request,
                CancellationToken ct)
        {
            if (!TryGetBearerToken(out string authorization, out string token))
            {
                return CrearError<CheckOutRelevoResponse>(
                    401,
                    "No fue posible obtener un token de autorización válido.");
            }

            bool allowed = await _abac.CheckAsync(
                ACTIVIDAD_GESTIONAR_RELEVO,
                token,
                ct);

            if (!allowed)
            {
                return CrearError<CheckOutRelevoResponse>(
                    403,
                    "No se tiene acceso a esta función");
            }

            string? numeroUsuario = ObtenerNumeroUsuario();

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                return CrearError<CheckOutRelevoResponse>(
                    401,
                    "No fue posible identificar al usuario autenticado.");
            }

            if (request == null)
            {
                return CrearError<CheckOutRelevoResponse>(
                    400,
                    "El request es obligatorio.");
            }

            if (request.ServicioEmpleadoAfectadoId <= 0)
            {
                return CrearError<CheckOutRelevoResponse>(
                    400,
                    "ServicioEmpleadoAfectadoId es inválido.");
            }

            if (request.FotoEvidencia == null ||
                request.FotoEvidencia.Length == 0)
            {
                return CrearError<CheckOutRelevoResponse>(
                    400,
                    "La fotografía de evidencia es obligatoria.");
            }

            if (!request.PuedePermanecer &&
                string.IsNullOrWhiteSpace(request.MotivoNoPermanencia))
            {
                return CrearError<CheckOutRelevoResponse>(
                    400,
                    "El motivo por el cual el empleado no puede permanecer es obligatorio.");
            }

            try
            {
                return await _asistenciasFunctions
                    .ProcesarCheckOutSinRelevoAsync(
                        request,
                        numeroUsuario,
                        authorization,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<CheckOutRelevoResponse>(
                    408,
                    "La operación de Check-Out sin relevo fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<CheckOutRelevoResponse>(
                    500,
                    "Error al procesar el Check-Out sin relevo.",
                    ex.Message);
            }
        }

        #endregion


        #region AUTENTICACION

        private bool TryGetBearerToken(
            out string authorization,
            out string token)
        {
            authorization = Request.Headers.Authorization.ToString();
            token = string.Empty;

            if (string.IsNullOrWhiteSpace(authorization))
            {
                return false;
            }

            if (!authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            token = authorization["Bearer ".Length..].Trim();

            return !string.IsNullOrWhiteSpace(token);
        }


        private string? ObtenerNumeroUsuario()
        {
            string? numeroUsuario =
                User.FindFirst("num")?.Value;

            return string.IsNullOrWhiteSpace(numeroUsuario)
                ? null
                : numeroUsuario.Trim();
        }

        #endregion


        #region RESPONSE

        private static ResponseModel<T> CrearError<T>(
            int code,
            string message,
            string? desc = null)
        {
            return new ResponseModel<T>
            {
                isSuccess = false,
                code = code,
                message = message,
                desc = desc,
                data = default
            };
        }

        #endregion
    }
}