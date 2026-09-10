namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class SolicitudRelevoNoPlaneadoResponse
    {
        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public long ServicioEmpleadoAfectadoId { get; set; }

        public long? ServicioEmpleadoSalienteId { get; set; }

        public int ServicioId { get; set; }

        public string NombreServicio { get; set; } = null!;

        public int EmpleadoAfectadoId { get; set; }

        public string NumeroUsuarioAfectado { get; set; } = null!;

        public string NombreEmpleadoAfectado { get; set; } = null!;

        public string OrigenClave { get; set; } = null!;

        public string OrigenNombre { get; set; } = null!;

        public string EstatusClave { get; set; } = null!;

        public string EstatusNombre { get; set; } = null!;

        public DateTime FechaHoraInicioCobertura { get; set; }

        public DateTime FechaHoraFinCobertura { get; set; }

        public string MotivoRelevo { get; set; } = null!;

        public string? MotivoNoPermanencia { get; set; }

        public string RutaFotoEvidencia { get; set; } = null!;

        public DateTime FechaRegistro { get; set; }

        public List<RelevoNoPlaneadoAsignacionResponse> Asignaciones
        {
            get;
            set;
        } = new List<RelevoNoPlaneadoAsignacionResponse>();
    }
}