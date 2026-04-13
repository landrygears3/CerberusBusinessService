namespace CerberusBusinessService.Models.R2
{
    public class FileUploadRequest
    {
        public IFormFile file { get; set; }
        public string numeroUsuario { get; set; }
        public string modulo { get; set; }
        public string categoria { get; set; }
        public string fileName { get; set; }
        public int fileType { get; set; }
    }
}
