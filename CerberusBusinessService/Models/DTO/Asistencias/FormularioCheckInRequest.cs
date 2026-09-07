namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class FormularioCheckInRequest
    {
        public string IdEmpleadoSaliente { get; set; } = null!;

        public IFormFile ImagenFirmaEntrante { get; set; } = null!;

        public IFormFile ImagenFirmaSaliente { get; set; } = null!;

        public string? Observaciones { get; set; }

        public IFormFile Foto { get; set; } = null!;
    }
}