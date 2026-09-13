namespace CerberusBusinessService.Models.DTO.ServicioSupervisor
{
    public class AsignarServiciosSupervisorRequest
    {
        public int ServicioId { get; set; }

        public List<AsignacionServicioSupervisorRequest> Supervisores { get; set; }
            = new List<AsignacionServicioSupervisorRequest>();
    }
}