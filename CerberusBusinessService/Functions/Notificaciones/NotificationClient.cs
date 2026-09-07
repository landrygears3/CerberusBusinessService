using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Notificaciones;
using CerberusBusinessService.Models.Notificaciones;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace CerberusBusinessService.Functions.Notificaciones
{
    public class NotificationClient
    {
        private readonly NotificationOptions _options;


        public NotificationClient(
            IOptions<NotificationOptions> options)
        {
            _options =
                options.Value;
        }


        public async Task<ResponseModel<NotificationResponse<T>>> SendAsync<T>(
            CreateNotificationRequest<T> request,
            string accessToken,
            CancellationToken ct)
        {
            ResponseModel<NotificationResponse<T>> response =
                new ResponseModel<NotificationResponse<T>>
                {
                    isSuccess = false,
                    code = 500,
                    message = "No fue posible enviar la notificación.",
                    desc = null,
                    data = null
                };


            if (request == null)
            {
                response.code = 400;
                response.message = "La notificación es obligatoria.";

                return response;
            }


            if (string.IsNullOrWhiteSpace(request.Type))
            {
                response.code = 400;
                response.message =
                    "El tipo de notificación es obligatorio.";

                return response;
            }


            if (string.IsNullOrWhiteSpace(request.Titulo))
            {
                response.code = 400;
                response.message =
                    "El título de la notificación es obligatorio.";

                return response;
            }


            if (string.IsNullOrWhiteSpace(request.Mensaje))
            {
                response.code = 400;
                response.message =
                    "El mensaje de la notificación es obligatorio.";

                return response;
            }


            if (string.IsNullOrWhiteSpace(accessToken))
            {
                response.code = 401;
                response.message =
                    "El token de autorización es obligatorio.";

                return response;
            }


            if (string.IsNullOrWhiteSpace(_options.HubUrl))
            {
                response.code = 500;
                response.message =
                    "No está configurada la URL del servicio de notificaciones.";

                return response;
            }


            string token =
                NormalizarToken(accessToken);


            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct);


            timeoutCts.CancelAfter(
                TimeSpan.FromSeconds(
                    _options.TimeoutSeconds > 0
                        ? _options.TimeoutSeconds
                        : 30));


            await using var connection =
                new HubConnectionBuilder()
                    .WithUrl(
                        _options.HubUrl,
                        options =>
                        {
                            options.AccessTokenProvider =
                                () =>
                                    Task.FromResult<string?>(
                                        token);
                        })
                    .Build();


            try
            {
                await connection.StartAsync(
                    timeoutCts.Token);


                NotificationResponse<T> notification =
                    await connection
                        .InvokeAsync<NotificationResponse<T>>(
                            "SendNotification",
                            request,
                            timeoutCts.Token);


                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Notificación enviada correctamente.";
                response.desc = null;
                response.data = notification;


                return response;
            }
            catch (OperationCanceledException)
                when (!ct.IsCancellationRequested)
            {
                response.isSuccess = false;
                response.code = 408;
                response.message =
                    "El servicio de notificaciones excedió el tiempo de espera.";
                response.desc = null;
                response.data = null;

                return response;
            }
            catch (OperationCanceledException)
            {
                response.isSuccess = false;
                response.code = 408;
                response.message =
                    "La operación de notificación fue cancelada.";
                response.desc = null;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "No fue posible enviar la notificación.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            finally
            {
                try
                {
                    if (connection.State !=
                        HubConnectionState.Disconnected)
                    {
                        await connection.StopAsync(
                            CancellationToken.None);
                    }
                }
                catch
                {
                    /*
                     * No reemplazar el resultado principal
                     * por un error al cerrar la conexión.
                     */
                }
            }
        }


        private string NormalizarToken(
            string accessToken)
        {
            string token =
                accessToken.Trim();


            if (token.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
            {
                token =
                    token["Bearer ".Length..]
                        .Trim();
            }


            return token;
        }
    }
}