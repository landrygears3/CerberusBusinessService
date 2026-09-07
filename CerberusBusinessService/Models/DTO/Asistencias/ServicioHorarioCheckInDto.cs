namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class ServicioHorarioCheckInDto
    {
        public int IdServicioHorario { get; set; }

        public int IdServicio { get; set; }

        public byte DiaSemana { get; set; }

        public TimeSpan HoraInicio { get; set; }

        public TimeSpan HoraFin { get; set; }

        public bool CruzaDia { get; set; }

        public bool Activo { get; set; }

        public DateTime? VigenteDesde { get; set; }

        public DateTime? VigenteHasta { get; set; }
    }
}