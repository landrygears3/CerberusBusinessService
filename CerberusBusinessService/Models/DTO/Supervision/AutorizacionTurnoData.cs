namespace CerberusBusinessService.Models.DTO.Supervision
{
    public class AutorizacionTurnoData
    {
        public long AsistenciaId { get; set; }

        public int ServicioId { get; set; }

        public string NumeroEmpleadoEntrante { get; set; } = null!;

        public string? NumeroEmpleadoSaliente { get; set; }

        public DateTime FechaTurno { get; set; }

        public DateTime? FechaHoraCheckOut { get; set; }

        public int Estatus { get; set; }
    }
}