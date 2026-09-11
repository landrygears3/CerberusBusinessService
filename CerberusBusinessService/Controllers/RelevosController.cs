using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Relevos;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Relevos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RelevosController : ControllerBase
    {
        #region CONSTANTES

        private const string ACTIVIDAD_GESTIONAR_RELEVO =
            "ASISTENCIAS.GESTIONAR_RELEVO";

        private const string ACTIVIDAD_SUPERVISOR =
            "ASISTENCIAS.AUTORIZAR_TURNO";

        #endregion


        #region PROPIEDADES

        private readonly RelevoNoPlaneadoFunctions
            _relevoNoPlaneadoFunctions;

        private readonly ValidaAccionFunction _abac;

        #endregion


        #region CONSTRUCTOR

        public RelevosController(
            RelevoNoPlaneadoFunctions relevoNoPlaneadoFunctions,
            ValidaAccionFunction abac)
        {
            _relevoNoPlaneadoFunctions =
                relevoNoPlaneadoFunctions;

            _abac =
                abac;
        }

        #endregion


        #region SOLICITUDES

        [HttpPost("CrearSolicitud")]
        [Consumes("multipart/form-data")]
        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitud(
                [FromForm] CrearSolicitudRelevoNoPlaneadoDto request,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out _,
                    out string token))
                {
                    return CrearError<SolicitudRelevoNoPlaneadoDto>(
                        401,
                        "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_GESTIONAR_RELEVO,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<SolicitudRelevoNoPlaneadoDto>(
                        403,
                        "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<SolicitudRelevoNoPlaneadoDto>(
                        401,
                        "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .CrearSolicitudAsync(
                        request,
                        numeroUsuario,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<SolicitudRelevoNoPlaneadoDto>(
                    408,
                    "La operación fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<SolicitudRelevoNoPlaneadoDto>(
                    500,
                    "Error al crear la solicitud de relevo.",
                    ex.Message);
            }
        }


        [HttpPost("CancelarSolicitud")]
        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CancelarSolicitud(
                [FromBody]
                CancelarSolicitudRelevoNoPlaneadoRequest request,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out _,
                    out string token))
                {
                    return CrearError<SolicitudRelevoNoPlaneadoDto>(
                        401,
                        "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_SUPERVISOR,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<SolicitudRelevoNoPlaneadoDto>(
                        403,
                        "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<SolicitudRelevoNoPlaneadoDto>(
                        401,
                        "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .CancelarSolicitudAsync(
                        request,
                        numeroUsuario,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<SolicitudRelevoNoPlaneadoDto>(
                    408,
                    "La operación fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<SolicitudRelevoNoPlaneadoDto>(
                    500,
                    "Error al cancelar la solicitud de relevo.",
                    ex.Message);
            }
        }

        #endregion


        #region ASIGNACIONES SUPERVISOR

        [HttpPost("AsignarEmpleado")]
        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarEmpleado(
                [FromBody]
                AsignarEmpleadoRelevoNoPlaneadoRequest request,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out string authorization,
                    out string token))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_SUPERVISOR,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        403,
                        "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .AsignarEmpleadoAsync(
                        request,
                        numeroUsuario,
                        authorization,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    408,
                    "La operación fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    500,
                    "Error al asignar al empleado sustituto.",
                    ex.Message);
            }
        }


        [HttpPost("AsignarseSupervisor")]
        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarseSupervisor(
                [FromBody]
                AsignarseSupervisorRelevoNoPlaneadoRequest request,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out string authorization,
                    out string token))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_SUPERVISOR,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        403,
                        "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .AsignarseSupervisorAsync(
                        request,
                        numeroUsuario,
                        authorization,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    408,
                    "La operación fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    500,
                    "Error al registrar la cobertura por supervisor.",
                    ex.Message);
            }
        }


        [HttpPost("AutorizarAsignacion")]
        [Consumes("multipart/form-data")]
        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AutorizarAsignacion(
                [FromForm]
                AutorizarAsignacionRelevoNoPlaneadoRequest request,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out string authorization,
                    out string token))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_SUPERVISOR,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        403,
                        "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .AutorizarAsignacionAsync(
                        request,
                        numeroUsuario,
                        authorization,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    408,
                    "La operación fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    500,
                    "Error al autorizar la asignación.",
                    ex.Message);
            }
        }


        [HttpPost("RechazarAsignacionSupervisor")]
        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionSupervisor(
                [FromBody]
                RechazarAsignacionSupervisorRelevoNoPlaneadoRequest
                    request,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out string authorization,
                    out string token))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_SUPERVISOR,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        403,
                        "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .RechazarAsignacionSupervisorAsync(
                        request,
                        numeroUsuario,
                        authorization,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    408,
                    "La operación fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    500,
                    "Error al rechazar la asignación.",
                    ex.Message);
            }
        }

        #endregion


        #region ASIGNACIONES EMPLEADO

        [HttpPost("FirmarResponsiva")]
        [Consumes("multipart/form-data")]
        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            FirmarResponsiva(
                [FromForm]
                FirmarResponsivaRelevoNoPlaneadoRequest request,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out string authorization,
                    out string token))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_GESTIONAR_RELEVO,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        403,
                        "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .FirmarResponsivaAsync(
                        request,
                        numeroUsuario,
                        authorization,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    408,
                    "La operación fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    500,
                    "Error al firmar la responsiva.",
                    ex.Message);
            }
        }


        [HttpPost("RechazarAsignacion")]
        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacion(
                [FromBody]
                RechazarAsignacionRelevoNoPlaneadoRequest request,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out string authorization,
                    out string token))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_GESTIONAR_RELEVO,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        403,
                        "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<RelevoNoPlaneadoAsignacionDto>(
                        401,
                        "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .RechazarAsignacionAsync(
                        request,
                        numeroUsuario,
                        authorization,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    408,
                    "La operación fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<RelevoNoPlaneadoAsignacionDto>(
                    500,
                    "Error al rechazar la cobertura.",
                    ex.Message);
            }
        }

        #endregion


        #region CONSULTAS EMPLEADO

        [HttpGet("PendientesEmpleado")]
        public async Task<
            ResponseModel<List<RelevoPendienteEmpleadoResponse>>>
            PendientesEmpleado(
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out _,
                    out string token))
                {
                    return CrearError<
                        List<RelevoPendienteEmpleadoResponse>>(
                            401,
                            "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_GESTIONAR_RELEVO,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<
                        List<RelevoPendienteEmpleadoResponse>>(
                            403,
                            "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<
                        List<RelevoPendienteEmpleadoResponse>>(
                            401,
                            "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .ObtenerPendientesEmpleadoAsync(
                        numeroUsuario,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<
                    List<RelevoPendienteEmpleadoResponse>>(
                        408,
                        "La consulta fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<
                    List<RelevoPendienteEmpleadoResponse>>(
                        500,
                        "Error al obtener los relevos pendientes del empleado.",
                        ex.Message);
            }
        }

        #endregion


        #region CONSULTAS SUPERVISOR

        [HttpGet("PendientesSupervisor")]
        public async Task<
            ResponseModel<List<RelevoPendienteSupervisorResponse>>>
            PendientesSupervisor(
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out _,
                    out string token))
                {
                    return CrearError<
                        List<RelevoPendienteSupervisorResponse>>(
                            401,
                            "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_SUPERVISOR,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<
                        List<RelevoPendienteSupervisorResponse>>(
                            403,
                            "No se tiene acceso a esta función");
                }

                string? numeroUsuario =
                    ObtenerNumeroUsuario();

                if (string.IsNullOrWhiteSpace(
                    numeroUsuario))
                {
                    return CrearError<
                        List<RelevoPendienteSupervisorResponse>>(
                            401,
                            "No fue posible identificar al usuario autenticado.");
                }

                return await _relevoNoPlaneadoFunctions
                    .ObtenerPendientesSupervisorAsync(
                        numeroUsuario,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<
                    List<RelevoPendienteSupervisorResponse>>(
                        408,
                        "La consulta fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<
                    List<RelevoPendienteSupervisorResponse>>(
                        500,
                        "Error al obtener los relevos pendientes del supervisor.",
                        ex.Message);
            }
        }

        #endregion


        #region CONSULTAS DETALLE

        [HttpGet("Solicitud/{solicitudId:long}")]
        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerSolicitud(
                long solicitudId,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out _,
                    out string token))
                {
                    return CrearError<
                        SolicitudRelevoNoPlaneadoResponse>(
                            401,
                            "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_GESTIONAR_RELEVO,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<
                        SolicitudRelevoNoPlaneadoResponse>(
                            403,
                            "No se tiene acceso a esta función");
                }

                if (solicitudId <= 0)
                {
                    return CrearError<
                        SolicitudRelevoNoPlaneadoResponse>(
                            400,
                            "SolicitudRelevoNoPlaneadoId es inválido.");
                }

                return await _relevoNoPlaneadoFunctions
                    .ObtenerSolicitudAsync(
                        solicitudId,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<
                    SolicitudRelevoNoPlaneadoResponse>(
                        408,
                        "La consulta fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<
                    SolicitudRelevoNoPlaneadoResponse>(
                        500,
                        "Error al obtener la solicitud de relevo.",
                        ex.Message);
            }
        }


        [HttpGet(
            "Asignacion/{relevoNoPlaneadoAsignacionId:long}")]
        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerAsignacion(
                long relevoNoPlaneadoAsignacionId,
                CancellationToken ct)
        {
            try
            {
                if (!TryGetBearerToken(
                    out _,
                    out string token))
                {
                    return CrearError<
                        SolicitudRelevoNoPlaneadoResponse>(
                            401,
                            "No fue posible obtener un token de autorización válido.");
                }

                bool allowed =
                    await _abac.CheckAsync(
                        ACTIVIDAD_GESTIONAR_RELEVO,
                        token,
                        ct);

                if (!allowed)
                {
                    return CrearError<
                        SolicitudRelevoNoPlaneadoResponse>(
                            403,
                            "No se tiene acceso a esta función");
                }

                if (relevoNoPlaneadoAsignacionId <= 0)
                {
                    return CrearError<
                        SolicitudRelevoNoPlaneadoResponse>(
                            400,
                            "RelevoNoPlaneadoAsignacionId es inválido.");
                }

                return await _relevoNoPlaneadoFunctions
                    .ObtenerAsignacionAsync(
                        relevoNoPlaneadoAsignacionId,
                        ct);
            }
            catch (OperationCanceledException)
            {
                return CrearError<
                    SolicitudRelevoNoPlaneadoResponse>(
                        408,
                        "La consulta fue cancelada.");
            }
            catch (Exception ex)
            {
                return CrearError<
                    SolicitudRelevoNoPlaneadoResponse>(
                        500,
                        "Error al obtener la asignación de relevo.",
                        ex.Message);
            }
        }

        #endregion


        #region AUTENTICACION

        private bool TryGetBearerToken(
            out string authorization,
            out string token)
        {
            authorization =
                Request.Headers.Authorization
                    .ToString();

            token =
                string.Empty;

            if (string.IsNullOrWhiteSpace(
                authorization))
            {
                return false;
            }

            if (!authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            token =
                authorization["Bearer ".Length..]
                    .Trim();

            return !string.IsNullOrWhiteSpace(
                token);
        }


        private string? ObtenerNumeroUsuario()
        {
            string? numeroUsuario =
                User.FindFirst("num")?.Value;

            if (string.IsNullOrWhiteSpace(
                numeroUsuario))
            {
                return null;
            }

            return numeroUsuario.Trim();
        }

        #endregion


        #region RESPONSE

        private static ResponseModel<T>
            CrearError<T>(
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