namespace CerberusBusinessService.Models.DTO.Supervision
{
    public class RetirarElementoSupervisionRequest
    {
        public long SupervisionId { get; set; }

        public string MotivoRelevo { get; set; } = null!;
    }
}