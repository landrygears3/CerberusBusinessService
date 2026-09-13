namespace CerberusBusinessService.Models.DTO.Servicios
{
    public class ServicioHorarioResponse
    {
        public int ServicioHorarioId { get; set; }
        public int ServicioId { get; set; }
        public byte DiaSemana { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public bool CruzaDia { get; set; }
        public DateTime FechaInicioVigencia { get; set; }
        public DateTime? FechaFinVigencia { get; set; }
        public bool Estatus { get; set; }
        public DateTime FechaAlta { get; set; }
    }
}