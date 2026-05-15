-- =============================================================================
-- BUGFIX #7 — sp_medidores_por_ruta_ws devuelve clientes ya facturados
-- Fecha: 2026-05-14
--
-- Problema:
--   El SP que alimenta GetOfflineSnapshotV3 (WS → app) devolvia todos los
--   medidores de la ruta para el periodo, sin discriminar los que ya tenian
--   factura activa emitida. Como el guard FACTURA_YA_EMITIDA esta en
--   sp_lectura_v3 (sync), el lector veia clientes pendientes en la app y
--   solo al subir las lecturas se topaba con el rechazo. UX pobre + posible
--   re-lectura accidental.
--
-- Fix:
--   Agregar 5to parametro p_excluir_facturados boolean DEFAULT true.
--   PostgreSQL aplica el default cuando el caller pasa solo 4 args, asi que
--   el WS C# (que llama con 4 params) no necesita recompilarse.
--
--   Filtro nuevo:
--     NOT EXISTS (
--       SELECT 1 FROM factura f
--       WHERE f.clientecodigo = cm.maestro_cliente_clave
--         AND f.ano = p_anio::text
--         AND f.mes = p_mes::text
--         AND COALESCE(f.estado, '') = 'A'  -- Activa (no anulada)
--     )
--
--   Si la factura del periodo esta anulada (estado='N'), el cliente VUELVE a
--   aparecer disponible para re-emision. Eso es deseado: anular = liberar.
--
-- Deuda post-25:
--   - Migrar a estado_id numerico (cfg_estado_documento_comercial)
--   - Soporte multi-empresa: agregar p_company_id al SP
-- =============================================================================

DROP FUNCTION IF EXISTS public.sp_medidores_por_ruta_ws(character varying, integer, integer, integer);
DROP FUNCTION IF EXISTS public.sp_medidores_por_ruta_ws(character varying, integer, integer, integer, boolean);

CREATE OR REPLACE FUNCTION public.sp_medidores_por_ruta_ws(
    p_ruta character varying,
    p_ciclo integer,
    p_anio integer,
    p_mes integer,
    p_excluir_facturados boolean DEFAULT true
)
RETURNS TABLE(
    maestro_cliente_id integer,
    maestro_cliente_clave character varying,
    maestro_medidor_numero character varying,
    maestro_cliente_identidad text,
    maestro_cliente_nombre text,
    descuento_valor numeric,
    ciclo character varying,
    secuencia character varying,
    ruta character varying,
    categoria character varying,
    tipo integer,
    tiene_med boolean,
    lect_ant numeric,
    fecha_lect_ant date,
    lect_act numeric,
    fecha_lect_act date,
    direccion character varying,
    codigo character varying,
    rtn text,
    ser1 boolean, ser2 boolean, ser3 boolean, ser4 boolean, ser5 boolean,
    ser6 boolean, ser7 boolean, ser8 boolean, ser9 boolean, ser10 boolean,
    agua_anterior numeric, alcantarillado_anterior numeric, ambiental_anterior numeric,
    convenio_anterior numeric, otro_anterior numeric, ersap_anterior numeric,
    gestion_legal_anterior numeric, sdo_ser6_anterior numeric, sdo_ser7_anterior numeric,
    sdo_ser8_anterior numeric, sdo_ser9_anterior numeric, sdo_ser10_anterior numeric,
    agua_recargo numeric, alcantarillado_recargo numeric, ambiental_recargo numeric,
    convenio_recargo numeric, otro_recargo numeric, ersap_recargo numeric,
    gestion_legal_recargo numeric, sdo_ser6_recargo numeric, sdo_ser7_recargo numeric,
    sdo_ser8_recargo numeric, sdo_ser9_recargo numeric, sdo_ser10_recargo numeric,
    promedio numeric,
    tiene_descuento boolean
)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_ruta text := COALESCE(NULLIF(btrim(p_ruta), ''), '');
    v_ruta_norm text;
BEGIN
    IF v_ruta ~ '^[0-9]+$' AND length(v_ruta) <= 5 THEN
        v_ruta_norm := lpad(v_ruta, 5, '0');
    ELSE
        v_ruta_norm := v_ruta;
    END IF;

    RETURN QUERY
    SELECT
        cm.maestro_cliente_id::int,
        cm.maestro_cliente_clave::varchar,
        mm.maestro_medidor_numero::varchar,
        cm.maestro_cliente_identidad::text,
        cm.maestro_cliente_nombre::text,
        COALESCE(cm.descuento_tercera_edad, 0)::numeric,
        c.ciclos_codigo::varchar,
        cm.maestro_cliente_secuencia::varchar,
        cm.maestro_cliente_indicativo_ruta::varchar,
        cm.categoria_servicio_id::varchar,
        CASE
            WHEN COALESCE(cm.tipo_uso_codigo, '') ~ '^[0-9]+$' THEN cm.tipo_uso_codigo::int
            ELSE 1
        END AS tipo,
        COALESCE(cm.maestro_cliente_tiene_medidor, false)::boolean,
        COALESCE(hm.lect_ant, 0)::numeric,
        COALESCE(hm.fecha_lect_ant, current_date)::date,
        COALESCE(hm.lect_act, 0)::numeric,
        COALESCE(hm.fecha_lect_act, current_date)::date,
        cd.detalle_cliente_direccion::varchar,
        COALESCE(NULLIF(split_part(COALESCE(cm.letracodigo, ''), ',', 3), ''), 'A')::varchar,
        cm.maestro_cliente_rtn::text,

        false::boolean, false::boolean, false::boolean, false::boolean, false::boolean,
        false::boolean, false::boolean, false::boolean, false::boolean, false::boolean,

        0::numeric, 0::numeric, 0::numeric, 0::numeric, 0::numeric, 0::numeric,
        0::numeric, 0::numeric, 0::numeric, 0::numeric, 0::numeric, 0::numeric,

        0::numeric, 0::numeric, 0::numeric, 0::numeric, 0::numeric, 0::numeric,
        0::numeric, 0::numeric, 0::numeric, 0::numeric, 0::numeric, 0::numeric,

        COALESCE(sp_promedio_por_clave_ws(cm.maestro_cliente_clave, p_anio, p_mes), 0)::numeric,
        COALESCE(cm.maestro_cliente_tercera_edad, false)::boolean

    FROM cliente_maestro cm
    INNER JOIN cliente_detalle cd ON cm.maestro_cliente_id = cd.maestro_cliente_id
    LEFT JOIN maestro_medidor mm ON mm.maestro_medidor_id = cd.maestro_medidor_id
    INNER JOIN ciclos c ON c.ciclos_id = p_ciclo
    LEFT JOIN historicomedicion hm
        ON cm.maestro_cliente_clave = hm.clave
       AND hm.ano = p_anio
       AND hm.mes = p_mes
    WHERE cm.estado = true
      AND (
            CASE
                WHEN split_part(cm.maestro_cliente_indicativo_ruta, '-', 3) ~ '^[0-9]+$'
                THEN lpad(split_part(cm.maestro_cliente_indicativo_ruta, '-', 3), 5, '0')
                ELSE split_part(cm.maestro_cliente_indicativo_ruta, '-', 3)
            END = v_ruta_norm
            OR cm.maestro_cliente_indicativo_ruta = v_ruta_norm
          )
      AND cm.ciclos_id = p_ciclo
      -- BUGFIX 2026-05-14 #7: excluir clientes con factura activa del periodo.
      AND (
            NOT p_excluir_facturados
            OR NOT EXISTS (
                SELECT 1
                FROM public.factura f
                WHERE f.clientecodigo = cm.maestro_cliente_clave
                  AND f.ano = p_anio::text
                  AND f.mes = p_mes::text
                  AND COALESCE(f.estado, '') = 'A'
            )
          )
    ORDER BY cm.maestro_cliente_secuencia, cm.maestro_cliente_clave;
END;
$function$;

COMMENT ON FUNCTION public.sp_medidores_por_ruta_ws(character varying, integer, integer, integer, boolean) IS
'Devuelve medidores de la ruta para descarga app. Bugfix 2026-05-14 #7:
filtra clientes con factura activa del periodo via p_excluir_facturados=true (default).
Si la factura del periodo esta anulada (estado=N), el cliente vuelve a aparecer.';
