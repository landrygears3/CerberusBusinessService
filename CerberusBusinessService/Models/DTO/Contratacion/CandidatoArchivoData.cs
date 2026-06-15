namespace CerberusBusinessService.Models.DTO.Contratacion
{
    public class CandidatoArchivoData
    {
        public int IdArchivo { get; set; }
        public int CandidatoId { get; set; }
        public string Modulo { get; set; } = string.Empty;
        public string FileNamed { get; set; } = string.Empty;
        public int FileType { get; set; }
        public string RutaArchivo { get; set; } = string.Empty;
        public DateTime FechaAlta { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public DateTime? FechaExpedicion { get; set; }
    }
}
