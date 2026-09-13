namespace CerberusBusinessService.Models.DTO.ServicioSupervisor
{
    public class ServicioSupervisorListadoResponse
    {
        public string NombreServicio { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? Direccion { get; set; }
        public int Estatus { get; set; }
        public string EstatusDesc { get; set; } = string.Empty;
    }
}