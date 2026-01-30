namespace CerberusBusinessService.Models.DTO
{
    public class ResponseModel<T>
    {
        public bool IsSuccess { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public string Desc { get; set; }
        public T Data { get; set; }
    }
}
