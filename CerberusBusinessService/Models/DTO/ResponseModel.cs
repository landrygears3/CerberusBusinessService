namespace CerberusBusinessService.Models.DTO
{
    public class ResponseModel<T>
    {
        public bool isSuccess { get; set; }
        public int code { get; set; }
        public string message { get; set; }
        public string desc { get; set; }
        public T data { get; set; }
    }
}
