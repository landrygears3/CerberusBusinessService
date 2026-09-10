namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class RechazarAsignacionRelevoNoPlaneadoRequest
    {
        public long RelevoNoPlaneadoAsignacionId { get; set; }

        public string MotivoRechazo { get; set; } = null!;
    }
}