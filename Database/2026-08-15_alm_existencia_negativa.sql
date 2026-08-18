-- =============================================================================
-- Almacén: permitir EXISTENCIA NEGATIVA en salidas de inventario (interruptor)
-- Fecha: 2026-08-15
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- En la operación real el almacenero físicamente tiene el material y lo entrega
-- aunque el sistema ya marque 0 (desfase físico vs. sistema). Hoy el motor de
-- inventario (SIAD.Services/Almacen/InventarioPostingService.ValidarAsync) BLOQUEA
-- cualquier salida que dejaría la existencia en negativo. Se quiere PERMITIR el
-- negativo (para reflejar el físico y reconciliar después), pero NO abierto para
-- todos: detrás de un interruptor por EMPRESA con override opcional por BODEGA.
--
-- QUÉ HACE (aditivo, en dos piezas)
--   1. Tabla cfg_inventario_negativo (una fila por empresa, la PK ES el company_id):
--      columna 'permitir' boolean. Interruptor MAESTRO de la empresa. Nace en false
--      (= comportamiento actual). Semilla idempotente: una fila 'false' por cada
--      empresa que ya tenga artículos.
--   2. Columna alm_bodega.permite_existencia_negativa boolean NULL: override por
--      bodega, TRI-ESTADO -> NULL = hereda de la empresa · true = fuerza permitir ·
--      false = fuerza bloquear. Nace NULL en TODAS las bodegas (= hereda, sin cambio).
--
--   Efectivo resuelto por el motor = override_bodega ?? interruptor_empresa.
--
-- SIN parte contable ni de negocio destructiva: solo agrega el interruptor. El motor
-- lo lee (F1). Nada nace activado, así que el bloqueo sigue igual que hoy hasta que
-- alguien active el interruptor a mano.
--
-- Cambio ADITIVO. No borra ni modifica ningún dato de negocio. Tabla nueva sin FK +
-- columna nueva NULL sin default (sin backfill, sin NOT NULL).
-- IDEMPOTENTE: CREATE TABLE IF NOT EXISTS + ADD COLUMN IF NOT EXISTS +
--              INSERT ... ON CONFLICT DO NOTHING.
-- REVERSIBLE: DROP TABLE + DROP COLUMN (ver ROLLBACK al final).
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Interruptor maestro por empresa
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.cfg_inventario_negativo (
    company_id           BIGINT       PRIMARY KEY,
    permitir             BOOLEAN      NOT NULL DEFAULT false,
    usuariocreacion      VARCHAR(100) NULL,
    fechacreacion        TIMESTAMP WITHOUT TIME ZONE NULL,
    usuariomodificacion  VARCHAR(100) NULL,
    fechamodificacion    TIMESTAMP WITHOUT TIME ZONE NULL
);

COMMENT ON TABLE public.cfg_inventario_negativo IS
    'Interruptor por empresa (la PK ES el company_id) para permitir existencia negativa en salidas de inventario. Nace en false = comportamiento actual (el motor bloquea la salida a negativo). El override por bodega (alm_bodega.permite_existencia_negativa) gana sobre este cuando no es NULL.';
COMMENT ON COLUMN public.cfg_inventario_negativo.permitir IS
    'true = las salidas de la empresa pueden dejar la existencia en negativo (salvo que una bodega lo fuerce a false). false (default) = el motor rechaza toda salida que cruce a negativo.';

-- Semilla: una fila 'false' (default, conservador) por empresa que ya tenga artículos.
INSERT INTO public.cfg_inventario_negativo (company_id, usuariocreacion, fechacreacion)
SELECT DISTINCT a.company_id, 'system', now()
  FROM public.alm_articulo a
ON CONFLICT (company_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 2) Override opcional por bodega (tri-estado)
-- ---------------------------------------------------------------------------
ALTER TABLE public.alm_bodega
    ADD COLUMN IF NOT EXISTS permite_existencia_negativa BOOLEAN NULL;

COMMENT ON COLUMN public.alm_bodega.permite_existencia_negativa IS
    'Override del interruptor de existencia negativa, por bodega. TRI-ESTADO: NULL = hereda de cfg_inventario_negativo.permitir de la empresa; true = fuerza PERMITIR aquí; false = fuerza BLOQUEAR aquí. Nace NULL en todas las bodegas (= hereda, sin cambio de comportamiento).';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) La tabla existe con su PK y su default:
-- SELECT column_name, data_type, is_nullable, column_default
--   FROM information_schema.columns
--  WHERE table_name = 'cfg_inventario_negativo' ORDER BY ordinal_position;
--
-- 2) Una fila por empresa con artículos, todas en false tras la semilla:
-- SELECT company_id, permitir FROM public.cfg_inventario_negativo ORDER BY company_id;
--
-- 3) La columna nueva existe, es nullable y todas las bodegas quedaron en NULL:
-- SELECT count(*) AS total,
--        count(*) FILTER (WHERE permite_existencia_negativa IS NULL) AS en_null
--   FROM public.alm_bodega;
--
-- 4) Idempotencia: volver a correr el script no debe fallar ni duplicar filas.
--
-- =============================================================================
-- ROLLBACK (solo si hay que revertir; se pierde el interruptor configurado)
-- =============================================================================
-- BEGIN;
-- ALTER TABLE public.alm_bodega DROP COLUMN IF EXISTS permite_existencia_negativa;
-- DROP TABLE IF EXISTS public.cfg_inventario_negativo;
-- COMMIT;
-- =============================================================================
