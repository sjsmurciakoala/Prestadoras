-- =============================================================================
-- Almacén: clasificación de ISV en compras por tipo de artículo
--          (alm_tipo_articulo.impuesto_tasa_id)
-- Fecha: 2026-07-30
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- El catálogo de impuestos (cfg_impuesto + cfg_impuesto_tasa) dice CUÁNTO es el ISV
-- y desde cuándo rige, pero nada en el sistema decía si una compra de artículos LO
-- APLICA o no. Este cambio permite configurar esa decisión POR TIPO DE ARTÍCULO
-- (los 9 grupos), que es el mismo eje con que el tipo ya lleva sus cuentas y su
-- bandera maneja_inventario:
--   - tipo con una tasa GRAVADA (ej. ISV 15%)  -> sus compras registran el ISV y
--     este se suma al costo del artículo;
--   - tipo con una tasa EXENTA, o sin tasa (NULL) -> sus compras no registran ISV.
-- La tasa (porcentaje y vigencia) se REUTILIZA del catálogo cfg_impuesto_tasa; aquí
-- solo se guarda a cuál apunta cada tipo. No hay tabla nueva ni nada contable
-- (cuentas de crédito fiscal / asientos quedan explícitamente fuera de alcance).
--
-- QUÉ HACE
--   1. alm_tipo_articulo.impuesto_tasa_id INT NULL. Nace NULL en todos los tipos
--      existentes: ninguno cambia de comportamiento hasta que el usuario le asigne
--      una tasa desde Mantenimientos -> Tipos de artículo.
--   2. FK a cfg_impuesto_tasa(id) ON DELETE RESTRICT: una tasa referida por un tipo
--      no se puede borrar en cascada (igual criterio que cfg_impuesto_tasa -> cfg_impuesto).
--   3. Índice parcial sobre impuesto_tasa_id (solo filas con tasa asignada), para
--      las consultas que resuelven la tasa del tipo.
--
-- Cambio ADITIVO de estructura. No borra ni modifica ningún dato de negocio.
-- IDEMPOTENTE: ADD COLUMN IF NOT EXISTS + FK creado solo si no existe + CREATE INDEX IF NOT EXISTS.
-- REVERSIBLE: DROP de la FK, el índice y la columna (ver ROLLBACK al final).
--
-- PRERREQUISITO: la tabla cfg_impuesto_tasa (catálogo del ISV, script
-- 2026-07-14_cfg_impuestos.sql) debe existir en la base destino. Si falta, este
-- script se detiene con un mensaje claro y NO deja nada a medias.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 0) Guarda de prerrequisito: sin el catálogo del ISV, la FK no se puede crear.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF to_regclass('public.cfg_impuesto_tasa') IS NULL THEN
        RAISE EXCEPTION 'Prerrequisito faltante: la tabla cfg_impuesto_tasa no existe. Aplique primero Database/2026-07-14_cfg_impuestos.sql y vuelva a correr este script.';
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 1) Columna: a qué tasa del ISV apunta el tipo de artículo (NULL = no aplica ISV)
-- ---------------------------------------------------------------------------
ALTER TABLE public.alm_tipo_articulo
    ADD COLUMN IF NOT EXISTS impuesto_tasa_id integer NULL;

COMMENT ON COLUMN public.alm_tipo_articulo.impuesto_tasa_id IS
    'Tasa de ISV (cfg_impuesto_tasa) que aplica a las COMPRAS de artículos de este tipo. Una tasa GRAVADA hace que el ISV se registre y se sume al costo; una tasa EXENTA o NULL = no registra ISV. Reutiliza el porcentaje y la vigencia del catálogo global; aquí solo se guarda la referencia. Sin implicaciones contables (crédito fiscal fuera de alcance).';

-- ---------------------------------------------------------------------------
-- 2) FK a cfg_impuesto_tasa (ON DELETE RESTRICT). Idempotente por nombre.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_tipo_articulo_impuesto_tasa'
    ) THEN
        ALTER TABLE public.alm_tipo_articulo
            ADD CONSTRAINT fk_alm_tipo_articulo_impuesto_tasa
            FOREIGN KEY (impuesto_tasa_id)
            REFERENCES public.cfg_impuesto_tasa(id)
            ON DELETE RESTRICT;
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 3) Índice parcial: solo los tipos que tienen tasa asignada
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_alm_tipo_articulo_impuesto_tasa
    ON public.alm_tipo_articulo(impuesto_tasa_id)
    WHERE impuesto_tasa_id IS NOT NULL;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) La columna existe y es nullable:
-- SELECT column_name, data_type, is_nullable
--   FROM information_schema.columns
--  WHERE table_name = 'alm_tipo_articulo' AND column_name = 'impuesto_tasa_id';
--
-- 2) Todos los tipos nacieron sin tasa (con_tasa = 0 tras aplicar):
-- SELECT count(*) AS total,
--        count(impuesto_tasa_id) AS con_tasa
--   FROM public.alm_tipo_articulo;
--
-- 3) La FK existe y apunta a cfg_impuesto_tasa:
-- SELECT conname, confrelid::regclass AS referencia, confdeltype
--   FROM pg_constraint WHERE conname = 'fk_alm_tipo_articulo_impuesto_tasa';
--   -- confdeltype 'r' = RESTRICT
--
-- 4) El índice parcial existe:
-- SELECT indexname, indexdef FROM pg_indexes
--  WHERE tablename = 'alm_tipo_articulo' AND indexname = 'ix_alm_tipo_articulo_impuesto_tasa';
--
-- 5) Las tasas del ISV disponibles para asignar (lo que verá el selector):
-- SELECT t.id, t.codigo, t.nombre, t.tipo, t.porcentaje, t.vigencia_desde, t.vigencia_hasta
--   FROM public.cfg_impuesto_tasa t
--   JOIN public.cfg_impuesto i ON i.id = t.impuesto_id
--  WHERE i.codigo = 'ISV' AND t.activo
--  ORDER BY t.tipo, t.porcentaje;
--
-- 6) Idempotencia: volver a correr el script no debe fallar ni cambiar conteos.
--
-- =============================================================================
-- ROLLBACK (solo si hay que revertir; se pierde qué tasa tenía cada tipo)
-- =============================================================================
-- BEGIN;
-- DROP INDEX IF EXISTS ix_alm_tipo_articulo_impuesto_tasa;
-- ALTER TABLE public.alm_tipo_articulo DROP CONSTRAINT IF EXISTS fk_alm_tipo_articulo_impuesto_tasa;
-- ALTER TABLE public.alm_tipo_articulo DROP COLUMN IF EXISTS impuesto_tasa_id;
-- COMMIT;
-- =============================================================================
