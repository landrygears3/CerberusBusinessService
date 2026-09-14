using CerberusBusinessService.Models.DTO;
using System.Net.Http.Headers;

namespace CerberusBusinessService.Functions.Seguridad
{
    public class UsuarioRolesFunction
    {
        #region PROPIEDADES

        private readonly HttpClient _http;

        #endregion

        #region CONSTRUCTOR

        public UsuarioRolesFunction(
            HttpClient http)
        {
            _http = http;
        }

        #endregion

        #region OBTENER ROLES

        public async Task<List<string>>
            ObtenerRolesAsync(
                string bearerToken,
                CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(
                bearerToken))
            {
                return new List<string>();
            }

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    "api/abac/roles");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    bearerToken);

            using HttpResponseMessage response =
                await _http.SendAsync(
                    request,
                    ct);

            response.EnsureSuccessStatusCode();

            ResponseModel<List<string>>? result =
                await response.Content
                    .ReadFromJsonAsync<
                        ResponseModel<List<string>>>(
                        cancellationToken: ct);

            if (result == null)
            {
                throw new InvalidOperationException(
                    "La respuesta de roles es nula.");
            }

            if (!result.isSuccess)
            {
                throw new InvalidOperationException(
                    result.message
                    ?? "No fue posible obtener los roles.");
            }

            return result.data
                ?? new List<string>();
        }

        #endregion
    }
}