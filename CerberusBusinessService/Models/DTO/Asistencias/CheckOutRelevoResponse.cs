namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class CheckOutRelevoResponse
    {
        public long AsistenciaId { get; set; }

        public int ServicioId { get; set; }

        public long ServicioEmpleadoSalienteId { get; set; }

        public long ServicioEmpleadoAfectadoId { get; set; }

        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public long? RelevoNoPlaneadoAsignacionId { get; set; }

        public bool PuedePermanecer { get; set; }

        public bool CheckOutRealizado { get; set; }

        public DateTime? FechaHoraCheckOut { get; set; }

        public string SolicitudEstatusClave { get; set; } = null!;

        public string? AsignacionEstatusClave { get; set; }
    }
}