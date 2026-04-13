using CerberusBusinessService.Models.DTO.Empleados.Items;

namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class SaludGetResponse
    {
        public string NumeroSeguroSocial { get; set; }
        public int TipoSangreId { get; set; }

        public List<SaludContactoResponse> Contactos { get; set; } = new();

        public List<int> AlergiasIds { get; set; } = new();
        public List<int> EnfermedadesIds { get; set; } = new();
        public List<int> DiscapacidadesIds { get; set; } = new();
    }
}
