namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class RelevoPendienteEmpleadoResponse
    {
        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public long RelevoNoPlaneadoAsignacionId { get; set; }

        public int ServicioId { get; set; }

        public string NombreServicio { get; set; } = null!;

        public long ServicioEmpleadoAfectadoId { get; set; }

        public string TipoCoberturaClave { get; set; } = null!;

        public string EstatusClave { get; set; } = null!;

        public DateTime FechaHoraInicioCobertura { get; set; }

        public DateTime FechaHoraFinCobertura { get; set; }

        public string TextoResponsiva { get; set; } = null!;

        public DateTime FechaAsignacion { get; set; }
    }
}