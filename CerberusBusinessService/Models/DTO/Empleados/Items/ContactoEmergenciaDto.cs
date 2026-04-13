namespace CerberusBusinessService.Models.DTO.Empleados.Items
{
    public class ContactoEmergenciaDto
    {
        // Si ya tienes ID en front, úsalo para update; si no, se ignora y se inserta.
        public int? IdContacto { get; set; }

        public string NombreCompleto { get; set; }

        // parentesco es ID (catálogo)
        public int ParentescoId { get; set; }

        public string Celular { get; set; }
    }
}
