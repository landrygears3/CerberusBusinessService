namespace CerberusBusinessService.Models.DTO.Candidatos
{
    public class ListadoCandidatosResponse
    {
        public int Id { get; set; }

        public string NombreCompleto { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        public string Fase { get; set; } = "Disponible";

        public string Areas { get; set; } = string.Empty;
    }
}
