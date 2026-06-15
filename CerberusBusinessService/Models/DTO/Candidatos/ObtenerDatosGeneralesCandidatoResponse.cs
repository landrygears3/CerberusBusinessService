namespace CerberusBusinessService.Models.DTO.Candidatos
{
    public class ObtenerDatosGeneralesCandidatoResponse
    {
        public int Id { get; set; }

        public string Nombres { get; set; } = string.Empty;

        public string ApellidoPaterno { get; set; } = string.Empty;

        public string? ApellidoMaterno { get; set; }

        public DateTime FechaNacimiento { get; set; }

        public int SexoId { get; set; }

        public string Curp { get; set; } = string.Empty;

        public int EscolaridadId { get; set; }

        public int EstadoCivilId { get; set; }

        public string? RFC { get; set; }

        public string? Celular { get; set; }

        public string? Telefono { get; set; }

        public string? CorreoElectronico { get; set; }

        public int NacionalidadId { get; set; }

        public string UsuarioOperacion { get; set; } = string.Empty;

        public List<long> Puestos { get; set; } = new();
    }
}
