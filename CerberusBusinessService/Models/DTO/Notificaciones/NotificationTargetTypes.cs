namespace CerberusBusinessService.Models.DTO.Notificaciones
{
    public static class NotificationTargetTypes
    {
        public const string Generica = "GENERICA";
        public const string Usuarios = "USUARIOS";
        public const string Rol = "ROL";
        public const string Actividad = "ACTIVIDAD";
    }

    public class NotificationTarget
    {
        public string Tipo { get; set; } = null!;

        public List<string> NumeroUsuarios { get; set; } = new();

        public List<int> RolIds { get; set; } = new();

        public List<int> ActividadIds { get; set; } = new();

        public string MatchMode { get; set; } = "ANY";
    }
}