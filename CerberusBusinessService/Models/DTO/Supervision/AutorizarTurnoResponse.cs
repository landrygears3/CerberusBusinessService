namespace CerberusBusinessService.Models.DTO.Supervision
{
    public class AutorizarTurnoResponse
    {
        public int ServicioId { get; set; }

        public long AsistenciaEntranteId { get; set; }

        public long AsistenciaSalienteId { get; set; }

        public string NumeroEmpleadoEntrante { get; set; } = null!;

        public string NumeroEmpleadoSaliente { get; set; } = null!;

        public DateTime FechaHoraAutorizacion { get; set; }

        public DateTime FechaHoraCheckOut { get; set; }

        public int EstatusEntrante { get; set; }

        public int EstatusSaliente { get; set; }
    }
}