-- =============================================================================
-- Recepción de compra (factura de proveedor) — alm_compra_hdr / correlativo
-- Fecha: 2026-07-31
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ
-- El portal no tiene captura de compras: /almacen/compras es sólo consulta sobre
-- alm_compra, una tabla PLANA (una fila = una línea) que hoy únicamente guarda el
-- histórico migrado de SIMAFI. La captura vive en Centura (frmFacturacionPRV). Con
-- las órdenes de compra ya creadas (2026-07-30_alm_orden_compra.sql) falta la pieza
-- que las consume: la recepción, que descarga la O/C y postea al kardex.
-- Diseño: docs/centura-flujos/README_compras_recepcion_proveedor.md (mockup aprobado).
--
-- QUÉ SE CREA
--   1) alm_compra_hdr          -> cabecera de la recepción (los N renglones de una
--                                 misma factura comparten una fila aquí). Multitenant.
--   2) alm_compra_correlativo  -> numeración interna por empresa (contador).
--   3) alm_compra.compra_hdr_id -> enlace línea -> cabecera. Única columna que se
--                                 toca en alm_compra.
--
-- DECISIONES CERRADAS (usuario, 2026-07-31) — cierran D-3/D-4/D-5 del README:
--   - D-3 Agrupador de documento: CABECERA REAL (alm_compra_hdr), no un campo
--     agrupador dentro de la tabla plana. Motivo: los datos de factura (proveedor,
--     SAR, CAI, bodega, moneda, descuento global, otros gastos, flete) son del
--     DOCUMENTO, no del renglón; meterlos en alm_compra los repetiría en cada línea
--     y dejaría el descuento/flete global sin un lugar único donde prorratearse.
--     alm_compra sigue siendo la unidad de POSTEO (un uuid por línea): el motor de
--     inventario NO cambia y el histórico SIMAFI queda intacto (compra_hdr_id NULL).
--   - D-4 Numeración: contador por company_id con UPDATE ... RETURNING, idéntico a
--     alm_orden_compra_correlativo (no el patrón CNF_CONFIGURACION de Centura).
--   - D-5 Factura SAR: columna nueva numero_factura_sar VARCHAR(30). alm_compra.
--     numero_factura es NUMERIC(11,0) y no admite el formato con guiones; se deja
--     como está, sirviendo al histórico SIMAFI.
--
-- FUERA DE ALCANCE (no lo toca este script): contabilidad del ISV (D-1, contador),
-- cuentas por pagar / desglose de pago al proveedor (D-7), multimoneda operativa
-- (D-8: las columnas moneda/tasa_cambio quedan creadas, la 1ª entrega usa HNL).
--
-- Cambio ADITIVO y reversible: dos tablas nuevas (vacías) y una columna NULL en
-- alm_compra. No altera ni borra datos existentes, no cambia tipos, no agrega
-- NOT NULL a nada existente, no toca trg_alm_compra_blindaje.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 0) Prerrequisitos (fallar temprano y con mensaje claro, no con un error de FK)
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF to_regclass('public.alm_orden_compra') IS NULL THEN
        RAISE EXCEPTION 'Falta alm_orden_compra. Aplique antes Database/2026-07-30_alm_orden_compra.sql (paso 23 del runbook).';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_orden_compra_tenant') THEN
        RAISE EXCEPTION 'Falta la clave alterna uq_alm_orden_compra_tenant en alm_orden_compra. Aplique antes Database/2026-07-30_alm_orden_compra.sql.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_bodega_company_id') THEN
        RAISE EXCEPTION 'Falta la clave alterna uq_alm_bodega_company_id en alm_bodega. Aplique antes Database/2026-07-14_alm_fk_compuestas_tenant.sql.';
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 1) Cabecera de la recepción (factura de proveedor)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alm_compra_hdr (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    numero              INTEGER        NOT NULL,             -- correlativo interno por empresa (D-4)
    fecha               DATE           NOT NULL,             -- fecha de la transacción (≡ FECHA_TRANSACCION)
    fecha_factura       DATE           NULL,                 -- fecha de la factura del proveedor
    fecha_vencimiento   DATE           NULL,
    cod_proveedor       VARCHAR(20)    NOT NULL,             -- prv_proveedores (sin FK: keyless/multiempresa, se valida en el servicio)
    proveedor           VARCHAR(100)   NULL,                 -- snapshot del nombre al momento de recibir
    numero_factura_sar  VARCHAR(30)    NULL,                 -- D-5: admite el formato con guiones (00100101-00000001)
    cai                 VARCHAR(50)    NULL,
    orden_compra_id     INTEGER        NULL,                 -- modo Con O/C; NULL = compra directa
    bodega_id           INTEGER        NOT NULL,             -- bodega que RECIBE la mercadería
    terminos_pago       VARCHAR(100)   NULL,
    tipo_compra         SMALLINT       NOT NULL DEFAULT 0,   -- espejo de alm_compra.tipo_compra (contado/crédito)
    plazo_dias          INTEGER        NULL,
    moneda              VARCHAR(3)     NOT NULL DEFAULT 'HNL',
    tasa_cambio         NUMERIC(14,6)  NOT NULL DEFAULT 1,   -- 1 = Lempiras
    consumo_interno     BOOLEAN        NOT NULL DEFAULT false,
    detallar_isv        BOOLEAN        NULL,                 -- override por factura; NULL = regla del tipo de artículo
    descuento           NUMERIC(12,4)  NOT NULL DEFAULT 0,   -- PORCENTAJE global (igual que la O/C)
    otros_gastos        NUMERIC(14,2)  NOT NULL DEFAULT 0,
    flete_seguro        NUMERIC(14,2)  NOT NULL DEFAULT 0,
    sub_total           NUMERIC(14,2)  NOT NULL DEFAULT 0,
    impuesto            NUMERIC(14,2)  NOT NULL DEFAULT 0,
    total               NUMERIC(14,2)  NOT NULL DEFAULT 0,
    observaciones       VARCHAR(1000)  NULL,
    estado              SMALLINT       NOT NULL DEFAULT 1,   -- 1 Registrada · 9 Anulada (ver CHECK)
    posteado            BOOLEAN        NOT NULL DEFAULT false,
    fecha_posteo        TIMESTAMP      NULL,
    uuid                UUID           NULL,                 -- identidad del DOCUMENTO (idempotencia del alta)
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    -- Número irrepetible por empresa.
    CONSTRAINT uq_alm_compra_hdr_numero UNIQUE (company_id, numero),
    -- AK para la FK compuesta tenant-safe desde alm_compra (los renglones).
    CONSTRAINT uq_alm_compra_hdr_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_compra_hdr_estado CHECK (estado IN (1, 9)),
    CONSTRAINT ck_alm_compra_hdr_tasa   CHECK (tasa_cambio > 0),
    CONSTRAINT ck_alm_compra_hdr_moneda CHECK (moneda IN ('HNL', 'USD')),
    -- Evidencia del posteo: si está posteada, tiene identidad y sello de tiempo.
    CONSTRAINT ck_alm_compra_hdr_posteo
        CHECK (posteado = false OR (uuid IS NOT NULL AND fecha_posteo IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_alm_compra_hdr_company   ON alm_compra_hdr(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_compra_hdr_proveedor ON alm_compra_hdr(company_id, cod_proveedor);
CREATE INDEX IF NOT EXISTS ix_alm_compra_hdr_fecha     ON alm_compra_hdr(company_id, fecha);
CREATE INDEX IF NOT EXISTS ix_alm_compra_hdr_oc        ON alm_compra_hdr(company_id, orden_compra_id);
CREATE INDEX IF NOT EXISTS ix_alm_compra_hdr_bodega    ON alm_compra_hdr(bodega_id);

-- Idempotencia del alta: el mismo documento no puede existir dos veces en la empresa
-- (reintento de red, doble clic). Parcial porque el uuid lo pone el servicio al crear.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_compra_hdr_company_uuid
    ON alm_compra_hdr(company_id, uuid) WHERE uuid IS NOT NULL;

-- La MISMA factura del MISMO proveedor no se captura dos veces. Se libera al anular
-- (estado 9), para poder recapturar una factura mal ingresada.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_compra_hdr_factura_prov
    ON alm_compra_hdr(company_id, cod_proveedor, numero_factura_sar)
    WHERE numero_factura_sar IS NOT NULL AND estado <> 9;

-- Lo pendiente de postear es lo único que el motor busca.
CREATE INDEX IF NOT EXISTS ix_alm_compra_hdr_pendiente
    ON alm_compra_hdr(company_id) WHERE posteado = false AND estado = 1;

-- FK compuestas tenant-safe. DO block por idempotencia (PG no soporta ADD CONSTRAINT IF NOT EXISTS).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_compra_hdr_bodega') THEN
        ALTER TABLE alm_compra_hdr
            ADD CONSTRAINT fk_alm_compra_hdr_bodega
                FOREIGN KEY (company_id, bodega_id)
                REFERENCES alm_bodega (company_id, id)
                ON DELETE RESTRICT;
    END IF;

    -- Con orden_compra_id NULL (compra directa) MATCH SIMPLE no valida la referencia.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_compra_hdr_oc') THEN
        ALTER TABLE alm_compra_hdr
            ADD CONSTRAINT fk_alm_compra_hdr_oc
                FOREIGN KEY (company_id, orden_compra_id)
                REFERENCES alm_orden_compra (company_id, id)
                ON DELETE RESTRICT;
    END IF;
END $$;

COMMENT ON TABLE  alm_compra_hdr IS 'Cabecera de la recepción de compra (factura de proveedor). Agrupa los N renglones de alm_compra que comparten factura. Migrado del flujo Centura frmFacturacionPRV / PRV_FACTURAS_HDR. Toda cabecera es de origen SIAD: el histórico SIMAFI vive en alm_compra con compra_hdr_id NULL.';
COMMENT ON COLUMN alm_compra_hdr.numero IS 'Correlativo interno por empresa (alm_compra_correlativo). Único por company_id. Equivale a PRV_FACTURAS_HDR.NUM_FACTURA_PROV (Centura lo sacaba de CNF_CONFIGURACION 47).';
COMMENT ON COLUMN alm_compra_hdr.cod_proveedor IS 'Código del proveedor (prv_proveedores.cod_proveedor). Sin FK: keyless + multiempresa; se valida en el servicio.';
COMMENT ON COLUMN alm_compra_hdr.numero_factura_sar IS 'Número de factura del proveedor con formato SAR (admite guiones). alm_compra.numero_factura es NUMERIC y sólo sirve al histórico SIMAFI.';
COMMENT ON COLUMN alm_compra_hdr.orden_compra_id IS 'O/C que se está recibiendo (modo Con orden de compra). NULL = compra directa (decisión D-A del usuario: Centura siempre exigía O/C).';
COMMENT ON COLUMN alm_compra_hdr.bodega_id IS 'Bodega que RECIBE la mercadería. Obligatoria: toda cabecera es un documento nuevo (SIAD).';
COMMENT ON COLUMN alm_compra_hdr.detallar_isv IS 'Override manual del ISV por factura (≡ casilla cb3 de Centura). NULL = resolver por alm_tipo_articulo.impuesto_tasa_id. true = ISV separado del costo; false = capitalizado al costo.';
COMMENT ON COLUMN alm_compra_hdr.descuento IS 'Descuento global en PORCENTAJE (misma convención que alm_orden_compra.descuento).';
COMMENT ON COLUMN alm_compra_hdr.moneda IS 'HNL o USD. La 1ª entrega opera sólo en HNL (D-8 sin resolver); la columna queda creada para no volver a alterar la tabla.';
COMMENT ON COLUMN alm_compra_hdr.estado IS '1 Registrada · 9 Anulada.';
COMMENT ON COLUMN alm_compra_hdr.posteado IS 'CACHÉ del estado: true = todos los renglones de esta factura ya generaron su asiento en alm_kardex. La garantía de no duplicar NO es esta bandera, sino los índices únicos sobre uuid (aquí y en alm_compra).';
COMMENT ON COLUMN alm_compra_hdr.uuid IS 'IDENTIDAD del documento, generada al CREAR la recepción (no al postear). Hace idempotente el reintento del cliente. El uuid de cada línea en alm_compra es independiente y es el que deriva el asiento del kardex.';

-- -----------------------------------------------------------------------------
-- 2) Numeración por empresa (contador; el servicio hace UPDATE ... RETURNING)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alm_compra_correlativo (
    company_id     BIGINT   PRIMARY KEY,
    ultimo_numero  INTEGER  NOT NULL DEFAULT 0
);
COMMENT ON TABLE alm_compra_correlativo IS 'Último número de recepción de compra emitido por empresa. El servicio incrementa con UPDATE ... RETURNING (atómico, sin carreras). Mismo patrón que alm_orden_compra_correlativo (decisión D-4).';

-- -----------------------------------------------------------------------------
-- 3) Enlace línea -> cabecera (aditivo sobre la tabla existente)
-- -----------------------------------------------------------------------------
-- NULL en el histórico SIMAFI y en cualquier línea previa: la tabla plana sigue
-- siendo válida por sí sola. No se agrega CHECK que lo obligue en documentos SIAD
-- porque la carga inicial y los ajustes también escriben líneas sin cabecera.
ALTER TABLE alm_compra
    ADD COLUMN IF NOT EXISTS compra_hdr_id INTEGER NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_compra_hdr') THEN
        ALTER TABLE alm_compra
            ADD CONSTRAINT fk_alm_compra_hdr
                FOREIGN KEY (company_id, compra_hdr_id)
                REFERENCES alm_compra_hdr (company_id, id)
                ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_alm_compra_hdr_id ON alm_compra(company_id, compra_hdr_id);

COMMENT ON COLUMN alm_compra.compra_hdr_id IS 'Cabecera (factura) a la que pertenece esta línea. NULL = histórico SIMAFI o línea sin documento de recepción. La línea sigue siendo la unidad de posteo al kardex.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Tablas creadas:
-- SELECT table_name FROM information_schema.tables
--  WHERE table_name IN ('alm_compra_hdr','alm_compra_correlativo') ORDER BY table_name;
--
-- 2) Constraints de la cabecera:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='alm_compra_hdr'::regclass ORDER BY contype, conname;
--   -> ck_alm_compra_hdr_estado(c), ck_alm_compra_hdr_moneda(c), ck_alm_compra_hdr_posteo(c),
--      ck_alm_compra_hdr_tasa(c), fk_alm_compra_hdr_bodega(f), fk_alm_compra_hdr_oc(f),
--      alm_compra_hdr_pkey(p), uq_alm_compra_hdr_numero(u), uq_alm_compra_hdr_tenant(u)
--
-- 3) Las DOS FK de la cabecera deben ser COMPUESTAS (company_id, ...):
-- SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
--  WHERE conrelid='alm_compra_hdr'::regclass AND contype='f';
--
-- 4) Columna, FK e índice nuevos en alm_compra:
-- SELECT column_name, is_nullable FROM information_schema.columns
--  WHERE table_name='alm_compra' AND column_name='compra_hdr_id';   -> YES (nullable)
-- SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='fk_alm_compra_hdr';
--
-- 5) El histórico SIMAFI NO se tocó (todas sus líneas quedan con compra_hdr_id NULL):
-- SELECT origen, count(*) AS lineas, count(compra_hdr_id) AS con_cabecera
--   FROM alm_compra GROUP BY origen ORDER BY origen;   -> con_cabecera = 0 en SIMAFI
--
-- 6) El CHECK de estado debe FALLAR:
-- INSERT INTO alm_compra_hdr (company_id, numero, fecha, cod_proveedor, bodega_id, estado)
-- VALUES (2, 1, current_date, '0451', 1, 5);
--   -> ERROR ck_alm_compra_hdr_estado
--
-- 7) La factura duplicada del mismo proveedor debe FALLAR (índice único parcial):
--    (dos INSERT con el mismo cod_proveedor + numero_factura_sar y estado 1)
--   -> ERROR uq_alm_compra_hdr_factura_prov
-- =============================================================================
