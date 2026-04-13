using CerberusBusinessService.Models.DTO.Empleados.Items;

namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class SaludCommitRequest
    {
        // CER00007 (NumeroUsuario)
        public string Usuario { get; set; }

        // NSS (pero tú dijiste que se almacenará usando CER00007 como NSS en esta pantalla)
        // Aun así lo dejo por si lo quieres persistir como texto independiente.
        public string NumeroSeguroSocial { get; set; }

        // Id del catálogo de tipo de sangre
        public int TipoSangreId { get; set; }

        // Contactos de emergencia (tabla aparte)
        public List<ContactoEmergenciaDto> ContactosEmergencia { get; set; } = new();

        // Relación usuario-catálogos (tablas aparte)
        public List<int> AlergiasIds { get; set; } = new();
        public List<int> EnfermedadesIds { get; set; } = new();
        public List<int> DiscapacidadesIds { get; set; } = new();
    }
}
