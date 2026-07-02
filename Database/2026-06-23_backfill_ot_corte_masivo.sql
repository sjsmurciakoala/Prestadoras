-- ============================================================
-- Backfill de órdenes de trabajo (tipo 33 / corte) para lotes de
-- corte masivo creados ANTES de que existiera la generación de OT
-- (lotes con cln_corte_masivo_dtl.orden_id IS NULL).
--
-- Replica la lógica de CorteMasivoService.GenerarAsync:
--   tipo='33', estado='P', concepto='ORDEN DE CORTE - Lote <correlativo>',
--   fecha = fecha_generacion + dias_corte, orden_numero = MAX()+1 secuencial.
--
-- Idempotente y self-scoping: solo crea OT para detalles sin orden_id.
-- Re-ejecutarlo no duplica.
--
-- Fecha: 2026-06-23
-- Regla DB Mirror: ejecutar el MISMO script en SRV (siad_v3) y en
-- localhost (siad_v3_restore). Cada BD rellena sus propios huecos.
-- ============================================================

BEGIN;

-- Serializa la asignación de orden_numero (orden_trabajo no tiene índice único).
SELECT pg_advisory_xact_lock(833301);

DO $$
DECLARE
    r        RECORD;
    next_num INT;
    new_id   INT;
    cnt      INT := 0;
BEGIN
    SELECT COALESCE(MAX(orden_numero), 50000) INTO next_num FROM orden_trabajo;

    FOR r IN
        SELECT d.id            AS dtl_id,
               d.cliente_clave,
               d.saldo_adeudado,
               h.correlativo,
               h.fecha_generacion,
               h.dias_corte,
               h.periodo_anio,
               h.periodo_mes,
               h.usuario,
               h.usuariocreacion
          FROM cln_corte_masivo_dtl d
          JOIN cln_corte_masivo_hdr h ON h.id = d.hdr_id
         WHERE d.orden_id IS NULL
         ORDER BY d.hdr_id, d.id
    LOOP
        next_num := next_num + 1;

        INSERT INTO orden_trabajo
            (orden_numero, maestro_cliente_clave, concepto, estado,
             fecha, fecha_creacion, ano, mes, saldo, usuario, tipo)
        VALUES
            (next_num,
             r.cliente_clave,
             'ORDEN DE CORTE - Lote ' || r.correlativo,
             'P',
             r.fecha_generacion + COALESCE(r.dias_corte, 0),
             now(),
             COALESCE(r.periodo_anio, EXTRACT(YEAR  FROM r.fecha_generacion)::int),
             COALESCE(r.periodo_mes,  EXTRACT(MONTH FROM r.fecha_generacion)::int),
             r.saldo_adeudado,
             COALESCE(r.usuario, r.usuariocreacion, 'backfill'),
             '33')
        RETURNING orden_id INTO new_id;

        UPDATE cln_corte_masivo_dtl SET orden_id = new_id WHERE id = r.dtl_id;
        cnt := cnt + 1;
    END LOOP;

    RAISE NOTICE 'OT de corte creadas (backfill): %', cnt;
END $$;

COMMIT;
