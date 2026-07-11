-- =============================================================================
-- Presupuesto SIMAFI  ·  Eliminar presupuestos históricos 2017–2024
-- Fecha: 2026-07-09
--
-- Contexto: la migración SIMAFI cargó un presupuesto por año PRE-2017..PRE-2025
--   (ver 2026-07-08_presupuesto_simafi_02_transform.sql). El usuario pidió
--   conservar únicamente PRE-2025 y eliminar los años 2017 a 2024.
--
-- Seguridad (verificado 2026-07-09 en SRV y mirror):
--   · FK pst_config_presupuesto_dtl -> hdr = ON DELETE CASCADE  => el detalle
--     (~895 líneas de estos 8 presupuestos) se borra automáticamente.
--   · FK RESTRICT pst_actividad_presupuesto / pst_solicitud_actividad_presupuesto
--     tienen 0 filas para estos presupuestos => NO bloquean el DELETE.
--   · Idempotente: si los ids ya no existen, no borra nada.
--
-- OJO: re-ejecutar 02_transform.sql regenera PRE-2017..PRE-2025 desde staging.
--   Si la eliminación debe ser permanente frente a una recarga, filtrar el año
--   en ese script (p. ej. WHERE ano >= 2025).
--
-- Aplicar en SRV (172.16.0.9/siad_v3) y mirror (localhost/siad_v3_restore).
-- =============================================================================

BEGIN;

DELETE FROM public.pst_config_presupuesto_hdr
 WHERE id_presupuesto IN (
        'PRE-2017', 'PRE-2018', 'PRE-2019', 'PRE-2020',
        'PRE-2021', 'PRE-2022', 'PRE-2023', 'PRE-2024'
       );

COMMIT;
