using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.DTO.Oficinas;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CerberusBusinessService.Functions.Oficinas
{
    public class OficinasFunctions
    {
        #region PROPIEDADES

        private readonly string _csCerberus;

        #endregion

        #region CONSTRUCTOR

        public OficinasFunctions(IConfiguration config)
        {
            _csCerberus =
                config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "No existe la cadena DefaultConnection.");
        }

        #endregion

        #region COMMIT OFICINA

        public async Task<ResponseModel<CommitOficinaResponse>>
            CommitOficinaAsync(
                CommitOficinaRequest request,
                string numeroUsuario,
                CancellationToken ct)
        {
            ResponseModel<CommitOficinaResponse> response =
                new ResponseModel<CommitOficinaResponse>();

            if (request == null)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "El request es obligatorio.";
                response.data = null;
                return response;
            }

            if (request.OficinaId < 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "OficinaId no es válido.";
                response.data = null;
                return response;
            }

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "Nombre es obligatorio.";
                response.data = null;
                return response;
            }

            if (string.IsNullOrWhiteSpace(numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al usuario autenticado.";
                response.desc = null;
                response.data = null;
                return response;
            }

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                int oficinaId;
                bool esNuevo;

                if (request.OficinaId == 0)
                {
                    const string sql = @"
INSERT INTO dbo.Oficinas
(
    Nombre,
    Descripcion,
    Direccion,
    UsuarioAlta
)
OUTPUT INSERTED.OficinaId
VALUES
(
    @Nombre,
    @Descripcion,
    @Direccion,
    @UsuarioAlta
);";

                    oficinaId =
                        await conn.ExecuteScalarAsync<int>(
                            new CommandDefinition(
                                sql,
                                new
                                {
                                    Nombre =
                                        request.Nombre.Trim(),

                                    Descripcion =
                                        string.IsNullOrWhiteSpace(
                                            request.Descripcion)
                                            ? null
                                            : request.Descripcion.Trim(),

                                    Direccion =
                                        string.IsNullOrWhiteSpace(
                                            request.Direccion)
                                            ? null
                                            : request.Direccion.Trim(),

                                    UsuarioAlta =
                                        numeroUsuario.Trim()
                                },
                                cancellationToken: ct));

                    esNuevo = true;
                }
                else
                {
                    const string sqlExiste = @"
SELECT COUNT(1)
FROM dbo.Oficinas
WHERE OficinaId = @OficinaId;";

                    int existe =
                        await conn.ExecuteScalarAsync<int>(
                            new CommandDefinition(
                                sqlExiste,
                                new
                                {
                                    request.OficinaId
                                },
                                cancellationToken: ct));

                    if (existe == 0)
                    {
                        response.isSuccess = false;
                        response.code = 404;
                        response.message =
                            "Oficina no encontrada.";
                        response.desc = null;
                        response.data = null;
                        return response;
                    }

                    const string sqlUpdate = @"
UPDATE dbo.Oficinas
SET
    Nombre = @Nombre,
    Descripcion = @Descripcion,
    Direccion = @Direccion,
    UsuarioModificacion = @UsuarioModificacion,
    FechaModificacion = SYSDATETIME()
WHERE OficinaId = @OficinaId;";

                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlUpdate,
                            new
                            {
                                request.OficinaId,

                                Nombre =
                                    request.Nombre.Trim(),

                                Descripcion =
                                    string.IsNullOrWhiteSpace(
                                        request.Descripcion)
                                        ? null
                                        : request.Descripcion.Trim(),

                                Direccion =
                                    string.IsNullOrWhiteSpace(
                                        request.Direccion)
                                        ? null
                                        : request.Direccion.Trim(),

                                UsuarioModificacion =
                                    numeroUsuario.Trim()
                            },
                            cancellationToken: ct));

                    oficinaId =
                        request.OficinaId;

                    esNuevo = false;
                }

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    esNuevo
                        ? "Oficina registrada correctamente."
                        : "Oficina actualizada correctamente.";
                response.desc = null;

                response.data =
                    new CommitOficinaResponse
                    {
                        OficinaId = oficinaId,
                        EsNuevo = esNuevo
                    };

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al registrar o actualizar la oficina.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region OBTENER OFICINAS

        public async Task<ResponseModel<List<OficinaResponse>>>
            ObtenerOficinasAsync(
                CancellationToken ct)
        {
            ResponseModel<List<OficinaResponse>> response =
                new ResponseModel<List<OficinaResponse>>();

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                const string sql = @"
SELECT
    OficinaId,
    Nombre,
    Descripcion,
    Direccion,
    Estatus,
    FechaAlta
FROM dbo.Oficinas
ORDER BY
    Estatus DESC,
    Nombre;";

                var result =
                    await conn.QueryAsync<OficinaResponse>(
                        new CommandDefinition(
                            sql,
                            cancellationToken: ct));

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Listado de oficinas obtenido correctamente.";
                response.desc = null;
                response.data = result.ToList();

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener las oficinas.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region OBTENER OFICINA

        public async Task<ResponseModel<OficinaResponse>>
            ObtenerOficinaAsync(
                GetOficinaRequest request,
                CancellationToken ct)
        {
            ResponseModel<OficinaResponse> response =
                new ResponseModel<OficinaResponse>();

            if (request == null ||
                request.OficinaId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc =
                    "OficinaId es obligatorio.";
                response.data = null;
                return response;
            }

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                const string sql = @"
SELECT
    OficinaId,
    Nombre,
    Descripcion,
    Direccion,
    Estatus,
    FechaAlta
FROM dbo.Oficinas
WHERE OficinaId = @OficinaId;";

                OficinaResponse? oficina =
                    await conn.QueryFirstOrDefaultAsync<OficinaResponse>(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                request.OficinaId
                            },
                            cancellationToken: ct));

                if (oficina == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "Oficina no encontrada.";
                    response.desc = null;
                    response.data = null;
                    return response;
                }

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Oficina obtenida correctamente.";
                response.desc = null;
                response.data = oficina;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener la oficina.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region COMMIT SERVICIO OFICINA

        public async Task<ResponseModel<CommitServicioOficinaResponse>>
            CommitServicioOficinaAsync(
                CommitServicioOficinaRequest request,
                string numeroUsuario,
                CancellationToken ct)
        {
            ResponseModel<CommitServicioOficinaResponse> response =
                new ResponseModel<CommitServicioOficinaResponse>();

            SqlTransaction? transaction = null;

            #region VALIDACIONES REQUEST

            if (request == null)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "El request es obligatorio.";
                response.data = null;

                return response;
            }

            if (request.ServicioOficinaId < 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "ServicioOficinaId no es válido.";
                response.data = null;

                return response;
            }

            if (request.OficinaId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "OficinaId es obligatorio.";
                response.data = null;

                return response;
            }

            if (request.DepartamentoId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "DepartamentoId es obligatorio.";
                response.data = null;

                return response;
            }

            if (string.IsNullOrWhiteSpace(
                request.NombreServicio))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc = "NombreServicio es obligatorio.";
                response.data = null;

                return response;
            }

            if (request.Horarios == null ||
                request.Horarios.Count == 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc =
                    "Debe registrar al menos un día operativo.";
                response.data = null;

                return response;
            }

            if (request.Horarios.Any(x =>
                x.DiaSemana < 1 ||
                x.DiaSemana > 7))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc =
                    "DiaSemana debe tener un valor entre 1 y 7.";
                response.data = null;

                return response;
            }

            if (request.Horarios
                .GroupBy(x => x.DiaSemana)
                .Any(x => x.Count() > 1))
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc =
                    "No se puede registrar más de un horario para el mismo día.";
                response.data = null;

                return response;
            }

            foreach (
                ServicioOficinaHorarioRequest horario
                in request.Horarios)
            {
                string? errorHorario =
                    ValidarHorario(horario);

                if (errorHorario != null)
                {
                    response.isSuccess = false;
                    response.code = 400;
                    response.message = "Request inválido";
                    response.desc =
                        $"Día {horario.DiaSemana}: {errorHorario}";
                    response.data = null;

                    return response;
                }
            }

            if (string.IsNullOrWhiteSpace(
                numeroUsuario))
            {
                response.isSuccess = false;
                response.code = 401;
                response.message =
                    "No fue posible identificar al usuario autenticado.";
                response.desc = null;
                response.data = null;

                return response;
            }

            #endregion

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                #region VALIDAR OFICINA

                const string sqlOficina = @"
SELECT COUNT(1)
FROM dbo.Oficinas
WHERE OficinaId = @OficinaId
  AND Estatus = 1;";

                int existeOficina =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlOficina,
                            new
                            {
                                request.OficinaId
                            },
                            transaction,
                            cancellationToken: ct));

                if (existeOficina == 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "La oficina no existe o no se encuentra activa.";
                    response.desc = null;
                    response.data = null;

                    return response;
                }

                #endregion

                #region VALIDAR DEPARTAMENTO

                const string sqlDepartamento = @"
SELECT COUNT(1)
FROM CerberusConfig.dbo.Departamentos
WHERE Id = @DepartamentoId
  AND Activo = 1;";

                int existeDepartamento =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlDepartamento,
                            new
                            {
                                request.DepartamentoId
                            },
                            transaction,
                            cancellationToken: ct));

                if (existeDepartamento == 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El departamento no existe o no está activo.";
                    response.desc = null;
                    response.data = null;

                    return response;
                }

                #endregion

                int servicioOficinaId;
                bool esNuevo;

                #region ALTA SERVICIO OFICINA

                if (request.ServicioOficinaId == 0)
                {
                    const string sqlInsert = @"
INSERT INTO dbo.ServicioOficina
(
    OficinaId,
    DepartamentoId,
    NombreServicio,
    Descripcion,
    UsuarioAlta
)
OUTPUT INSERTED.ServicioOficinaId
VALUES
(
    @OficinaId,
    @DepartamentoId,
    @NombreServicio,
    @Descripcion,
    @UsuarioAlta
);";

                    servicioOficinaId =
                        await conn.ExecuteScalarAsync<int>(
                            new CommandDefinition(
                                sqlInsert,
                                new
                                {
                                    request.OficinaId,
                                    request.DepartamentoId,

                                    NombreServicio =
                                        request.NombreServicio.Trim(),

                                    Descripcion =
                                        string.IsNullOrWhiteSpace(
                                            request.Descripcion)
                                            ? null
                                            : request.Descripcion.Trim(),

                                    UsuarioAlta =
                                        numeroUsuario.Trim()
                                },
                                transaction,
                                cancellationToken: ct));

                    esNuevo = true;
                }

                #endregion

                #region ACTUALIZAR SERVICIO OFICINA

                else
                {
                    const string sqlExisteServicio = @"
SELECT COUNT(1)
FROM dbo.ServicioOficina
WHERE ServicioOficinaId =
      @ServicioOficinaId;";

                    int existeServicio =
                        await conn.ExecuteScalarAsync<int>(
                            new CommandDefinition(
                                sqlExisteServicio,
                                new
                                {
                                    request.ServicioOficinaId
                                },
                                transaction,
                                cancellationToken: ct));

                    if (existeServicio == 0)
                    {
                        transaction.Rollback();
                        transaction = null;

                        response.isSuccess = false;
                        response.code = 404;
                        response.message =
                            "Servicio de oficina no encontrado.";
                        response.desc = null;
                        response.data = null;

                        return response;
                    }

                    const string sqlUpdate = @"
UPDATE dbo.ServicioOficina
SET
    OficinaId = @OficinaId,
    DepartamentoId = @DepartamentoId,
    NombreServicio = @NombreServicio,
    Descripcion = @Descripcion,
    UsuarioModificacion = @UsuarioModificacion,
    FechaModificacion = SYSDATETIME()
WHERE ServicioOficinaId =
      @ServicioOficinaId;";

                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlUpdate,
                            new
                            {
                                request.ServicioOficinaId,
                                request.OficinaId,
                                request.DepartamentoId,

                                NombreServicio =
                                    request.NombreServicio.Trim(),

                                Descripcion =
                                    string.IsNullOrWhiteSpace(
                                        request.Descripcion)
                                        ? null
                                        : request.Descripcion.Trim(),

                                UsuarioModificacion =
                                    numeroUsuario.Trim()
                            },
                            transaction,
                            cancellationToken: ct));

                    servicioOficinaId =
                        request.ServicioOficinaId;

                    esNuevo = false;
                }

                #endregion

                #region HORARIOS

                byte[] diasEnviados =
                    request.Horarios
                        .Select(x => x.DiaSemana)
                        .ToArray();

                const string sqlDesactivarHorarios = @"
UPDATE dbo.ServicioOficinaHorario
SET
    Estatus = 0,
    UsuarioModificacion = @UsuarioModificacion,
    FechaModificacion = SYSDATETIME()
WHERE ServicioOficinaId =
      @ServicioOficinaId
  AND Estatus = 1
  AND DiaSemana NOT IN @DiasEnviados;";

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlDesactivarHorarios,
                        new
                        {
                            ServicioOficinaId =
                                servicioOficinaId,

                            DiasEnviados =
                                diasEnviados,

                            UsuarioModificacion =
                                numeroUsuario.Trim()
                        },
                        transaction,
                        cancellationToken: ct));

                foreach (
                    ServicioOficinaHorarioRequest horario
                    in request.Horarios)
                {
                    const string sqlHorarioExistente = @"
SELECT TOP (1)
    ServicioOficinaHorarioId
FROM dbo.ServicioOficinaHorario
WHERE ServicioOficinaId =
      @ServicioOficinaId
  AND DiaSemana =
      @DiaSemana
  AND Estatus = 1
ORDER BY ServicioOficinaHorarioId DESC;";

                    int? servicioOficinaHorarioId =
                        await conn.QueryFirstOrDefaultAsync<int?>(
                            new CommandDefinition(
                                sqlHorarioExistente,
                                new
                                {
                                    ServicioOficinaId =
                                        servicioOficinaId,

                                    horario.DiaSemana
                                },
                                transaction,
                                cancellationToken: ct));

                    if (servicioOficinaHorarioId.HasValue)
                    {
                        const string sqlUpdateHorario = @"
UPDATE dbo.ServicioOficinaHorario
SET
    HoraEntrada = @HoraEntrada,
    HoraSalida = @HoraSalida,
    SalidaDiaSiguiente = @SalidaDiaSiguiente,
    UsuarioModificacion = @UsuarioModificacion,
    FechaModificacion = SYSDATETIME()
WHERE ServicioOficinaHorarioId =
      @ServicioOficinaHorarioId;";

                        await conn.ExecuteAsync(
                            new CommandDefinition(
                                sqlUpdateHorario,
                                new
                                {
                                    ServicioOficinaHorarioId =
                                        servicioOficinaHorarioId.Value,

                                    horario.HoraEntrada,
                                    horario.HoraSalida,
                                    horario.SalidaDiaSiguiente,

                                    UsuarioModificacion =
                                        numeroUsuario.Trim()
                                },
                                transaction,
                                cancellationToken: ct));
                    }
                    else
                    {
                        const string sqlInsertHorario = @"
INSERT INTO dbo.ServicioOficinaHorario
(
    ServicioOficinaId,
    DiaSemana,
    HoraEntrada,
    HoraSalida,
    SalidaDiaSiguiente,
    UsuarioAlta
)
VALUES
(
    @ServicioOficinaId,
    @DiaSemana,
    @HoraEntrada,
    @HoraSalida,
    @SalidaDiaSiguiente,
    @UsuarioAlta
);";

                        await conn.ExecuteAsync(
                            new CommandDefinition(
                                sqlInsertHorario,
                                new
                                {
                                    ServicioOficinaId =
                                        servicioOficinaId,

                                    horario.DiaSemana,
                                    horario.HoraEntrada,
                                    horario.HoraSalida,
                                    horario.SalidaDiaSiguiente,

                                    UsuarioAlta =
                                        numeroUsuario.Trim()
                                },
                                transaction,
                                cancellationToken: ct));
                    }
                }

                #endregion

                #region COMMIT

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;

                response.message =
                    esNuevo
                        ? "Servicio de oficina registrado correctamente."
                        : "Servicio de oficina actualizado correctamente.";

                response.desc = null;

                response.data =
                    new CommitServicioOficinaResponse
                    {
                        ServicioOficinaId =
                            servicioOficinaId,

                        EsNuevo =
                            esNuevo
                    };

                return response;

                #endregion
            }
            catch (SqlException ex)
            {
                #region ERROR SQL

                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error SQL al registrar o actualizar el servicio de oficina.";
                response.desc = ex.Message;
                response.data = null;

                return response;

                #endregion
            }
            catch (Exception ex)
            {
                #region ERROR GENERAL

                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al registrar o actualizar el servicio de oficina.";
                response.desc = ex.Message;
                response.data = null;

                return response;

                #endregion
            }
        }

        #endregion

        #region OBTENER SERVICIOS OFICINA

        public async Task<ResponseModel<List<ServicioOficinaResponse>>>
            ObtenerServiciosOficinaAsync(
                GetOficinaRequest request,
                CancellationToken ct)
        {
            ResponseModel<List<ServicioOficinaResponse>> response =
                new ResponseModel<List<ServicioOficinaResponse>>();

            if (request == null ||
                request.OficinaId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc =
                    "OficinaId es obligatorio.";
                response.data = null;
                return response;
            }

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                const string sql = @"
SELECT
    ServicioOficinaId,
    OficinaId,
    NombreServicio,
    Descripcion,
    Estatus,
    FechaAlta
FROM dbo.ServicioOficina
WHERE OficinaId = @OficinaId
ORDER BY
    Estatus DESC,
    NombreServicio;";

                var result =
                    await conn.QueryAsync<ServicioOficinaResponse>(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                request.OficinaId
                            },
                            cancellationToken: ct));

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Servicios de oficina obtenidos correctamente.";
                response.desc = null;
                response.data = result.ToList();

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los servicios de oficina.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region OBTENER SERVICIO OFICINA

        public async Task<ResponseModel<ServicioOficinaResponse>>
            ObtenerServicioOficinaAsync(
                GetServicioOficinaRequest request,
                CancellationToken ct)
        {
            ResponseModel<ServicioOficinaResponse> response =
                new ResponseModel<ServicioOficinaResponse>();

            if (request == null ||
                request.ServicioOficinaId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc =
                    "ServicioOficinaId es obligatorio.";
                response.data = null;
                return response;
            }

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                const string sqlServicio = @"
SELECT
    ServicioOficinaId,
    OficinaId,
    NombreServicio,
    Descripcion,
    Estatus,
    FechaAlta
FROM dbo.ServicioOficina
WHERE ServicioOficinaId =
      @ServicioOficinaId;";

                ServicioOficinaResponse? servicio =
                    await conn.QueryFirstOrDefaultAsync<
                        ServicioOficinaResponse>(
                        new CommandDefinition(
                            sqlServicio,
                            new
                            {
                                request.ServicioOficinaId
                            },
                            cancellationToken: ct));

                if (servicio == null)
                {
                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "Servicio de oficina no encontrado.";
                    response.data = null;
                    return response;
                }

                const string sqlHorarios = @"
SELECT
    ServicioOficinaHorarioId,
    ServicioOficinaId,
    DiaSemana,
    HoraEntrada,
    HoraSalida,
    SalidaDiaSiguiente,
    Estatus
FROM dbo.ServicioOficinaHorario
WHERE ServicioOficinaId =
      @ServicioOficinaId
  AND Estatus = 1
ORDER BY DiaSemana;";

                var horarios =
                    await conn.QueryAsync<
                        ServicioOficinaHorarioResponse>(
                        new CommandDefinition(
                            sqlHorarios,
                            new
                            {
                                request.ServicioOficinaId
                            },
                            cancellationToken: ct));

                servicio.Horarios =
                    horarios.ToList();

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Servicio de oficina obtenido correctamente.";
                response.desc = null;
                response.data = servicio;

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener el servicio de oficina.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region ASIGNAR EMPLEADO

        public async Task<
            ResponseModel<AsignarEmpleadoServicioOficinaResponse>>
            AsignarEmpleadoAsync(
                AsignarEmpleadoServicioOficinaRequest request,
                string numeroUsuario,
                CancellationToken ct)
        {
            ResponseModel<
                AsignarEmpleadoServicioOficinaResponse> response =
                new ResponseModel<
                    AsignarEmpleadoServicioOficinaResponse>();

            SqlTransaction? transaction = null;

            if (request == null ||
                request.ServicioOficinaId <= 0 ||
                request.EmpleadoId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc =
                    "ServicioOficinaId y EmpleadoId son obligatorios.";
                response.data = null;
                return response;
            }

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                transaction =
                    conn.BeginTransaction();

                const string sqlServicio = @"
SELECT COUNT(1)
FROM dbo.ServicioOficina
    WITH (UPDLOCK, HOLDLOCK)
WHERE ServicioOficinaId =
      @ServicioOficinaId
  AND Estatus = 1;";

                int existeServicio =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlServicio,
                            new
                            {
                                request.ServicioOficinaId
                            },
                            transaction,
                            cancellationToken: ct));

                if (existeServicio == 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El servicio de oficina no existe o no está activo.";
                    response.data = null;
                    return response;
                }

                const string sqlEmpleado = @"
SELECT COUNT(1)
FROM dbo.DatosGeneralesEmpleado
WHERE ID = @EmpleadoId;";

                int existeEmpleado =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlEmpleado,
                            new
                            {
                                request.EmpleadoId
                            },
                            transaction,
                            cancellationToken: ct));

                if (existeEmpleado == 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 404;
                    response.message =
                        "El empleado indicado no existe.";
                    response.data = null;
                    return response;
                }

                const string sqlDuplicado = @"
SELECT COUNT(1)
FROM dbo.ServicioOficinaEmpleado
    WITH (UPDLOCK, HOLDLOCK)
WHERE ServicioOficinaId =
      @ServicioOficinaId
  AND EmpleadoId =
      @EmpleadoId
  AND Estatus = 1;";

                int duplicado =
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            sqlDuplicado,
                            new
                            {
                                request.ServicioOficinaId,
                                request.EmpleadoId
                            },
                            transaction,
                            cancellationToken: ct));

                if (duplicado > 0)
                {
                    transaction.Rollback();
                    transaction = null;

                    response.isSuccess = false;
                    response.code = 409;
                    response.message =
                        "El empleado ya está asignado a este servicio de oficina.";
                    response.data = null;
                    return response;
                }

                const string sqlInsert = @"
INSERT INTO dbo.ServicioOficinaEmpleado
(
    ServicioOficinaId,
    EmpleadoId,
    UsuarioAlta
)
OUTPUT INSERTED.ServicioOficinaEmpleadoId
VALUES
(
    @ServicioOficinaId,
    @EmpleadoId,
    @UsuarioAlta
);";

                long asignacionId =
                    await conn.ExecuteScalarAsync<long>(
                        new CommandDefinition(
                            sqlInsert,
                            new
                            {
                                request.ServicioOficinaId,
                                request.EmpleadoId,

                                UsuarioAlta =
                                    numeroUsuario.Trim()
                            },
                            transaction,
                            cancellationToken: ct));

                transaction.Commit();
                transaction = null;

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Empleado asignado correctamente al servicio de oficina.";
                response.desc = null;

                response.data =
                    new AsignarEmpleadoServicioOficinaResponse
                    {
                        ServicioOficinaEmpleadoId =
                            asignacionId,

                        ServicioOficinaId =
                            request.ServicioOficinaId,

                        EmpleadoId =
                            request.EmpleadoId
                    };

                return response;
            }
            catch (Exception ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                }

                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al asignar el empleado al servicio de oficina.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region OBTENER EMPLEADOS SERVICIO

        public async Task<
            ResponseModel<List<ServicioOficinaEmpleadoResponse>>>
            ObtenerEmpleadosServicioAsync(
                GetServicioOficinaRequest request,
                CancellationToken ct)
        {
            ResponseModel<
                List<ServicioOficinaEmpleadoResponse>> response =
                new ResponseModel<
                    List<ServicioOficinaEmpleadoResponse>>();

            if (request == null ||
                request.ServicioOficinaId <= 0)
            {
                response.isSuccess = false;
                response.code = 400;
                response.message = "Request inválido";
                response.desc =
                    "ServicioOficinaId es obligatorio.";
                response.data = null;
                return response;
            }

            try
            {
                using var conn =
                    new SqlConnection(_csCerberus);

                await conn.OpenAsync(ct);

                const string sql = @"
SELECT
    SOE.ServicioOficinaEmpleadoId,
    SOE.ServicioOficinaId,
    SOE.EmpleadoId,

    ISNULL(
        DGE.UsuarioAsignado,
        ''
    ) AS NumeroUsuario,

    LTRIM(
        RTRIM(
            CONCAT(
                ISNULL(DGE.Nombres, ''),
                ' ',
                ISNULL(DGE.ApellidoPaterno, ''),
                ' ',
                ISNULL(DGE.ApellidoMaterno, '')
            )
        )
    ) AS NombreEmpleado,

    SOE.Estatus,
    SOE.FechaAlta

FROM dbo.ServicioOficinaEmpleado SOE

INNER JOIN dbo.DatosGeneralesEmpleado DGE
    ON DGE.ID = SOE.EmpleadoId

WHERE SOE.ServicioOficinaId =
      @ServicioOficinaId
  AND SOE.Estatus = 1

ORDER BY
    NombreEmpleado;";

                var result =
                    await conn.QueryAsync<
                        ServicioOficinaEmpleadoResponse>(
                        new CommandDefinition(
                            sql,
                            new
                            {
                                request.ServicioOficinaId
                            },
                            cancellationToken: ct));

                response.isSuccess = true;
                response.code = 200;
                response.message =
                    "Empleados asignados obtenidos correctamente.";
                response.desc = null;
                response.data = result.ToList();

                return response;
            }
            catch (Exception ex)
            {
                response.isSuccess = false;
                response.code = 500;
                response.message =
                    "Error al obtener los empleados asignados.";
                response.desc = ex.Message;
                response.data = null;

                return response;
            }
        }

        #endregion

        #region VALIDACIONES

        private string? ValidarHorario(
            ServicioOficinaHorarioRequest horario)
        {
            long inicio =
                (long)horario.HoraEntrada.TotalSeconds;

            long fin =
                (long)horario.HoraSalida.TotalSeconds;

            if (horario.SalidaDiaSiguiente)
            {
                fin += 86400;
            }

            if (fin <= inicio)
            {
                return
                    "HoraSalida debe ser posterior a HoraEntrada.";
            }

            if (fin - inicio > 86400)
            {
                return
                    "El horario no puede superar 24 horas.";
            }

            return null;
        }

        #endregion
    }
}