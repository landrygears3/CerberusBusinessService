namespace CerberusBusinessService.Models.R2
{
    public class ArchivoDTO
    {
        public int IdArchivo { get; set; }
        public string FileName { get; set; }
        public DateTime FechaAlta { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public DateTime? FechaExpedicion { get; set; }
    }
}
