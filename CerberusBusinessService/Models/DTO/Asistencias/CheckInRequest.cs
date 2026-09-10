namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class CheckInRequest
    {
        // ============================================================
        // GEOLOCALIZACIÓN
        // ============================================================
        //
        // La ubicación la obtiene el dispositivo / navegador.
        //
        // NO se obtiene desde la IP del request.
        //
        // Latitud:
        // -90 a 90
        //
        // Longitud:
        // -180 a 180
        // ============================================================

        public double? Latitud { get; set; }

        public double? Longitud { get; set; }


        // ============================================================
        // DATOS DEL RELEVO
        // ============================================================
        //
        // Estos campos solamente son obligatorios cuando
        // el servicio requiere relevo continuo.
        //
        // En una apertura de servicio pueden venir null.
        // ============================================================

        public FormatoEntradaCheckInRequest? FormatoEntrada { get; set; }

        public List<ResguardoCheckInRequest>? Resguardo { get; set; }

        public FormularioCheckInRequest? Formulario { get; set; }
    }
}