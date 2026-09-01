-- =============================================================================
-- Compras: política del ISV en compras, por empresa (cfg_compra_isv)
-- Fecha: 2026-07-30
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- Es la SEGUNDA capa de la configuración del ISV en compras. La primera
-- (alm_tipo_articulo.impuesto_tasa_id, script 2026-07-30_alm_tipo_articulo_impuesto_tasa.sql)
-- define, por tipo de artículo, SI la compra lleva ISV y de cuánto (gravado/exento).
-- Esta capa define, UNA sola vez por empresa, QUÉ se hace con ese ISV:
--   - COSTO   -> el ISV se suma al costo del artículo (el material vale más);
--   - FISCAL  -> el ISV se registra como impuesto (fiscal), separado del costo.
-- Es una decisión de la empresa (transversal a todo lo que compra), no del artículo,
-- por eso vive en su propia fila por empresa y NO en almacén: la tabla
-- alm_config_inventario declara explícitamente que la política del ISV no va allí,
-- para no obligar a compras/contabilidad a depender del almacén.
--
-- SIN parte contable: este script solo guarda la DECISIÓN (una bandera). Construir el
-- asiento del crédito fiscal (cuenta, matriz de integración) queda fuera de alcance.
--
-- QUÉ HACE
--   1. Tabla cfg_compra_isv (una fila por empresa, la PK ES el company_id).
--   2. CHECK del vocabulario tratamiento ∈ ('COSTO','FISCAL').
--   3. Semilla idempotente: una fila 'COSTO' por cada empresa que ya tenga artículos.
--      COSTO es el comportamiento por defecto (conservador) y es editable desde la app.
--
-- Cambio ADITIVO. No borra ni modifica ningún dato de negocio. Tabla nueva, sin FK.
-- IDEMPOTENTE: CREATE TABLE IF NOT EXISTS + INSERT ... ON CONFLICT DO NOTHING.
-- REVERSIBLE: DROP TABLE (ver ROLLBACK al final).
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Política del ISV en compras, por empresa
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.cfg_compra_isv (
    company_id           BIGINT       PRIMARY KEY,
    tratamiento          VARCHAR(10)  NOT NULL DEFAULT 'COSTO',
    usuariocreacion      VARCHAR(100) NULL,
    fechacreacion        TIMESTAMP WITHOUT TIME ZONE NULL,
    usuariomodificacion  VARCHAR(100) NULL,
    fechamodificacion    TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT ck_cfg_compra_isv_tratamiento
        CHECK (tratamiento IN ('COSTO', 'FISCAL'))
);

COMMENT ON TABLE public.cfg_compra_isv IS
    'Política del ISV en compras, una fila por empresa (la PK ES el company_id). Segunda capa de la configuración del ISV: la primera (alm_tipo_articulo.impuesto_tasa_id) dice cuánto ISV lleva cada compra; esta dice qué se hace con él. Sin parte contable: solo guarda la decisión.';
COMMENT ON COLUMN public.cfg_compra_isv.tratamiento IS
    'COSTO = el ISV pagado se suma al costo del artículo. FISCAL = el ISV se registra como impuesto (fiscal), separado del costo. Espejo de la constante SIAD.Core.Constants.TratamientoIsvCompra: agregar un valor obliga a ampliar este CHECK.';

-- Semilla: una fila 'COSTO' (default) por empresa que ya tenga artículos.
INSERT INTO public.cfg_compra_isv (company_id, usuariocreacion, fechacreacion)
SELECT DISTINCT a.company_id, 'system', now()
  FROM public.alm_articulo a
ON CONFLICT (company_id) DO NOTHING;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) La tabla existe con su CHECK:
-- SELECT pg_get_constraintdef(oid) FROM pg_constraint
--  WHERE conname = 'ck_cfg_compra_isv_tratamiento';
--
-- 2) Una fila por empresa con artículos, todas en COSTO tras la semilla:
-- SELECT company_id, tratamiento FROM public.cfg_compra_isv ORDER BY company_id;
--
-- 3) El CHECK rechaza un valor fuera del vocabulario (debe FALLAR con 23514):
-- INSERT INTO public.cfg_compra_isv (company_id, tratamiento) VALUES (-1, 'OTRO');
--
-- 4) Idempotencia: volver a correr el script no debe fallar ni duplicar filas.
--
-- =============================================================================
-- ROLLBACK (solo si hay que revertir; se pierde el tratamiento configurado)
-- =============================================================================
-- BEGIN;
-- DROP TABLE IF EXISTS public.cfg_compra_isv;
-- COMMIT;
-- =============================================================================
