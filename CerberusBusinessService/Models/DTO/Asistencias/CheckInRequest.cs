namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class CheckInRequest
    {
        public FormatoEntradaCheckInRequest? FormatoEntrada { get; set; }

        public List<ResguardoCheckInRequest>? Resguardo { get; set; }

        public FormularioCheckInRequest? Formulario { get; set; }
    }
}