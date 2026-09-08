namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class AsistenciaActivaCheckOutDto
    {
        public long AsistenciaId { get; set; }

        public int ServicioId { get; set; }

        public long ServicioEmpleadoId { get; set; }

        public string NumeroEmpleadoEntrante { get; set; } = null!;

        public DateTime FechaTurno { get; set; }

        public DateTime FechaHoraEntradaProgramada { get; set; }

        public DateTime FechaHoraSalidaProgramada { get; set; }

        public DateTime FechaHoraCheckIn { get; set; }

        public int Estatus { get; set; }
    }
}