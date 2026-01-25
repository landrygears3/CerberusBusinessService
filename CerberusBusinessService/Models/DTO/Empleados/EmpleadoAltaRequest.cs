namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class EmpleadoAltaRequest
    {
        public string Nombres { get; set; }

        public string ApellidoMaterno { get; set; }
        public string ApellidoPaterno { get; set; }
        public int sexoId { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Curp { get; set; }
        public string Celular { get; set; }
        public string CorreoElectronico { get; set; }
        public int escolaridadId { get; set; }
        public int estadoCivilId { get; set; } 
    }
}
