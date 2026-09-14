namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class TurnoCheckInDto
    {
        public string TipoOrigen { get; set; } =
            string.Empty;

        public int? ServicioId { get; set; }

        public long? ServicioEmpleadoId { get; set; }

        public long? ServicioSupervisorId { get; set; }

        public long? ServicioOficinaEmpleadoId { get; set; }

        public DateTime FechaTurno { get; set; }

        public DateTime EntradaProgramada { get; set; }

        public DateTime SalidaProgramada { get; set; }

        public bool EsActivo { get; set; }
    }
}