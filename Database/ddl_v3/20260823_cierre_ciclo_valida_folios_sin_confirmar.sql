-- =============================================================================
-- Cerrar un ciclo avisa si quedan folios CAI sin confirmar
-- Fecha: 2026-08-23
--
-- Problema:
--   sp_adm_periodo_ciclo_cerrar ya bloquea el cierre si hay rutas sin facturas
--   emitidas (CICLO_RUTAS_PENDIENTES), pero no ve el otro caso: lecturas que el
--   lector YA emitio en el telefono —con su folio CAI impreso y entregado— y
--   que todavia no subieron.
--
--   Esas lecturas dejan rastro en el servidor: el `prepare` inserta la reserva
--   en adm_cai_correlativo_emitido con estado PENDING_SYNC y factura_id NULL.
--   Si el ciclo se cierra mientras existen, esas lecturas ya no pueden subir
--   nunca: sp_lectura_v3 las rechaza porque su periodo no esta abierto, y el
--   trabajo de campo se pierde. Paso en el piloto: 16 lecturas de junio
--   quedaron atrapadas en un telefono despues de un cierre forzado
--   (app_lectores, docs/DIAGNOSTICO_CORRELATIVO_DUPLICADO.md).
--
-- Fix:
--   Un chequeo hermano del que ya existe, con la misma politica: bloquea el
--   cierre normal y se salta con p_forzar. La idea no es impedir el cierre
--   —a veces hay que cerrar igual— sino que la decision sea informada, porque
--   del otro lado hay un ticket ya entregado a un abonado.
--
--   Las reservas sin cliente_id no se cuentan: no hay forma de atribuirlas a un
--   ciclo, y adivinar seria peor que no avisar.
-- =============================================================================

CREATE OR REPLACE FUNCTION public.sp_adm_periodo_ciclo_cerrar(
    p_company_id bigint,
    p_periodo_ciclo_id bigint,
    p_usuario text,
    p_forzar boolean DEFAULT false
) RETURNS void
LANGUAGE plpgsql
AS $function$
DECLARE
    v_ciclo record;
    v_pendientes bigint;
    v_folios bigint;
BEGIN
    SELECT pc.*, p.anio, p.mes
    INTO v_ciclo
    FROM public.adm_periodo_comercial_ciclo pc
    JOIN public.adm_periodo_comercial p
      ON p.company_id = pc.company_id
     AND p.periodo_comercial_id = pc.periodo_comercial_id
    WHERE pc.company_id = p_company_id
      AND pc.periodo_ciclo_id = p_periodo_ciclo_id
    FOR UPDATE OF pc;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe el ciclo de período comercial % para company_id=%.',
            p_periodo_ciclo_id, p_company_id;
    END IF;

    IF v_ciclo.status_id <> 1 THEN
        RAISE EXCEPTION 'CICLO_YA_CERRADO: el ciclo % del período %-% ya está cerrado.',
            v_ciclo.ciclo_codigo, v_ciclo.anio, lpad(v_ciclo.mes::text, 2, '0');
    END IF;

    IF NOT p_forzar THEN
        SELECT count(*)
        INTO v_pendientes
        FROM public.fn_adm_periodo_ciclo_rutas_pendientes(p_company_id, p_periodo_ciclo_id) rp
        WHERE rp.pendiente;

        IF v_pendientes > 0 THEN
            RAISE EXCEPTION 'CICLO_RUTAS_PENDIENTES: % ruta(s) del ciclo % sin facturas emitidas en %-%.',
                v_pendientes, v_ciclo.ciclo_codigo, v_ciclo.anio, lpad(v_ciclo.mes::text, 2, '0');
        END IF;

        -- 2026-08-23: folios entregados que todavía no subieron. La comparación
        -- de ciclo replica la de fn_adm_periodo_comercial_ciclo_abierto: se
        -- normaliza a dos dígitos sólo cuando el código es numérico.
        SELECT count(*)
        INTO v_folios
        FROM public.adm_cai_correlativo_emitido e
        JOIN public.cliente_maestro cm
          ON cm.company_id = e.company_id
         AND cm.maestro_cliente_id = e.cliente_id
        LEFT JOIN public.ciclos c
          ON c.ciclos_id = cm.ciclos_id
        WHERE e.company_id = p_company_id
          AND e.status_id = 1
          AND e.estado_codigo = 'PENDING_SYNC'
          AND e.factura_id IS NULL
          AND (
                CASE
                    WHEN btrim(coalesce(nullif(btrim(c.ciclos_codigo), ''),
                                        lpad(cm.ciclos_id::text, 2, '0'))) ~ '^[0-9]+$'
                    THEN lpad(btrim(coalesce(nullif(btrim(c.ciclos_codigo), ''),
                                             lpad(cm.ciclos_id::text, 2, '0'))), 2, '0')
                    ELSE btrim(coalesce(nullif(btrim(c.ciclos_codigo), ''),
                                        lpad(cm.ciclos_id::text, 2, '0')))
                END
              ) = (
                CASE
                    WHEN btrim(v_ciclo.ciclo_codigo) ~ '^[0-9]+$'
                    THEN lpad(btrim(v_ciclo.ciclo_codigo), 2, '0')
                    ELSE btrim(v_ciclo.ciclo_codigo)
                END
              );

        IF v_folios > 0 THEN
            RAISE EXCEPTION 'CICLO_FOLIOS_SIN_CONFIRMAR: % folio(s) CAI del ciclo % reservados y sin confirmar. Son lecturas ya emitidas que siguen en un teléfono: si se cierra %-%, no van a poder subir.',
                v_folios, v_ciclo.ciclo_codigo, v_ciclo.anio, lpad(v_ciclo.mes::text, 2, '0');
        END IF;
    END IF;

    UPDATE public.adm_periodo_comercial_ciclo pc
    SET status_id = 2,
        fecha_cierre = now(),
        cerrado_por = left(p_usuario, 100),
        updated_at = now(),
        updated_by = left(p_usuario, 100)
    WHERE pc.company_id = p_company_id
      AND pc.periodo_ciclo_id = p_periodo_ciclo_id;
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_periodo_ciclo_cerrar(bigint, bigint, text, boolean) IS
'Cierra un ciclo de período comercial. Sin p_forzar valida dos cosas:
  - CICLO_RUTAS_PENDIENTES: rutas del ciclo sin facturas emitidas (F7).
  - CICLO_FOLIOS_SIN_CONFIRMAR (2026-08-23): folios CAI reservados sin confirmar,
    es decir lecturas ya emitidas que siguen en un teléfono sin subir. Cerrar con
    esos folios vivos las deja sin destino: sp_lectura_v3 las rechaza después
    porque su período no está abierto.';

-- -----------------------------------------------------------------------------
-- Consulta de apoyo: qué folios están frenando el cierre de un ciclo
-- -----------------------------------------------------------------------------
-- select e.cai_correlativo_emitido_id, e.correlativo, e.numero_factura,
--        cm.maestro_cliente_clave as clave, e.created_at, e.created_by
-- from public.adm_cai_correlativo_emitido e
-- join public.cliente_maestro cm
--   on cm.company_id = e.company_id and cm.maestro_cliente_id = e.cliente_id
-- left join public.ciclos c on c.ciclos_id = cm.ciclos_id
-- where e.status_id = 1 and e.estado_codigo = 'PENDING_SYNC' and e.factura_id is null
--   and coalesce(nullif(btrim(c.ciclos_codigo), ''), lpad(cm.ciclos_id::text, 2, '0')) = '19'
-- order by e.correlativo;
