-- =============================================================================
-- Órdenes de compra — alm_orden_compra / alm_orden_compra_detalle / correlativo
-- Fecha: 2026-07-30
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ
-- El portal no tenía módulo de órdenes de compra: la captura vivía en Centura
-- (frmOCMercaderias, GA_IN.APT) y en alm_compra sólo quedaba `orden_compra` como
-- texto suelto. Se crea el modelo de O/C para que la recepción de compras (factura
-- de proveedor) pueda operar en modo "Con orden de compra". Diseño documentado en
-- docs/centura-flujos/README_orden_compra.md (mockup aprobado por el usuario).
--
-- QUÉ SE CREA
--   1) alm_orden_compra              -> cabecera (pedido al proveedor). Multitenant.
--   2) alm_orden_compra_detalle      -> renglones. FK compuesta tenant-safe a la
--                                       cabecera y a alm_articulo.
--   3) alm_orden_compra_correlativo  -> numeración por empresa (contador).
--   4) alm_compra.orden_compra_detalle_id -> enlace recepción -> renglón de O/C.
--
-- DECISIONES (usuario, 2026-07-30):
--   - Aprobación por PERMISO de rol (no jerárquica esJefe).
--   - Descuento en PORCENTAJE.
--   - Bodega destino NO se fija en la O/C (sólo al recibir).
--   - Centro de costo por renglón, SÓLO informativo (no valida presupuesto).
--   - Estados: 1 Borrador · 2 Aprobada · 3 Recibida parcial · 4 Cerrada · 9 Anulada.
--
-- Cambio ADITIVO y reversible: tres tablas nuevas (vacías) y una columna NULL en
-- alm_compra. No altera datos existentes.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Cabecera de la orden de compra
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alm_orden_compra (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    numero              INTEGER        NOT NULL,             -- correlativo por empresa
    fecha               DATE           NULL,
    fecha_emision       DATE           NULL,
    cod_proveedor       VARCHAR(20)    NOT NULL,             -- prv_proveedores (sin FK: keyless/multiempresa, se valida en servicio)
    terminos_pago       VARCHAR(100)   NULL,
    destino_uso         VARCHAR(250)   NULL,                 -- ≡ PUNTO_DE_VENTA de Centura
    calcula_isv         BOOLEAN        NOT NULL DEFAULT true,
    descuento           NUMERIC(12,4)  NOT NULL DEFAULT 0,   -- PORCENTAJE
    otros_gastos        NUMERIC(14,2)  NOT NULL DEFAULT 0,
    sub_total           NUMERIC(14,2)  NOT NULL DEFAULT 0,
    impuesto            NUMERIC(14,2)  NOT NULL DEFAULT 0,
    total               NUMERIC(14,2)  NOT NULL DEFAULT 0,
    estado              SMALLINT       NOT NULL DEFAULT 1,   -- 1 Borrador (ver CHECK)
    observaciones       VARCHAR(1000)  NULL,
    requisicion_id      INTEGER        NULL,                 -- origen si nació de una requisición (alm_requisicion)
    aprobado_por        VARCHAR(100)   NULL,
    fecha_aprobacion    TIMESTAMP      NULL,
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    -- Número irrepetible por empresa.
    CONSTRAINT uq_alm_orden_compra_numero UNIQUE (company_id, numero),
    -- AK para las FK compuestas tenant-safe (detalle -> cabecera).
    CONSTRAINT uq_alm_orden_compra_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_orden_compra_estado CHECK (estado IN (1, 2, 3, 4, 9))
);
CREATE INDEX IF NOT EXISTS ix_alm_orden_compra_company   ON alm_orden_compra(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_orden_compra_proveedor ON alm_orden_compra(company_id, cod_proveedor);
CREATE INDEX IF NOT EXISTS ix_alm_orden_compra_estado    ON alm_orden_compra(company_id, estado);

COMMENT ON TABLE  alm_orden_compra IS 'Órdenes de compra a proveedores (cabecera). Se aprueba y luego se recibe (alm_compra). Migrado del flujo Centura frmOCMercaderias.';
COMMENT ON COLUMN alm_orden_compra.numero IS 'Correlativo por empresa (alm_orden_compra_correlativo). Único por company_id.';
COMMENT ON COLUMN alm_orden_compra.cod_proveedor IS 'Código del proveedor (prv_proveedores.cod_proveedor). Sin FK: keyless + multiempresa; se valida en el servicio.';
COMMENT ON COLUMN alm_orden_compra.descuento IS 'Descuento global en PORCENTAJE (decisión usuario 2026-07-30).';
COMMENT ON COLUMN alm_orden_compra.estado IS '1 Borrador · 2 Aprobada · 3 Recibida parcial · 4 Cerrada · 9 Anulada.';
COMMENT ON COLUMN alm_orden_compra.aprobado_por IS 'Usuario que aprobó (aprobación por permiso de rol). NULL mientras está en borrador.';

-- -----------------------------------------------------------------------------
-- 2) Renglones de la orden de compra
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alm_orden_compra_detalle (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    orden_compra_id     INTEGER        NOT NULL,
    -- La FK al artículo es COMPUESTA tenant-safe y se declara abajo, fuera del CREATE TABLE,
    -- para poder repararla también en bases donde este script ya corrió con la FK simple.
    articulo_id         INTEGER        NOT NULL,
    codigo_upc          VARCHAR(40)    NULL,                 -- snapshot del código de proveedor (alm_articulo_proveedor.codigo_upc)
    centro_costo        VARCHAR(40)    NULL,                 -- referencia informativa (no valida presupuesto)
    descripcion         VARCHAR(250)   NULL,
    cantidad_pedida     NUMERIC(14,4)  NOT NULL DEFAULT 0,
    costo_unitario      NUMERIC(14,4)  NOT NULL DEFAULT 0,
    impuesto            NUMERIC(14,2)  NOT NULL DEFAULT 0,
    total               NUMERIC(14,2)  NOT NULL DEFAULT 0,
    cantidad_aplicada   NUMERIC(14,4)  NOT NULL DEFAULT 0,   -- lo ya recibido; la recepción lo incrementa
    -- Tenant-safe: el renglón vive SIEMPRE en la empresa de su cabecera.
    CONSTRAINT fk_alm_orden_compra_detalle_oc
        FOREIGN KEY (company_id, orden_compra_id)
        REFERENCES alm_orden_compra (company_id, id)
        ON DELETE CASCADE,
    -- AK para la FK compuesta tenant-safe desde alm_compra (recepción).
    CONSTRAINT uq_alm_orden_compra_detalle_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_orden_compra_detalle_cant CHECK (cantidad_pedida >= 0 AND cantidad_aplicada >= 0)
);
CREATE INDEX IF NOT EXISTS ix_alm_orden_compra_detalle_company  ON alm_orden_compra_detalle(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_orden_compra_detalle_oc       ON alm_orden_compra_detalle(company_id, orden_compra_id);
CREATE INDEX IF NOT EXISTS ix_alm_orden_compra_detalle_articulo ON alm_orden_compra_detalle(articulo_id);

-- FK COMPUESTA tenant-safe al artículo (company_id, articulo_id) -> alm_articulo (company_id, id).
-- Es la convención del módulo: 5 de las FK que apuntan a alm_articulo ya son compuestas
-- (alm_articulo_bodega, alm_compra, alm_descargo, alm_requisicion, alm_ajuste_inventario). Sin
-- ella, la BD aceptaría un renglón cuyo artículo pertenece a OTRA empresa: el filtro global de
-- EF protege las lecturas, no la integridad referencial.
-- Bloque idempotente y REPARADOR: si este script ya corrió con la FK simple, la sustituye.
DO $$
BEGIN
    -- Prerrequisito: la clave alterna por tenant de alm_articulo
    -- (Database/2026-07-14_alm_fk_compuestas_tenant.sql). Sin ella la FK compuesta no se puede crear.
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = 'alm_articulo'::regclass
           AND contype IN ('u','p')
           AND conkey = ARRAY[
                 (SELECT attnum FROM pg_attribute WHERE attrelid='alm_articulo'::regclass AND attname='company_id'),
                 (SELECT attnum FROM pg_attribute WHERE attrelid='alm_articulo'::regclass AND attname='id')
               ]::smallint[]
    ) THEN
        RAISE EXCEPTION 'Falta la clave alterna (company_id, id) en alm_articulo. Aplique antes Database/2026-07-14_alm_fk_compuestas_tenant.sql';
    END IF;

    -- Quita la FK simple si una corrida anterior de este script la dejó.
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'alm_orden_compra_detalle_articulo_id_fkey') THEN
        ALTER TABLE alm_orden_compra_detalle DROP CONSTRAINT alm_orden_compra_detalle_articulo_id_fkey;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_orden_compra_detalle_articulo') THEN
        ALTER TABLE alm_orden_compra_detalle
            ADD CONSTRAINT fk_alm_orden_compra_detalle_articulo
                FOREIGN KEY (company_id, articulo_id)
                REFERENCES alm_articulo (company_id, id)
                ON DELETE RESTRICT;
    END IF;
END $$;

COMMENT ON TABLE  alm_orden_compra_detalle IS 'Renglones de la orden de compra. Una fila por artículo pedido. cantidad_aplicada la mueve la recepción.';
COMMENT ON COLUMN alm_orden_compra_detalle.codigo_upc IS 'Código del proveedor para el artículo (snapshot de alm_articulo_proveedor.codigo_upc).';
COMMENT ON COLUMN alm_orden_compra_detalle.centro_costo IS 'Centro de costo (informativo). No valida presupuesto (decisión usuario 2026-07-30).';
COMMENT ON COLUMN alm_orden_compra_detalle.cantidad_aplicada IS 'Cantidad ya recibida contra este renglón. La incrementa la recepción; al cubrir todos los renglones, la O/C pasa a Cerrada.';

-- -----------------------------------------------------------------------------
-- 3) Numeración por empresa (contador; el servicio hace UPDATE ... RETURNING)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alm_orden_compra_correlativo (
    company_id     BIGINT   PRIMARY KEY,
    ultimo_numero  INTEGER  NOT NULL DEFAULT 0
);
COMMENT ON TABLE alm_orden_compra_correlativo IS 'Último número de O/C emitido por empresa. El servicio incrementa con UPDATE ... RETURNING (atómico, sin carreras).';

-- -----------------------------------------------------------------------------
-- 4) Enlace recepción -> renglón de O/C (aditivo sobre la tabla existente)
-- -----------------------------------------------------------------------------
ALTER TABLE alm_compra
    ADD COLUMN IF NOT EXISTS orden_compra_detalle_id INTEGER NULL;

-- FK compuesta tenant-safe. Con columna NULL (compra directa / histórico SIMAFI),
-- MATCH SIMPLE no valida la referencia. DO block para idempotencia (PG no soporta
-- ADD CONSTRAINT IF NOT EXISTS).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_compra_oc_detalle') THEN
        ALTER TABLE alm_compra
            ADD CONSTRAINT fk_alm_compra_oc_detalle
                FOREIGN KEY (company_id, orden_compra_detalle_id)
                REFERENCES alm_orden_compra_detalle (company_id, id)
                ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_alm_compra_oc_detalle ON alm_compra(company_id, orden_compra_detalle_id);

COMMENT ON COLUMN alm_compra.orden_compra_detalle_id IS 'Renglón de O/C que originó esta línea de compra (modo Con orden de compra). NULL = compra directa o histórico SIMAFI.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Tablas creadas:
-- SELECT table_name FROM information_schema.tables
--  WHERE table_name IN ('alm_orden_compra','alm_orden_compra_detalle','alm_orden_compra_correlativo')
--  ORDER BY table_name;
-- 2) Constraints de la cabecera:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='alm_orden_compra'::regclass ORDER BY contype, conname;
--   -> ck_alm_orden_compra_estado(c), alm_orden_compra_pkey(p),
--      uq_alm_orden_compra_numero(u), uq_alm_orden_compra_tenant(u)
-- 3) FK tenant-safe del detalle:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='alm_orden_compra_detalle'::regclass ORDER BY contype, conname;
--   -> ck_alm_orden_compra_detalle_cant(c), fk_alm_orden_compra_detalle_articulo(f),
--      fk_alm_orden_compra_detalle_oc(f), alm_orden_compra_detalle_pkey(p),
--      uq_alm_orden_compra_detalle_tenant(u)
-- 3b) Las DOS FK del detalle deben ser COMPUESTAS (company_id, ...):
-- SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
--  WHERE conrelid='alm_orden_compra_detalle'::regclass AND contype='f';
-- 4) Columna y FK nuevas en alm_compra:
-- SELECT column_name FROM information_schema.columns
--  WHERE table_name='alm_compra' AND column_name='orden_compra_detalle_id';
-- SELECT conname FROM pg_constraint WHERE conname='fk_alm_compra_oc_detalle';
-- 5) El CHECK de estado debe FALLAR:
-- INSERT INTO alm_orden_compra (company_id, numero, cod_proveedor, estado) VALUES (2, 1, '0451', 7);
--   -> ERROR ck_alm_orden_compra_estado
-- =============================================================================
