namespace CerberusBusinessService.Models.DTO.Candidatos
{
    public class AltaDomicilioCandidatoRequest
    {
        public int CandidatoId { get; set; }

        public List<DomicilioCandidatoDto> domicilios { get; set; } = new();
    }

}
