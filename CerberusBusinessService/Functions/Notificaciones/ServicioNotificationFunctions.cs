using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Notificaciones;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Notificaciones
{
    public class ServicioNotificationFunctions
    {
        private readonly string _csCerberus;
        private readonly NotificationClient _notificationClient;


        public ServicioNotificationFunctions(
            IConfiguration config,
            NotificationClient notificationClient)
        {
            _csCerberus =
                config.GetConnectionString(
                    "DefaultConnection")!;

            _notificationClient =
                notificationClient;
        }


        // ============================================================
        // ENVIAR NOTIFICACIÓN A SUPERVISORES DEL SERVICIO
        // ============================================================

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
            ResponseModel<NotificationResponse<T>> response =
                new ResponseModel<NotificationResponse<T>>
                {
                    isSuccess = false,
                    code = 500,
                    message =
                        "No fue posible enviar la notificación.",
                    desc = null,
                    data = null
                };


            if (servicioId <= 0)
            {
                response.code = 400;
                response.message =
                    "ServicioId inválido.";

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
                // ====================================================
                // 1. OBTENER SUPERVISORES DEL SERVICIO
                // ====================================================

                List<string> supervisores =
                    await ObtenerSupervisoresServicioAsync(
                        servicioId,
                        fechaReferencia,
                        ct);


                if (supervisores.Count == 0)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El servicio no tiene supervisores asignados para la fecha indicada.";
                    response.desc =
                        $"ServicioId: {servicioId}. " +
                        $"Fecha: {fechaReferencia:yyyy-MM-dd}.";
                    response.data = null;

                    return response;
                }


                // ====================================================
                // 2. CONSTRUIR NOTIFICACIÓN
                // ====================================================

                var request =
                    new CreateNotificationRequest<T>
                    {
                        Type =
                            type.Trim(),

                        Titulo =
                            titulo.Trim(),

                        Mensaje =
                            mensaje.Trim(),

                        Target =
                            new NotificationTarget
                            {
                                Tipo =
                                    NotificationTargetTypes
                                        .Usuarios,

                                NumeroUsuarios =
                                    supervisores,

                                RolIds =
                                    new List<int>(),

                                ActividadIds =
                                    new List<int>(),

                                MatchMode =
                                    "ANY"
                            },

                        Data =
                            data
                    };


                // ====================================================
                // 3. NOTIFICACIONES
                // ====================================================

                response =
                    await _notificationClient
                        .SendAsync(
                            request,
                            accessToken,
                            ct);


                return response;
            }
            catch (SqlException ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al obtener los supervisores del servicio.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al enviar la notificación a los supervisores del servicio.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }


        // ============================================================
        // OBTENER SUPERVISORES VIGENTES DEL SERVICIO
        // ============================================================

        private async Task<List<string>>
            ObtenerSupervisoresServicioAsync(
                int servicioId,
                DateTime fechaReferencia,
                CancellationToken ct)
        {
            using var conn =
                new SqlConnection(
                    _csCerberus);


            await conn.OpenAsync(ct);


            /*
             * SupervisorEmpleadoId pertenece a
             * DatosGeneralesEmpleado.ID.
             *
             * UsuarioAsignado es el NumeroUsuario CERxxxxx
             * que utiliza SignalR / Seguridad.
             *
             * Se toman TODOS los supervisores cuya
             * asignación esté vigente en esa fecha.
             *
             * No filtramos HoraEntrada / HoraSalida.
             */

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
                            ServicioId =
                                servicioId,

                            Fecha =
                                fechaReferencia.Date
                        },
                        cancellationToken: ct));


            return result
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}