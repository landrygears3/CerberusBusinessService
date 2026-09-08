namespace CerberusBusinessService.Models.DTO.Supervision
{
    public class CheckInFormularioAutorizacionResponse
    {
        public string RutaFirmaEntrante { get; set; } = null!;

        public string RutaFirmaSaliente { get; set; } = null!;

        public string? Observaciones { get; set; }

        public string RutaFotoZona { get; set; } = null!;
    }
}
