namespace CerberusBusinessService.Models.DTO.Servicios
{
    public class ListadoServicioResponse
    {
        public int ServicioId { get; set; }
        public int ClienteId { get; set; }
        public string NombreServicio { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Estatus { get; set; }
        public DateTime FechaAlta { get; set; }
        public int TipoServicioId { get; set; }
        public string TipoServicioClave { get; set; } = string.Empty;
        public string TipoServicioNombre { get; set; } = string.Empty;
        public int CantidadEmpleadosRequeridos { get; set; }
        public int IdActividadServ { get; set; }
        public string? Direccion { get; set; }
        public int EstatusOperativo { get; set; }
        public string DesEstatusOp { get; set; } = string.Empty;
    }
}