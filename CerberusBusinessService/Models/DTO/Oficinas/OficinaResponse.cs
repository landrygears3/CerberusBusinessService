namespace CerberusBusinessService.Models.DTO.Oficinas
{
    public class OficinaResponse
    {
        public int OficinaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? Direccion { get; set; }
        public bool Estatus { get; set; }
        public DateTime FechaAlta { get; set; }
    }
}