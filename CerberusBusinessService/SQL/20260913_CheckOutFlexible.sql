USE Cerberus;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    Check-Out flexible / abandono de turno

    1. Permite que una solicitud de cobertura no tenga
       ServicioEmpleadoAfectadoId cuando no existe relevo siguiente.

    2. Crea ABANDONO_TURNO copiando la configuración de afectación
       de nómina de FALTA_RECHAZO_TURNO. Así conserva el mismo tipo
       de descuento variable de la falta y no fija un monto nuevo.
*/

BEGIN TRY
    BEGIN TRANSACTION;

    -- ============================================================
    -- SOLICITUD SIN RELEVO AFECTADO
    -- ============================================================

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id =
              OBJECT_ID('dbo.SolicitudRelevoNoPlaneado')
          AND name = 'ServicioEmpleadoAfectadoId'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE dbo.SolicitudRelevoNoPlaneado
        ALTER COLUMN ServicioEmpleadoAfectadoId BIGINT NULL;
    END;

    -- ============================================================
    -- INCIDENCIA ABANDONO_TURNO
    -- ============================================================

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.CAT_TIPO_INCIDENCIA
        WHERE Clave = 'ABANDONO_TURNO'
    )
    BEGIN
        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.CAT_TIPO_INCIDENCIA
            WHERE Clave = 'FALTA_RECHAZO_TURNO'
        )
        BEGIN
            THROW 51001,
                'No existe FALTA_RECHAZO_TURNO para copiar su configuración de nómina.',
                1;
        END;

        DECLARE @Columnas NVARCHAR(MAX);
        DECLARE @Valores NVARCHAR(MAX);
        DECLARE @Sql NVARCHAR(MAX);

        SELECT
            @Columnas =
                STRING_AGG(
                    QUOTENAME(C.name),
                    ', '
                ) WITHIN GROUP
                (
                    ORDER BY C.column_id
                ),

            @Valores =
                STRING_AGG(
                    CASE C.name
                        WHEN 'Clave' THEN
                            '''ABANDONO_TURNO'''

                        WHEN 'Nombre' THEN
                            '''Abandono de turno'''

                        WHEN 'Descripcion' THEN
                            '''Salida del empleado antes de finalizar su turno programado.'''

                        ELSE
                            'SRC.' + QUOTENAME(C.name)
                    END,
                    ', '
                ) WITHIN GROUP
                (
                    ORDER BY C.column_id
                )
        FROM sys.columns C
        WHERE C.object_id =
              OBJECT_ID('dbo.CAT_TIPO_INCIDENCIA')
          AND C.name <> 'TipoIncidenciaId'
          AND C.is_identity = 0
          AND C.is_computed = 0
          AND C.system_type_id <> 189;

        IF NULLIF(@Columnas, '') IS NULL
           OR NULLIF(@Valores, '') IS NULL
        BEGIN
            THROW 51002,
                'No fue posible determinar las columnas de CAT_TIPO_INCIDENCIA.',
                1;
        END;

        SET @Sql =
            N'INSERT INTO dbo.CAT_TIPO_INCIDENCIA (' +
            @Columnas +
            N') SELECT ' +
            @Valores +
            N' FROM dbo.CAT_TIPO_INCIDENCIA SRC ' +
            N'WHERE SRC.Clave = ''FALTA_RECHAZO_TURNO'';';

        EXEC sys.sp_executesql @Sql;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO

-- ============================================================
-- VALIDACION
-- ============================================================

SELECT
    C.name AS Columna,
    C.is_nullable AS PermiteNull
FROM sys.columns C
WHERE C.object_id =
      OBJECT_ID('dbo.SolicitudRelevoNoPlaneado')
  AND C.name = 'ServicioEmpleadoAfectadoId';

SELECT
    TipoIncidenciaId,
    Clave,
    AfectaNomina,
    TipoAfectacionNomina,
    MontoAfectacion,
    Estatus
FROM dbo.CAT_TIPO_INCIDENCIA
WHERE Clave IN
(
    'FALTA_RECHAZO_TURNO',
    'ABANDONO_TURNO'
)
ORDER BY TipoIncidenciaId;
GO
