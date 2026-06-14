namespace CerberusBusinessService.Models.DTO.Candidatos
{
    public class CommitDatosGeneralesCandidatoRequest
    {
        public int Id { get; set; }

        public string Nombres { get; set; }

        public string ApellidoPaterno { get; set; }

        public string ApellidoMaterno { get; set; }

        public DateTime FechaNacimiento { get; set; }

        public int SexoId { get; set; }

        public string Curp { get; set; }

        public int EscolaridadId { get; set; }

        public int EstadoCivilId { get; set; }

        public string RFC { get; set; }

        public string Celular { get; set; }

        public string Telefono { get; set; }

        public string CorreoElectronico { get; set; }

        public int NacionalidadId { get; set; }

        public string UsuarioOperacion { get; set; }

        public List<long> Puestos { get; set; }
    }
}
