-- ============================================================================
-- Unificación de cobranza — revisión operativa ronda 2 (2026-07-28)
--
-- 1) Arqueo del cierre de caja: el cajero cuenta el efectivo físico al cerrar
--    y el sistema lo guarda (sesion_caja.monto_cierre) junto al fondo inicial
--    (monto_apertura) para poder auditar la diferencia de cada turno.
-- 2) Seed de los porcentajes de aplicación de abonos (hoja "Abono Normal
--    automático" del piloto): Agua 60 / Alcantarillado 30 / Tasa Ambiental 5 /
--    Tasa SVA ERSAPS 5. Solo se siembra en empresas SIN configuración previa
--    (no pisa lo que el usuario haya guardado en /tarifario/desglose-abonos).
-- ============================================================================

BEGIN;

-- 1) Efectivo contado en el cierre (arqueo)
ALTER TABLE public.sesion_caja
    ADD COLUMN IF NOT EXISTS monto_cierre numeric(18,2);

COMMENT ON COLUMN public.sesion_caja.monto_apertura IS
    'Fondo inicial del turno (efectivo con el que abre la caja).';
COMMENT ON COLUMN public.sesion_caja.monto_cierre IS
    'Efectivo contado por el cajero en el cierre (arqueo). La diferencia contra fondo + efectivo cobrado se audita con el resumen del turno.';

-- 2) Seed de porcentajes de distribución de abonos por empresa
INSERT INTO public.adm_desglose_abono_porcentaje (company_id, item_codigo, porcentaje, usuario)
SELECT s.company_id, s.codigo, v.porcentaje, 'seed_2026-07-28'
FROM (VALUES
        ('AGUA_POTABLE',    60.00),
        ('ALCANTARILLADO',  30.00),
        ('TASA_AMBIENTAL',   5.00),
        ('TASA_SVA_ERSAPS',  5.00)
     ) AS v(codigo, porcentaje)
JOIN public.adm_servicio s
  ON s.codigo = v.codigo
 AND s.status_id = 1
WHERE NOT EXISTS (
        SELECT 1
        FROM public.adm_desglose_abono_porcentaje p
        WHERE p.company_id = s.company_id
      );

COMMIT;
