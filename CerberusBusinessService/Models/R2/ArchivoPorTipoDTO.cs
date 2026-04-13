namespace CerberusBusinessService.Models.R2
{
    public class ArchivoPorTipoDTO
    {
        public int FileType { get; set; }
        public List<ArchivoDTO> Archivos { get; set; }
    }
}
