namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class RelevoNoPlaneadoAsignacionDto
    {
        public long RelevoNoPlaneadoAsignacionId { get; set; }

        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public int EmpleadoIdAsignado { get; set; }

        public int RelevoTipoCoberturaId { get; set; }

        public int RelevoAsignacionEstatusId { get; set; }

        public string TextoResponsiva { get; set; } = null!;

        public int? SupervisorEmpleadoIdAutoriza { get; set; }

        public string? RutaFirmaSupervisor { get; set; }

        public DateTime? FechaHoraFirmaSupervisor { get; set; }

        public string? RutaFirmaEmpleado { get; set; }

        public DateTime? FechaHoraFirmaEmpleado { get; set; }

        public string? MotivoRechazo { get; set; }

        public DateTime? FechaHoraRechazo { get; set; }

        public long? ServicioEmpleadoTemporalId { get; set; }

        public DateTime FechaAsignacion { get; set; }

        public string UsuarioAsignacion { get; set; } = null!;
    }
}