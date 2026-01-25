namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class EmpleadoAltaGeneralesRequest
    {
        public string Nombres { get; set; }
        public string ApellidoPaterno { get; set; }
        public string ApellidoMaterno { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public int sexoId { get; set; }
        public string Curp { get; set; }
        public int EscolaridadId { get; set; }
        public int EstadoCivilId { get; set; }
        public string RFC { get; set; }
        public string Celular { get; set; }
        public string Telefono { get; set; }
        public string CorreoElectronico { get; set; }
        public int NacionalidadId { get; set; }
        public int origenVacanteId { get; set; } = 0;
        public string UsuarioAlta  { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioAsignado { get; set; } //No se manda de frontend
        public int Departamento { get; set; }

    }
}
