namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class RelevoNoPlaneadoAsignacionResponse
    {
        public long RelevoNoPlaneadoAsignacionId { get; set; }

        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public int EmpleadoIdAsignado { get; set; }

        public string NumeroUsuarioAsignado { get; set; } = null!;

        public string NombreEmpleadoAsignado { get; set; } = null!;

        public string TipoCoberturaClave { get; set; } = null!;

        public string TipoCoberturaNombre { get; set; } = null!;

        public string EstatusClave { get; set; } = null!;

        public string EstatusNombre { get; set; } = null!;

        public string TextoResponsiva { get; set; } = null!;

        public int? SupervisorEmpleadoIdAutoriza { get; set; }

        public DateTime? FechaHoraFirmaSupervisor { get; set; }

        public DateTime? FechaHoraFirmaAceptacion { get; set; }

        public string? MotivoRechazo { get; set; }

        public DateTime? FechaHoraRechazo { get; set; }

        public long? ServicioEmpleadoTemporalId { get; set; }

        public DateTime FechaAsignacion { get; set; }
    }
}