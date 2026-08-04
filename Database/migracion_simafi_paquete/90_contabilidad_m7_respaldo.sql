-- =============================================================================
-- 90 — CONTABILIDAD (M7): SOLO EL RESPALDO. La re-migración es aparte.
-- =============================================================================
-- M7 = re-migrar la contabilidad con los números de comprobante ORIGINALES
-- (decisión ya tomada: histórico fiel). ESTE archivo únicamente respalda las
-- partidas actuales en tablas espejo _pre_m7 — la limpieza y la re-migración
-- se ejecutan con el plan de la sesión (PDF) y el respaldo VERIFICADO.
-- Idempotente: si el respaldo ya tiene filas, no lo pisa.

BEGIN;

CREATE TABLE IF NOT EXISTS public.con_partida_hdr_pre_m7 AS
    SELECT * FROM public.con_partida_hdr WHERE false;
CREATE TABLE IF NOT EXISTS public.con_partida_dtl_pre_m7 AS
    SELECT * FROM public.con_partida_dtl WHERE false;

INSERT INTO public.con_partida_hdr_pre_m7
SELECT h.* FROM public.con_partida_hdr h
WHERE NOT EXISTS (SELECT 1 FROM public.con_partida_hdr_pre_m7 LIMIT 1);

INSERT INTO public.con_partida_dtl_pre_m7
SELECT d.* FROM public.con_partida_dtl d
WHERE NOT EXISTS (SELECT 1 FROM public.con_partida_dtl_pre_m7 LIMIT 1);

COMMIT;

\echo '=== Verificación 90: el respaldo debe igualar a las tablas vivas ==='
SELECT (SELECT count(*) FROM con_partida_hdr)         AS hdr_vivas,
       (SELECT count(*) FROM con_partida_hdr_pre_m7)  AS hdr_respaldo,
       (SELECT count(*) FROM con_partida_dtl)         AS dtl_vivas,
       (SELECT count(*) FROM con_partida_dtl_pre_m7)  AS dtl_respaldo;
