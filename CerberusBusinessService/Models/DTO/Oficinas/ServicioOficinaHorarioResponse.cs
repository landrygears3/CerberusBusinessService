namespace CerberusBusinessService.Models.DTO.Oficinas
{
    public class ServicioOficinaHorarioResponse
    {
        public int ServicioOficinaHorarioId { get; set; }
        public int ServicioOficinaId { get; set; }
        public byte DiaSemana { get; set; }
        public TimeSpan HoraEntrada { get; set; }
        public TimeSpan HoraSalida { get; set; }
        public bool SalidaDiaSiguiente { get; set; }
        public bool Estatus { get; set; }
    }
}