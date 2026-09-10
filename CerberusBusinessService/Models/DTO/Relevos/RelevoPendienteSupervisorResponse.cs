namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class RelevoPendienteSupervisorResponse
    {
        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public long? RelevoNoPlaneadoAsignacionId { get; set; }

        public int ServicioId { get; set; }

        public string NombreServicio { get; set; } = null!;

        public long ServicioEmpleadoAfectadoId { get; set; }

        public int EmpleadoAfectadoId { get; set; }

        public string NumeroUsuarioAfectado { get; set; } = null!;

        public string NombreEmpleadoAfectado { get; set; } = null!;

        public string OrigenClave { get; set; } = null!;

        public string SolicitudEstatusClave { get; set; } = null!;

        public DateTime FechaHoraInicioCobertura { get; set; }

        public DateTime FechaHoraFinCobertura { get; set; }

        public string MotivoRelevo { get; set; } = null!;

        public string RutaFotoEvidencia { get; set; } = null!;

        public int? EmpleadoIdAsignado { get; set; }

        public string? NumeroUsuarioAsignado { get; set; }

        public string? NombreEmpleadoAsignado { get; set; }

        public string? TipoCoberturaClave { get; set; }

        public string? AsignacionEstatusClave { get; set; }

        public DateTime FechaRegistro { get; set; }
    }
}