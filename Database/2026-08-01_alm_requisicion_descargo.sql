-- =============================================================================
-- Requisiciones y descargos (salida de bodega) — cabeceras, correlativos y parcialidad
-- Fecha: 2026-08-01
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) antes que en el SRV.
--
-- POR QUÉ
--   La captura de requisiciones no existe en el portal, y tampoco en Centura: el menú
--   "Ingreso de Requisiciones" solo llama ShowRequisiciones() (GA_IN.APT:2733), función
--   externa de prGarantias.dll. Hoy /almacen/requisiciones y /almacen/descargos son solo
--   consulta sobre el histórico migrado de SIMAFI.
--   Diseño completo: docs/centura-flujos/README_requisiciones_descargos.md
--
-- REGLA ESTRUCTURAL (medida en el mirror, no supuesta)
--   alm_requisicion (42.866 líneas), alm_descargo (42.757) y alm_kardex tipo 202 (42.698)
--   son EL MISMO HECHO: 42.653 pares comunes por (numero ↔ numero_requisicion,
--   codigo_articulo), y solo 15 descargos sin requisición. Por lo tanto:
--     · SOLO el DESCARGO postea al kardex. La requisición solicita, nunca asienta.
--     · El histórico NO se migra ni se re-postea (queda origen='SIMAFI', posteado=true).
--   Postear ambos documentos duplicaría toda la salida del almacén.
--
-- PATRÓN: misma decisión D-3 de Database/2026-07-31_alm_compra_recepcion.sql — cabecera
--   real nueva + la tabla plana existente sigue siendo la UNIDAD DE POSTEO (un uuid por
--   línea). El motor no cambia de contrato y el histórico queda con hdr_id NULL.
--
-- ADITIVO salvo por un DROP deliberado: ix_alm_requisicion_pendiente, el índice que define
--   el conjunto "requisiciones SIAD pendientes de postear" — un barrido futuro sobre ese
--   índice convertiría cada requisición en una salida duplicada. Se desarma el arma cargada.
-- IDEMPOTENTE (IF NOT EXISTS / DO blocks guardados por pg_constraint).
-- REVERSIBLE (bloque de rollback al final).
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 0) Prerrequisitos: fallar temprano y con mensaje accionable, no con error de FK
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'alm_requisicion' AND column_name = 'origen') THEN
        RAISE EXCEPTION 'Falta Database/2026-07-14_alm_documentos_bodega_posteo.sql (no existe alm_requisicion.origen). Aplíquelo primero.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_bodega_company_id') THEN
        RAISE EXCEPTION 'Falta la clave alterna uq_alm_bodega_company_id. Aplique Database/2026-07-14_alm_fk_compuestas_tenant.sql.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_articulo_company_id') THEN
        RAISE EXCEPTION 'Falta la clave alterna uq_alm_articulo_company_id. Aplique Database/2026-07-14_alm_fk_compuestas_tenant.sql.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_kardex_documento_tipo') THEN
        RAISE EXCEPTION 'Falta Database/2026-07-14_alm_kardex_trazabilidad.sql (no existe ck_alm_kardex_documento_tipo).';
    END IF;

    -- El histórico debe seguir intacto. Si ya hay documentos SIAD vivos en las planas,
    -- alguien empezó a capturar por otra vía: hay que revisarlo antes de tocar nada.
    IF EXISTS (SELECT 1 FROM alm_requisicion WHERE origen <> 'SIMAFI')
       OR EXISTS (SELECT 1 FROM alm_descargo WHERE origen <> 'SIMAFI') THEN
        RAISE EXCEPTION 'Hay documentos con origen SIAD en alm_requisicion/alm_descargo. Revise antes de aplicar.';
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 1) Claves alternas por tenant. Sin ellas no se puede declarar NINGUNA FK
--    compuesta contra las tablas planas.
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_requisicion_tenant') THEN
        ALTER TABLE public.alm_requisicion ADD CONSTRAINT uq_alm_requisicion_tenant UNIQUE (company_id, id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_descargo_tenant') THEN
        ALTER TABLE public.alm_descargo ADD CONSTRAINT uq_alm_descargo_tenant UNIQUE (company_id, id);
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 2) Cabecera de la REQUISICIÓN (solicitud). NO genera asientos.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_requisicion_hdr (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    numero              INTEGER       NOT NULL,             -- correlativo por empresa (continúa el histórico)
    tipo                SMALLINT      NOT NULL DEFAULT 1,   -- 1 salida de bodega · 2 reabastecimiento
    estado              SMALLINT      NOT NULL DEFAULT 1,
    fecha               DATE          NOT NULL,
    fecha_requerida     DATE          NULL,
    bodega_id           INTEGER       NOT NULL,             -- de dónde saldrá la mercadería
    departamento        VARCHAR(3)    NULL,                 -- texto: no hay catálogo (README §11 D-15)
    solicitante         VARCHAR(120)  NOT NULL,
    cargo_solicitante   VARCHAR(80)   NULL,
    usuario_solicita    VARCHAR(100)  NOT NULL,             -- login; distinto del nombre mostrado
    aplicacion          VARCHAR(254)  NULL,                 -- destino/uso (≡ alm_requisicion.aplicacion)
    observacion         VARCHAR(1000) NULL,
    aprobado_por        VARCHAR(100)  NULL,
    fecha_aprobacion    TIMESTAMP WITHOUT TIME ZONE NULL,
    rechazado_por       VARCHAR(100)  NULL,
    fecha_rechazo       TIMESTAMP WITHOUT TIME ZONE NULL,
    motivo_rechazo      VARCHAR(500)  NULL,
    anulado_por         VARCHAR(100)  NULL,
    fecha_anulacion     TIMESTAMP WITHOUT TIME ZONE NULL,
    motivo_anulacion    VARCHAR(500)  NULL,
    total               NUMERIC(14,2) NOT NULL DEFAULT 0,   -- referencial (precio de solicitud)
    uuid                UUID          NULL,                 -- idempotencia del ALTA
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT uq_alm_requisicion_hdr_numero UNIQUE (company_id, numero),
    CONSTRAINT uq_alm_requisicion_hdr_tenant UNIQUE (company_id, id),

    -- Dominio del estado. La MÁQUINA DE ESTADOS vive en el servicio, no en un trigger:
    -- mismo criterio que alm_orden_compra, cuyo único control en BD es su CHECK.
    CONSTRAINT ck_alm_requisicion_hdr_estado CHECK (estado IN (1,2,3,4,5,6,8,9)),
    CONSTRAINT ck_alm_requisicion_hdr_tipo   CHECK (tipo IN (1,2)),
    -- Un reabastecimiento NO descuenta stock: no alcanza estados de despacho.
    CONSTRAINT ck_alm_requisicion_hdr_tipo_estado CHECK (tipo = 1 OR estado NOT IN (4,5)),
    -- "Cerrada en O/C" solo existe en reabastecimiento.
    CONSTRAINT ck_alm_requisicion_hdr_cierre_oc   CHECK (tipo = 2 OR estado <> 6),

    -- EVIDENCIA. Corrige de frente el defecto del legacy: INV_REQUISICION_HDR.FECHA_APROBADA
    -- existía en la tabla y ningún código de aplicación la escribía.
    CONSTRAINT ck_alm_requisicion_hdr_aprobacion
        CHECK (estado NOT IN (3,4,5,6) OR (aprobado_por IS NOT NULL AND fecha_aprobacion IS NOT NULL)),
    CONSTRAINT ck_alm_requisicion_hdr_rechazo
        CHECK (estado <> 8 OR (rechazado_por IS NOT NULL AND fecha_rechazo IS NOT NULL
                               AND motivo_rechazo IS NOT NULL AND length(btrim(motivo_rechazo)) > 0)),
    CONSTRAINT ck_alm_requisicion_hdr_anulacion
        CHECK (estado <> 9 OR (anulado_por IS NOT NULL AND fecha_anulacion IS NOT NULL)),
    CONSTRAINT ck_alm_requisicion_hdr_solicitante CHECK (length(btrim(solicitante)) > 0)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_requisicion_hdr_bodega') THEN
        ALTER TABLE public.alm_requisicion_hdr
            ADD CONSTRAINT fk_alm_requisicion_hdr_bodega
                FOREIGN KEY (company_id, bodega_id)
                REFERENCES public.alm_bodega (company_id, id) ON DELETE RESTRICT;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_requisicion_hdr_company_uuid
    ON public.alm_requisicion_hdr (company_id, uuid) WHERE uuid IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_company
    ON public.alm_requisicion_hdr (company_id);
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_aprobables
    ON public.alm_requisicion_hdr (company_id, fecha) WHERE estado = 2;
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_despachables
    ON public.alm_requisicion_hdr (company_id, bodega_id) WHERE estado IN (3,4);
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_para_oc
    ON public.alm_requisicion_hdr (company_id) WHERE tipo = 2 AND estado = 3;
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_departamento
    ON public.alm_requisicion_hdr (company_id, departamento);

COMMENT ON TABLE public.alm_requisicion_hdr IS
 'Cabecera de la requisición interna de materiales (SOLICITUD). NUNCA genera asientos de kardex: la salida la produce el DESCARGO (alm_descargo). El histórico migrado de SIMAFI vive en la tabla plana alm_requisicion con requisicion_hdr_id NULL.';
COMMENT ON COLUMN public.alm_requisicion_hdr.numero IS
 'Correlativo por empresa (alm_requisicion_correlativo), SEMBRADO desde max(alm_requisicion.numero) del histórico (17124 en la empresa 2) para que la numeración nueva CONTINÚE la vieja y ningún número identifique dos documentos.';
COMMENT ON COLUMN public.alm_requisicion_hdr.estado IS
 '1 Borrador · 2 En revisión · 3 Aprobada · 4 Despachada parcial · 5 Despachada · 6 Cerrada en O/C · 8 Rechazada · 9 Anulada. Los estados 4 y 5 son DERIVADOS de cantidad_despachada, no se capturan a mano.';
COMMENT ON COLUMN public.alm_requisicion_hdr.tipo IS
 '1 Salida de bodega (consume stock) · 2 Reabastecimiento (alimenta una orden de compra, no toca inventario). Los dos tipos tienen aprobación separada en Centura (GA_IN.APT:2735 y :2746).';
COMMENT ON COLUMN public.alm_requisicion_hdr.bodega_id IS
 'Bodega de la que saldrá la mercadería. Obligatoria: sin ella no hay par artículo/bodega que despachar. El histórico SIMAFI no la traía (bodega_id NULL en sus 42.866 líneas).';

-- -----------------------------------------------------------------------------
-- 3) LÍNEA de la requisición: columnas aditivas sobre la tabla plana existente.
--    El DEFAULT se pone en un ALTER COLUMN POSTERIOR, nunca en el ADD COLUMN, para
--    que el centinela NULL sobreviva hasta el backfill.
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_requisicion
    ADD COLUMN IF NOT EXISTS requisicion_hdr_id  INTEGER       NULL,
    ADD COLUMN IF NOT EXISTS cantidad_despachada NUMERIC(12,2) NULL,
    ADD COLUMN IF NOT EXISTS aplicado_en_oc      BOOLEAN       NULL;

-- Backfill del histórico: lo ya descargado se da por entregado por completo.
UPDATE public.alm_requisicion SET cantidad_despachada = cantidad
 WHERE cantidad_despachada IS NULL AND descargado;
UPDATE public.alm_requisicion SET cantidad_despachada = 0 WHERE cantidad_despachada IS NULL;
UPDATE public.alm_requisicion SET aplicado_en_oc = false  WHERE aplicado_en_oc IS NULL;

ALTER TABLE public.alm_requisicion
    ALTER COLUMN cantidad_despachada SET DEFAULT 0,
    ALTER COLUMN cantidad_despachada SET NOT NULL,
    ALTER COLUMN aplicado_en_oc      SET DEFAULT false,
    ALTER COLUMN aplicado_en_oc      SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_requisicion_hdr') THEN
        ALTER TABLE public.alm_requisicion
            ADD CONSTRAINT fk_alm_requisicion_hdr
                FOREIGN KEY (company_id, requisicion_hdr_id)
                REFERENCES public.alm_requisicion_hdr (company_id, id) ON DELETE RESTRICT;
    END IF;

    -- TOPE DE LA PARCIALIDAD: la garantía que el legacy nunca tuvo en la base. Es la RED
    -- FINAL; el tope operativo lo valida el servicio bajo SELECT ... FOR UPDATE.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_requisicion_despachada') THEN
        ALTER TABLE public.alm_requisicion
            ADD CONSTRAINT ck_alm_requisicion_despachada
            CHECK (cantidad_despachada >= 0 AND cantidad_despachada <= cantidad);
    END IF;

    -- La requisición NO postea. Sin este CHECK, cualquier barrido futuro de "pendientes"
    -- convertiría solicitudes en salidas, duplicando lo que ya asentó el descargo.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_requisicion_no_postea') THEN
        ALTER TABLE public.alm_requisicion
            ADD CONSTRAINT ck_alm_requisicion_no_postea
            CHECK (origen = 'SIMAFI' OR posteado = false);
    END IF;
END $$;

-- El índice de "pendiente de postear" define justamente ese conjunto peligroso. Como la
-- requisición nunca postea, se elimina (única operación no aditiva del script).
DROP INDEX IF EXISTS public.ix_alm_requisicion_pendiente;

CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_id
    ON public.alm_requisicion (company_id, requisicion_hdr_id);
-- Reporte "Requisiciones pendientes de entregar" (en Centura, CANTIDAD > CANTIDAD_APLICADA).
-- Parcial: el filtro por requisicion_hdr_id NOT NULL es lo que impide que las 42.866 líneas
-- del histórico entren al cálculo.
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_pendiente_entrega
    ON public.alm_requisicion (company_id, articulo_id, bodega_id)
    WHERE requisicion_hdr_id IS NOT NULL AND cantidad_despachada < cantidad;

COMMENT ON COLUMN public.alm_requisicion.cantidad_despachada IS
 'Cantidad ya entregada por descargos vigentes. Equivalente de INV_REQUISICION_DTL.CANTIDAD_APLICADA. La escribe el servicio de despacho bajo SELECT ... FOR UPDATE; ck_alm_requisicion_despachada es la red final.';
COMMENT ON COLUMN public.alm_requisicion.requisicion_hdr_id IS
 'Cabecera del documento nuevo. NULL en las 42.866 líneas del histórico SIMAFI, que no tienen cabecera y no se migran.';
COMMENT ON COLUMN public.alm_requisicion.aplicado_en_oc IS
 'Solo para requisiciones de reabastecimiento: marca el renglón ya volcado a una orden de compra. Equivale a FLAG_SELECCIONADO_PARA_COMPRA de INV_REQUISICION_DTL (GA_IN.APT:11018).';

-- -----------------------------------------------------------------------------
-- 4) Cabecera del DESCARGO (entrega). Este documento SÍ postea.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_descargo_hdr (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    numero              INTEGER       NOT NULL,
    fecha               DATE          NOT NULL,             -- fecha contable del asiento
    requisicion_hdr_id  INTEGER       NULL,                 -- NULL = descargo directo (sin requisición)
    bodega_id           INTEGER       NOT NULL,
    departamento        VARCHAR(3)    NULL,
    entregado_por       VARCHAR(100)  NULL,                 -- bodeguero
    recibido_por        VARCHAR(120)  NULL,                 -- quien retira
    motivo              VARCHAR(120)  NULL,                 -- obligatorio si NO hay requisición
    observaciones       VARCHAR(1000) NULL,
    total               NUMERIC(14,2) NOT NULL DEFAULT 0,   -- valorizado al promedio, lo estampa el posteo
    estado              SMALLINT      NOT NULL DEFAULT 1,   -- 1 Registrado · 9 Anulado
    motivo_anulacion    VARCHAR(500)  NULL,
    anulado_por         VARCHAR(100)  NULL,
    fecha_anulacion     TIMESTAMP WITHOUT TIME ZONE NULL,
    posteado            BOOLEAN       NOT NULL DEFAULT false,
    fecha_posteo        TIMESTAMP WITHOUT TIME ZONE NULL,
    uuid                UUID          NULL,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT uq_alm_descargo_hdr_numero UNIQUE (company_id, numero),
    CONSTRAINT uq_alm_descargo_hdr_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_descargo_hdr_estado CHECK (estado IN (1, 9)),
    -- Un descargo directo (sin requisición que lo justifique) exige motivo, igual que el
    -- ajuste de inventario.
    CONSTRAINT ck_alm_descargo_hdr_motivo
        CHECK (requisicion_hdr_id IS NOT NULL
               OR (motivo IS NOT NULL AND length(btrim(motivo)) > 0)),
    CONSTRAINT ck_alm_descargo_hdr_posteo
        CHECK (posteado = false OR (uuid IS NOT NULL AND fecha_posteo IS NOT NULL)),
    CONSTRAINT ck_alm_descargo_hdr_anulacion
        CHECK (estado <> 9 OR (anulado_por IS NOT NULL AND fecha_anulacion IS NOT NULL))
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_descargo_hdr_bodega') THEN
        ALTER TABLE public.alm_descargo_hdr
            ADD CONSTRAINT fk_alm_descargo_hdr_bodega
                FOREIGN KEY (company_id, bodega_id)
                REFERENCES public.alm_bodega (company_id, id) ON DELETE RESTRICT;
    END IF;
    -- Con requisicion_hdr_id NULL (descargo directo) MATCH SIMPLE no valida la referencia.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_descargo_hdr_requisicion') THEN
        ALTER TABLE public.alm_descargo_hdr
            ADD CONSTRAINT fk_alm_descargo_hdr_requisicion
                FOREIGN KEY (company_id, requisicion_hdr_id)
                REFERENCES public.alm_requisicion_hdr (company_id, id) ON DELETE RESTRICT;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_descargo_hdr_company_uuid
    ON public.alm_descargo_hdr (company_id, uuid) WHERE uuid IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_company
    ON public.alm_descargo_hdr (company_id);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_fecha
    ON public.alm_descargo_hdr (company_id, fecha DESC, id DESC);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_requisicion
    ON public.alm_descargo_hdr (company_id, requisicion_hdr_id);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_pendiente
    ON public.alm_descargo_hdr (company_id) WHERE posteado = false AND estado = 1;

COMMENT ON TABLE public.alm_descargo_hdr IS
 'Cabecera del descargo (ENTREGA física de materiales). ÚNICO documento del par requisición/descargo que genera asientos en alm_kardex (documento_tipo = DESCARGO). Una requisición admite N descargos (entrega parcial); un descargo sin requisicion_hdr_id es una salida directa justificada por motivo.';
COMMENT ON COLUMN public.alm_descargo_hdr.estado IS
 '1 Registrado · 9 Anulado. Un descargo no se edita: si estuvo mal se anula, se devuelve la cantidad a la requisición y el kardex se corrige con un asiento REVERSA (nunca con UPDATE: trg_alm_kardex_inmutable, SQLSTATE K0001).';

-- -----------------------------------------------------------------------------
-- 5) LÍNEA del descargo = UNIDAD DE POSTEO. Columnas aditivas sobre la plana.
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_descargo
    ADD COLUMN IF NOT EXISTS descargo_hdr_id INTEGER NULL,
    ADD COLUMN IF NOT EXISTS requisicion_id  INTEGER NULL;   -- LÍNEA de requisición servida

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_descargo_hdr') THEN
        ALTER TABLE public.alm_descargo
            ADD CONSTRAINT fk_alm_descargo_hdr
                FOREIGN KEY (company_id, descargo_hdr_id)
                REFERENCES public.alm_descargo_hdr (company_id, id) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_descargo_requisicion_linea') THEN
        ALTER TABLE public.alm_descargo
            ADD CONSTRAINT fk_alm_descargo_requisicion_linea
                FOREIGN KEY (company_id, requisicion_id)
                REFERENCES public.alm_requisicion (company_id, id) ON DELETE RESTRICT;
    END IF;
    -- Dos renglones del MISMO descargo no pueden servir la misma línea de requisición: por
    -- separado pasan el tope y juntos lo rompen. En compras esta regla vive solo en C#;
    -- aquí la garantiza la base.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_descargo_renglon') THEN
        ALTER TABLE public.alm_descargo
            ADD CONSTRAINT uq_alm_descargo_renglon
            UNIQUE (company_id, descargo_hdr_id, requisicion_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_id
    ON public.alm_descargo (company_id, descargo_hdr_id);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_requisicion_linea
    ON public.alm_descargo (company_id, requisicion_id);

COMMENT ON COLUMN public.alm_descargo.descargo_hdr_id IS
 'Cabecera del documento nuevo. NULL en las 42.757 líneas del histórico SIMAFI.';
COMMENT ON COLUMN public.alm_descargo.requisicion_id IS
 'LÍNEA de alm_requisicion que sirve este renglón (FK compuesta tenant-safe). El histórico enlaza por numero_requisicion (NUMERIC, sin FK) y queda NULL aquí.';

-- -----------------------------------------------------------------------------
-- 6) Correlativos por empresa, SEMBRADOS desde el máximo histórico
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_requisicion_correlativo (
    company_id    BIGINT  PRIMARY KEY,
    ultimo_numero INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS public.alm_descargo_correlativo (
    company_id    BIGINT  PRIMARY KEY,
    ultimo_numero INTEGER NOT NULL DEFAULT 0
);

-- SEMBRADO AUTO-CORREGIBLE: DO UPDATE ... GREATEST, nunca DO NOTHING. Así el script es
-- re-ejecutable y CORRIGE un contador que el servicio ya hubiera creado en 0 — el patrón
-- del servicio (INSERT ... VALUES(company,0) ON CONFLICT DO NOTHING) sobre una tabla CON
-- HISTORIA anularía esta siembra en silencio y la numeración nueva pisaría la vieja.
INSERT INTO public.alm_requisicion_correlativo (company_id, ultimo_numero)
SELECT company_id, COALESCE(max(numero), 0)::int FROM public.alm_requisicion GROUP BY company_id
ON CONFLICT (company_id) DO UPDATE
   SET ultimo_numero = GREATEST(alm_requisicion_correlativo.ultimo_numero, EXCLUDED.ultimo_numero);

INSERT INTO public.alm_descargo_correlativo (company_id, ultimo_numero)
SELECT company_id, COALESCE(max(numero_documento), 0)::int FROM public.alm_descargo GROUP BY company_id
ON CONFLICT (company_id) DO UPDATE
   SET ultimo_numero = GREATEST(alm_descargo_correlativo.ultimo_numero, EXCLUDED.ultimo_numero);

COMMENT ON TABLE public.alm_requisicion_correlativo IS
 'Último número de requisición emitido por empresa. Sembrado desde max(alm_requisicion.numero) del histórico SIMAFI (17124 en la empresa 2): la primera requisición nueva es la 17125, para que el número siga identificando UN solo documento en todo el módulo.';
COMMENT ON TABLE public.alm_descargo_correlativo IS
 'Último número de descargo emitido por empresa. Sembrado desde max(alm_descargo.numero_documento) del histórico (17124 en la empresa 2).';

-- -----------------------------------------------------------------------------
-- 7) Guarda de no-negativos de la reserva (validada: las 638 filas están en 0).
--    NO se agrega CHECK sobre `existencia`: hay 3 filas negativas legítimas del histórico
--    que lo harían fallar. Y este CHECK NO garantiza comprometida <= existencia, que es
--    imposible de sostener mientras existan esas negativas.
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_articulo_bodega_reserva') THEN
        ALTER TABLE public.alm_articulo_bodega
            ADD CONSTRAINT ck_alm_articulo_bodega_reserva
            CHECK (existencia_comprometida >= 0 AND existencia_transito >= 0);
    END IF;
END $$;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- V1) Las 4 tablas nuevas existen (esperado: 4 filas):
-- SELECT table_name FROM information_schema.tables
--  WHERE table_name IN ('alm_requisicion_hdr','alm_descargo_hdr',
--                       'alm_requisicion_correlativo','alm_descargo_correlativo')
--  ORDER BY table_name;
--
-- V2) TODAS las FK nuevas deben ser COMPUESTAS (company_id, ...):
-- SELECT conrelid::regclass AS tabla, conname, pg_get_constraintdef(oid)
--   FROM pg_constraint
--  WHERE contype='f'
--    AND conname IN ('fk_alm_requisicion_hdr_bodega','fk_alm_requisicion_hdr',
--                    'fk_alm_descargo_hdr_bodega','fk_alm_descargo_hdr_requisicion',
--                    'fk_alm_descargo_hdr','fk_alm_descargo_requisicion_linea')
--  ORDER BY 1, 2;
--
-- V3) Correlativos sembrados (empresa 2 → 17124 en ambos):
-- SELECT 'req' AS t, company_id, ultimo_numero FROM alm_requisicion_correlativo
-- UNION ALL SELECT 'des', company_id, ultimo_numero FROM alm_descargo_correlativo ORDER BY 1,2;
--
-- V4) El histórico NO cambió (esperado: 42.866 y 42.757, 100% SIMAFI/posteado):
-- SELECT 'req' AS t, origen, posteado, count(*) FROM alm_requisicion GROUP BY 1,2,3
-- UNION ALL SELECT 'des', origen, posteado, count(*) FROM alm_descargo GROUP BY 1,2,3;
--
-- V5) El backfill satisface el CHECK (esperado: 0):
-- SELECT count(*) FROM alm_requisicion WHERE cantidad_despachada > cantidad;
--
-- V6) El arma cargada quedó desarmada (esperado: 0 filas y índice ausente):
-- SELECT count(*) FROM alm_requisicion WHERE origen='SIAD' AND posteado=false;
-- SELECT indexname FROM pg_indexes WHERE indexname='ix_alm_requisicion_pendiente';
--
-- V7) PRUEBA NEGATIVA — despachar de más debe FALLAR (ck_alm_requisicion_despachada):
-- BEGIN; UPDATE alm_requisicion SET cantidad_despachada = cantidad + 1
--   WHERE id = (SELECT min(id) FROM alm_requisicion); ROLLBACK;
--
-- V8) PRUEBA NEGATIVA — una requisición SIAD posteada debe FALLAR (ck_alm_requisicion_no_postea):
-- BEGIN; UPDATE alm_requisicion SET origen='SIAD', posteado=true
--   WHERE id = (SELECT min(id) FROM alm_requisicion); ROLLBACK;
--   (ojo: trg_alm_requisicion_blindaje también rechaza cambiar `origen`, SQLSTATE K0002)
-- =============================================================================

-- =============================================================================
-- ROLLBACK (en este orden)
-- =============================================================================
-- BEGIN;
-- ALTER TABLE alm_articulo_bodega DROP CONSTRAINT IF EXISTS ck_alm_articulo_bodega_reserva;
-- ALTER TABLE alm_descargo    DROP CONSTRAINT IF EXISTS uq_alm_descargo_renglon;
-- ALTER TABLE alm_descargo    DROP CONSTRAINT IF EXISTS fk_alm_descargo_requisicion_linea;
-- ALTER TABLE alm_descargo    DROP CONSTRAINT IF EXISTS fk_alm_descargo_hdr;
-- ALTER TABLE alm_descargo    DROP COLUMN IF EXISTS requisicion_id, DROP COLUMN IF EXISTS descargo_hdr_id;
-- ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS ck_alm_requisicion_no_postea;
-- ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS ck_alm_requisicion_despachada;
-- ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS fk_alm_requisicion_hdr;
-- ALTER TABLE alm_requisicion DROP COLUMN IF EXISTS aplicado_en_oc,
--     DROP COLUMN IF EXISTS cantidad_despachada, DROP COLUMN IF EXISTS requisicion_hdr_id;
-- CREATE INDEX IF NOT EXISTS ix_alm_requisicion_pendiente
--     ON alm_requisicion(company_id) WHERE origen='SIAD' AND posteado=false;
-- DROP TABLE IF EXISTS alm_descargo_hdr;
-- DROP TABLE IF EXISTS alm_requisicion_hdr;
-- DROP TABLE IF EXISTS alm_descargo_correlativo;
-- DROP TABLE IF EXISTS alm_requisicion_correlativo;
-- ALTER TABLE alm_descargo    DROP CONSTRAINT IF EXISTS uq_alm_descargo_tenant;
-- ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS uq_alm_requisicion_tenant;
-- COMMIT;
-- =============================================================================
