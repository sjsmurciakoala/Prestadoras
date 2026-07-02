-- =============================================================================
-- Módulo Almacén (alm_) y Activo Fijo (af_) — migración desde bdsimafi (MySQL)
-- Fecha: 2026-07-01
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Origen (MySQL bdsimafi, sin FKs declarados — MyISAM):
--   almacen        -> alm_articulo               (catálogo de artículos + existencia)
--   inventariotra  -> alm_kardex                 (movimientos de bodega; relacionada
--                                                  con almacen/compras/requisiciones/
--                                                  descargos por codigo_articulo, NO
--                                                  con la tabla "inventario" pese al
--                                                  nombre)
--   compras        -> alm_compra
--   requisiciones  -> alm_requisicion
--   descargos      -> alm_descargo
--   inventario     -> af_activo_fijo             (activo fijo, NO existencias de
--                                                  bodega: trae depreciación, vida
--                                                  útil, valor en libros, responsable)
--   depreinve      -> af_activo_fijo_depreciacion (detalle mensual de depreciación,
--                                                  relacionada con inventario por
--                                                  codinve/cod_inve)
--
-- Notas de migración:
--   - Ninguna de las tablas MySQL origen tiene PK real utilizable como clave
--     primaria multiempresa; se usa id SERIAL nuevo en todas.
--   - Columnas con nombres ambiguos en el origen (p. ej. "codigo" en compras,
--     "otros" en requisiciones, los pares contable/ctacontable en descargos,
--     "valor_depr2" en inventario) se preservan con nombre descriptivo pero su
--     semántica exacta debe validarse con el cliente antes de construir UI sobre
--     ellas — ver comentarios de columna.
--   - af_activo_fijo_depreciacion.activo_fijo_id se deja NULL en la carga inicial
--     y se resuelve en un segundo paso (UPDATE por codigo_activo) una vez cargada
--     af_activo_fijo, ya que MySQL no tiene esa relación como FK física.
-- =============================================================================

BEGIN;

-- ── 1. alm_articulo (ex almacen) ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS alm_articulo (
    id                 SERIAL        PRIMARY KEY,
    company_id         BIGINT        NOT NULL,
    codigo_articulo    VARCHAR(20)   NOT NULL,
    descripcion        VARCHAR(120)  NOT NULL DEFAULT '',
    fecha_registro     DATE          NULL,
    cantidad           NUMERIC(15,2) NOT NULL DEFAULT 0,
    existencia         NUMERIC(15,2) NOT NULL DEFAULT 0,
    existencia_minima  NUMERIC(11,2) NOT NULL DEFAULT 0,
    valor_unitario     NUMERIC(12,4) NOT NULL DEFAULT 0,
    linea              VARCHAR(2)    NULL,
    grupo              VARCHAR(6)    NULL,
    unidad_medida      VARCHAR(40)   NULL,
    diametro           VARCHAR(80)   NULL,
    cuenta_contable    VARCHAR(20)   NULL,
    CONSTRAINT uq_alm_articulo_company_codigo UNIQUE (company_id, codigo_articulo)
);

CREATE INDEX IF NOT EXISTS ix_alm_articulo_company ON alm_articulo(company_id);

COMMENT ON TABLE alm_articulo IS 'Catálogo de artículos de almacén con existencia actual (migrado de MySQL bdsimafi.almacen).';

-- ── 2. alm_kardex (ex inventariotra) ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS alm_kardex (
    id                 SERIAL        PRIMARY KEY,
    company_id         BIGINT        NOT NULL,
    numero_documento   NUMERIC(11,0) NULL,
    tipo_transaccion   VARCHAR(20)   NULL,
    fecha              DATE          NULL,
    codigo_articulo    VARCHAR(20)   NULL,
    cantidad           NUMERIC(11,2) NOT NULL DEFAULT 0,
    bodega             VARCHAR(2)    NULL,
    ingresos           NUMERIC(11,2) NOT NULL DEFAULT 0,
    salidas            NUMERIC(11,2) NOT NULL DEFAULT 0,
    valor_unitario     NUMERIC(11,2) NOT NULL DEFAULT 0,
    total              NUMERIC(11,2) NOT NULL DEFAULT 0,
    debe               NUMERIC(11,2) NOT NULL DEFAULT 0,
    haber              NUMERIC(11,2) NOT NULL DEFAULT 0,
    cuenta_contable    VARCHAR(25)   NULL,
    departamento       VARCHAR(3)    NULL,
    departamento_desc  VARCHAR(100)  NULL,
    linea              VARCHAR(2)    NULL,
    linea_desc         VARCHAR(150)  NULL,
    barrio             VARCHAR(3)    NULL,
    es_ajuste          BOOLEAN       NOT NULL DEFAULT false,
    descripcion        VARCHAR(120)  NULL,
    observacion        VARCHAR(254)  NULL
);

CREATE INDEX IF NOT EXISTS ix_alm_kardex_company         ON alm_kardex(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_kardex_codigo_articulo ON alm_kardex(codigo_articulo);
CREATE INDEX IF NOT EXISTS ix_alm_kardex_fecha           ON alm_kardex(fecha);

COMMENT ON TABLE alm_kardex IS 'Kardex de movimientos de bodega (ingresos/salidas por artículo). Migrado de MySQL bdsimafi.inventariotra — pese al nombre de origen, se relaciona con alm_articulo por codigo_articulo, no con af_activo_fijo.';

-- ── 3. alm_compra (ex compras) ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS alm_compra (
    id                        SERIAL         PRIMARY KEY,
    company_id                BIGINT         NOT NULL,
    fecha                     DATE           NULL,
    fecha_factura             DATE           NULL,
    codigo_articulo           VARCHAR(20)    NULL,
    cantidad                  NUMERIC(15,2)  NOT NULL DEFAULT 0,
    precio_unitario           NUMERIC(11,4)  NOT NULL DEFAULT 0,
    precio_unitario_anterior  NUMERIC(11,4)  NOT NULL DEFAULT 0,
    total                     NUMERIC(15,2)  NOT NULL DEFAULT 0,
    impuesto                  NUMERIC(11,2)  NOT NULL DEFAULT 0,
    descuento                 NUMERIC(11,2)  NOT NULL DEFAULT 0,
    oficina                   VARCHAR(5)     NULL,
    proveedor                 VARCHAR(100)   NULL,
    numero_factura            NUMERIC(11,0)  NULL,
    numero                    NUMERIC(11,0)  NULL,
    orden_compra              VARCHAR(20)    NULL,
    plazo_dias                NUMERIC(11,0)  NULL,
    tipo_compra               SMALLINT       NOT NULL DEFAULT 0,
    traslado                  VARCHAR(1)     NULL,
    cuenta_contable           VARCHAR(20)    NULL,
    cuenta_contable_anterior  VARCHAR(30)    NULL,
    cuenta_por_pagar          VARCHAR(30)    NULL,
    cuenta_por_pagar_anterior VARCHAR(30)    NULL,
    codigo_compra             VARCHAR(20)    NULL,
    concepto                  VARCHAR(254)   NULL
);

CREATE INDEX IF NOT EXISTS ix_alm_compra_company         ON alm_compra(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_compra_codigo_articulo ON alm_compra(codigo_articulo);
CREATE INDEX IF NOT EXISTS ix_alm_compra_fecha            ON alm_compra(fecha);

COMMENT ON TABLE alm_compra IS 'Compra de artículo de almacén (línea por artículo, con datos de proveedor/factura repetidos por fila — estructura plana heredada de MySQL bdsimafi.compras).';
COMMENT ON COLUMN alm_compra.codigo_compra IS 'Ex "codigo" en MySQL: semántica exacta sin confirmar, validar con el cliente antes de usarla en UI.';

-- ── 4. alm_requisicion (ex requisiciones) ────────────────────────────────────
CREATE TABLE IF NOT EXISTS alm_requisicion (
    id                        SERIAL         PRIMARY KEY,
    company_id                BIGINT         NOT NULL,
    numero                    NUMERIC(11,0)  NOT NULL,
    codigo_articulo           VARCHAR(20)    NULL,
    descripcion               VARCHAR(200)   NULL,
    aplicacion                VARCHAR(254)   NULL,
    cantidad                  NUMERIC(12,2)  NOT NULL DEFAULT 0,
    precio_unitario           NUMERIC(12,2)  NOT NULL DEFAULT 0,
    valor                     NUMERIC(12,2)  NOT NULL DEFAULT 0,
    impuesto_aplica           BOOLEAN        NOT NULL DEFAULT false,
    impuesto                  NUMERIC(13,2)  NOT NULL DEFAULT 0,
    descuento_aplica          BOOLEAN        NOT NULL DEFAULT false,
    valor_descuento           NUMERIC(12,2)  NOT NULL DEFAULT 0,
    total                     NUMERIC(13,2)  NOT NULL DEFAULT 0,
    tipo_requisicion          SMALLINT       NOT NULL DEFAULT 1,
    oficina                   VARCHAR(20)    NULL,
    departamento              VARCHAR(3)     NULL,
    solicitante                VARCHAR(120)  NULL,
    cargo_solicitante          VARCHAR(80)   NULL,
    diametro                   VARCHAR(80)   NULL,
    cuenta_contable             VARCHAR(30)  NULL,
    cuenta_contable_anterior    VARCHAR(30)  NULL,
    cuenta_por_pagar            VARCHAR(30)  NULL,
    fecha_requisicion          DATE          NULL,
    fecha_presupuesto          DATE          NULL,
    fecha_aprobacion           DATE          NULL,
    fecha_rechazo              DATE          NULL,
    fecha_entrega              DATE          NULL,
    aprobado                    BOOLEAN      NOT NULL DEFAULT false,
    rechazado                   BOOLEAN      NOT NULL DEFAULT false,
    descargado                  BOOLEAN      NOT NULL DEFAULT false,
    estatus                     VARCHAR(1)   NULL,
    observacion                 VARCHAR(300) NULL
);

CREATE INDEX IF NOT EXISTS ix_alm_requisicion_company         ON alm_requisicion(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_numero          ON alm_requisicion(numero);
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_codigo_articulo ON alm_requisicion(codigo_articulo);

COMMENT ON TABLE alm_requisicion IS 'Requisición interna de artículos de almacén (línea por renglón, con datos de cabecera repetidos — estructura plana heredada de MySQL bdsimafi.requisiciones).';

-- ── 5. alm_descargo (ex descargos) ───────────────────────────────────────────
CREATE TABLE IF NOT EXISTS alm_descargo (
    id                          SERIAL        PRIMARY KEY,
    company_id                  BIGINT        NOT NULL,
    fecha                       DATE          NULL,
    codigo_articulo             VARCHAR(20)   NULL,
    cantidad                    NUMERIC(12,2) NOT NULL DEFAULT 0,
    precio_unitario             NUMERIC(11,2) NOT NULL DEFAULT 0,
    total                       NUMERIC(11,2) NOT NULL DEFAULT 0,
    oficina                     VARCHAR(5)    NULL,
    departamento                VARCHAR(2)    NULL,
    numero_requisicion          NUMERIC(14,0) NULL,
    numero_documento            NUMERIC(11,0) NULL,
    tipo_requisicion            SMALLINT      NULL,
    traslado                    VARCHAR(1)    NULL,
    cuenta_contable_1           VARCHAR(30)   NULL,
    cuenta_contable_1_detalle   VARCHAR(30)   NULL,
    cuenta_contable_2           VARCHAR(30)   NULL,
    cuenta_contable_2_detalle   VARCHAR(30)   NULL,
    comentario                  VARCHAR(254)  NULL
);

CREATE INDEX IF NOT EXISTS ix_alm_descargo_company             ON alm_descargo(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_codigo_articulo     ON alm_descargo(codigo_articulo);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_numero_requisicion  ON alm_descargo(numero_requisicion);

COMMENT ON TABLE alm_descargo IS 'Descargo (salida/consumo) de artículo de almacén hacia un departamento. Migrado de MySQL bdsimafi.descargos.';
COMMENT ON COLUMN alm_descargo.cuenta_contable_1 IS 'Ex "contable"/"ctacontable"/"contable2"/"ctacontable2" en MySQL: dos pares de cuentas cuyo rol exacto (origen/destino, debe/haber) no está confirmado — validar con contabilidad antes de usarlas en asientos.';

-- ── 6. af_activo_fijo (ex inventario) ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS af_activo_fijo (
    id                        SERIAL        PRIMARY KEY,
    company_id                BIGINT        NOT NULL,
    codigo_activo             VARCHAR(50)   NOT NULL,
    descripcion               VARCHAR(254)  NOT NULL DEFAULT '',
    tipo                      VARCHAR(15)   NULL,
    clase                     VARCHAR(55)   NULL,
    modelo                    VARCHAR(30)   NULL,
    serie                     VARCHAR(30)   NULL,
    propiedades_especiales    VARCHAR(254)  NULL,
    estado                    SMALLINT      NULL,
    ubicacion                 VARCHAR(50)   NULL,
    direccion_foto            VARCHAR(160)  NULL,
    codigo_empleado           VARCHAR(15)   NULL,
    responsable               VARCHAR(80)   NULL,
    cargo_responsable         VARCHAR(50)   NULL,
    origen                    VARCHAR(2)    NULL,
    origen_desc               VARCHAR(60)   NULL,
    proveedor                 VARCHAR(80)   NULL,
    numero_factura            VARCHAR(20)   NULL,
    numero_cheque             NUMERIC(12,0) NULL,
    fecha_compra              DATE          NULL,
    fecha_cheque              DATE          NULL,
    fecha_asignacion          DATE          NULL,
    valor_compra              NUMERIC(12,2) NOT NULL DEFAULT 0,
    valor_rescate             NUMERIC(7,2)  NOT NULL DEFAULT 0,
    vida_util_anios           NUMERIC(3,0)  NULL,
    vida_util_periodos        NUMERIC(3,0)  NULL,
    meses_depreciados         NUMERIC(2,0)  NULL,
    depreciar                 BOOLEAN       NOT NULL DEFAULT false,
    fecha_ultima_depreciacion DATE          NULL,
    valor_a_depreciar         NUMERIC(12,2) NOT NULL DEFAULT 0,
    valor_depreciado          NUMERIC(12,2) NOT NULL DEFAULT 0,
    depreciacion_acumulada    NUMERIC(11,2) NOT NULL DEFAULT 0,
    depreciacion_mensual      NUMERIC(11,2) NOT NULL DEFAULT 0,
    depreciacion_diaria       NUMERIC(11,2) NOT NULL DEFAULT 0,
    valor_libros              NUMERIC(12,2) NOT NULL DEFAULT 0,
    valor_libros_alterno      NUMERIC(11,2) NULL,
    cuenta_contable           VARCHAR(25)   NULL,
    cuenta_contable_anterior  VARCHAR(30)   NULL,
    cuenta_depreciacion       VARCHAR(25)   NULL,
    cuenta_gasto              VARCHAR(25)   NULL,
    descargado                BOOLEAN       NOT NULL DEFAULT false,
    fecha_descargo            DATE          NULL,
    vendido                   BOOLEAN       NOT NULL DEFAULT false,
    valor_venta               NUMERIC(11,2) NULL,
    observacion               VARCHAR(254)  NULL,
    CONSTRAINT uq_af_activo_fijo_company_codigo UNIQUE (company_id, codigo_activo)
);

CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_company         ON af_activo_fijo(company_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_cuenta_contable ON af_activo_fijo(cuenta_contable);

COMMENT ON TABLE af_activo_fijo IS 'Maestro de activo fijo con datos de depreciación. Migrado de MySQL bdsimafi.inventario (nombre de origen engañoso: no es existencias de bodega, es activo fijo).';
COMMENT ON COLUMN af_activo_fijo.valor_libros_alterno IS 'Ex "valor_depr2" en MySQL: semántica exacta sin confirmar frente a valor_libros, validar con contabilidad.';

-- ── 7. af_activo_fijo_depreciacion (ex depreinve) ────────────────────────────
CREATE TABLE IF NOT EXISTS af_activo_fijo_depreciacion (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    activo_fijo_id      INTEGER       NULL REFERENCES af_activo_fijo(id) ON DELETE CASCADE,
    codigo_activo       VARCHAR(30)   NULL,
    anio                SMALLINT      NOT NULL,
    mes                 SMALLINT      NOT NULL,
    fecha_depreciacion  DATE          NULL,
    valor_depreciado    NUMERIC(11,2) NOT NULL DEFAULT 0,
    valor_neto_libros   NUMERIC(11,2) NOT NULL DEFAULT 0,
    cuenta_depreciacion VARCHAR(25)   NULL,
    cuenta_gasto        VARCHAR(25)   NULL,
    traslado            VARCHAR(1)    NULL,
    descripcion         VARCHAR(150)  NULL
);

CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_depreciacion_company ON af_activo_fijo_depreciacion(company_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_depreciacion_activo  ON af_activo_fijo_depreciacion(activo_fijo_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_depreciacion_codigo  ON af_activo_fijo_depreciacion(codigo_activo);

COMMENT ON TABLE af_activo_fijo_depreciacion IS 'Detalle mensual de depreciación por activo fijo. Migrado de MySQL bdsimafi.depreinve, relacionada con af_activo_fijo por codigo_activo (activo_fijo_id se completa en un segundo paso tras la carga inicial).';
COMMENT ON COLUMN af_activo_fijo_depreciacion.codigo_activo IS 'Nulo en 3 de 44,579 filas de origen (depreinve.codinve vacío) — dato huérfano ya existente en MySQL, no un error de migración.';

COMMIT;
