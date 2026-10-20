namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class HorarioOficinaCheckInDto
    {
        public long HorarioOficinaId { get; set; }
        public string UsuarioAsignado { get; set; } = string.Empty;
        public byte DiaSemana { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public bool CruzaDia { get; set; }
        public DateTime FechaInicioVigencia { get; set; }
        public DateTime? FechaFinVigencia { get; set; }
    }
}