using CerberusBusinessService.Models.DTO.Empleados;

namespace CerberusBusinessService.Models.DTO.Candidatos
{
    public class CommitCandidatosSaludRequest
    {
       public int candidatoId { get; set; }
        public SaludCommitRequest datos { get; set; }
    }
}
