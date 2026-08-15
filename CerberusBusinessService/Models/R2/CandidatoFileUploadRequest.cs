namespace CerberusBusinessService.Models.R2
{
    public class CandidatoFileUploadRequest
    {
        public int CandidatoId { get; set; }

        public string modulo { get; set; } = string.Empty;

        public string categoria { get; set; } = string.Empty;

        public int fileType { get; set; }

        public string fileName { get; set; } = string.Empty;

        public IFormFile file { get; set; }
        public DateTime? FechaVencimiento { get; set; } 
        public DateTime? FechaExpedicion { get; set; }
    }
}
