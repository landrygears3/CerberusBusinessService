namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class EmpleadoAltaGeneralesResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public DateTime FechaAlta { get; set; }
    }
}
