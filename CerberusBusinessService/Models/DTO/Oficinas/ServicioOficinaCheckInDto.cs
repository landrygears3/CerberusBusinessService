namespace CerberusBusinessService.Models.DTO.Oficinas
{
    public class ServicioOficinaCheckInDto
    {
        public long ServicioOficinaEmpleadoId { get; set; }

        public int ServicioOficinaId { get; set; }

        public int OficinaId { get; set; }

        public int EmpleadoId { get; set; }

        public byte DiaSemana { get; set; }

        public TimeSpan HoraEntrada { get; set; }

        public TimeSpan HoraSalida { get; set; }

        public bool SalidaDiaSiguiente { get; set; }
    }
}