namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class RelevoEsperadoCheckOutResponse
    {
        public long ServicioEmpleadoId { get; set; }

        public int EmpleadoId { get; set; }

        public string NumeroUsuario { get; set; } = null!;

        public string NombreCompleto { get; set; } = null!;

        public DateTime FechaHoraEntradaProgramada { get; set; }

        public DateTime FechaHoraSalidaProgramada { get; set; }

        public long? AsistenciaId { get; set; }

        public int? EstatusAsistencia { get; set; }
    }
}