-- =============================================================================
-- Bancos: sanea ban_cuenta.numero_cuenta — deja SOLO el número de cuenta.
-- Fecha: 2026-08-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor (siad_v3).
--
-- Contexto: el combo "Cuenta del banco" del pago a proveedores muestra ahora únicamente
-- numero_cuenta. Algunos valores migrados traen ruido en el propio dato:
--   * sufijo desambiguador entre paréntesis:  "11-701-000572-3 (SIMS06)"
--   * prefijo "Cta. " / "Cta. No.":            "Cta. 11-701-001233-9", "Cta. No.117010000271"
-- Este script quita ese ruido para que quede el número limpio.
--
-- OJO — restricción UNIQUE (company_id, numero_cuenta): el sufijo "(SIMSxx)/(SIMCxxxx)" fue
-- puesto para DESAMBIGUAR cuentas que comparten el mismo número base. Quitarlo a todas
-- provocaría colisiones (p.ej. SIMS01/SIMS06 comparten 11-701-000572-3). Por eso el saneo:
--   1) Toca SOLO cuentas ACTIVAS (activo = true) — son las únicas que aparecen en el combo;
--      las inactivas son histórico invisible y conservan su sufijo desambiguador.
--   2) Lleva guardia anti-colisión (NOT EXISTS): no limpia una cuenta si el número resultante
--      ya existe en otra cuenta de la MISMA empresa (defensa por si el SRV difiere del mirror).
--
-- CORRECTIVO de datos · NO destructivo · REVERSIBLE · IDEMPOTENTE. No crea ni borra estructura.
-- No usa company_id fijo → multiempresa (aplica donde exista el ruido).
--
-- En el mirror (empresa 2) afecta 5 filas ACTIVAS:
--   id 70  SIMS06  "11-701-000572-3 (SIMS06)"  -> 11-701-000572-3
--   id 56  SIMS10  "Cta. 11-701-001233-9"      -> 11-701-001233-9
--   id 45  SIMS12  "Cta. No.117010000271"      -> 117010000271
--   id 44  SIMS11  "Cta. No.117010001014"      -> 117010001014
--   id 43  SIMS13  "Cta. No.117010001048"      -> 117010001048
-- Las inactivas con ruido (SIMS01, SIMC1140, SIMC2586, SIMC7535, SIMC7753) NO se tocan.
--
-- RESPALDO recomendado ANTES de aplicar (guardar esta salida por si se necesita revertir):
--   SELECT company_id, banco_cuenta_id, code, numero_cuenta
--     FROM public.ban_cuenta
--    WHERE numero_cuenta ~ '\([^)]*\)\s*$' OR numero_cuenta ~* '^\s*Cta\.';
-- =============================================================================
BEGIN;

WITH limpieza AS (
    SELECT c.banco_cuenta_id,
           c.company_id,
           btrim(
               regexp_replace(
                   regexp_replace(c.numero_cuenta, '\s*\([^)]*\)\s*$', '', 'g'),  -- sufijo "(....)"
                   '^\s*Cta\.\s*(No\.)?\s*', '', 'i')                             -- prefijo "Cta." / "Cta. No."
           ) AS numero_limpio
      FROM public.ban_cuenta c
     WHERE c.activo = true
       AND (c.numero_cuenta ~ '\([^)]*\)\s*$' OR c.numero_cuenta ~* '^\s*Cta\.')
)
UPDATE public.ban_cuenta c
   SET numero_cuenta = l.numero_limpio,
       updated_at    = now(),
       updated_by    = 'saneo-numero-cuenta'
  FROM limpieza l
 WHERE c.banco_cuenta_id = l.banco_cuenta_id
   AND l.numero_limpio <> ''
   AND l.numero_limpio <> c.numero_cuenta
   AND NOT EXISTS (                       -- guardia anti-colisión con el UNIQUE (company_id, numero_cuenta)
        SELECT 1
          FROM public.ban_cuenta d
         WHERE d.company_id = c.company_id
           AND d.banco_cuenta_id <> c.banco_cuenta_id
           AND d.numero_cuenta = l.numero_limpio);

COMMIT;

-- =============================================================================
-- Verificación (¿ya aplicado?): debe devolver 0 filas.
--   Cuentas ACTIVAS cuyo numero_cuenta todavía trae ruido.
-- =============================================================================
-- SELECT company_id, banco_cuenta_id, code, numero_cuenta
--   FROM public.ban_cuenta
--  WHERE activo = true
--    AND (numero_cuenta ~ '\([^)]*\)\s*$' OR numero_cuenta ~* '^\s*Cta\.');

-- =============================================================================
-- ROLLBACK manual (solo si hiciera falta) — restaura los valores originales (empresa 2):
-- =============================================================================
-- BEGIN;
-- UPDATE public.ban_cuenta SET numero_cuenta = '11-701-000572-3 (SIMS06)' WHERE company_id = 2 AND banco_cuenta_id = 70;
-- UPDATE public.ban_cuenta SET numero_cuenta = 'Cta. 11-701-001233-9'     WHERE company_id = 2 AND banco_cuenta_id = 56;
-- UPDATE public.ban_cuenta SET numero_cuenta = 'Cta. No.117010000271'     WHERE company_id = 2 AND banco_cuenta_id = 45;
-- UPDATE public.ban_cuenta SET numero_cuenta = 'Cta. No.117010001014'     WHERE company_id = 2 AND banco_cuenta_id = 44;
-- UPDATE public.ban_cuenta SET numero_cuenta = 'Cta. No.117010001048'     WHERE company_id = 2 AND banco_cuenta_id = 43;
-- COMMIT;
