namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class CrearSolicitudRelevoNoPlaneadoDto
    {
        public long ServicioEmpleadoAfectadoId { get; set; }

        public long? ServicioEmpleadoSalienteId { get; set; }

        public string OrigenClave { get; set; } = null!;

        public DateTime FechaHoraInicioCobertura { get; set; }

        public DateTime FechaHoraFinCobertura { get; set; }

        public string MotivoRelevo { get; set; } = null!;

        public string? MotivoNoPermanencia { get; set; }

        public IFormFile FotoEvidencia { get; set; } = null!;
    }
}