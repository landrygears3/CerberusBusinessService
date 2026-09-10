namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class CrearAsignacionRelevoNoPlaneadoDto
    {
        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public int EmpleadoIdAsignado { get; set; }

        public string TipoCoberturaClave { get; set; } = null!;
    }
}