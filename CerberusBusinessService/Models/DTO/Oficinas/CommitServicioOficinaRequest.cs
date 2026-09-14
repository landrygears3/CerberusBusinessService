namespace CerberusBusinessService.Models.DTO.Oficinas
{
    public class CommitServicioOficinaRequest
    {
        public int ServicioOficinaId { get; set; }

        public int OficinaId { get; set; }

        public int DepartamentoId { get; set; }

        public string NombreServicio { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public List<ServicioOficinaHorarioRequest> Horarios { get; set; }
            = new List<ServicioOficinaHorarioRequest>();
    }
}