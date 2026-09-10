using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Notificaciones;

namespace CerberusBusinessService.Functions.Notificaciones
{
    public class RelevoNotificationFunctions
    {
        #region TIPOS DE NOTIFICACION

        public const string RELEVO_COBERTURA_REQUERIDA =
            "RELEVO_COBERTURA_REQUERIDA";

        public const string RELEVO_EXTENSION_AUTORIZAR =
            "RELEVO_EXTENSION_AUTORIZAR";

        public const string RELEVO_EXTENSION_FIRMAR =
            "RELEVO_EXTENSION_FIRMAR";

        public const string RELEVO_ASIGNACION_FIRMAR =
            "RELEVO_ASIGNACION_FIRMAR";

        public const string RELEVO_COBERTURA_ACEPTADA =
            "RELEVO_COBERTURA_ACEPTADA";

        #endregion


        #region PROPIEDADES

        private readonly ServicioNotificationFunctions
            _servicioNotificationFunctions;

        #endregion


        #region CONSTRUCTOR

        public RelevoNotificationFunctions(
            ServicioNotificationFunctions servicioNotificationFunctions)
        {
            _servicioNotificationFunctions =
                servicioNotificationFunctions;
        }

        #endregion


        #region COBERTURA REQUERIDA

        public async Task<ResponseModel<NotificationResponse<object>>>
            NotificarCoberturaRequeridaAsync(
                int servicioId,
                string nombreServicio,
                long solicitudId,
                long servicioEmpleadoAfectadoId,
                DateTime fechaHoraInicioCobertura,
                DateTime fechaHoraFinCobertura,
                string motivoRelevo,
                string origenClave,
                string accessToken,
                CancellationToken ct)
        {
            object notificationData =
                new
                {
                    Evento =
                        RELEVO_COBERTURA_REQUERIDA,

                    Accion =
                        "GESTIONAR_COBERTURA",

                    SolicitudRelevoNoPlaneadoId =
                        solicitudId,

                    ServicioId =
                        servicioId,

                    ServicioEmpleadoAfectadoId =
                        servicioEmpleadoAfectadoId,

                    NombreServicio =
                        nombreServicio,

                    OrigenClave =
                        origenClave,

                    FechaHoraInicioCobertura =
                        fechaHoraInicioCobertura,

                    FechaHoraFinCobertura =
                        fechaHoraFinCobertura,

                    MotivoRelevo =
                        motivoRelevo
                };

            return await EjecutarSeguroAsync(
                () =>
                    _servicioNotificationFunctions
                        .EnviarASupervisoresAsync(
                            servicioId,
                            fechaHoraInicioCobertura.Date,
                            RELEVO_COBERTURA_REQUERIDA,
                            "Cobertura requerida",
                            $"Se requiere una cobertura no planeada para el servicio {nombreServicio}.",
                            notificationData,
                            accessToken,
                            ct));
        }

        #endregion


        #region EXTENSION PENDIENTE DE AUTORIZACION

        public async Task<ResponseModel<NotificationResponse<object>>>
            NotificarExtensionPendienteAutorizacionAsync(
                int servicioId,
                string nombreServicio,
                long solicitudId,
                long relevoNoPlaneadoAsignacionId,
                int empleadoId,
                string numeroEmpleado,
                string nombreEmpleado,
                DateTime fechaHoraInicioCobertura,
                DateTime fechaHoraFinCobertura,
                string textoResponsiva,
                string accessToken,
                CancellationToken ct)
        {
            object notificationData =
                new
                {
                    Evento =
                        RELEVO_EXTENSION_AUTORIZAR,

                    Accion =
                        "AUTORIZAR_EXTENSION",

                    SolicitudRelevoNoPlaneadoId =
                        solicitudId,

                    RelevoNoPlaneadoAsignacionId =
                        relevoNoPlaneadoAsignacionId,

                    ServicioId =
                        servicioId,

                    NombreServicio =
                        nombreServicio,

                    EmpleadoId =
                        empleadoId,

                    NumeroEmpleado =
                        numeroEmpleado,

                    NombreEmpleado =
                        nombreEmpleado,

                    TipoCoberturaClave =
                        "EXTENSION",

                    FechaHoraInicioCobertura =
                        fechaHoraInicioCobertura,

                    FechaHoraFinCobertura =
                        fechaHoraFinCobertura,

                    TextoResponsiva =
                        textoResponsiva
                };

            return await EjecutarSeguroAsync(
                () =>
                    _servicioNotificationFunctions
                        .EnviarASupervisoresAsync(
                            servicioId,
                            fechaHoraInicioCobertura.Date,
                            RELEVO_EXTENSION_AUTORIZAR,
                            "Extensión pendiente de autorización",
                            $"{nombreEmpleado} puede permanecer en el servicio {nombreServicio} y requiere autorización para extender su turno.",
                            notificationData,
                            accessToken,
                            ct));
        }

        #endregion


        #region EXTENSION PENDIENTE DE FIRMA

        public async Task<ResponseModel<NotificationResponse<object>>>
            NotificarExtensionPendienteFirmaEmpleadoAsync(
                string numeroEmpleado,
                int servicioId,
                string nombreServicio,
                long solicitudId,
                long relevoNoPlaneadoAsignacionId,
                DateTime fechaHoraInicioCobertura,
                DateTime fechaHoraFinCobertura,
                string textoResponsiva,
                string accessToken,
                CancellationToken ct)
        {
            object notificationData =
                new
                {
                    Evento =
                        RELEVO_EXTENSION_FIRMAR,

                    Accion =
                        "FIRMAR_RESPONSIVA",

                    SolicitudRelevoNoPlaneadoId =
                        solicitudId,

                    RelevoNoPlaneadoAsignacionId =
                        relevoNoPlaneadoAsignacionId,

                    ServicioId =
                        servicioId,

                    NombreServicio =
                        nombreServicio,

                    TipoCoberturaClave =
                        "EXTENSION",

                    FechaHoraInicioCobertura =
                        fechaHoraInicioCobertura,

                    FechaHoraFinCobertura =
                        fechaHoraFinCobertura,

                    TextoResponsiva =
                        textoResponsiva
                };

            return await EjecutarSeguroAsync(
                () =>
                    _servicioNotificationFunctions
                        .EnviarAUsuarioAsync(
                            numeroEmpleado,
                            RELEVO_EXTENSION_FIRMAR,
                            "Extensión pendiente de aceptación",
                            $"La extensión de tu turno en el servicio {nombreServicio} fue autorizada. Revisa y firma la responsiva.",
                            notificationData,
                            accessToken,
                            ct));
        }

        #endregion


        #region ASIGNACION PENDIENTE DE FIRMA

        public async Task<ResponseModel<NotificationResponse<object>>>
            NotificarAsignacionPendienteFirmaEmpleadoAsync(
                string numeroEmpleado,
                int servicioId,
                string nombreServicio,
                long solicitudId,
                long relevoNoPlaneadoAsignacionId,
                string tipoCoberturaClave,
                DateTime fechaHoraInicioCobertura,
                DateTime fechaHoraFinCobertura,
                string textoResponsiva,
                string accessToken,
                CancellationToken ct)
        {
            string tipoCobertura =
                tipoCoberturaClave
                    .Trim()
                    .ToUpperInvariant();

            object notificationData =
                new
                {
                    Evento =
                        RELEVO_ASIGNACION_FIRMAR,

                    Accion =
                        "FIRMAR_RESPONSIVA",

                    SolicitudRelevoNoPlaneadoId =
                        solicitudId,

                    RelevoNoPlaneadoAsignacionId =
                        relevoNoPlaneadoAsignacionId,

                    ServicioId =
                        servicioId,

                    NombreServicio =
                        nombreServicio,

                    TipoCoberturaClave =
                        tipoCobertura,

                    FechaHoraInicioCobertura =
                        fechaHoraInicioCobertura,

                    FechaHoraFinCobertura =
                        fechaHoraFinCobertura,

                    TextoResponsiva =
                        textoResponsiva
                };

            string mensaje =
                tipoCobertura == "SUPERVISOR"
                    ? $"Fuiste asignado para cubrir temporalmente el servicio {nombreServicio}. Revisa y firma la responsiva."
                    : $"Fuiste asignado como sustituto para cubrir el servicio {nombreServicio}. Revisa y firma la responsiva.";

            return await EjecutarSeguroAsync(
                () =>
                    _servicioNotificationFunctions
                        .EnviarAUsuarioAsync(
                            numeroEmpleado,
                            RELEVO_ASIGNACION_FIRMAR,
                            "Cobertura pendiente de aceptación",
                            mensaje,
                            notificationData,
                            accessToken,
                            ct));
        }

        #endregion


        #region COBERTURA ACEPTADA

        public async Task<ResponseModel<NotificationResponse<object>>>
            NotificarCoberturaAceptadaAsync(
                int servicioId,
                string nombreServicio,
                long solicitudId,
                long relevoNoPlaneadoAsignacionId,
                long servicioEmpleadoTemporalId,
                string tipoCoberturaClave,
                int empleadoId,
                string numeroEmpleado,
                string nombreEmpleado,
                DateTime fechaHoraInicioCobertura,
                DateTime fechaHoraFinCobertura,
                string accessToken,
                CancellationToken ct)
        {
            string tipoCobertura =
                tipoCoberturaClave
                    .Trim()
                    .ToUpperInvariant();

            object notificationData =
                new
                {
                    Evento =
                        RELEVO_COBERTURA_ACEPTADA,

                    Accion =
                        "VER_COBERTURA",

                    SolicitudRelevoNoPlaneadoId =
                        solicitudId,

                    RelevoNoPlaneadoAsignacionId =
                        relevoNoPlaneadoAsignacionId,

                    ServicioEmpleadoTemporalId =
                        servicioEmpleadoTemporalId,

                    ServicioId =
                        servicioId,

                    NombreServicio =
                        nombreServicio,

                    TipoCoberturaClave =
                        tipoCobertura,

                    EmpleadoId =
                        empleadoId,

                    NumeroEmpleado =
                        numeroEmpleado,

                    NombreEmpleado =
                        nombreEmpleado,

                    FechaHoraInicioCobertura =
                        fechaHoraInicioCobertura,

                    FechaHoraFinCobertura =
                        fechaHoraFinCobertura
                };

            string descripcionCobertura =
                tipoCobertura switch
                {
                    "EXTENSION" =>
                        "una extensión de turno",

                    "SUPERVISOR" =>
                        "una cobertura realizada por un supervisor",

                    _ =>
                        "una cobertura por sustitución"
                };

            return await EjecutarSeguroAsync(
                () =>
                    _servicioNotificationFunctions
                        .EnviarASupervisoresAsync(
                            servicioId,
                            fechaHoraInicioCobertura.Date,
                            RELEVO_COBERTURA_ACEPTADA,
                            "Cobertura confirmada",
                            $"{nombreEmpleado} aceptó {descripcionCobertura} para el servicio {nombreServicio}.",
                            notificationData,
                            accessToken,
                            ct));
        }

        #endregion


        #region COBERTURA REQUERIDA POR RECHAZO

        public async Task<ResponseModel<NotificationResponse<object>>>
            NotificarCoberturaRequeridaPorRechazoAsync(
                int servicioId,
                string nombreServicio,
                long solicitudId,
                long relevoNoPlaneadoAsignacionId,
                string tipoCoberturaClave,
                string rechazadoPor,
                string motivoRechazo,
                DateTime fechaHoraInicioCobertura,
                DateTime fechaHoraFinCobertura,
                string accessToken,
                CancellationToken ct)
        {
            string tipoCobertura =
                tipoCoberturaClave
                    .Trim()
                    .ToUpperInvariant();

            object notificationData =
                new
                {
                    Evento =
                        RELEVO_COBERTURA_REQUERIDA,

                    Accion =
                        "GESTIONAR_COBERTURA",

                    SolicitudRelevoNoPlaneadoId =
                        solicitudId,

                    RelevoNoPlaneadoAsignacionId =
                        relevoNoPlaneadoAsignacionId,

                    ServicioId =
                        servicioId,

                    NombreServicio =
                        nombreServicio,

                    TipoCoberturaRechazada =
                        tipoCobertura,

                    RechazadoPor =
                        rechazadoPor,

                    MotivoRechazo =
                        motivoRechazo,

                    FechaHoraInicioCobertura =
                        fechaHoraInicioCobertura,

                    FechaHoraFinCobertura =
                        fechaHoraFinCobertura
                };

            string mensaje =
                rechazadoPor.Equals(
                    "SUPERVISOR",
                    StringComparison.OrdinalIgnoreCase)
                    ? $"El supervisor rechazó la extensión propuesta para el servicio {nombreServicio}. Se requiere una nueva cobertura."
                    : $"La cobertura propuesta para el servicio {nombreServicio} fue rechazada por el empleado. Se requiere una nueva cobertura.";

            return await EjecutarSeguroAsync(
                () =>
                    _servicioNotificationFunctions
                        .EnviarASupervisoresAsync(
                            servicioId,
                            fechaHoraInicioCobertura.Date,
                            RELEVO_COBERTURA_REQUERIDA,
                            "Cobertura nuevamente requerida",
                            mensaje,
                            notificationData,
                            accessToken,
                            ct));
        }

        #endregion


        #region EJECUCION SEGURA

        private async Task<ResponseModel<NotificationResponse<object>>>
            EjecutarSeguroAsync(
                Func<Task<ResponseModel<NotificationResponse<object>>>> action)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                return new ResponseModel<NotificationResponse<object>>
                {
                    isSuccess = false,
                    code = 500,
                    message =
                        "La operación principal se completó, pero no fue posible enviar la notificación.",
                    desc =
                        ex.Message,
                    data =
                        null
                };
            }
        }

        #endregion
    }
}