namespace CerberusBusinessService.Models.DTO.Contratacion
{
    public class CandidatoDatos
    {
        public int ID { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string ApellidoMaterno { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public int SexoId { get; set; }
        public string Curp { get; set; } = string.Empty;
        public int EscolaridadId { get; set; }
        public int EstadoCivilId { get; set; }
        public string RFC { get; set; } = string.Empty;
        public string Celular { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public int NacionalidadId { get; set; }
    }
}
