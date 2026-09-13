namespace CerberusBusinessService.Models.DTO.Servicios
{
    public class CommitServicioRequest
    {
        public int ServicioId { get; set; }
        public int ClienteId { get; set; }
        public string NombreServicio { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int TipoServicioId { get; set; }
        public int CantidadEmpleadosRequeridos { get; set; }
        public int IdActividadServ { get; set; } = 1;
        public string? Direccion { get; set; }
        public List<ServicioHorarioRequest> Horarios { get; set; } = new List<ServicioHorarioRequest>();
    }
}