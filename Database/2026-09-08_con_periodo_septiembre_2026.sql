-- =============================================================================
-- 2026-09-08 — Abre el periodo contable de septiembre 2026
--
-- POR QUE EXISTE ESTE ARCHIVO
--   El mirror `siad_v3_restore` se quedo sin periodo contable vigente: el
--   ultimo (`period_id` 70, "Julio 2026") termina el 2026-09-01 05:59:59 y
--   despues de esa fecha no hay ninguno que cubra el dia de hoy.
--
--   `OrdenesPagoDirectoService.ResolvePeriodoIdAsync` busca un periodo cuyo
--   rango contenga la fecha del documento y que este ABIERTO; al no
--   encontrarlo lanza "No se encontro un periodo contable valido para la fecha
--   de la orden". Desde el 2026-09-01 eso tumba 13 pruebas de
--   `AbonosCompromisoTests` y `ProcesamientoRetencionesTests` **sin que haya
--   cambiado una linea de codigo**: lo unico que paso fue el calendario.
--
-- DONDE SE APLICA
--   * `siad_v3_restore` (mirror local) — LE FALTA. Es el objetivo de este script.
--   * `siad_v3_desarrollo` (3.208.232.209) — presumiblemente le falta tambien.
--   * `siad_v4` (produccion) — YA LO TIENE (`period_id` 85, creado el 2026-09-04
--     desde el portal por `admin@siad-demo.com`). NO hace falta aplicarlo arriba,
--     y si se aplica no hace nada: el guard por `code` lo salta.
--
-- QUE INSERTA
--   Una fila por empresa que ya tenga periodos contables, con la MISMA forma
--   que el periodo 85 de produccion:
--
--     code        202609
--     name        Septiembre 2026
--     start_date  2026-09-01 06:00:00-06
--     end_date    2026-10-01 05:59:59-06
--     status      ABIERTO   (status_id = 0)
--
--   Los limites se copian literalmente de produccion. Ojo: ese rango arranca a
--   las 06:00 del dia 1, no a la medianoche, porque asi estan TODOS los
--   periodos de las dos bases (en UTC quedan a las 12:00). Se replica el patron
--   existente a proposito; corregir el desfase seria otro trabajo y afectaria a
--   los periodos historicos.
--
-- IDEMPOTENTE
--   Solo inserta donde no exista ya un periodo con `code = '202609'` para esa
--   empresa. Reejecutarlo no hace nada.
--
-- IMPACTO
--   Aditivo. No borra, no actualiza y no toca estructura. Habilita el registro
--   contable con fecha de septiembre, que hoy esta bloqueado en el mirror.
--
-- LO QUE ESTE SCRIPT **NO** HACE (y conviene saber)
--   * No parte el periodo 70. En el mirror ese periodo se llama "Julio 2026"
--     pero su rango abarca julio Y agosto, y sigue ABIERTO. En produccion esta
--     recortado a julio, con un periodo 84 aparte para agosto, ambos CERRADOS.
--     Esa diferencia viene de `2026-09-04_cierre_periodos_2026-07_2026-08.sql`,
--     que ademas revierte polizas por id y no es aplicable al mirror tal cual.
--   * No cierra nada. El periodo 70 queda abierto y solapado con nada: el nuevo
--     empieza justo donde el otro termina.
--   * No resuelve el fondo. En octubre vuelve a faltar el periodo y las mismas
--     13 pruebas se caen otra vez. El arreglo duradero es que esas pruebas
--     creen el periodo que necesitan dentro de su transaccion, como ya hacen
--     con la integracion contable, el control presupuestario y la aprobacion
--     por niveles (ver `SIAD.Tests/Infrastructure/IntegrationTestBase.cs`).
--
-- REVERSA
--   DELETE FROM public.con_periodo_contable
--    WHERE code = '202609' AND created_by = 'script_2026-09-08_periodo_septiembre';
--   Solo mientras no se le haya colgado ninguna partida.
-- =============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

INSERT INTO public.con_periodo_contable (
    period_id, company_id, code, name,
    start_date, end_date, status, status_id,
    created_at, created_by)
SELECT
    (SELECT COALESCE(MAX(p.period_id), 0) FROM public.con_periodo_contable p)
        + row_number() OVER (ORDER BY e.company_id),
    e.company_id,
    '202609',
    'Septiembre 2026',
    TIMESTAMPTZ '2026-09-01 06:00:00-06',
    TIMESTAMPTZ '2026-10-01 05:59:59-06',
    'ABIERTO',
    0,
    now(),
    'script_2026-09-08_periodo_septiembre'
FROM (SELECT DISTINCT company_id FROM public.con_periodo_contable) e
WHERE NOT EXISTS (
    SELECT 1
      FROM public.con_periodo_contable x
     WHERE x.company_id = e.company_id
       AND x.code = '202609');

COMMIT;

-- =============================================================================
-- VERIFICACION (ejecutar aparte, despues del COMMIT)
-- =============================================================================
--
-- Debe existir exactamente un periodo que cubra hoy, y estar abierto:
--   SELECT period_id, company_id, code, name, start_date, end_date, status
--     FROM public.con_periodo_contable
--    WHERE start_date <= now() AND end_date >= now()
--    ORDER BY company_id;
--   -- esperado: 1 fila por empresa, code 202609, status ABIERTO
--
-- La funcion que consulta el codigo debe resolver el periodo. OJO: no devuelve
-- un booleano pese al nombre, devuelve el `period_id`:
--   SELECT public.fn_con_periodo_abierto(2, current_date);
--   -- esperado: el period_id recien creado (71 en el mirror), no NULL ni 0
--
-- Y las 13 pruebas que se caian deben pasar:
--   dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~AbonosCompromisoTests|FullyQualifiedName~ProcesamientoRetencionesTests"
