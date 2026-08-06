-- =============================================================================
-- Traslado entre bodegas: tránsito, recepción parcial y modo directo — Fase 5.2
-- Fecha: 2026-08-04
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) antes que en el SRV.
--
-- POR QUÉ
--   Fase 5 de docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md.
--   El traslado reutiliza alm_movimiento_hdr/_dtl (clase NUEVA 'TRASLADO') y añade:
--     · el tránsito de dos pasos (envío → recepción), con existencia_transito como puente;
--     · la recepción PARCIAL por renglón (tablas nuevas alm_traslado_recepcion / _dtl);
--     · el interruptor requiere_recepcion (dos pasos) vs directo (un paso), por documento.
--   El costo viaja con la mercadería; los dos lados se asientan como TRASLADO en el kardex.
--
-- ADITIVO + WIDENING de dos CHECK. Sin DROP de columnas, sin DELETE/TRUNCATE de datos.
-- IDEMPOTENTE  (ADD COLUMN IF NOT EXISTS / CREATE TABLE IF NOT EXISTS / DO blocks / ON CONFLICT;
--               los dos CHECK ampliados se recrean con DROP IF EXISTS + ADD, seguro al re-correr).
-- REVERSIBLE   (bloque de ROLLBACK comentado al final).
-- MULTIEMPRESA (company_id + FK compuestas tenant-safe en las dos tablas nuevas).
--
-- PRERREQUISITOS (el script los verifica y aborta con mensaje claro):
--   · alm_movimiento_hdr / _dtl        — paso 28 (2026-08-03_alm_movimiento.sql)
--   · alm_tipo_movimiento              — paso 27 (2026-08-01_alm_tipo_movimiento.sql)
--   · alm_articulo / alm_bodega con clave alterna (company_id, id) — 2026-07-14_alm_fk_compuestas_tenant.sql
--   · alm_articulo_bodega.existencia_transito — 2026-07-13_alm_articulo_bodega_comprometido_transito_costos.sql
--   · alm_kardex.bodega_destino_id     — ya existe en el scaffold
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 0) Guardas de prerrequisitos
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF to_regclass('public.alm_movimiento_hdr') IS NULL OR to_regclass('public.alm_movimiento_dtl') IS NULL THEN
        RAISE EXCEPTION 'Faltan alm_movimiento_hdr/_dtl. Aplicar antes Database/2026-08-03_alm_movimiento.sql (paso 28).';
    END IF;
    IF to_regclass('public.alm_tipo_movimiento') IS NULL THEN
        RAISE EXCEPTION 'Falta alm_tipo_movimiento. Aplicar antes Database/2026-08-01_alm_tipo_movimiento.sql (paso 27).';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='alm_articulo_bodega'
                      AND column_name='existencia_transito') THEN
        RAISE EXCEPTION 'Falta alm_articulo_bodega.existencia_transito. Aplicar Database/2026-07-13_alm_articulo_bodega_comprometido_transito_costos.sql.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='alm_kardex'
                      AND column_name='bodega_destino_id') THEN
        RAISE EXCEPTION 'Falta alm_kardex.bodega_destino_id.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE contype='u' AND conrelid='public.alm_bodega'::regclass
                      AND pg_get_constraintdef(oid) ILIKE '%(company_id, id)%') THEN
        RAISE EXCEPTION 'alm_bodega no tiene clave alterna (company_id, id). Aplicar Database/2026-07-14_alm_fk_compuestas_tenant.sql.';
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 1) alm_movimiento_hdr: columnas del traslado
--    bodega_destino_id NULL en entradas/salidas normales; obligatorio (por el servicio)
--    en clase TRASLADO. requiere_recepcion es el interruptor del modo.
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_movimiento_hdr
    ADD COLUMN IF NOT EXISTS bodega_destino_id  INTEGER      NULL,
    ADD COLUMN IF NOT EXISTS requiere_recepcion BOOLEAN      NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS recibido_por       VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS fecha_recepcion    TIMESTAMP WITHOUT TIME ZONE NULL;

COMMENT ON COLUMN public.alm_movimiento_hdr.bodega_destino_id IS
 'Destino del traslado (clase TRASLADO). NULL en entradas/salidas normales. La bodega afectada por cada asiento es bodega_id / la de la recepción; ésta es la contraparte.';
COMMENT ON COLUMN public.alm_movimiento_hdr.requiere_recepcion IS
 'Interruptor del modo del traslado: true = dos pasos (nace En tránsito, se recibe aparte) · false = directo (un paso, nace Recibido con recepción automática). Irrelevante fuera de clase TRASLADO.';
COMMENT ON COLUMN public.alm_movimiento_hdr.recibido_por IS
 'Usuario de la ÚLTIMA recepción (se sella cuando el traslado queda totalmente Recibido).';

-- FK compuesta tenant-safe a alm_bodega. MATCH SIMPLE (el default): si bodega_destino_id es
-- NULL, la FK no se comprueba, así que los documentos que no son traslado no la violan.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conname='fk_alm_movimiento_hdr_bodega_destino'
                      AND conrelid='public.alm_movimiento_hdr'::regclass) THEN
        ALTER TABLE public.alm_movimiento_hdr
            ADD CONSTRAINT fk_alm_movimiento_hdr_bodega_destino
            FOREIGN KEY (company_id, bodega_destino_id)
            REFERENCES public.alm_bodega (company_id, id) ON DELETE RESTRICT;
    END IF;
END $$;

-- El destino no puede ser el mismo que el origen (la obligatoriedad «si es TRASLADO, destino
-- no es NULL» la garantiza el servicio: la clase vive en otra tabla y un CHECK no la ve).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conname='ck_alm_movimiento_hdr_traslado'
                      AND conrelid='public.alm_movimiento_hdr'::regclass) THEN
        ALTER TABLE public.alm_movimiento_hdr
            ADD CONSTRAINT ck_alm_movimiento_hdr_traslado
            CHECK (bodega_destino_id IS NULL OR bodega_destino_id <> bodega_id);
    END IF;
END $$;

-- WIDENING del CHECK de estado: agrega 2 (En tránsito) y 3 (Recibido).
ALTER TABLE public.alm_movimiento_hdr DROP CONSTRAINT IF EXISTS ck_alm_movimiento_hdr_estado;
ALTER TABLE public.alm_movimiento_hdr
    ADD CONSTRAINT ck_alm_movimiento_hdr_estado CHECK (estado IN (1, 2, 3, 9));
COMMENT ON COLUMN public.alm_movimiento_hdr.estado IS
 '1 Registrado (entrada/salida normal) · 2 En tránsito (traslado con recepción, algo por recibir) · 3 Recibido (traslado completo; un directo nace aquí) · 9 Anulado.';

-- -----------------------------------------------------------------------------
-- 2) alm_movimiento_dtl: cantidad recibida acumulada del renglón (traslado)
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_movimiento_dtl
    ADD COLUMN IF NOT EXISTS cantidad_recibida NUMERIC(15,2) NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conname='ck_alm_movimiento_dtl_recibida'
                      AND conrelid='public.alm_movimiento_dtl'::regclass) THEN
        ALTER TABLE public.alm_movimiento_dtl
            ADD CONSTRAINT ck_alm_movimiento_dtl_recibida
            CHECK (cantidad_recibida >= 0 AND cantidad_recibida <= cantidad);
    END IF;
END $$;

COMMENT ON COLUMN public.alm_movimiento_dtl.cantidad_recibida IS
 'Traslado: suma de lo ya recibido en destino de este renglón. En tránsito del renglón = cantidad - cantidad_recibida. 0 en entradas/salidas normales.';

-- -----------------------------------------------------------------------------
-- 3) alm_articulo_bodega: backstop de tránsito no-negativo
--    existencia_transito la mueve el servicio con UPDATE crudo (sin uuid): este CHECK
--    convierte una descarga de más por una carrera no cubierta en un abort, no un negativo.
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conname='ck_alm_articulo_bodega_transito_no_neg'
                      AND conrelid='public.alm_articulo_bodega'::regclass) THEN
        ALTER TABLE public.alm_articulo_bodega
            ADD CONSTRAINT ck_alm_articulo_bodega_transito_no_neg CHECK (existencia_transito >= 0);
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 4) alm_tipo_movimiento: WIDENING del CHECK de clase para admitir TRASLADO
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_tipo_movimiento DROP CONSTRAINT IF EXISTS ck_alm_tipo_movimiento_clase;
ALTER TABLE public.alm_tipo_movimiento
    ADD CONSTRAINT ck_alm_tipo_movimiento_clase CHECK (clase IN ('ENTRADA','SALIDA','VALOR','TRASLADO'));

-- -----------------------------------------------------------------------------
-- 5) alm_traslado_recepcion — cabecera de un acto de recepción
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_traslado_recepcion (
    id                SERIAL       PRIMARY KEY,
    company_id        BIGINT       NOT NULL,
    movimiento_hdr_id INTEGER      NOT NULL,          -- el traslado que se recibe
    fecha             DATE         NOT NULL,
    observaciones     VARCHAR(500) NULL,
    uuid              UUID         NOT NULL,           -- idempotencia del acto de recepción
    usuariocreacion   VARCHAR(100) NULL,
    fechacreacion     TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),

    CONSTRAINT uq_alm_traslado_recepcion_tenant UNIQUE (company_id, id),
    CONSTRAINT uq_alm_traslado_recepcion_uuid   UNIQUE (company_id, uuid),
    CONSTRAINT fk_alm_traslado_recepcion_hdr
        FOREIGN KEY (company_id, movimiento_hdr_id)
        REFERENCES public.alm_movimiento_hdr (company_id, id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_alm_traslado_recepcion_hdr
    ON public.alm_traslado_recepcion (company_id, movimiento_hdr_id);

COMMENT ON TABLE public.alm_traslado_recepcion IS
 'Cabecera de un acto de recepción de un traslado. Un traslado con recepción se recibe en una o varias tandas (parcial); cada tanda es una fila con su fecha, usuario y uuid de idempotencia. Un traslado directo genera una recepción automática.';

-- -----------------------------------------------------------------------------
-- 6) alm_traslado_recepcion_dtl — renglón recibido: la UNIDAD DE POSTEO de la entrada
--    La entrada a destino se ancla a ESTA línea (no a la del traslado): un renglón de
--    traslado se recibe en varias recepciones, igual que una requisición en varios descargos.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_traslado_recepcion_dtl (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        BIGINT        NOT NULL,
    recepcion_id      INTEGER       NOT NULL,          -- alm_traslado_recepcion
    movimiento_dtl_id INTEGER       NOT NULL,          -- renglón del traslado que se recibe
    articulo_id       INTEGER       NOT NULL,
    cantidad          NUMERIC(15,2) NOT NULL,          -- lo recibido en ESTE acto
    costo_real        NUMERIC(12,4) NOT NULL,          -- copiado del renglón del traslado (viaja con la mercadería)
    total             NUMERIC(14,2) NOT NULL DEFAULT 0,
    kardex_id         INTEGER       NULL,              -- asiento de ENTRADA a destino
    uuid              UUID          NOT NULL,

    CONSTRAINT uq_alm_traslado_recepcion_dtl_uuid   UNIQUE (company_id, uuid),
    CONSTRAINT uq_alm_traslado_recepcion_dtl_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_traslado_recepcion_dtl_cant   CHECK (cantidad > 0),
    CONSTRAINT ck_alm_traslado_recepcion_dtl_costo  CHECK (costo_real >= 0),

    CONSTRAINT fk_alm_traslado_recepcion_dtl_rec
        FOREIGN KEY (company_id, recepcion_id)
        REFERENCES public.alm_traslado_recepcion (company_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_alm_traslado_recepcion_dtl_dtl
        FOREIGN KEY (company_id, movimiento_dtl_id)
        REFERENCES public.alm_movimiento_dtl (company_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_alm_traslado_recepcion_dtl_articulo
        FOREIGN KEY (company_id, articulo_id)
        REFERENCES public.alm_articulo (company_id, id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_alm_traslado_recepcion_dtl_rec
    ON public.alm_traslado_recepcion_dtl (company_id, recepcion_id);
CREATE INDEX IF NOT EXISTS ix_alm_traslado_recepcion_dtl_dtl
    ON public.alm_traslado_recepcion_dtl (company_id, movimiento_dtl_id);
CREATE INDEX IF NOT EXISTS ix_alm_traslado_recepcion_dtl_articulo
    ON public.alm_traslado_recepcion_dtl (company_id, articulo_id);
CREATE INDEX IF NOT EXISTS ix_alm_traslado_recepcion_dtl_kardex
    ON public.alm_traslado_recepcion_dtl (kardex_id) WHERE kardex_id IS NOT NULL;

COMMENT ON TABLE public.alm_traslado_recepcion_dtl IS
 'Renglón recibido de un traslado: la UNIDAD DE POSTEO de la entrada a destino. Su uuid ancla la idempotencia del asiento de entrada; anclarlo a la línea del traslado haría que la segunda recepción parcial se tomara por un reintento de la primera.';

-- -----------------------------------------------------------------------------
-- 7) Semilla: el tipo TRF «Traslado entre bodegas» (clase TRASLADO), por empresa con bodegas.
--    TFE/TFS quedan como estaban (histórico inactivo del legacy). ON CONFLICT: re-ejecutable
--    y respeta lo que el usuario ya haya editado.
-- -----------------------------------------------------------------------------
INSERT INTO public.alm_tipo_movimiento
       (company_id, codigo, nombre, clase, activo, orden, usuariocreacion, fechacreacion)
SELECT DISTINCT company_id, 'TRF', 'TRASLADO ENTRE BODEGAS', 'TRASLADO', true, 0, 'seed-fase5', (now() AT TIME ZONE 'utc')
FROM   public.alm_bodega
ON CONFLICT (company_id, codigo) DO NOTHING;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- V1) Columnas nuevas del hdr (esperado: 4 filas):
-- SELECT column_name FROM information_schema.columns
--  WHERE table_name='alm_movimiento_hdr'
--    AND column_name IN ('bodega_destino_id','requiere_recepcion','recibido_por','fecha_recepcion')
--  ORDER BY 1;
--
-- V2) El CHECK de estado admite ahora 1,2,3,9:
-- SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='ck_alm_movimiento_hdr_estado';
--
-- V3) El CHECK de clase admite TRASLADO:
-- SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='ck_alm_tipo_movimiento_clase';
--
-- V4) Las 2 tablas nuevas y sus FK COMPUESTAS (esperado: 4 FK, todas con company_id):
-- SELECT conrelid::regclass AS tabla, conname, pg_get_constraintdef(oid)
--   FROM pg_constraint
--  WHERE contype='f'
--    AND conrelid IN ('public.alm_traslado_recepcion'::regclass,'public.alm_traslado_recepcion_dtl'::regclass)
--  ORDER BY 1,2;
--
-- V5) La semilla TRF quedó por empresa con bodegas (esperado: 1 por company_id, clase TRASLADO, activo):
-- SELECT company_id, codigo, clase, activo FROM alm_tipo_movimiento WHERE codigo='TRF' ORDER BY company_id;
--
-- V6) PRUEBA NEGATIVA — destino = origen debe FALLAR (ck_alm_movimiento_hdr_traslado):
-- BEGIN;
--   INSERT INTO alm_movimiento_hdr (company_id, numero, tipo_movimiento_id, bodega_id, bodega_destino_id, fecha, motivo)
--   SELECT 2, 999101, t.id, b.id, b.id, current_date, 'prueba'
--     FROM alm_tipo_movimiento t, alm_bodega b WHERE t.company_id=2 AND t.codigo='TRF' AND b.company_id=2 LIMIT 1;
-- ROLLBACK;   -- espera 23514
--
-- V7) PRUEBA NEGATIVA — existencia_transito negativa debe FALLAR (ck_..._transito_no_neg):
-- BEGIN; UPDATE alm_articulo_bodega SET existencia_transito = -1 WHERE id = (SELECT id FROM alm_articulo_bodega LIMIT 1); ROLLBACK;  -- espera 23514
--
-- V8) Re-ejecutar el script entero NO duplica nada (comparar V1 y V5).
-- =============================================================================

-- =============================================================================
-- ROLLBACK (revierte esta Fase 5.2; correr SOLO si no hay traslados posteados)
-- =============================================================================
-- BEGIN;
-- DROP TABLE IF EXISTS public.alm_traslado_recepcion_dtl;
-- DROP TABLE IF EXISTS public.alm_traslado_recepcion;
-- -- Quitar la semilla TRF solo si ningún documento la usa:
-- DELETE FROM public.alm_tipo_movimiento t
--  WHERE t.codigo='TRF'
--    AND NOT EXISTS (SELECT 1 FROM public.alm_movimiento_hdr h WHERE h.tipo_movimiento_id=t.id);
-- -- Restaurar el CHECK de clase a 3 valores (falla si quedó algún tipo TRASLADO en uso):
-- ALTER TABLE public.alm_tipo_movimiento DROP CONSTRAINT IF EXISTS ck_alm_tipo_movimiento_clase;
-- ALTER TABLE public.alm_tipo_movimiento
--     ADD CONSTRAINT ck_alm_tipo_movimiento_clase CHECK (clase IN ('ENTRADA','SALIDA','VALOR'));
-- ALTER TABLE public.alm_articulo_bodega DROP CONSTRAINT IF EXISTS ck_alm_articulo_bodega_transito_no_neg;
-- -- Al soltar las columnas del hdr se sueltan también su FK y su CHECK (dependen de ellas):
-- ALTER TABLE public.alm_movimiento_hdr
--     DROP COLUMN IF EXISTS bodega_destino_id,
--     DROP COLUMN IF EXISTS requiere_recepcion,
--     DROP COLUMN IF EXISTS recibido_por,
--     DROP COLUMN IF EXISTS fecha_recepcion;
-- ALTER TABLE public.alm_movimiento_dtl DROP COLUMN IF EXISTS cantidad_recibida;  -- suelta ck_..._recibida
-- -- Restaurar el CHECK de estado a 2 valores:
-- ALTER TABLE public.alm_movimiento_hdr DROP CONSTRAINT IF EXISTS ck_alm_movimiento_hdr_estado;
-- ALTER TABLE public.alm_movimiento_hdr
--     ADD CONSTRAINT ck_alm_movimiento_hdr_estado CHECK (estado IN (1, 9));
-- COMMIT;
-- =============================================================================
