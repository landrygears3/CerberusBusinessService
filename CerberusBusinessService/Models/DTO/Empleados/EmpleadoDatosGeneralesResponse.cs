namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class EmpleadoDatosGeneralesResponse
    {
        public int Id { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string ApellidoMaterno { get; set; } = string.Empty;

        public DateTime FechaNacimiento { get; set; }
        public int SexoId { get; set; }

        public string Curp { get; set; } = string.Empty;
        public string RFC { get; set; } = string.Empty;

        public string Celular { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;

        public int OrigenVacanteId { get; set; }
        public string UsuarioAlta { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }

        public string UsuarioAsignado { get; set; } = string.Empty;
        public int NacionalidadId { get; set; }
        public int EscolaridadId { get; set; }
        public int EstadoCivilId { get; set; }
        public int DepartamentoId { get; set; }
    }
}
