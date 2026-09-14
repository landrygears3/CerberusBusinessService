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
        private readonly RelevoFlexibleFunctions _flexibles;

        #endregion

        #region CONSTRUCTOR

        public RelevoNoPlaneadoFunctions(
            RelevoSolicitudFunctions solicitudes,
            RelevoAsignacionFunctions asignaciones,
            RelevoConsultaFunctions consultas,
            RelevoFlexibleFunctions flexibles)
        {
            _solicitudes = solicitudes;
            _asignaciones = asignaciones;
            _consultas = consultas;
            _flexibles = flexibles;
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
                string accessToken,
                CancellationToken ct)
        {
            return await _solicitudes
                .CrearSolicitudDesdeAsistenciaAsync(
                    data,
                    asistenciaSalienteId,
                    realizarCheckOut,
                    numeroUsuario,
                    accessToken,
                    ct);
        }

        internal async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CrearSolicitudDesdeSupervisionAsync(
                long supervisionId,
                string motivoRelevo,
                string numeroSupervisor,
                string accessToken,
                CancellationToken ct)
        {
            return await _solicitudes
                .CrearSolicitudDesdeSupervisionAsync(
                    supervisionId,
                    motivoRelevo,
                    numeroSupervisor,
                    accessToken,
                    ct);
        }

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoDto>>
            CancelarSolicitudAsync(
                CancelarSolicitudRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                CancellationToken ct)
        {
            if (data != null &&
                await _flexibles.EsSolicitudSinAfectadoAsync(
                    data.SolicitudRelevoNoPlaneadoId,
                    ct))
            {
                return await _flexibles.CancelarSolicitudAsync(
                    data,
                    numeroUsuario,
                    ct);
            }

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
                string accessToken,
                CancellationToken ct)
        {
            if (data != null &&
                await _flexibles.EsSolicitudSinAfectadoAsync(
                    data.SolicitudRelevoNoPlaneadoId,
                    ct))
            {
                return await _flexibles.AsignarEmpleadoAsync(
                    data,
                    numeroUsuario,
                    accessToken,
                    ct);
            }

            return await _asignaciones.AsignarEmpleadoAsync(
                data,
                numeroUsuario,
                accessToken,
                ct);
        }

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AsignarseSupervisorAsync(
                AsignarseSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (data != null &&
                await _flexibles.EsSolicitudSinAfectadoAsync(
                    data.SolicitudRelevoNoPlaneadoId,
                    ct))
            {
                return await _flexibles.AsignarseSupervisorAsync(
                    data,
                    numeroUsuario,
                    accessToken,
                    ct);
            }

            return await _asignaciones.AsignarseSupervisorAsync(
                data,
                numeroUsuario,
                accessToken,
                ct);
        }

        internal async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            CrearExtensionAsync(
                long solicitudRelevoNoPlaneadoId,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (await _flexibles.EsSolicitudSinAfectadoAsync(
                solicitudRelevoNoPlaneadoId,
                ct))
            {
                return await _flexibles.CrearExtensionAsync(
                    solicitudRelevoNoPlaneadoId,
                    numeroUsuario,
                    accessToken,
                    ct);
            }

            return await _asignaciones.CrearExtensionAsync(
                solicitudRelevoNoPlaneadoId,
                numeroUsuario,
                accessToken,
                ct);
        }

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            AutorizarAsignacionAsync(
                AutorizarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (data != null &&
                await _flexibles.EsAsignacionSinAfectadoAsync(
                    data.RelevoNoPlaneadoAsignacionId,
                    ct))
            {
                return await _flexibles.AutorizarAsignacionAsync(
                    data,
                    numeroUsuario,
                    accessToken,
                    ct);
            }

            return await _asignaciones.AutorizarAsignacionAsync(
                data,
                numeroUsuario,
                accessToken,
                ct);
        }

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            FirmarResponsivaAsync(
                FirmarResponsivaRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (data != null &&
                await _flexibles.EsAsignacionSinAfectadoAsync(
                    data.RelevoNoPlaneadoAsignacionId,
                    ct))
            {
                return await _flexibles.FirmarResponsivaAsync(
                    data,
                    numeroUsuario,
                    accessToken,
                    ct);
            }

            return await _asignaciones.FirmarResponsivaAsync(
                data,
                numeroUsuario,
                accessToken,
                ct);
        }

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionAsync(
                RechazarAsignacionRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (data != null &&
                await _flexibles.EsAsignacionSinAfectadoAsync(
                    data.RelevoNoPlaneadoAsignacionId,
                    ct))
            {
                return await _flexibles.RechazarAsignacionAsync(
                    data,
                    numeroUsuario,
                    accessToken,
                    ct);
            }

            return await _asignaciones.RechazarAsignacionAsync(
                data,
                numeroUsuario,
                accessToken,
                ct);
        }

        public async Task<ResponseModel<RelevoNoPlaneadoAsignacionDto>>
            RechazarAsignacionSupervisorAsync(
                RechazarAsignacionSupervisorRelevoNoPlaneadoRequest data,
                string numeroUsuario,
                string accessToken,
                CancellationToken ct)
        {
            if (data != null &&
                await _flexibles.EsAsignacionSinAfectadoAsync(
                    data.RelevoNoPlaneadoAsignacionId,
                    ct))
            {
                return await _flexibles
                    .RechazarAsignacionSupervisorAsync(
                        data,
                        numeroUsuario,
                        accessToken,
                        ct);
            }

            return await _asignaciones
                .RechazarAsignacionSupervisorAsync(
                    data,
                    numeroUsuario,
                    accessToken,
                    ct);
        }

        #endregion

        #region CONSULTAS

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerSolicitudAsync(
                long solicitudId,
                CancellationToken ct)
        {
            if (await _flexibles.EsSolicitudSinAfectadoAsync(
                solicitudId,
                ct))
            {
                return await _flexibles.ObtenerSolicitudAsync(
                    solicitudId,
                    ct);
            }

            return await _consultas.ObtenerSolicitudAsync(
                solicitudId,
                ct);
        }

        public async Task<ResponseModel<SolicitudRelevoNoPlaneadoResponse>>
            ObtenerAsignacionAsync(
                long relevoNoPlaneadoAsignacionId,
                CancellationToken ct)
        {
            if (await _flexibles.EsAsignacionSinAfectadoAsync(
                relevoNoPlaneadoAsignacionId,
                ct))
            {
                return await _flexibles.ObtenerAsignacionAsync(
                    relevoNoPlaneadoAsignacionId,
                    ct);
            }

            return await _consultas.ObtenerAsignacionAsync(
                relevoNoPlaneadoAsignacionId,
                ct);
        }

        public async Task<ResponseModel<List<RelevoPendienteEmpleadoResponse>>>
            ObtenerPendientesEmpleadoAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            var normales =
                await _consultas.ObtenerPendientesEmpleadoAsync(
                    numeroUsuario,
                    ct);

            if (!normales.isSuccess)
            {
                return normales;
            }

            var flexibles =
                await _flexibles.ObtenerPendientesEmpleadoAsync(
                    numeroUsuario,
                    ct);

            if (!flexibles.isSuccess)
            {
                return flexibles;
            }

            List<RelevoPendienteEmpleadoResponse> data =
                (normales.data ?? new List<RelevoPendienteEmpleadoResponse>())
                .Concat(
                    flexibles.data ??
                    new List<RelevoPendienteEmpleadoResponse>())
                .OrderBy(x => x.FechaHoraInicioCobertura)
                .ThenBy(x => x.FechaAsignacion)
                .ToList();

            return new ResponseModel<List<RelevoPendienteEmpleadoResponse>>
            {
                isSuccess = true,
                code = 200,
                message =
                    "Relevos pendientes del empleado obtenidos correctamente.",
                desc = null,
                data = data
            };
        }

        public async Task<ResponseModel<List<RelevoPendienteSupervisorResponse>>>
            ObtenerPendientesSupervisorAsync(
                string numeroUsuario,
                CancellationToken ct)
        {
            var normales =
                await _consultas.ObtenerPendientesSupervisorAsync(
                    numeroUsuario,
                    ct);

            if (!normales.isSuccess)
            {
                return normales;
            }

            var flexibles =
                await _flexibles.ObtenerPendientesSupervisorAsync(
                    numeroUsuario,
                    ct);

            if (!flexibles.isSuccess)
            {
                return flexibles;
            }

            List<RelevoPendienteSupervisorResponse> data =
                (normales.data ?? new List<RelevoPendienteSupervisorResponse>())
                .Concat(
                    flexibles.data ??
                    new List<RelevoPendienteSupervisorResponse>())
                .OrderBy(x => x.FechaHoraInicioCobertura)
                .ThenBy(x => x.FechaRegistro)
                .ToList();

            return new ResponseModel<List<RelevoPendienteSupervisorResponse>>
            {
                isSuccess = true,
                code = 200,
                message =
                    "Relevos pendientes del supervisor obtenidos correctamente.",
                desc = null,
                data = data
            };
        }

        #endregion
    }
}
