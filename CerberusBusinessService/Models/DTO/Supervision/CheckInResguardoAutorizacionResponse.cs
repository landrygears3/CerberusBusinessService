namespace CerberusBusinessService.Models.DTO.Supervision
{
    public class CheckInResguardoAutorizacionResponse
    {
        public int IdObjeto { get; set; }

        public int Cantidad { get; set; }

        public string Identificador { get; set; } = null!;

        public int IdEstado { get; set; }

        public string? Observaciones { get; set; }

        public string? RutaFoto { get; set; }
    }

}
