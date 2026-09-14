namespace CerberusBusinessService.Models.DTO.Oficinas
{
    public class ServicioOficinaEmpleadoResponse
    {
        public long ServicioOficinaEmpleadoId { get; set; }
        public int ServicioOficinaId { get; set; }
        public int EmpleadoId { get; set; }

        public string NumeroUsuario { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;

        public bool Estatus { get; set; }
        public DateTime FechaAlta { get; set; }
    }
}