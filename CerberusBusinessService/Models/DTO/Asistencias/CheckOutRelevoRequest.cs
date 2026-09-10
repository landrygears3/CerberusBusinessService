namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class CheckOutRelevoRequest
    {
        public long ServicioEmpleadoAfectadoId { get; set; }

        public bool PuedePermanecer { get; set; }

        public string? MotivoNoPermanencia { get; set; }

        public IFormFile FotoEvidencia { get; set; } = null!;
    }
}