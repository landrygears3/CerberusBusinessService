namespace CerberusBusinessService.Models.DTO.Mail
{
    public class SendWelcomeRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
