using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Relevos;

namespace CerberusBusinessService.Functions.Relevos
{
    public class RelevoNoPlaneadoFunctions
    {
        #region PROPIEDADES

        private readonly RelevoSolicitudFunctions _solicitudes;
        private readonly RelevoAsignacionFunctions _asignaciones;
        private readonly RelevoConsultaFunctions _consultas;

        #endregion


        #region CONSTRUCTOR

        public RelevoNoPlaneadoFunctions(
            RelevoSolicitudFunctions solicitudes,
            RelevoAsignacionFunctions asignaciones,
            RelevoConsultaFunctions consultas)
        {
            _solicitudes = solicitudes;
            _asignaciones = asignaciones;
            _consultas = consultas;
        }

        #endregion


        #region SOLICITUDES

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudAsync(
                CrearSolicitudRelevoNoPlaneadoDto data,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _solicitudes.CrearSolicitudAsync(
                data,
                numeroUsuario,
                ct);
        }


        internal async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudDesdeAsistenciaAsync(
                CrearSolicitudRelevoNoPlaneadoDto data,
                long asistenciaSalienteId,
                bool realizarCheckOut,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _solicitudes
                .CrearSolicitudDesdeAsistenciaAsync(
                    data,
                    asistenciaSalienteId,
                    realizarCheckOut,
                    numeroUsuario,
                    ct);
        }


        internal async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudDesdeSupervisionAsync(
                long supervisionId,
                string motivoRelevo,
                string numeroSupervisor,
                CancellationToken ct)
        {
            return await _solicitudes
                .CrearSolicitudDesdeSupervisionAsync(
                    supervisionId,
                    motivoRelevo,
                    numeroSupervisor,
                    ct);
        }


        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CancelarSolicitudAsync(
                CancelarSolicitudRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _solicitudes.CancelarSolicitudAsync(
                data,
                numeroUsuario,
                ct);
        }

        #endregion


        #region ASIGNACIONES

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarEmpleadoAsync(
                AsignarEmpleadoRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _asignaciones.AsignarEmpleadoAsync(
                data,
                numeroUsuario,
                ct);
        }


        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarseSupervisorAsync(
                AsignarseSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _asignaciones.AsignarseSupervisorAsync(
                data,
                numeroUsuario,
                ct);
        }


        internal async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            CrearExtensionAsync(
                long solicitudRelevoNoPlaneadoId,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _asignaciones.CrearExtensionAsync(
                solicitudRelevoNoPlaneadoId,
                numeroUsuario,
                ct);
        }


        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AutorizarAsignacionAsync(
                AutorizarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _asignaciones.AutorizarAsignacionAsync(
                data,
                numeroUsuario,
                ct);
        }


        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            FirmarResponsivaAsync(
                FirmarResponsivaRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _asignaciones.FirmarResponsivaAsync(
                data,
                numeroUsuario,
                ct);
        }


        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionAsync(
                RechazarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _asignaciones.RechazarAsignacionAsync(
                data,
                numeroUsuario,
                ct);
        }


        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionSupervisorAsync(
                RechazarAsignacionSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _asignaciones
                .RechazarAsignacionSupervisorAsync(
                    data,
                    numeroUsuario,
                    ct);
        }

        #endregion


        #region CONSULTAS

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerSolicitudAsync(
                long solicitudId,
                CancellationToken ct)
        {
            return await _consultas.ObtenerSolicitudAsync(
                solicitudId,
                ct);
        }


        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerAsignacionAsync(
                long relevoNoPlaneadoAsignacionId,
                CancellationToken ct)
        {
            return await _consultas.ObtenerAsignacionAsync(
                relevoNoPlaneadoAsignacionId,
                ct);
        }


        public async Task<ResponseModel<List<RelevoPendienteEmpleadoResponse>>>
            ObtenerPendientesEmpleadoAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _consultas.ObtenerPendientesEmpleadoAsync(
                numeroUsuario,
                ct);
        }


        public async Task<ResponseModel<List<RelevoPendienteSupervisorResponse>>>
            ObtenerPendientesSupervisorAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            return await _consultas.ObtenerPendientesSupervisorAsync(
                numeroUsuario,
                ct);
        }

        #endregion
    }
}