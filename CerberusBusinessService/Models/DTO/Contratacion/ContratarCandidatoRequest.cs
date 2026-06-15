namespace CerberusBusinessService.Models.DTO.Contratacion
{
    public class ContratarCandidatoRequest
    {
        public int CandidatoId { get; set; }
        public int VacanteId { get; set; }
        public string PasswordConfirmacion { get; set; } = string.Empty;
        public IFormFile EntrevistaArchivo { get; set; }
    }
}
