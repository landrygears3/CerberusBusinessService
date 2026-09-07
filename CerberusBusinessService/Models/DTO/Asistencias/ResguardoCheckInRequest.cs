namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class ResguardoCheckInRequest
    {
        public int IdObjeto { get; set; }

        public int Cantidad { get; set; }

        public string Identificador { get; set; } = null!;

        public int IdEstado { get; set; }

        public string? Observaciones { get; set; }

        public IFormFile Foto { get; set; } = null!;
    }
}