namespace CerberusBusinessService.Models.DTO.Supervision
{
    public class CheckInAutorizacionResponse
    {
        public long AsistenciaId { get; set; }

        public long ServicioEmpleadoId { get; set; }

        public int ServicioId { get; set; }

        public string NumeroEmpleadoEntrante { get; set; } = null!;

        public string? NumeroEmpleadoSaliente { get; set; }

        public DateTime FechaTurno { get; set; }

        public DateTime FechaHoraEntradaProgramada { get; set; }

        public DateTime FechaHoraSalidaProgramada { get; set; }

        public DateTime FechaHoraCheckIn { get; set; }

        public DateTime FechaHoraRegistro { get; set; }

        public bool EsRetardo { get; set; }

        public int? MinutosRetardo { get; set; }

        public int ToleranciaMinutos { get; set; }

        public int Estatus { get; set; }

        public string EstatusDescripcion { get; set; } = null!;

        public CheckInFormatoEntradaAutorizacionResponse?
            FormatoEntrada
        { get; set; }

        public List<CheckInResguardoAutorizacionResponse>
            Resguardos
        { get; set; } = new();

        public CheckInFormularioAutorizacionResponse?
            Formulario
        { get; set; }
    }
}
