namespace CerberusBusinessService.Models.DTO.Notificaciones
{
    public class CreateNotificationRequest<T>
    {
        public string Type { get; set; } = null!;

        public string Titulo { get; set; } = null!;

        public string Mensaje { get; set; } = null!;

        public NotificationTarget? Target { get; set; }

        public T Data { get; set; } = default!;
    }
}