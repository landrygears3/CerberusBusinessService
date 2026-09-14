namespace CerberusBusinessService.Models.DTO.Oficinas
{
    public class CommitOficinaRequest
    {
        public int OficinaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? Direccion { get; set; }
    }
}