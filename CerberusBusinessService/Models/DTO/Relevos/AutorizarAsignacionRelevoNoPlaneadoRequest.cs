namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class AutorizarAsignacionRelevoNoPlaneadoRequest
    {
        public long RelevoNoPlaneadoAsignacionId { get; set; }

        public IFormFile FirmaSupervisor { get; set; } = null!;
    }
}