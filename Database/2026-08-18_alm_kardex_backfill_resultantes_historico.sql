-- =============================================================================
-- Backfill de existencia_resultante / costo_promedio_resultante en el histórico
-- migrado de SIMAFI (asientos con uuid IS NULL) de alm_kardex.
--
-- Fecha: 2026-08-18
-- Regla DB Mirror: aplicar el MISMO script en localhost (siad_v3_restore) PRIMERO
--                  y, por decisión del usuario, luego en SRV (siad_v3 @ 172.16.0.9).
--
-- QUÉ HACE
-- El motor de posteo (InventarioPostingService) ya persiste el saldo y el costo
-- promedio resultante en CADA asiento nuevo. El histórico migrado de SIMAFI, en
-- cambio, trae esas dos columnas en NULL — por eso el libro por bodega imprime "—"
-- y la vista por artículo tiene que DERIVAR el corrido al vuelo. Este script rellena
-- esas columnas en los asientos históricos, recorriendo cada par (artículo, bodega)
-- en orden de posteo (fecha, id) y calculando el saldo y el costo promedio CORRIDO
-- con la MISMA fórmula que KardexService.AplicarPuntoDeCorte:
--     saldo = Σ (ingresos − salidas)
--     valor = Σ (ingresos − salidas) × valor_unitario   -- acumulado SIN redondear
--     costo = saldo > 0 ? valor / saldo : NULL           -- redondeo sólo al dividir
--
-- INMUTABILIDAD (escotilla documentada)
-- alm_kardex es un libro mayor inmutable: trg_alm_kardex_inmutable rechaza todo
-- UPDATE/DELETE (SQLSTATE K0001, ver Database/2026-07-14_alm_kardex_trazabilidad.sql).
-- Un backfill ES un UPDATE, así que se usa la escotilla que ese mismo script documenta:
-- DISABLE el trigger, corregir, ENABLE — todo dentro de UNA transacción. Si algo falla,
-- el ROLLBACK revierte también el DISABLE (es transaccional en Postgres): el trigger
-- nunca queda desactivado fuera de la transacción.
--
-- IDEMPOTENTE y SEGURO
--   * Sólo toca filas del histórico: WHERE uuid IS NULL AND existencia_resultante IS NULL.
--     No pisa los snapshots del motor (uuid IS NOT NULL) ni ningún dato ya calculado.
--   * Re-ejecutar no re-toca nada (la guarda IS NULL ya no matchea).
--   * Multiempresa: la partición incluye company_id (hoy sólo company_id=2 tiene histórico).
--   * Excluye 12 asientos basura (articulo_id IS NULL, sin código, todo en 0): quedan NULL.
--
-- BORDES MEDIDOS EN EL MIRROR (2026-08-18, company_id=2)
--   * Universo a rellenar: 47.203 asientos (uuid NULL, articulo_id NOT NULL), 588 pares.
--   * Interleaving (histórico posterior a una fila del motor del mismo par): 0
--     → la window pura es exacta; NO hace falta variante PL/pgSQL.
--   * Revaluación real (ingresos=0 y salidas=0 con saldo previo ≠ 0): 0
--     (los 12 con ceros son los mismos 12 basura, con saldo previo 0 → no son revaluación).
--   * Huérfanos con código (articulo_id NULL pero código válido): 0
--     → particionar por articulo_id coincide con KardexService.
--
-- REQUISITOS DE EJECUCIÓN
--   * Privilegio OWNER de alm_kardex (para DISABLE/ENABLE TRIGGER).
--   * Ventana de bajo uso: el LOCK TABLE bloquea escritores mientras corre el UPDATE.
--   * Backup previo en SRV (Database/backup_bd_simple.ps1).
--   Correr con: psql "<conn>" -v ON_ERROR_STOP=1 -f <este archivo>
-- =============================================================================

BEGIN;

-- Serializa el backfill (evita dos corridas simultáneas) y bloquea escritores del
-- kardex durante el UPDATE. El motor sólo hace INSERT; el riesgo real es bajo, pero
-- con el trigger desactivado conviene que nadie más escriba la tabla.
SELECT pg_advisory_xact_lock(20260818);
LOCK TABLE alm_kardex IN SHARE ROW EXCLUSIVE MODE;

-- Escotilla: levantar la inmutabilidad SÓLO dentro de esta transacción.
ALTER TABLE alm_kardex DISABLE TRIGGER trg_alm_kardex_inmutable;

-- Corrido por par (artículo, bodega) en orden (fecha, id), acumulando desde el
-- primer asiento del par. Se escribe únicamente en las filas históricas aún vacías.
UPDATE alm_kardex k
SET existencia_resultante     = c.saldo_r,
    costo_promedio_resultante = c.costo_r
FROM (
    SELECT id,
           ROUND(saldo_acum, 2) AS saldo_r,
           CASE WHEN saldo_acum > 0
                THEN ROUND(valor_acum / saldo_acum, 4)
                ELSE NULL                       -- saldo ≤ 0 → sin costo (la UI pinta "—")
           END AS costo_r
    FROM (
        SELECT id,
               SUM(ingresos - salidas)                        OVER w AS saldo_acum,
               SUM((ingresos - salidas) * valor_unitario)     OVER w AS valor_acum
        FROM alm_kardex
        WHERE uuid IS NULL                       -- sólo histórico migrado
          AND articulo_id IS NOT NULL            -- excluye los 12 asientos basura
        WINDOW w AS (
            PARTITION BY company_id, articulo_id, COALESCE(bodega_id, 0)
            ORDER BY fecha, id
            ROWS UNBOUNDED PRECEDING
        )
    ) acum
) c
WHERE k.id = c.id
  AND k.uuid IS NULL
  AND k.existencia_resultante IS NULL;           -- idempotente + no toca snapshots del motor

-- Restaurar la inmutabilidad ANTES del COMMIT (queda activa en cuanto confirma).
ALTER TABLE alm_kardex ENABLE TRIGGER trg_alm_kardex_inmutable;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr por separado; son sólo lectura)
-- =============================================================================
-- Pre-checks (antes de aplicar) — deben dar: interleaving 0, huérfanos con código 0,
-- revaluación real 0. Si alguno > 0, revisar antes de aplicar.
--
--   SELECT count(*) AS universo
--     FROM alm_kardex WHERE uuid IS NULL AND existencia_resultante IS NULL;   -- ~47.203
--
--   SELECT count(*) AS huerfanos_con_codigo
--     FROM alm_kardex
--    WHERE uuid IS NULL AND articulo_id IS NULL
--      AND codigo_articulo IS NOT NULL AND btrim(codigo_articulo) <> '';       -- esperado 0
--
--   SELECT count(*) AS interleaving
--     FROM alm_kardex h JOIN alm_kardex m
--       ON m.company_id=h.company_id AND m.articulo_id=h.articulo_id
--      AND COALESCE(m.bodega_id,0)=COALESCE(h.bodega_id,0)
--    WHERE h.uuid IS NULL AND m.uuid IS NOT NULL
--      AND (h.fecha, h.id) > (m.fecha, m.id);                                  -- esperado 0
--
-- Post-checks (después de aplicar):
--   -- Deben quedar sólo los 12 basura sin resultante:
--   SELECT count(*) FROM alm_kardex
--    WHERE uuid IS NULL AND existencia_resultante IS NULL;                     -- esperado 12
--
--   -- Los snapshots del motor NO cambiaron (control): deberían seguir intactos.
--   SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL;                    -- igual que antes
--
--   -- Muestra: el libro por bodega del artículo 634 ya no debería tener NULL en el histórico.
--   SELECT id, fecha, ingresos, salidas, valor_unitario,
--          existencia_resultante, costo_promedio_resultante
--     FROM alm_kardex
--    WHERE company_id=2 AND articulo_id=634 AND uuid IS NULL
--    ORDER BY fecha, id;
-- =============================================================================
