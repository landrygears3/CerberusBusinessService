namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class TurnoCheckInRolDto
    {
        public string TipoOrigen { get; set; } = string.Empty;

        public int? ServicioId { get; set; }

        public DateTime FechaTurno { get; set; }

        public DateTime EntradaProgramada { get; set; }

        public DateTime SalidaProgramada { get; set; }

        public bool EsActivo { get; set; }
    }
}