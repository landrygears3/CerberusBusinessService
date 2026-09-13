namespace CerberusBusinessService.Models.DTO.Servicios
{
    public class ServicioHorarioRequest
    {
        public byte DiaSemana { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public bool CruzaDia { get; set; }
        public DateTime FechaInicioVigencia { get; set; }
    }
}