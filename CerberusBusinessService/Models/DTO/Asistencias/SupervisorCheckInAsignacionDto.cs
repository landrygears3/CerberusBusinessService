namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class SupervisorCheckInAsignacionDto
    {
        public long ServicioSupervisorId { get; set; }

        public int ServicioId { get; set; }

        public int SupervisorEmpleadoId { get; set; }

        public DateTime FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        public TimeSpan HoraEntrada { get; set; }

        public TimeSpan HoraSalida { get; set; }

        public bool SalidaDiaSiguiente { get; set; }
    }
}