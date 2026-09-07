namespace CerberusBusinessService.Models.DTO.Notificaciones
{
    public class NotificationResponse<T>
    {
        public long Id { get; set; }

        public string Titulo { get; set; } = null!;

        public string Mensaje { get; set; } = null!;

        public NotificationMetadata Metadata { get; set; } = null!;

        public T Data { get; set; } = default!;
    }
}