namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class MiAsistenciaResponse
    {
        public long? AsistenciaId { get; set; }

        public int ServicioId { get; set; }

        public long ServicioEmpleadoId { get; set; }

        public DateTime Fecha { get; set; }

        public string Servicio { get; set; } = string.Empty;

        public string? Relevo { get; set; }

        public DateTime? CheckIn { get; set; }

        public DateTime? CheckOut { get; set; }

        public string Estado { get; set; } = string.Empty;

        public string EstadoDescripcion { get; set; } = string.Empty;
    }
}