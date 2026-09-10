namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class RelevoSupervisionDataDto
    {
        public long SupervisionId { get; set; }

        public long ServicioEmpleadoId { get; set; }

        public int SupervisorEmpleadoId { get; set; }

        public int ServicioId { get; set; }

        public int EmpleadoAfectadoId { get; set; }

        public string RutaFotoEmpleado { get; set; } = null!;
    }
}