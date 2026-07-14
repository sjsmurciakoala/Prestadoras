-- =============================================================================
-- Documentos de almacén: bodega, estado de posteo y BLINDAJE del histórico SIMAFI
-- Tablas: alm_compra, alm_requisicion, alm_descargo
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- #############################################################################
-- ## ADVERTENCIA — TODA IMPORTACIÓN FUTURA DE DATOS SIMAFI                   ##
-- ##                                                                         ##
-- ## `origen` es NOT NULL y **NO TIENE DEFAULT**. Es DELIBERADO.             ##
-- ##                                                                         ##
-- ## Si mañana se completa la migración desde MySQL ("faltaban las compras   ##
-- ## de 2019"), el INSERT DEBE setear explícitamente:                        ##
-- ##                                                                         ##
-- ##     origen = 'SIMAFI', posteado = true                                  ##
-- ##                                                                         ##
-- ## Si `origen` tuviera DEFAULT 'SIAD', olvidar la columna sería SILENCIOSO:##
-- ## las filas nacerían SIAD + posteado=false, el motor las postearía y      ##
-- ## DUPLICARÍA el inventario — y el centinela NULL ya no podría rescatarlas ##
-- ## (origen es NOT NULL: el UPDATE ... WHERE origen IS NULL no las vería).  ##
-- ## Sin DEFAULT, ese mismo olvido FALLA RUIDOSAMENTE (violación de NOT      ##
-- ## NULL) y obliga a decidir el origen. Fail-closed, no fail-open.          ##
-- ##                                                                         ##
-- ## `posteado` SÍ tiene DEFAULT false: nacer NO posteado es el lado seguro. ##
-- #############################################################################
--
-- POR QUÉ
-- Estas tres tablas son los DOCUMENTOS que originarán los asientos del kardex
-- (libro mayor inmutable, ver Database/2026-07-14_alm_kardex_trazabilidad.sql).
-- Hoy les falta todo lo que el motor de posteo necesita:
--
--   1. bodega_id    -> no hay forma de saber a qué bodega ENTRA (compra) o de cuál
--                      SALE (descargo/requisición) la mercadería. Las columnas legacy
--                      `oficina` y `departamento` son unidades ORGANIZATIVAS, no
--                      bodegas: no son derivables a alm_bodega (ver nota al final).
--   2. posteado /   -> sin bandera de estado, un doble clic postea dos veces.
--      fecha_posteo    Es CACHÉ del estado, NO la garantía de unicidad (esa es el uuid).
--   3. uuid         -> IDENTIDAD del documento-línea (una fila = una línea; son tablas
--                      planas, sin encabezado). Ver "CONTRATO DEL UUID" abajo.
--   4. origen       -> 'SIMAFI' (histórico migrado desde MySQL bdsimafi) contra
--                      'SIAD' (creado por el sistema nuevo).
--
-- CONTRATO DEL UUID — NO ES "EL UUID DEL POSTEO"
--   * documento.uuid se genera al **CREAR** el documento, NO al postearlo, y es
--     INMUTABLE. Es lo que hace idempotente el REINTENTO DEL CLIENTE (doble clic,
--     retry de red): el mismo uuid re-enviado no crea una segunda línea.
--     Una clave generada DURANTE el intento de posteo no protegería de nada: dos
--     posteos concurrentes harían dos Guid.NewGuid() distintos, ambos pasarían el
--     índice único y saldrían DOS asientos.
--   * kardex.uuid se DERIVA determinísticamente del uuid del documento:
--         UUIDv5(documento_tipo | documento.uuid | evento)
--     donde `evento` distingue emisión de recepción. Así una compra que genera DOS
--     asientos (tránsito+ al emitir, existencia+ al recibir) no colisiona consigo
--     misma en uq_alm_kardex_company_uuid.
--   * posteado / fecha_posteo son la CACHÉ del estado. La garantía de no duplicar
--     está en los índices únicos sobre uuid, no en la bandera.
--
-- BLINDAJE DEL HISTÓRICO
-- Estas tablas YA contienen histórico migrado de SIMAFI, cuya existencia ya está
-- reflejada en el inventario. Si el motor llegara a postearlo, DUPLICA el stock.
-- Por eso el histórico se marca origen='SIMAFI' + posteado=true: el motor solo
-- levanta lo pendiente (origen='SIAD' AND posteado=false), así que nunca lo toca.
-- Y trg_alm_<tabla>_blindaje (sección 4) impide que un UPDATE manual lo desarme.
--
-- PATRÓN DEL CENTINELA NULL (por qué el orden de los statements importa)
-- El backfill debe ser idempotente DE VERDAD. La versión ingenua:
--
--     ADD COLUMN origen VARCHAR(10) NOT NULL DEFAULT 'SIAD';   -- MAL
--     UPDATE alm_compra SET origen='SIMAFI', posteado=true
--      WHERE origen='SIAD' AND posteado=false;                 -- MAL
--
-- ...al re-correrse marcaría como SIMAFI+posteado a los documentos SIAD legítimos
-- que aún no se han posteado, dejándolos imposibles de postear PARA SIEMPRE.
--
-- Aquí, en cambio, las filas históricas son las que NO TIENEN MARCA (origen IS NULL),
-- no las que "parecen" SIAD:
--     1. ADD COLUMN nullable y SIN default -> las filas existentes quedan en NULL.
--     2. UPDATE ... WHERE origen IS NULL   -> solo el histórico. Re-correr: 0 filas.
--     3. Recién entonces SET NOT NULL (y el DEFAULT de posteado), para las filas NUEVAS.
--
-- Un DEFAULT, cuando lo hay, va SIEMPRE en un ALTER COLUMN ... SET DEFAULT posterior,
-- nunca en el ADD COLUMN: en Postgres 11+, "ADD COLUMN ... DEFAULT <expr>" evalúa y
-- ESTAMPA el default a las filas existentes — lo que destruiría el centinela (misma
-- lección que fechacreacion en 2026-07-14_alm_kardex_trazabilidad.sql).
--
-- Se escriben las tres tablas explícitas, sin bucle: la legibilidad del blindaje
-- importa más que la brevedad.
--
-- Cambio aditivo: no altera columnas ni datos existentes.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 0. Guardianes fuera (para que el script sea re-ejecutable)
-- -----------------------------------------------------------------------------
-- El backfill de más abajo hace origen: NULL -> 'SIMAFI', y el guardián
-- trg_alm_<tabla>_blindaje (sección 4) rechaza cambiar `origen`. En la PRIMERA
-- corrida el trigger todavía no existe; en una RE-corrida sí existiría — y aunque
-- el UPDATE afectaría 0 filas (origen ya es NOT NULL) y el trigger no llegaría a
-- dispararse, no se deja depender de eso: se sueltan los guardianes al inicio y se
-- recrean al final, dentro de la MISMA transacción.
DROP TRIGGER IF EXISTS trg_alm_compra_blindaje      ON alm_compra;
DROP TRIGGER IF EXISTS trg_alm_requisicion_blindaje ON alm_requisicion;
DROP TRIGGER IF EXISTS trg_alm_descargo_blindaje    ON alm_descargo;

-- -----------------------------------------------------------------------------
-- 1. alm_compra
-- -----------------------------------------------------------------------------
-- 1.a Columnas NULLABLE y SIN default: las filas existentes quedan en NULL.
ALTER TABLE alm_compra
    ADD COLUMN IF NOT EXISTS bodega_id    INT REFERENCES alm_bodega(id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS posteado     BOOLEAN,
    ADD COLUMN IF NOT EXISTS fecha_posteo TIMESTAMP WITHOUT TIME ZONE,
    ADD COLUMN IF NOT EXISTS uuid         UUID,
    ADD COLUMN IF NOT EXISTS origen       VARCHAR(10);

-- 1.b BLINDAJE: solo las filas SIN MARCAR son el histórico SIMAFI. Re-correr no toca nada.
-- uuid y fecha_posteo quedan NULL a propósito: el histórico nunca pasó por el motor
-- y no sabemos cuándo se registró en SIMAFI.
UPDATE alm_compra SET origen = 'SIMAFI', posteado = true WHERE origen IS NULL;

-- 1.c Recién ahora: NOT NULL (y el default de posteado), que aplican a las filas NUEVAS.
-- origen NO lleva DEFAULT: ver la ADVERTENCIA de la cabecera.
ALTER TABLE alm_compra
    ALTER COLUMN origen   SET NOT NULL,
    ALTER COLUMN posteado SET DEFAULT false,
    ALTER COLUMN posteado SET NOT NULL;

-- 1.d Vocabulario cerrado (espejo de SIAD.Core.Constants.OrigenDocumento).
ALTER TABLE alm_compra DROP CONSTRAINT IF EXISTS ck_alm_compra_origen;
ALTER TABLE alm_compra ADD CONSTRAINT ck_alm_compra_origen
    CHECK (origen IN ('SIMAFI', 'SIAD'));

-- 1.e La bodega es obligatoria en documentos NUEVOS, no en el histórico.
ALTER TABLE alm_compra DROP CONSTRAINT IF EXISTS ck_alm_compra_bodega_si_siad;
ALTER TABLE alm_compra ADD CONSTRAINT ck_alm_compra_bodega_si_siad
    CHECK (origen = 'SIMAFI' OR bodega_id IS NOT NULL);

-- 1.f El uuid (identidad del documento-línea) es obligatorio en documentos NUEVOS.
ALTER TABLE alm_compra DROP CONSTRAINT IF EXISTS ck_alm_compra_uuid_si_siad;
ALTER TABLE alm_compra ADD CONSTRAINT ck_alm_compra_uuid_si_siad
    CHECK (origen = 'SIMAFI' OR uuid IS NOT NULL);

-- 1.g Un documento SIAD posteado DEBE tener su evidencia (uuid + fecha_posteo).
-- Sin esto, un SIAD con posteado=true y sin evidencia es un documento PERDIDO: el
-- motor no lo levanta (ya está "posteado") y no hay asiento que lo respalde.
ALTER TABLE alm_compra DROP CONSTRAINT IF EXISTS ck_alm_compra_posteo_evidencia;
ALTER TABLE alm_compra ADD CONSTRAINT ck_alm_compra_posteo_evidencia
    CHECK (origen = 'SIMAFI' OR posteado = false
           OR (uuid IS NOT NULL AND fecha_posteo IS NOT NULL));

-- 1.h Idempotencia: el mismo documento-línea no puede existir dos veces en la empresa.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_compra_company_uuid
    ON alm_compra(company_id, uuid) WHERE uuid IS NOT NULL;

-- 1.i Índice PARCIAL de lo pendiente: es lo único que el motor busca, y hoy el 100%
-- de las filas son posteado=true (histórico). Indexarlas todas sería desperdicio.
DROP INDEX IF EXISTS ix_alm_compra_posteo;
CREATE INDEX IF NOT EXISTS ix_alm_compra_pendiente
    ON alm_compra(company_id) WHERE origen = 'SIAD' AND posteado = false;

-- 1.j FK sin índice.
CREATE INDEX IF NOT EXISTS ix_alm_compra_bodega ON alm_compra(bodega_id);

-- -----------------------------------------------------------------------------
-- 2. alm_requisicion
-- -----------------------------------------------------------------------------
-- 2.a Columnas NULLABLE y SIN default: las filas existentes quedan en NULL.
ALTER TABLE alm_requisicion
    ADD COLUMN IF NOT EXISTS bodega_id    INT REFERENCES alm_bodega(id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS posteado     BOOLEAN,
    ADD COLUMN IF NOT EXISTS fecha_posteo TIMESTAMP WITHOUT TIME ZONE,
    ADD COLUMN IF NOT EXISTS uuid         UUID,
    ADD COLUMN IF NOT EXISTS origen       VARCHAR(10);

-- 2.b BLINDAJE: solo las filas SIN MARCAR son el histórico SIMAFI. Re-correr no toca nada.
UPDATE alm_requisicion SET origen = 'SIMAFI', posteado = true WHERE origen IS NULL;

-- 2.c Recién ahora: NOT NULL (y el default de posteado). origen NO lleva DEFAULT.
ALTER TABLE alm_requisicion
    ALTER COLUMN origen   SET NOT NULL,
    ALTER COLUMN posteado SET DEFAULT false,
    ALTER COLUMN posteado SET NOT NULL;

-- 2.d Vocabulario cerrado (espejo de SIAD.Core.Constants.OrigenDocumento).
ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS ck_alm_requisicion_origen;
ALTER TABLE alm_requisicion ADD CONSTRAINT ck_alm_requisicion_origen
    CHECK (origen IN ('SIMAFI', 'SIAD'));

-- 2.e La bodega es obligatoria en documentos NUEVOS, no en el histórico.
ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS ck_alm_requisicion_bodega_si_siad;
ALTER TABLE alm_requisicion ADD CONSTRAINT ck_alm_requisicion_bodega_si_siad
    CHECK (origen = 'SIMAFI' OR bodega_id IS NOT NULL);

-- 2.f El uuid (identidad del documento-línea) es obligatorio en documentos NUEVOS.
ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS ck_alm_requisicion_uuid_si_siad;
ALTER TABLE alm_requisicion ADD CONSTRAINT ck_alm_requisicion_uuid_si_siad
    CHECK (origen = 'SIMAFI' OR uuid IS NOT NULL);

-- 2.g Un documento SIAD posteado DEBE tener su evidencia (uuid + fecha_posteo).
ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS ck_alm_requisicion_posteo_evidencia;
ALTER TABLE alm_requisicion ADD CONSTRAINT ck_alm_requisicion_posteo_evidencia
    CHECK (origen = 'SIMAFI' OR posteado = false
           OR (uuid IS NOT NULL AND fecha_posteo IS NOT NULL));

-- 2.h Idempotencia.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_requisicion_company_uuid
    ON alm_requisicion(company_id, uuid) WHERE uuid IS NOT NULL;

-- 2.i Índice PARCIAL de lo pendiente.
DROP INDEX IF EXISTS ix_alm_requisicion_posteo;
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_pendiente
    ON alm_requisicion(company_id) WHERE origen = 'SIAD' AND posteado = false;

-- 2.j FK sin índice.
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_bodega ON alm_requisicion(bodega_id);

-- -----------------------------------------------------------------------------
-- 3. alm_descargo
-- -----------------------------------------------------------------------------
-- 3.a Columnas NULLABLE y SIN default: las filas existentes quedan en NULL.
ALTER TABLE alm_descargo
    ADD COLUMN IF NOT EXISTS bodega_id    INT REFERENCES alm_bodega(id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS posteado     BOOLEAN,
    ADD COLUMN IF NOT EXISTS fecha_posteo TIMESTAMP WITHOUT TIME ZONE,
    ADD COLUMN IF NOT EXISTS uuid         UUID,
    ADD COLUMN IF NOT EXISTS origen       VARCHAR(10);

-- 3.b BLINDAJE: solo las filas SIN MARCAR son el histórico SIMAFI. Re-correr no toca nada.
UPDATE alm_descargo SET origen = 'SIMAFI', posteado = true WHERE origen IS NULL;

-- 3.c Recién ahora: NOT NULL (y el default de posteado). origen NO lleva DEFAULT.
ALTER TABLE alm_descargo
    ALTER COLUMN origen   SET NOT NULL,
    ALTER COLUMN posteado SET DEFAULT false,
    ALTER COLUMN posteado SET NOT NULL;

-- 3.d Vocabulario cerrado (espejo de SIAD.Core.Constants.OrigenDocumento).
ALTER TABLE alm_descargo DROP CONSTRAINT IF EXISTS ck_alm_descargo_origen;
ALTER TABLE alm_descargo ADD CONSTRAINT ck_alm_descargo_origen
    CHECK (origen IN ('SIMAFI', 'SIAD'));

-- 3.e La bodega es obligatoria en documentos NUEVOS, no en el histórico.
ALTER TABLE alm_descargo DROP CONSTRAINT IF EXISTS ck_alm_descargo_bodega_si_siad;
ALTER TABLE alm_descargo ADD CONSTRAINT ck_alm_descargo_bodega_si_siad
    CHECK (origen = 'SIMAFI' OR bodega_id IS NOT NULL);

-- 3.f El uuid (identidad del documento-línea) es obligatorio en documentos NUEVOS.
ALTER TABLE alm_descargo DROP CONSTRAINT IF EXISTS ck_alm_descargo_uuid_si_siad;
ALTER TABLE alm_descargo ADD CONSTRAINT ck_alm_descargo_uuid_si_siad
    CHECK (origen = 'SIMAFI' OR uuid IS NOT NULL);

-- 3.g Un documento SIAD posteado DEBE tener su evidencia (uuid + fecha_posteo).
ALTER TABLE alm_descargo DROP CONSTRAINT IF EXISTS ck_alm_descargo_posteo_evidencia;
ALTER TABLE alm_descargo ADD CONSTRAINT ck_alm_descargo_posteo_evidencia
    CHECK (origen = 'SIMAFI' OR posteado = false
           OR (uuid IS NOT NULL AND fecha_posteo IS NOT NULL));

-- 3.h Idempotencia.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_descargo_company_uuid
    ON alm_descargo(company_id, uuid) WHERE uuid IS NOT NULL;

-- 3.i Índice PARCIAL de lo pendiente.
DROP INDEX IF EXISTS ix_alm_descargo_posteo;
CREATE INDEX IF NOT EXISTS ix_alm_descargo_pendiente
    ON alm_descargo(company_id) WHERE origen = 'SIAD' AND posteado = false;

-- 3.j FK sin índice.
CREATE INDEX IF NOT EXISTS ix_alm_descargo_bodega ON alm_descargo(bodega_id);

-- -----------------------------------------------------------------------------
-- 4. Guardián del blindaje (trigger BEFORE UPDATE en las tres tablas)
-- -----------------------------------------------------------------------------
-- Los CHECK protegen la FORMA de la fila, pero nada impedía hoy un
--     UPDATE alm_compra SET origen='SIAD', posteado=false WHERE ...;
-- desde una consola psql o un script de "arreglo" — y eso deja el histórico SIMAFI
-- listo para que el motor lo postee y DUPLIQUE el inventario. Este trigger es, para
-- los documentos, el equivalente de trg_alm_kardex_inmutable.
--
-- Rechaza:
--   * cambiar `origen` (una vez fijado es INMUTABLE: un documento no cambia de mundo);
--   * devolver `posteado` de true a false cuando origen='SIMAFI' (des-blindar el histórico).
-- Permite: des-postear un documento SIAD (la reversa/corrección es un flujo legítimo).
--
-- SQLSTATE propio K0002 para distinguirlo programáticamente desde Npgsql
-- (PostgresException.SqlState) sin parsear el texto. K0001 es el del kardex inmutable.
CREATE OR REPLACE FUNCTION alm_documento_blindaje() RETURNS trigger AS $$
BEGIN
    IF NEW.origen IS DISTINCT FROM OLD.origen THEN
        RAISE EXCEPTION
            '%: el origen de un documento de almacén es inmutable (% -> %). El histórico SIMAFI no puede convertirse en SIAD.',
            TG_TABLE_NAME, OLD.origen, NEW.origen
            USING ERRCODE = 'K0002';
    END IF;

    IF OLD.origen = 'SIMAFI' AND OLD.posteado AND NOT NEW.posteado THEN
        RAISE EXCEPTION
            '%: el histórico SIMAFI no puede des-postearse (posteado true -> false). Su existencia ya está en el inventario; postearlo lo DUPLICARÍA.',
            TG_TABLE_NAME
            USING ERRCODE = 'K0002';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION alm_documento_blindaje() IS 'Guardián del blindaje de los documentos de almacén (alm_compra, alm_requisicion, alm_descargo): rechaza cambiar `origen` (inmutable) y rechaza des-postear el histórico SIMAFI. SQLSTATE K0002.';

CREATE TRIGGER trg_alm_compra_blindaje
    BEFORE UPDATE ON alm_compra
    FOR EACH ROW EXECUTE FUNCTION alm_documento_blindaje();

CREATE TRIGGER trg_alm_requisicion_blindaje
    BEFORE UPDATE ON alm_requisicion
    FOR EACH ROW EXECUTE FUNCTION alm_documento_blindaje();

CREATE TRIGGER trg_alm_descargo_blindaje
    BEFORE UPDATE ON alm_descargo
    FOR EACH ROW EXECUTE FUNCTION alm_documento_blindaje();

-- ESCOTILLA DE ESCAPE — corrección puntual con el guardián activo.
-- Es excepcional y debe quedar documentada en un script fechado en Database/:
--     ALTER TABLE alm_compra DISABLE TRIGGER trg_alm_compra_blindaje;
--     -- ... corrección puntual, documentada ...
--     ALTER TABLE alm_compra ENABLE TRIGGER trg_alm_compra_blindaje;

-- -----------------------------------------------------------------------------
-- 5. Comentarios de contrato
-- -----------------------------------------------------------------------------
COMMENT ON COLUMN alm_compra.bodega_id    IS 'Bodega que RECIBE la mercadería (FK a alm_bodega). Obligatoria en documentos SIAD (ck_alm_compra_bodega_si_siad); NULL en el histórico SIMAFI, que no la traía.';
COMMENT ON COLUMN alm_compra.posteado     IS 'CACHÉ del estado: true = esta línea ya generó su asiento en alm_kardex. La garantía de no duplicar NO es esta bandera, sino los índices únicos sobre uuid. El histórico SIMAFI viene con true (blindaje: su existencia ya está en el inventario, postearlo lo DUPLICARÍA).';
COMMENT ON COLUMN alm_compra.fecha_posteo IS 'Fecha/hora UTC en que el motor posteó esta línea. NULL mientras no se postee, y siempre en el histórico SIMAFI (nunca pasó por el motor).';
COMMENT ON COLUMN alm_compra.uuid         IS 'IDENTIDAD del documento-línea, generada al CREAR el documento (NO al postear). Inmutable. Es lo que hace idempotente el reintento del cliente. El uuid del asiento en alm_kardex se DERIVA de éste: UUIDv5(documento_tipo | uuid | evento), donde evento distingue emisión de recepción. NULL solo en el histórico SIMAFI.';
COMMENT ON COLUMN alm_compra.origen       IS 'SIMAFI = histórico migrado (nunca posteable por el motor) | SIAD = creado por el sistema nuevo. Vocabulario cerrado, espejo de SIAD.Core.Constants.OrigenDocumento. SIN DEFAULT a propósito: toda importación SIMAFI debe declararlo explícitamente. Inmutable (trg_alm_compra_blindaje).';

COMMENT ON COLUMN alm_requisicion.bodega_id    IS 'Bodega de la que SALE la mercadería (FK a alm_bodega). Obligatoria en documentos SIAD (ck_alm_requisicion_bodega_si_siad); NULL en el histórico SIMAFI, que no la traía.';
COMMENT ON COLUMN alm_requisicion.posteado     IS 'CACHÉ del estado: true = esta línea ya generó su asiento en alm_kardex. La garantía de no duplicar son los índices únicos sobre uuid. El histórico SIMAFI viene con true (blindaje).';
COMMENT ON COLUMN alm_requisicion.fecha_posteo IS 'Fecha/hora UTC en que el motor posteó esta línea. NULL mientras no se postee, y siempre en el histórico SIMAFI.';
COMMENT ON COLUMN alm_requisicion.uuid         IS 'IDENTIDAD del documento-línea, generada al CREAR el documento (NO al postear). Inmutable. Hace idempotente el reintento del cliente. El uuid del asiento en alm_kardex se DERIVA de éste: UUIDv5(documento_tipo | uuid | evento). NULL solo en el histórico SIMAFI.';
COMMENT ON COLUMN alm_requisicion.origen       IS 'SIMAFI = histórico migrado (nunca posteable) | SIAD = creado por el sistema nuevo. Espejo de SIAD.Core.Constants.OrigenDocumento. SIN DEFAULT a propósito: toda importación SIMAFI debe declararlo explícitamente. Inmutable (trg_alm_requisicion_blindaje).';

COMMENT ON COLUMN alm_descargo.bodega_id    IS 'Bodega de la que SALE la mercadería (FK a alm_bodega). Obligatoria en documentos SIAD (ck_alm_descargo_bodega_si_siad); NULL en el histórico SIMAFI, que no la traía.';
COMMENT ON COLUMN alm_descargo.posteado     IS 'CACHÉ del estado: true = esta línea ya generó su asiento en alm_kardex. La garantía de no duplicar son los índices únicos sobre uuid. El histórico SIMAFI viene con true (blindaje).';
COMMENT ON COLUMN alm_descargo.fecha_posteo IS 'Fecha/hora UTC en que el motor posteó esta línea. NULL mientras no se postee, y siempre en el histórico SIMAFI.';
COMMENT ON COLUMN alm_descargo.uuid         IS 'IDENTIDAD del documento-línea, generada al CREAR el documento (NO al postear). Inmutable. Hace idempotente el reintento del cliente. El uuid del asiento en alm_kardex se DERIVA de éste: UUIDv5(documento_tipo | uuid | evento). NULL solo en el histórico SIMAFI.';
COMMENT ON COLUMN alm_descargo.origen       IS 'SIMAFI = histórico migrado (nunca posteable) | SIAD = creado por el sistema nuevo. Espejo de SIAD.Core.Constants.OrigenDocumento. SIN DEFAULT a propósito: toda importación SIMAFI debe declararlo explícitamente. Inmutable (trg_alm_descargo_blindaje).';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr DESPUÉS de aplicar)
--
-- 1) Blindaje: hoy las tres tablas solo tienen histórico, así que NO debe aparecer
--    ninguna fila con origen='SIAD'. Todo debe ser origen='SIMAFI' + posteado=true.
--
-- SELECT 'compra' t, origen, posteado, count(*) FROM alm_compra GROUP BY 2,3
-- UNION ALL SELECT 'descargo', origen, posteado, count(*) FROM alm_descargo GROUP BY 2,3
-- UNION ALL SELECT 'requisicion', origen, posteado, count(*) FROM alm_requisicion GROUP BY 2,3;
--
-- 2) Nada pendiente de postear (el motor no debe encontrar NADA del histórico):
--
-- SELECT 'compra' t, count(*) FROM alm_compra           WHERE origen='SIAD' AND posteado=false
-- UNION ALL SELECT 'descargo', count(*) FROM alm_descargo       WHERE origen='SIAD' AND posteado=false
-- UNION ALL SELECT 'requisicion', count(*) FROM alm_requisicion WHERE origen='SIAD' AND posteado=false;
--
-- 3) El guardián muerde (ambos deben fallar con SQLSTATE K0002):
--
-- UPDATE alm_compra SET origen  = 'SIAD' WHERE id = (SELECT min(id) FROM alm_compra);
-- UPDATE alm_compra SET posteado = false WHERE id = (SELECT min(id) FROM alm_compra);
-- =============================================================================

-- -----------------------------------------------------------------------------
-- NOTA DE INVESTIGACIÓN — ¿se podía DERIVAR bodega_id para el histórico?
-- No. Ninguna de las tres tablas tiene columna de bodega:
--   * alm_compra      -> oficina VARCHAR(5), traslado VARCHAR(1)
--   * alm_requisicion -> oficina VARCHAR(20), departamento VARCHAR(3)
--   * alm_descargo    -> oficina VARCHAR(5), departamento VARCHAR(2), traslado VARCHAR(1)
-- `oficina` y `departamento` son unidades ORGANIZATIVAS (quién pide / a quién se le
-- carga el consumo), no ubicaciones físicas de stock: no se corresponden con el
-- catálogo alm_bodega. La única tabla del módulo que traía bodega desde SIMAFI es
-- alm_kardex, con su columna legacy `bodega` VARCHAR(2) ('01','11',''), normalizada
-- en Database/2026-07-09_alm_kardex_bodega_id.sql — y esa columna NO tiene
-- equivalente en los documentos.
-- Aunque hubiese algo derivable, NO se backfillearía: el histórico está blindado
-- (posteado=true) y nunca pasará por el motor, así que su bodega_id es irrelevante.
-- -----------------------------------------------------------------------------
