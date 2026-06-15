using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Candidatos;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Candidatos;
using CerberusBusinessService.Models.DTO.Empleados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CandidatosConroller : ControllerBase
    {
        private readonly ValidaAccionFunction _abac;
        private readonly CandidatosFunctions _candidatosFunctions;

        public CandidatosConroller(ValidaAccionFunction abac, CandidatosFunctions candidatosFunctions)
        {
            _abac = abac;
            _candidatosFunctions = candidatosFunctions;
        }

        #region Generales
        [HttpPost("GetCandidatos")]
        [Authorize]
        public async Task<ResponseModel<List<ListadoCandidatosResponse>>> ObtenerListadoCandidatos(CancellationToken ct)
        {
            ResponseModel<List<ListadoCandidatosResponse>> response = new ResponseModel<List<ListadoCandidatosResponse>>();
            // return await _candidatosFunctions.ObtenerListadoCandidatos();
            string tarea = "MODULO.RHH.CANDIDATOS.VER";
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

                    response = await _candidatosFunctions.ObtenerListadoCandidatos(ct);


                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al obtener el listado de candidatos";
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
        
        [HttpPost("CommitGeneralCandidato")]
        [Authorize]
        public async Task<ResponseModel<CommitDatosGeneralesCandidatoResponse>>  CommitDatosGeneralesCandidato(
        CommitDatosGeneralesCandidatoRequest data, CancellationToken ct)
        {
            ResponseModel<CommitDatosGeneralesCandidatoResponse> response = new ResponseModel<CommitDatosGeneralesCandidatoResponse>();

            string tarea = "MODULO.RHH.CANDIDATOS.COMMIT";
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

                    response = await _candidatosFunctions.CommitDatosGeneralesCandidato(data, ct) ;


                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al actualizar candidato";
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

        //ObtenerDatosGeneralesCandidato
        [HttpPost("ObtenerDatosGeneralesCandidato")]
        [Authorize]
        public async Task<ResponseModel<ObtenerDatosGeneralesCandidatoResponse>> ObtenerDatosGeneralesCandidato(ObtenerDatosGeneralesCandidatoRequest request, CancellationToken ct)
        {
            ResponseModel<ObtenerDatosGeneralesCandidatoResponse> response = new ResponseModel<ObtenerDatosGeneralesCandidatoResponse>();
            string tarea = "MODULO.RHH.CANDIDATOS.VER";
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
                    if (request == null || request.Id == 0)
                    {
                        response.isSuccess = false;
                        response.code = 400;
                        response.message = "Request inválido";
                        response.desc = "El campo Id es obligatorio.";
                        response.data = null;
                        return response;
                    }
                    var result = await _candidatosFunctions.ObtenerDatosGeneralesCandidato(request);
                    if (!result.isSuccess)
                    {
                        response.isSuccess = false;
                        response.code = result.code == 0 ? 400 : result.code;
                        response.message = "Error al obtener información del candidato";
                        response.desc = result.desc;
                        response.data = null;
                        return response;
                    }
                    response.isSuccess = true;
                    response.code = 200;
                    response.message = "Información del candidato obtenida correctamente";
                    response.desc = result.desc;
                    response.data = result.data;
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al obtener información del candidato";
                    response.desc = ex.Message;
                    response.data = null;
                }
            }
            else
            {
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
                response.data = null;
            }
            return response;
        }
        #endregion

        #region salud
        [HttpPost("CommitSaludCandidatos")]
        [Authorize]
        public async Task<ResponseModel<bool>> CommitSaludCandidatos(CommitCandidatosSaludRequest data, CancellationToken ct)
        {
            ResponseModel<bool> response = new ResponseModel<bool>();

            string tarea = "MODULO.RHH.CANDIDATOS.COMMIT";
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

                    response = await _candidatosFunctions.CommitSaludCandidatoAsync(data.candidatoId, data.datos, ct);


                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al actualizar candidato";
                    response.desc = ex.Message;
                    response.data = false;

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

        [HttpPost("GetSaludCandidato")]
        [Authorize]
        public async Task<ResponseModel<SaludGetResponse>> GetSaludCandidato(SaludGetRequest request, CancellationToken ct)
        {
            ResponseModel<SaludGetResponse> response = new ResponseModel<SaludGetResponse>();
            string tarea = "MODULO.RHH.CANDIDATOS.VER";

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
                    if (request == null || string.IsNullOrWhiteSpace(request.Usuario))
                    {
                        response.isSuccess = false;
                        response.code = 400;
                        response.message = "Request inválido";
                        response.desc = "El campo Usuario es obligatorio.";
                        response.data = null;
                        return response;
                    }

                    var result = await _candidatosFunctions.GetSaludAsync(request.Usuario, ct);

                    if (!result.isSuccess)
                    {
                        response.isSuccess = false;
                        response.code = result.code == 0 ? 400 : result.code;
                        response.message = "Error al obtener información de salud";
                        response.desc = result.desc;
                        response.data = null;
                        return response;
                    }

                    response.isSuccess = true;
                    response.code = 200;
                    response.message = "Información de salud obtenida correctamente";
                    response.desc = result.desc;
                    response.data = result.data;
                }
                catch (Exception ex)
                {
                    response.isSuccess = false;
                    response.code = 500;
                    response.message = "Error al obtener información de salud";
                    response.desc = ex.Message;
                    response.data = null;
                }
            }
            else
            {
                response.isSuccess = false;
                response.code = 403;
                response.message = "No se tiene acceso a esta función";
                response.data = null;
            }

            return response;
        }
        #endregion

        #region Domicilios
        [HttpPost("CandidatoActualizaPrincipal")]
        [Authorize]
        public async Task<ResponseModel<string>> ActualizaPrincipal([FromBody] ActualizaDomicilioPrincipalCandidatoRequest req, CancellationToken ct)
        {
            ResponseModel<string> response = new ResponseModel<string>();

            string tarea = "MODULO.RHH.CANDIDATOS.COMMIT";
            // 1) Tomar el bearer token del request actual
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _candidatosFunctions.ActualizarDomicilioPrincipalCandidato(req);
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

        [HttpPost("CandidatoEliminaDomicilio")]
        [Authorize]
        public async Task<ResponseModel<string>> EliminaDomicilio([FromBody] EliminadoDomicilioCandidatoRequest req, CancellationToken ct)
        {
            ResponseModel<string> response = new ResponseModel<string>();

            string tarea = "MODULO.RHH.CANDIDATOS.COMMIT";
            // 1) Tomar el bearer token del request actual
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _candidatosFunctions.EliminarDomicilioCandidato(req);
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

        [HttpPost("CandidatoListadoDomicilios")]
        [Authorize]
        public async Task<ResponseModel<List<ListadoDomiciliosResponse>>> ListadoDomicilios([FromBody] ListadoDomiciliosCandidatoRequest req, CancellationToken ct)
        {
            ResponseModel<List<ListadoDomiciliosResponse>> response = new ResponseModel<List<ListadoDomiciliosResponse>>();

            string tarea = "MODULO.RHH.CANDIDATOS.COMMIT";
            // 1) Tomar el bearer token del request actual
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _candidatosFunctions.ListadoDomiciliosCandidato(req.Candidato);
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

        [HttpPost("AltaDomicilio")]
        [Authorize]
        public async Task<ResponseModel<string>> AltaDomicilio([FromBody] AltaDomicilioCandidatoRequest req, CancellationToken ct)
        {
            ResponseModel<string> response = new ResponseModel<string>();

            string tarea = "MODULO.RHH.CANDIDATOS.COMMIT";
            // 1) Tomar el bearer token del request actual
            var auth = Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : auth.Trim();

            // 2) Llamar ABAC
            var allowed = await _abac.CheckAsync(tarea, token, ct);
            if (allowed)
            {
                response = await _candidatosFunctions.AltaDomiciliosCandidato(req);
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
        #endregion
    }
}
