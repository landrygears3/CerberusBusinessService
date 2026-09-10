namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class SolicitudRelevoNoPlaneadoDto
    {
        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public long ServicioEmpleadoAfectadoId { get; set; }

        public long? ServicioEmpleadoSalienteId { get; set; }

        public int RelevoNoPlaneadoOrigenId { get; set; }

        public int RelevoNoPlaneadoEstatusId { get; set; }

        public DateTime FechaHoraInicioCobertura { get; set; }

        public DateTime FechaHoraFinCobertura { get; set; }

        public string MotivoRelevo { get; set; } = null!;

        public string? MotivoNoPermanencia { get; set; }

        public string RutaFotoEvidencia { get; set; } = null!;

        public DateTime FechaRegistro { get; set; }

        public string UsuarioRegistro { get; set; } = null!;
    }
}