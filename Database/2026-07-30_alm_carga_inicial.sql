-- =============================================================================
-- Almacén: infraestructura de la CARGA INICIAL de existencias
--          (alm_config_inventario, alm_ajuste_inventario y guardas del kardex)
-- Fecha: 2026-07-30
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- Hasta ahora la existencia de un artículo en una bodega se tecleaba y se persistía
-- directo, sin dejar rastro documental: ni quién, ni cuándo, ni con qué documento, ni
-- a qué costo. El kardex nacía descuadrado y costo_promedio quedaba en 0, de modo que
-- la primera compra que entrara corrompería el promedio ponderado.
-- El motor de posteo (Fase 1, ya implementado en C#) convierte esa inicialización en un
-- asiento CARGA_INICIAL. Este script aporta lo que ese motor necesita de la base:
--   - dónde se guarda la política del corte por empresa (alm_config_inventario);
--   - el documento de AJUSTE, que es la vía legítima de mover stock cuando se cierre la
--     captura manual (sin él, el almacén quedaría en solo lectura);
--   - las guardas e índices del kardex para la idempotencia y las consultas del corte.
--
-- QUÉ HACE
--   1. Tabla alm_config_inventario (una fila por empresa) + semilla idempotente.
--   2. Tabla alm_ajuste_inventario (cabecera plana del ajuste).
--   3. Amplía el vocabulario de alm_kardex.documento_tipo con 'REVERSA'.
--   4. Dos CHECK NOT VALID sobre alm_kardex (libro nuevo / fecha obligatoria).
--   5. Dos índices parciales de apoyo (apertura vigente, reversa).
--   6. Actualiza los COMMENT de uuid y documento_tipo (la mecánica del uuid cambió).
--
-- Cambio ADITIVO. No borra ni modifica ningún dato de negocio. El único DROP es el del
-- CHECK ck_alm_kardex_documento_tipo, que se recrea como SUPERCONJUNTO del vigente.
-- IDEMPOTENTE: IF NOT EXISTS / ON CONFLICT / guardas por nombre de constraint.
-- REVERSIBLE: ver ROLLBACK al final.
--
-- ⚠️ PRERREQUISITOS (Fase 0(a) del diseño). Este script asume aplicados:
--   2026-07-09_alm_kardex_bodega_id.sql, 2026-07-13_alm_kardex_articulo_id.sql y los
--   cuatro de 2026-07-14 (trazabilidad, ampliar precisiones, FK RESTRICT, FK compuestas).
--   NINGUNO figura en el runbook vigente: confirmar en SRV ANTES de aplicar aquí.
--   Las guardas del paso 0 abortan con mensaje claro si falta algo.
--
-- ⚠️ Los CHECK del paso 4 se crean NOT VALID a propósito: la afirmación "todo el
--   histórico tiene documento_tipo NULL" es plausible pero NO está comprobada contra el
--   SRV, y una sola fila incompatible abortaría el ALTER —y con él toda la transacción—
--   en plena ventana de despliegue. La validación va como PASO PROPIO del runbook.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 0) Guardas de prerrequisito: sin la trazabilidad del kardex no hay motor posible.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF to_regclass('public.alm_kardex') IS NULL THEN
        RAISE EXCEPTION 'Prerrequisito faltante: no existe la tabla alm_kardex.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_name = 'alm_kardex' AND column_name = 'uuid'
    ) THEN
        RAISE EXCEPTION 'Prerrequisito faltante: alm_kardex no tiene las columnas de trazabilidad. Aplique primero Database/2026-07-14_alm_kardex_trazabilidad.sql.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_name = 'alm_kardex' AND column_name = 'existencia_resultante'
    ) THEN
        RAISE EXCEPTION 'Prerrequisito faltante: alm_kardex no tiene existencia_resultante. Aplique primero Database/2026-07-14_alm_kardex_trazabilidad.sql.';
    END IF;

    -- Claves alternas por tenant, necesarias para las FK compuestas de alm_ajuste_inventario.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_articulo_company_id') THEN
        RAISE EXCEPTION 'Prerrequisito faltante: no existe uq_alm_articulo_company_id. Aplique primero Database/2026-07-14_alm_fk_compuestas_tenant.sql.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_bodega_company_id') THEN
        RAISE EXCEPTION 'Prerrequisito faltante: no existe uq_alm_bodega_company_id. Aplique primero Database/2026-07-14_alm_fk_compuestas_tenant.sql.';
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 1) Política del corte, por empresa
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_config_inventario (
    company_id                   BIGINT       PRIMARY KEY,
    base_costo_apertura          VARCHAR(20)  NOT NULL DEFAULT 'VALOR_UNITARIO',
    costo_apertura_incluye_isv   BOOLEAN      NOT NULL DEFAULT false,
    fecha_corte_apertura         DATE         NULL,
    apertura_cerrada             BOOLEAN      NOT NULL DEFAULT false,
    usuariocreacion              VARCHAR(100) NULL,
    fechacreacion                TIMESTAMP WITHOUT TIME ZONE NULL,
    usuariomodificacion          VARCHAR(100) NULL,
    fechamodificacion            TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT ck_alm_config_inventario_base_costo
        CHECK (base_costo_apertura IN ('VALOR_UNITARIO', 'MANUAL'))
);

COMMENT ON TABLE public.alm_config_inventario IS
    'Política de la carga inicial de inventario, una fila por empresa. NO guarda la política del ISV: esa es fiscal/contable y vive en la configuración de empresa, no en almacén.';
COMMENT ON COLUMN public.alm_config_inventario.base_costo_apertura IS
    'De dónde sale el costo de apertura: VALOR_UNITARIO = alm_articulo.valor_unitario; MANUAL = capturado por el usuario en el proceso de corte.';
COMMENT ON COLUMN public.alm_config_inventario.costo_apertura_incluye_isv IS
    'Declara en qué base está el costo con el que se sembró la apertura. Resuelto por el contador el 2026-07-30: valor_unitario NO lleva ISV, por eso el default es false. Si la apertura y las compras futuras usaran bases distintas, el promedio ponderado mezclaría dos bases EN SILENCIO.';
COMMENT ON COLUMN public.alm_config_inventario.fecha_corte_apertura IS
    'Fecha contable del LOTE retroactivo (obligatoria al ejecutarlo). NULL = lote no ejecutado. No aplica a las aperturas unitarias, que se fechan el día del alta.';
COMMENT ON COLUMN public.alm_config_inventario.apertura_cerrada IS
    'true = el corte se dio por terminado: no se acepta ninguna apertura nueva para pares preexistentes. Los pares creados después siguen abriendo normalmente.';

-- Semilla: una fila por cada empresa que ya tenga artículos.
INSERT INTO public.alm_config_inventario (company_id, usuariocreacion, fechacreacion)
SELECT DISTINCT a.company_id, 'system', now()
  FROM public.alm_articulo a
ON CONFLICT (company_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 2) Documento de ajuste de inventario (cabecera plana)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_ajuste_inventario (
    id               SERIAL PRIMARY KEY,
    company_id       BIGINT        NOT NULL,
    articulo_id      INTEGER       NOT NULL,
    bodega_id        INTEGER       NOT NULL,
    clase            VARCHAR(10)   NOT NULL,
    cantidad         NUMERIC(15,2) NOT NULL DEFAULT 0,
    costo_unitario   NUMERIC(12,4) NOT NULL DEFAULT 0,
    motivo           VARCHAR(120)  NOT NULL,
    observacion      VARCHAR(254)  NULL,
    posteado         BOOLEAN       NOT NULL DEFAULT false,
    usuariocreacion  VARCHAR(100)  NULL,
    fechacreacion    TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT ck_alm_ajuste_inventario_clase
        CHECK (clase IN ('ENTRADA', 'SALIDA', 'VALOR')),
    CONSTRAINT ck_alm_ajuste_inventario_cantidad
        CHECK ((clase IN ('ENTRADA', 'SALIDA') AND cantidad > 0)
            OR (clase = 'VALOR' AND cantidad = 0)),
    CONSTRAINT ck_alm_ajuste_inventario_costo
        CHECK ((clase IN ('ENTRADA', 'VALOR') AND costo_unitario > 0)
            OR clase = 'SALIDA'),
    CONSTRAINT ck_alm_ajuste_inventario_motivo
        CHECK (length(btrim(motivo)) > 0)
);

COMMENT ON TABLE public.alm_ajuste_inventario IS
    'Documento de ajuste de inventario: la vía legítima de mover stock una vez cerrada la captura manual de existencia. Cabecera plana (una línea por documento); numeración formal y catálogo de motivos son trabajo posterior.';
COMMENT ON COLUMN public.alm_ajuste_inventario.clase IS
    'ENTRADA = suma existencia (sobrante de conteo); SALIDA = resta (faltante/merma); VALOR = solo corrige el costo, no mueve existencia.';
COMMENT ON COLUMN public.alm_ajuste_inventario.costo_unitario IS
    'Obligatorio (> 0) en ENTRADA y VALOR. En SALIDA se ignora: el motor usa el costo promedio vigente, porque una salida no cambia el promedio.';
COMMENT ON COLUMN public.alm_ajuste_inventario.posteado IS
    'true = ya tiene su asiento en alm_kardex. Se marca en la MISMA transacción del posteo.';

-- FK compuestas por tenant: un ajuste no puede cruzar de empresa.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_ajuste_inventario_articulo') THEN
        ALTER TABLE public.alm_ajuste_inventario
            ADD CONSTRAINT fk_alm_ajuste_inventario_articulo
            FOREIGN KEY (company_id, articulo_id)
            REFERENCES public.alm_articulo(company_id, id)
            ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_ajuste_inventario_bodega') THEN
        ALTER TABLE public.alm_ajuste_inventario
            ADD CONSTRAINT fk_alm_ajuste_inventario_bodega
            FOREIGN KEY (company_id, bodega_id)
            REFERENCES public.alm_bodega(company_id, id)
            ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_alm_ajuste_inventario_company
    ON public.alm_ajuste_inventario(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_ajuste_inventario_par
    ON public.alm_ajuste_inventario(company_id, articulo_id, bodega_id);
CREATE INDEX IF NOT EXISTS ix_alm_ajuste_inventario_sin_postear
    ON public.alm_ajuste_inventario(company_id) WHERE NOT posteado;

-- ---------------------------------------------------------------------------
-- 3) documento_tipo admite 'REVERSA' (superconjunto del CHECK vigente)
-- ---------------------------------------------------------------------------
ALTER TABLE public.alm_kardex DROP CONSTRAINT IF EXISTS ck_alm_kardex_documento_tipo;
ALTER TABLE public.alm_kardex ADD CONSTRAINT ck_alm_kardex_documento_tipo CHECK (
    documento_tipo IS NULL OR documento_tipo IN
    ('COMPRA','REQUISICION','DESCARGO','TRASLADO','AJUSTE','CARGA_INICIAL','REVERSA')
);

-- ---------------------------------------------------------------------------
-- 4) Guardas del "libro nuevo" (NOT VALID: no escanean el histórico)
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_kardex_libro_nuevo') THEN
        -- uuid y documento_tipo son EL MISMO discriminador: o están los dos, o ninguno.
        ALTER TABLE public.alm_kardex
            ADD CONSTRAINT ck_alm_kardex_libro_nuevo
            CHECK ((uuid IS NULL) = (documento_tipo IS NULL)) NOT VALID;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_kardex_fecha_si_uuid') THEN
        -- Todo asiento del motor lleva fecha contable: sin ella se ordena al principio
        -- del kardex y desaparece de cualquier filtro por rango.
        ALTER TABLE public.alm_kardex
            ADD CONSTRAINT ck_alm_kardex_fecha_si_uuid
            CHECK (uuid IS NULL OR fecha IS NOT NULL) NOT VALID;
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 5) Índices parciales de apoyo
-- ---------------------------------------------------------------------------
-- Guarda "¿el par ya tiene apertura vigente?" y reporte de pendientes del corte.
CREATE INDEX IF NOT EXISTS ix_alm_kardex_carga_inicial
    ON public.alm_kardex (company_id, articulo_id, bodega_id)
    WHERE documento_tipo = 'CARGA_INICIAL';

-- "¿este asiento ya fue revertido?" y cálculo del discriminador de intento del uuid.
CREATE INDEX IF NOT EXISTS ix_alm_kardex_reversa
    ON public.alm_kardex (company_id, documento_id)
    WHERE documento_tipo = 'REVERSA';

-- ---------------------------------------------------------------------------
-- 6) Comentarios que cambian (la mecánica del uuid ganó el discriminador de intento)
-- ---------------------------------------------------------------------------
COMMENT ON COLUMN public.alm_kardex.uuid IS
    'Idempotencia por MOVIMIENTO (línea), no por documento. Determinista (UUIDv5, namespace de inventario): CARGA_INICIAL usa (company_id, alm_articulo_bodega.id, intento) donde intento = 1 + aperturas del par ya revertidas —lo que permite reabrir tras una reversa sin chocar con el índice único—; REVERSA usa (company_id, kardex revertido); el resto, (documento_tipo, company_id, documento_id, alm_articulo_bodega.id). Reintentar el mismo posteo no duplica el asiento. NULL = histórico SIMAFI, no posteado por el motor.';

COMMENT ON COLUMN public.alm_kardex.documento_tipo IS
    'Tipo del documento que originó el asiento. Vocabulario cerrado: COMPRA, REQUISICION, DESCARGO, TRASLADO, AJUSTE, CARGA_INICIAL, REVERSA. NULL = histórico SIMAFI. Va siempre acompañado de uuid (ck_alm_kardex_libro_nuevo).';

COMMIT;

-- =============================================================================
-- VALIDACIÓN DE LOS CHECK  ⚠️ PASO PROPIO DEL RUNBOOK — NO va en este script
-- =============================================================================
-- Correr DESPUÉS de los pre-chequeos de la Fase 0(b), fuera de la ventana crítica.
-- Si alguno falla, hay filas del histórico que contradicen la regla: investigar ANTES
-- de escribir un solo asiento con el motor.
--
-- ALTER TABLE public.alm_kardex VALIDATE CONSTRAINT ck_alm_kardex_libro_nuevo;
-- ALTER TABLE public.alm_kardex VALIDATE CONSTRAINT ck_alm_kardex_fecha_si_uuid;
--
-- Pre-chequeo previo (los tres deben dar 0):
-- SELECT count(*) FROM alm_kardex WHERE documento_tipo IS NOT NULL AND uuid IS NULL;
-- SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL AND documento_tipo IS NULL;
-- SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL AND fecha IS NULL;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Las dos tablas existen:
-- SELECT table_name FROM information_schema.tables
--  WHERE table_name IN ('alm_config_inventario','alm_ajuste_inventario') ORDER BY 1;
--
-- 2) Una fila de config por empresa con artículos, y ninguna con corte ejecutado:
-- SELECT company_id, base_costo_apertura, costo_apertura_incluye_isv,
--        fecha_corte_apertura, apertura_cerrada
--   FROM public.alm_config_inventario ORDER BY company_id;
--
-- 3) documento_tipo ya admite REVERSA:
-- SELECT pg_get_constraintdef(oid) FROM pg_constraint
--  WHERE conname = 'ck_alm_kardex_documento_tipo';
--
-- 4) Los dos CHECK nuevos existen y están NO validados todavía (convalidated = false):
-- SELECT conname, convalidated FROM pg_constraint
--  WHERE conname IN ('ck_alm_kardex_libro_nuevo','ck_alm_kardex_fecha_si_uuid');
--
-- 5) Los índices parciales existen:
-- SELECT indexname FROM pg_indexes WHERE tablename IN ('alm_kardex','alm_ajuste_inventario')
--   AND indexname IN ('ix_alm_kardex_carga_inicial','ix_alm_kardex_reversa',
--                     'ix_alm_ajuste_inventario_company','ix_alm_ajuste_inventario_par',
--                     'ix_alm_ajuste_inventario_sin_postear') ORDER BY 1;
--
-- 6) Idempotencia: volver a correr el script no debe fallar ni cambiar conteos.

-- =============================================================================
-- ROLLBACK (solo si hay que revertir)
-- =============================================================================
-- BEGIN;
-- DROP INDEX IF EXISTS ix_alm_kardex_reversa;
-- DROP INDEX IF EXISTS ix_alm_kardex_carga_inicial;
-- ALTER TABLE public.alm_kardex DROP CONSTRAINT IF EXISTS ck_alm_kardex_fecha_si_uuid;
-- ALTER TABLE public.alm_kardex DROP CONSTRAINT IF EXISTS ck_alm_kardex_libro_nuevo;
-- -- Restaura el vocabulario anterior (sin REVERSA). OJO: falla si ya hay asientos REVERSA.
-- ALTER TABLE public.alm_kardex DROP CONSTRAINT IF EXISTS ck_alm_kardex_documento_tipo;
-- ALTER TABLE public.alm_kardex ADD CONSTRAINT ck_alm_kardex_documento_tipo CHECK (
--     documento_tipo IS NULL OR documento_tipo IN
--     ('COMPRA','REQUISICION','DESCARGO','TRASLADO','AJUSTE','CARGA_INICIAL'));
-- DROP TABLE IF EXISTS public.alm_ajuste_inventario;
-- DROP TABLE IF EXISTS public.alm_config_inventario;
-- COMMIT;
-- =============================================================================
