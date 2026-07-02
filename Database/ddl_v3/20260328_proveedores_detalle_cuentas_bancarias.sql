-- Script para SQL Server.
-- Crea el detalle de cuentas bancarias por proveedor y migra los datos legados.
-- Los comentarios de tabla y columnas se guardan con extended properties (MS_Description).
-- Nota: no se crea FK a dbo.prv_proveedores porque la tabla legado no tiene PK declarada en este repo.

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.prv_proveedores', N'U') IS NULL
BEGIN
    THROW 50000, 'No existe dbo.prv_proveedores. Este script debe ejecutarse sobre la base de datos SQL Server legado.', 1;
END;

IF OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[prv_proveedor_cuenta_bancaria]
    (
        [proveedor_cuenta_bancaria_id] BIGINT IDENTITY(1,1) NOT NULL,
        [cod_proveedor] VARCHAR(20) NOT NULL,
        [banco] VARCHAR(80) NOT NULL,
        [cuenta_bancaria] VARCHAR(50) NOT NULL,
        [orden] INT NOT NULL
            CONSTRAINT [DF_prv_proveedor_cuenta_bancaria_orden] DEFAULT ((1)),
        [fecha_creacion] DATETIME NOT NULL
            CONSTRAINT [DF_prv_proveedor_cuenta_bancaria_fecha_creacion] DEFAULT (GETDATE()),
        [fecha_modificacion] DATETIME NULL,
        [rowid] UNIQUEIDENTIFIER NULL
            CONSTRAINT [DF_prv_proveedor_cuenta_bancaria_rowid] DEFAULT (NEWID()),
        CONSTRAINT [PK_prv_proveedor_cuenta_bancaria]
            PRIMARY KEY CLUSTERED ([proveedor_cuenta_bancaria_id] ASC)
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_prv_proveedor_cuenta_bancaria_cod_proveedor'
      AND object_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_prv_proveedor_cuenta_bancaria_cod_proveedor]
        ON [dbo].[prv_proveedor_cuenta_bancaria] ([cod_proveedor]);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_prv_proveedor_cuenta_bancaria_orden'
      AND object_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_prv_proveedor_cuenta_bancaria_orden]
        ON [dbo].[prv_proveedor_cuenta_bancaria] ([cod_proveedor], [orden]);
END;

INSERT INTO [dbo].[prv_proveedor_cuenta_bancaria]
(
    [cod_proveedor],
    [banco],
    [cuenta_bancaria],
    [orden],
    [fecha_creacion]
)
SELECT LTRIM(RTRIM(p.[cod_proveedor])),
       LTRIM(RTRIM(p.[nombrebanco1])),
       LTRIM(RTRIM(p.[cuenta_bancaria])),
       1,
       ISNULL(p.[fecha_modificacion], ISNULL(p.[fecha_creacion], GETDATE()))
FROM [dbo].[prv_proveedores] p
WHERE LTRIM(RTRIM(ISNULL(p.[cod_proveedor], ''))) <> ''
  AND LTRIM(RTRIM(ISNULL(p.[nombrebanco1], ''))) <> ''
  AND LTRIM(RTRIM(ISNULL(p.[cuenta_bancaria], ''))) <> ''
  AND NOT EXISTS
  (
      SELECT 1
      FROM [dbo].[prv_proveedor_cuenta_bancaria] d
      WHERE d.[cod_proveedor] = p.[cod_proveedor]
        AND d.[orden] = 1
  );

INSERT INTO [dbo].[prv_proveedor_cuenta_bancaria]
(
    [cod_proveedor],
    [banco],
    [cuenta_bancaria],
    [orden],
    [fecha_creacion]
)
SELECT LTRIM(RTRIM(p.[cod_proveedor])),
       LTRIM(RTRIM(p.[nombrebanco2])),
       LTRIM(RTRIM(p.[cuenta_bancaria])),
       2,
       ISNULL(p.[fecha_modificacion], ISNULL(p.[fecha_creacion], GETDATE()))
FROM [dbo].[prv_proveedores] p
WHERE LTRIM(RTRIM(ISNULL(p.[cod_proveedor], ''))) <> ''
  AND LTRIM(RTRIM(ISNULL(p.[nombrebanco2], ''))) <> ''
  AND LTRIM(RTRIM(ISNULL(p.[cuenta_bancaria], ''))) <> ''
  AND NOT EXISTS
  (
      SELECT 1
      FROM [dbo].[prv_proveedor_cuenta_bancaria] d
      WHERE d.[cod_proveedor] = p.[cod_proveedor]
        AND d.[orden] = 2
  );

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND ep.minor_id = 0
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Detalle de cuentas bancarias registradas por proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Detalle de cuentas bancarias registradas por proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria';
END;

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    INNER JOIN sys.columns c
        ON c.object_id = ep.major_id
       AND c.column_id = ep.minor_id
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND c.name = N'proveedor_cuenta_bancaria_id'
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Identificador interno del detalle bancario del proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'proveedor_cuenta_bancaria_id';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Identificador interno del detalle bancario del proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'proveedor_cuenta_bancaria_id';
END;

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    INNER JOIN sys.columns c
        ON c.object_id = ep.major_id
       AND c.column_id = ep.minor_id
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND c.name = N'cod_proveedor'
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Codigo del proveedor asociado en prv_proveedores.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'cod_proveedor';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Codigo del proveedor asociado en prv_proveedores.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'cod_proveedor';
END;

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    INNER JOIN sys.columns c
        ON c.object_id = ep.major_id
       AND c.column_id = ep.minor_id
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND c.name = N'banco'
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Nombre del banco del proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'banco';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Nombre del banco del proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'banco';
END;

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    INNER JOIN sys.columns c
        ON c.object_id = ep.major_id
       AND c.column_id = ep.minor_id
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND c.name = N'cuenta_bancaria'
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Numero de cuenta bancaria del proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'cuenta_bancaria';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Numero de cuenta bancaria del proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'cuenta_bancaria';
END;

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    INNER JOIN sys.columns c
        ON c.object_id = ep.major_id
       AND c.column_id = ep.minor_id
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND c.name = N'orden'
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Orden de visualizacion de la cuenta bancaria dentro del proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'orden';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Orden de visualizacion de la cuenta bancaria dentro del proveedor.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'orden';
END;

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    INNER JOIN sys.columns c
        ON c.object_id = ep.major_id
       AND c.column_id = ep.minor_id
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND c.name = N'fecha_creacion'
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Fecha de creacion del detalle bancario.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'fecha_creacion';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Fecha de creacion del detalle bancario.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'fecha_creacion';
END;

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    INNER JOIN sys.columns c
        ON c.object_id = ep.major_id
       AND c.column_id = ep.minor_id
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND c.name = N'fecha_modificacion'
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Fecha de ultima modificacion del detalle bancario.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'fecha_modificacion';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Fecha de ultima modificacion del detalle bancario.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'fecha_modificacion';
END;

IF EXISTS (
    SELECT 1
    FROM sys.extended_properties ep
    INNER JOIN sys.columns c
        ON c.object_id = ep.major_id
       AND c.column_id = ep.minor_id
    WHERE ep.name = N'MS_Description'
      AND ep.major_id = OBJECT_ID(N'dbo.prv_proveedor_cuenta_bancaria')
      AND c.name = N'rowid'
)
BEGIN
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Identificador unico auxiliar del registro.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'rowid';
END;
ELSE
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Identificador unico auxiliar del registro.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'prv_proveedor_cuenta_bancaria',
        @level2type = N'COLUMN', @level2name = N'rowid';
END;
