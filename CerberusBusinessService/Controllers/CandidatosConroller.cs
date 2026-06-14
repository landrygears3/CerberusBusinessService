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

    }
}
