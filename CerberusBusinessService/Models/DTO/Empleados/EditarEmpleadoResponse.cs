namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class EditarEmpleadoResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string UsuarioAsignado { get; set; } = string.Empty;
        public DateTime FechaActualizacion { get; set; }
    }
}
