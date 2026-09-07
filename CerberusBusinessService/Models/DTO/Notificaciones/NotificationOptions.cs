namespace CerberusBusinessService.Models.Notificaciones
{
    public class NotificationOptions
    {
        public string HubUrl { get; set; } = null!;

        public int TimeoutSeconds { get; set; } = 30;
    }
}