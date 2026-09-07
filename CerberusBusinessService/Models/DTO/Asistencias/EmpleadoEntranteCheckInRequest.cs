namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class EmpleadoEntranteCheckInRequest
    {
        public bool RecepcionTurnoCompleto { get; set; }

        public bool EquipoTrabajoFuncional { get; set; }

        public bool ConocimientoConsignas { get; set; }

        public bool UniformeCompleto { get; set; }

        public bool CondicionesAptas { get; set; }

        public bool HorarioDiaDescanso { get; set; }
    }
}