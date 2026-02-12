namespace CerberusBusinessService.Models.DTO.Empleados
{
    public class AltaDomicilioRequest
    {
        public string Usuario { get; set; }

        public List<DTODomicilio> domicilios { get; set; }
    }

    public class DTODomicilio
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
