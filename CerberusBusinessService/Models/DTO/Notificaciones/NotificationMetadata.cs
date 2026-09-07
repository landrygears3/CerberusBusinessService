namespace CerberusBusinessService.Models.DTO.Notificaciones
{
    public class NotificationMetadata
    {
        public DateTime CreatedAt { get; set; }

        public string Type { get; set; } = null!;

        public string Estado { get; set; } = null!;

        public string Screen { get; set; } = null!;
    }
}