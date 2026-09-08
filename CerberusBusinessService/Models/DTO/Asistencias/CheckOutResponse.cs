namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class CheckOutResponse
    {
        public long AsistenciaId { get; set; }

        public int ServicioId { get; set; }

        public long ServicioEmpleadoId { get; set; }

        public string NumeroEmpleado { get; set; } = null!;

        public DateTime FechaTurno { get; set; }

        public DateTime FechaHoraEntradaProgramada { get; set; }

        public DateTime FechaHoraSalidaProgramada { get; set; }

        public DateTime FechaHoraCheckIn { get; set; }

        public DateTime FechaHoraCheckOut { get; set; }

        public int Estatus { get; set; }

        public string EstatusDescripcion { get; set; } = null!;
    }
}