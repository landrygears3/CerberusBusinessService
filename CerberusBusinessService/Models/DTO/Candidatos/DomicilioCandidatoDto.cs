namespace CerberusBusinessService.Models.DTO.Candidatos
{
    public class DomicilioCandidatoDto
    {
        public string Calle { get; set; } = string.Empty;

        public string? Numero_Interior { get; set; }

        public string Numero_Exterior { get; set; } = string.Empty;

        public string? Codigo_Postal { get; set; }

        public int EstadoID { get; set; }

        public int MunicipioID { get; set; }

        public int ColoniaID { get; set; }

        public bool Its_Principal { get; set; }
    }
}
