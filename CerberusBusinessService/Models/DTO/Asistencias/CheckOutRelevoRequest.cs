namespace CerberusBusinessService.Models.DTO.Asistencias
{
    public class CheckOutRelevoRequest
    {
        /// <summary>
        /// Asignación del empleado que debía tomar el siguiente turno.
        /// Puede ser null cuando no existe un siguiente relevo programado
        /// o cuando la cobertura corresponde al resto del turno actual
        /// por abandono anticipado.
        /// </summary>
        public long? ServicioEmpleadoAfectadoId { get; set; }

        public bool PuedePermanecer { get; set; }

        public string? MotivoNoPermanencia { get; set; }

        public IFormFile FotoEvidencia { get; set; } = null!;
    }
}
