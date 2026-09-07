namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class CheckInResponse
    {
        public long AsistenciaId { get; set; }

        public long ServicioEmpleadoId { get; set; }

        public int ServicioId { get; set; }

        public string NumeroEmpleadoEntrante { get; set; } = null!;

        public string NumeroEmpleadoSaliente { get; set; } = null!;

        public DateTime FechaTurno { get; set; }

        public DateTime FechaHoraEntradaProgramada { get; set; }

        public DateTime FechaHoraSalidaProgramada { get; set; }

        public DateTime FechaHoraCheckIn { get; set; }

        public bool EsRetardo { get; set; }

        public int? MinutosRetardo { get; set; }

        public long? IncidenciaRetardoId { get; set; }

        public int Estatus { get; set; }

        public string EstatusDescripcion { get; set; } = null!;
    }
}