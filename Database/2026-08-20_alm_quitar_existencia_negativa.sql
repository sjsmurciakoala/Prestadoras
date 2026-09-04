-- =============================================================================
-- Almacén: ELIMINAR la función de existencia negativa en salidas (revierte 2026-08-15)
-- Fecha: 2026-08-20
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ
-- Decisión de negocio (2026-08-20): una salida de inventario NUNCA debe dejar la
-- existencia en negativo. Se elimina por completo la función de "existencia negativa
-- en salidas" que introdujo 2026-08-15_alm_existencia_negativa.sql: el interruptor por
-- empresa, el override por bodega y la confirmación en pantalla del descargo. El motor
-- (SIAD.Services/Almacen/InventarioPostingService.ValidarAsync) ahora rechaza SIEMPRE
-- toda salida (descargo, ajuste negativo, traslado) que cruzaría a negativo, sin
-- interruptor que lo habilite. El código EF ya dejó de mapear la tabla y la columna.
--
-- QUÉ HACE (destructivo, dos piezas — es el ROLLBACK exacto de 2026-08-15)
--   1. DROP COLUMN alm_bodega.permite_existencia_negativa (override por bodega).
--   2. DROP TABLE cfg_inventario_negativo (interruptor maestro por empresa).
--
-- IMPACTO DE DATOS (medido en el mirror el 2026-08-20, company_id=2)
--   * cfg_inventario_negativo: 1 fila, permitir=false (en su default conservador).
--   * alm_bodega.permite_existencia_negativa: 3 bodegas, todas NULL (0 overrides).
--   * 0 claves foráneas referencian la tabla.
--   -> No se pierde ninguna configuración de negocio: todo estaba en "bloquear".
--
-- IDEMPOTENTE: DROP ... IF EXISTS (volver a correrlo no falla).
-- REVERSIBLE: re-aplicar Database/2026-08-15_alm_existencia_negativa.sql recrea ambas
--             piezas (nace todo en su default = bloquear, sin cambio de comportamiento).
-- =============================================================================
BEGIN;

-- 1) Override por bodega (tri-estado). Todas las bodegas estaban en NULL (heredaban).
ALTER TABLE public.alm_bodega
    DROP COLUMN IF EXISTS permite_existencia_negativa;

-- 2) Interruptor maestro por empresa. Sin FK entrantes; una fila en su default (false).
DROP TABLE IF EXISTS public.cfg_inventario_negativo;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) La tabla ya no existe (NULL = eliminada):
-- SELECT to_regclass('public.cfg_inventario_negativo') AS interruptor;   -- esperado: NULL
--
-- 2) La columna ya no existe en alm_bodega (0 = eliminada):
-- SELECT count(*) AS columna_existe
--   FROM information_schema.columns
--  WHERE table_name = 'alm_bodega' AND column_name = 'permite_existencia_negativa';  -- esperado: 0
-- =============================================================================
