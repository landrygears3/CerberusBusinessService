using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Oficinas;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Oficinas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CerberusBusinessService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OficinasController : ControllerBase
    {
        #region CONSTANTES

        private const string ACTIVIDAD_VER =
            "OFICINAS.VER";

        private const string ACTIVIDAD_COMMIT =
            "OFICINAS.COMMIT";

        private const string ACTIVIDAD_ASIGNAR =
            "OFICINAS.EMPLEADOS.ASIGNAR";

        #endregion

        #region PROPIEDADES

        private readonly ValidaAccionFunction _abac;
        private readonly OficinasFunctions _oficinasFunctions;

        #endregion

        #region CONSTRUCTOR

        public OficinasController(
            ValidaAccionFunction abac,
            OficinasFunctions oficinasFunctions)
        {
            _abac = abac;
            _oficinasFunctions = oficinasFunctions;
        }

        #endregion

        #region COMMIT OFICINA

        [HttpPost("CommitOficina")]
        [Authorize]
        public async Task<ResponseModel<CommitOficinaResponse>>
            CommitOficina(
                [FromBody] CommitOficinaRequest request,
                CancellationToken ct)
        {
            if (!TryGetToken(out string token))
            {
                return NoAutorizado<CommitOficinaResponse>();
            }

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_COMMIT,
                    token,
                    ct);

            if (!allowed)
            {
                return NoAutorizado<CommitOficinaResponse>();
            }

            string? numeroUsuario =
                ObtenerNumeroUsuario();

            if (string.IsNullOrWhiteSpace(
                numeroUsuario))
            {
                return NoAutorizado<CommitOficinaResponse>();
            }

            return await _oficinasFunctions
                .CommitOficinaAsync(
                    request,
                    numeroUsuario,
                    ct);
        }

        #endregion

        #region GET OFICINAS

        [HttpPost("GetOficinas")]
        [Authorize]
        public async Task<ResponseModel<List<OficinaResponse>>>
            GetOficinas(
                CancellationToken ct)
        {
            if (!TryGetToken(out string token))
            {
                return NoAutorizado<List<OficinaResponse>>();
            }

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_VER,
                    token,
                    ct);

            if (!allowed)
            {
                return NoAutorizado<List<OficinaResponse>>();
            }

            return await _oficinasFunctions
                .ObtenerOficinasAsync(ct);
        }

        #endregion

        #region GET OFICINA

        [HttpPost("GetOficina")]
        [Authorize]
        public async Task<ResponseModel<OficinaResponse>>
            GetOficina(
                [FromBody] GetOficinaRequest request,
                CancellationToken ct)
        {
            if (!TryGetToken(out string token))
            {
                return NoAutorizado<OficinaResponse>();
            }

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_VER,
                    token,
                    ct);

            if (!allowed)
            {
                return NoAutorizado<OficinaResponse>();
            }

            return await _oficinasFunctions
                .ObtenerOficinaAsync(
                    request,
                    ct);
        }

        #endregion

        #region COMMIT SERVICIO OFICINA

        [HttpPost("CommitServicioOficina")]
        [Authorize]
        public async Task<ResponseModel<CommitServicioOficinaResponse>>
            CommitServicioOficina(
                [FromBody] CommitServicioOficinaRequest request,
                CancellationToken ct)
        {
            if (!TryGetToken(out string token))
            {
                return NoAutorizado<CommitServicioOficinaResponse>();
            }

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_COMMIT,
                    token,
                    ct);

            if (!allowed)
            {
                return NoAutorizado<CommitServicioOficinaResponse>();
            }

            string? numeroUsuario =
                ObtenerNumeroUsuario();

            if (string.IsNullOrWhiteSpace(
                numeroUsuario))
            {
                return NoAutorizado<CommitServicioOficinaResponse>();
            }

            return await _oficinasFunctions
                .CommitServicioOficinaAsync(
                    request,
                    numeroUsuario,
                    ct);
        }

        #endregion

        #region GET SERVICIOS OFICINA

        [HttpPost("GetServiciosOficina")]
        [Authorize]
        public async Task<
            ResponseModel<List<ServicioOficinaResponse>>>
            GetServiciosOficina(
                [FromBody] GetOficinaRequest request,
                CancellationToken ct)
        {
            if (!TryGetToken(out string token))
            {
                return NoAutorizado<
                    List<ServicioOficinaResponse>>();
            }

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_VER,
                    token,
                    ct);

            if (!allowed)
            {
                return NoAutorizado<
                    List<ServicioOficinaResponse>>();
            }

            return await _oficinasFunctions
                .ObtenerServiciosOficinaAsync(
                    request,
                    ct);
        }

        #endregion

        #region GET SERVICIO OFICINA

        [HttpPost("GetServicioOficina")]
        [Authorize]
        public async Task<ResponseModel<ServicioOficinaResponse>>
            GetServicioOficina(
                [FromBody] GetServicioOficinaRequest request,
                CancellationToken ct)
        {
            if (!TryGetToken(out string token))
            {
                return NoAutorizado<ServicioOficinaResponse>();
            }

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_VER,
                    token,
                    ct);

            if (!allowed)
            {
                return NoAutorizado<ServicioOficinaResponse>();
            }

            return await _oficinasFunctions
                .ObtenerServicioOficinaAsync(
                    request,
                    ct);
        }

        #endregion

        #region ASIGNAR EMPLEADO

        [HttpPost("AsignarEmpleado")]
        [Authorize]
        public async Task<
            ResponseModel<AsignarEmpleadoServicioOficinaResponse>>
            AsignarEmpleado(
                [FromBody]
                AsignarEmpleadoServicioOficinaRequest request,
                CancellationToken ct)
        {
            if (!TryGetToken(out string token))
            {
                return NoAutorizado<
                    AsignarEmpleadoServicioOficinaResponse>();
            }

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_ASIGNAR,
                    token,
                    ct);

            if (!allowed)
            {
                return NoAutorizado<
                    AsignarEmpleadoServicioOficinaResponse>();
            }

            string? numeroUsuario =
                ObtenerNumeroUsuario();

            if (string.IsNullOrWhiteSpace(
                numeroUsuario))
            {
                return NoAutorizado<
                    AsignarEmpleadoServicioOficinaResponse>();
            }

            return await _oficinasFunctions
                .AsignarEmpleadoAsync(
                    request,
                    numeroUsuario,
                    ct);
        }

        #endregion

        #region GET EMPLEADOS SERVICIO

        [HttpPost("GetEmpleadosServicio")]
        [Authorize]
        public async Task<
            ResponseModel<List<ServicioOficinaEmpleadoResponse>>>
            GetEmpleadosServicio(
                [FromBody]
                GetServicioOficinaRequest request,
                CancellationToken ct)
        {
            if (!TryGetToken(out string token))
            {
                return NoAutorizado<
                    List<ServicioOficinaEmpleadoResponse>>();
            }

            bool allowed =
                await _abac.CheckAsync(
                    ACTIVIDAD_VER,
                    token,
                    ct);

            if (!allowed)
            {
                return NoAutorizado<
                    List<ServicioOficinaEmpleadoResponse>>();
            }

            return await _oficinasFunctions
                .ObtenerEmpleadosServicioAsync(
                    request,
                    ct);
        }

        #endregion

        #region AUTENTICACION

        private bool TryGetToken(
            out string token)
        {
            string authorization =
                Request.Headers.Authorization.ToString();

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

            return string.IsNullOrWhiteSpace(
                numeroUsuario)
                ? null
                : numeroUsuario.Trim();
        }

        #endregion

        #region RESPONSE

        private static ResponseModel<T>
            NoAutorizado<T>()
        {
            return new ResponseModel<T>
            {
                isSuccess = false,
                code = 401,
                message = "No autorizado",
                desc = null,
                data = default
            };
        }

        #endregion
    }
}