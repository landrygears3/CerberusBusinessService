using CerberusBusinessService.Models.DTO;

namespace CerberusBusinessService.Functions
{
    public class ValidaAccionFunction
    {
        private readonly HttpClient _http;

        public ValidaAccionFunction(HttpClient http)
        {
            _http = http;
        }

        public async Task<bool> CheckAsync(string activityKey, string bearerToken, CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/abac/check")
            {
                Content = JsonContent.Create(new { activityKey })
            };

            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            // El servicio regresa un boolean (true/false) en el body
            var result = await resp.Content.ReadFromJsonAsync<ResponseModel<bool>>(cancellationToken: ct);
            if (result == null)
            {
                throw new InvalidOperationException("La respuesta del servicio ABAC es nula.");
            }
            return result.Data;
        }
    }
}
