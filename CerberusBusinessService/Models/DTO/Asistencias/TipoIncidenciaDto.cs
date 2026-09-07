namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class TipoIncidenciaDto
    {
        public int TipoIncidenciaId { get; set; }

        public bool AfectaNomina { get; set; }

        public string? TipoAfectacionNomina { get; set; }

        public decimal? MontoAfectacion { get; set; }
    }
}