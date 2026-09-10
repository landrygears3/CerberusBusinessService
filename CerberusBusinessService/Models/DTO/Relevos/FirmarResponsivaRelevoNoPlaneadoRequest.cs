namespace CerberusBusinessService.Models.DTO.Relevos
{
    public class FirmarResponsivaRelevoNoPlaneadoRequest
    {
        public long RelevoNoPlaneadoAsignacionId { get; set; }

        public IFormFile FirmaAceptacion { get; set; } = null!;
    }
}