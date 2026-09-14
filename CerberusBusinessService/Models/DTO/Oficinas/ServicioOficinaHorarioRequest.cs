namespace CerberusBusinessService.Models.DTO.Oficinas
{
    public class ServicioOficinaHorarioRequest
    {
        public byte DiaSemana { get; set; }
        public TimeSpan HoraEntrada { get; set; }
        public TimeSpan HoraSalida { get; set; }
        public bool SalidaDiaSiguiente { get; set; }
    }
}