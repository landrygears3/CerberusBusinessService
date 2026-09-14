namespace CerberusBusinessService.Models.DTO.Oficinas
{
    public class ServicioOficinaResponse
    {
        public int ServicioOficinaId { get; set; }

        public int OficinaId { get; set; }

        public int DepartamentoId { get; set; }

        public string Departamento { get; set; } = string.Empty;

        public string NombreServicio { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public bool Estatus { get; set; }

        public DateTime FechaAlta { get; set; }

        public List<ServicioOficinaHorarioResponse> Horarios { get; set; }
            = new List<ServicioOficinaHorarioResponse>();
    }
}