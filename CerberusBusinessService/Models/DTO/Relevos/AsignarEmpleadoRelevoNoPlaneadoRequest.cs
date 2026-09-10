namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class AsignarEmpleadoRelevoNoPlaneadoRequest
    {
        public long SolicitudRelevoNoPlaneadoId { get; set; }

        public int EmpleadoIdAsignado { get; set; }
    }
}