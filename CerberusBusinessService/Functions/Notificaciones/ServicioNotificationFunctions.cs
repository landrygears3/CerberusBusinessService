using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Notificaciones;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Notificaciones
{
    public class ServicioNotificationFunctions
    {
        #region PROPIEDADES

        private readonly string _csCerberus;
        private readonly NotificationClient _notificationClient;

        #endregion


        #region CONSTRUCTOR

        public ServicioNotificationFunctions(
            IConfiguration config,
            NotificationClient notificationClient)
        {
            _csCerberus =
                config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");

            _notificationClient = notificationClient;
        }

        #endregion


        #region ENVIAR A SUPERVISORES DEL SERVICIO

        public async Task<ResponseModel<NotificationResponse<T>>>
            EnviarASupervisoresAsync<T>(
                int servicioId,
                DateTime fechaReferencia,
                string type,
                string titulo,
                string mensaje,
                T data,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                CrearRespuestaError<T>(
                    "No fue posible enviar la notificación.");

            if (servicioId <= 0)
            {
                response.code = 400;
                response.message = "ServicioId inválido.";
                return response;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                response.code = 400;
                response.message =
                    "El tipo de notificación es obligatorio.";
                return response;
            }

            if (string.IsNullOrWhiteSpace(titulo))
            {
                response.code = 400;
                response.message =
                    "El título es obligatorio.";
                return response;
            }

            if (string.IsNullOrWhiteSpace(mensaje))
            {
                response.code = 400;
                response.message =
                    "El mensaje es obligatorio.";
                return response;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                response.code = 401;
                response.message =
                    "El token de autorización es obligatorio.";
                return response;
            }

            try
            {
                List<string> supervisores =
                    await ObtenerSupervisoresServicioAsync(
                        servicioId,
                        fechaReferencia,
                        ct);

                if (supervisores.Count == 0)
                {
                    response.code = 404;
                    response.message =
                        "El servicio no tiene supervisores asignados para la fecha indicada.";
                    response.desc =
                        $"ServicioId: {servicioId}. " +
                        $"Fecha: {fechaReferencia:yyyy-MM-dd}.";

                    return response;
                }

                return await EnviarAUsuariosAsync(
                    supervisores,
                    type,
                    titulo,
                    mensaje,
                    data,
                    accessToken,
                    ct);
            }
            catch (SqlException ex)
            {
                response.code = 500;
                response.message =
                    "Error SQL al obtener los supervisores del servicio.";
                response.desc = ex.Message;

                return response;
            }
            catch (Exception ex)
            {
                response.code = 500;
                response.message =
                    "Error al enviar la notificación a los supervisores del servicio.";
                response.desc = ex.Message;

                return response;
            }
        }

        #endregion


        #region ENVIAR A UN USUARIO

        public async Task<ResponseModel<NotificationResponse<T>>>
            EnviarAUsuarioAsync<T>(
                string numeroUsuario,
                string type,
                string titulo,
                string mensaje,
                T data,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                CrearRespuestaError<T>(
                    "No fue posible enviar la notificación.");

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                response.code = 400;
                response.message =
                    "El NumeroUsuario del destinatario es obligatorio.";

                return response;
            }

            return await EnviarAUsuariosAsync(
                new List<string>
                {
                    numeroUsuario.Trim()
                },
                type,
                titulo,
                mensaje,
                data,
                accessToken,
                ct);
        }

        #endregion


        #region ENVIAR A USUARIOS

        public async Task<ResponseModel<NotificationResponse<T>>>
            EnviarAUsuariosAsync<T>(
                IEnumerable<string> numeroUsuarios,
                string type,
                string titulo,
                string mensaje,
                T data,
                string accessToken,
                CancellationToken ct)
        {
            var response =
                CrearRespuestaError<T>(
                    "No fue posible enviar la notificación.");

            if (numeroUsuarios == null)
            {
                response.code = 400;
                response.message =
                    "Los destinatarios son obligatorios.";

                return response;
            }

            List<string> destinatarios =
                numeroUsuarios
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (destinatarios.Count == 0)
            {
                response.code = 400;
                response.message =
                    "Debe existir al menos un destinatario válido.";

                return response;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                response.code = 400;
                response.message =
                    "El tipo de notificación es obligatorio.";

                return response;
            }

            if (string.IsNullOrWhiteSpace(titulo))
            {
                response.code = 400;
                response.message =
                    "El título es obligatorio.";

                return response;
            }

            if (string.IsNullOrWhiteSpace(mensaje))
            {
                response.code = 400;
                response.message =
                    "El mensaje es obligatorio.";

                return response;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                response.code = 401;
                response.message =
                    "El token de autorización es obligatorio.";

                return response;
            }

            try
            {
                var request =
                    new CreateNotificationRequest<T>
                    {
                        Type = type.Trim(),
                        Titulo = titulo.Trim(),
                        Mensaje = mensaje.Trim(),

                        Target =
                            new NotificationTarget
                            {
                                Tipo =
                                    NotificationTargetTypes.Usuarios,

                                NumeroUsuarios =
                                    destinatarios,

                                RolIds =
                                    new List<int>(),

                                ActividadIds =
                                    new List<int>(),

                                MatchMode =
                                    "ANY"
                            },

                        Data = data
                    };

                return await _notificationClient.SendAsync(
                    request,
                    accessToken,
                    ct);
            }
            catch (Exception ex)
            {
                response.code = 500;
                response.message =
                    "Error al enviar la notificación a los usuarios.";
                response.desc = ex.Message;

                return response;
            }
        }

        #endregion


        #region OBTENER SUPERVISORES DEL SERVICIO

        private async Task<List<string>>
            ObtenerSupervisoresServicioAsync(
                int servicioId,
                DateTime fechaReferencia,
                CancellationToken ct)
        {
            using var conn =
                new SqlConnection(_csCerberus);

            await conn.OpenAsync(ct);

            const string sql = @"
SELECT DISTINCT
    LTRIM(RTRIM(E.UsuarioAsignado)) AS NumeroUsuario
FROM dbo.ServicioSupervisor SS
INNER JOIN dbo.DatosGeneralesEmpleado E
    ON E.ID = SS.SupervisorEmpleadoId
WHERE SS.ServicioId = @ServicioId
  AND SS.FechaInicio <= @Fecha
  AND
  (
      SS.FechaFin IS NULL
      OR SS.FechaFin >= @Fecha
  )
  AND NULLIF(
      LTRIM(RTRIM(E.UsuarioAsignado)),
      ''
  ) IS NOT NULL
ORDER BY NumeroUsuario;";

            var result =
                await conn.QueryAsync<string>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            ServicioId = servicioId,
                            Fecha = fechaReferencia.Date
                        },
                        cancellationToken: ct));

            return result
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #endregion


        #region RESPONSE ERROR

        private ResponseModel<NotificationResponse<T>>
            CrearRespuestaError<T>(
                string message)
        {
            return new ResponseModel<NotificationResponse<T>>
            {
                isSuccess = false,
                code = 500,
                message = message,
                desc = null,
                data = null
            };
        }

        #endregion
    }
}