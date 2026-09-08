namespace CerberusBusinessService.Models.DTO.Supervision
{
    public class ListadoAsistenciaPendienteAutorizarResponse
    {
        public long AsistenciaId { get; set; }

        public long ServicioEmpleadoId { get; set; }

        public int ServicioId { get; set; }

        public string NumeroEmpleadoEntrante { get; set; } = null!;

        public string? NumeroEmpleadoSaliente { get; set; }

        public DateTime FechaTurno { get; set; }

        public DateTime FechaHoraEntradaProgramada { get; set; }

        public DateTime FechaHoraSalidaProgramada { get; set; }

        public DateTime FechaHoraCheckIn { get; set; }

        public bool EsRetardo { get; set; }

        public int? MinutosRetardo { get; set; }

        public int Estatus { get; set; }
    }
}