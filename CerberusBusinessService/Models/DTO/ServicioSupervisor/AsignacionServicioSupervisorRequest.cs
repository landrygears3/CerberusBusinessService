namespace CerberusBusinessService.Models.DTO.ServicioSupervisor
{
    public class AsignacionServicioSupervisorRequest
    {
        public int SupervisorEmpleadoId { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public TimeSpan HoraEntrada { get; set; }
        public TimeSpan HoraSalida { get; set; }
        public bool SalidaDiaSiguiente { get; set; }
    }
}