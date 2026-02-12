namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class ListadoDomiciliosResponse
    {
        public int? IDdomicilio { get; set; }
        public string Calle { get; set; }
        public string Numero_Exterior { get; set; }
        public string Numero_Interior { get; set; }
        public string Codigo_Postal { get; set; }
        public int EstadoID { get; set; }
        public int MunicipioID { get; set; }
        public int ColoniaID { get; set; }
        public bool Its_Principal { get; set; }
    }
}
