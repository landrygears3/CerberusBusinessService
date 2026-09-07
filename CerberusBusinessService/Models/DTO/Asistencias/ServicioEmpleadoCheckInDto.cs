namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class ServicioEmpleadoCheckInDto
    {
        public long ServicioEmpleadoId { get; set; }

        public int ServicioId { get; set; }

        public int EmpleadoId { get; set; }

        public int TipoAsignacionServicioId { get; set; }

        public int? EmpleadoCubiertoId { get; set; }

        public DateTime FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        public TimeSpan HoraEntrada { get; set; }

        public TimeSpan HoraSalida { get; set; }

        public bool SalidaDiaSiguiente { get; set; }
    }
}