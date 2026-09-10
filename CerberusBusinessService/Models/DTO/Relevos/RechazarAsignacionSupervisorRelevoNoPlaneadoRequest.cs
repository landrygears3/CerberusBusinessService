namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class RechazarAsignacionSupervisorRelevoNoPlaneadoRequest
    {
        public long RelevoNoPlaneadoAsignacionId { get; set; }

        public string MotivoRechazo { get; set; } = null!;
    }
}