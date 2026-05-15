-- ============================================================================
-- Reescritura de sp_medidores_por_ruta_ws SIN tablas legacy
-- Fecha: 2026-05-05
-- Contexto: el 2026-04-28 se dropearon 12 tablas legacy de la BD
-- (servicios_roles_ws, configuracion_tasas, configuracion_tasas_detalle,
-- tarifas, tarifas_contador, condicion_lectura, informativo, cai, letras,
-- letracodigo, configuracion_app_lectura_medidores, condicon_lectura).
--
-- La version anterior del SP hacia LEFT JOIN a:
--   - servicios_roles_ws (3 subqueries)
--   - configuracion_tasas + configuracion_tasas_detalle + servicios (1 subquery)
--   - transaccion_abonado JOIN servicios_roles_ws (2 subqueries)
-- Esos JOINs tronaban con "relation does not exist".
--
-- La firma RETURNS TABLE se mantiene IDENTICA (56 columnas, mismo orden y
-- mismos tipos) para no romper el WS C# que parsea por nombre de columna.
--
-- Los campos derivados de tablas legacy se devuelven como 0 / false:
--   - ser1..ser10: false (no hay catalogo de servicios legacy)
--   - agua_anterior..sdo_ser10_anterior: 0 (no se calculan saldos legacy aqui)
--   - agua_recargo..sdo_ser10_recargo: 0
--
-- El flujo V3 actual del app NO usa estos campos (usa el snapshot offline V3).
-- Quedan en la firma solo por compatibilidad con el contrato WS existente.
-- ============================================================================

DROP FUNCTION IF EXISTS public.sp_medidores_por_ruta_ws(character varying, integer, integer, integer);

CREATE OR REPLACE FUNCTION public.sp_medidores_por_ruta_ws(
    p_ruta character varying,
    p_ciclo integer,
    p_anio integer,
    p_mes integer
)
RETURNS TABLE (
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
    ser1 boolean,
    ser2 boolean,
    ser3 boolean,
    ser4 boolean,
    ser5 boolean,
    ser6 boolean,
    ser7 boolean,
    ser8 boolean,
    ser9 boolean,
    ser10 boolean,
    agua_anterior numeric,
    alcantarillado_anterior numeric,
    ambiental_anterior numeric,
    convenio_anterior numeric,
    otro_anterior numeric,
    ersap_anterior numeric,
    gestion_legal_anterior numeric,
    sdo_ser6_anterior numeric,
    sdo_ser7_anterior numeric,
    sdo_ser8_anterior numeric,
    sdo_ser9_anterior numeric,
    sdo_ser10_anterior numeric,
    agua_recargo numeric,
    alcantarillado_recargo numeric,
    ambiental_recargo numeric,
    convenio_recargo numeric,
    otro_recargo numeric,
    ersap_recargo numeric,
    gestion_legal_recargo numeric,
    sdo_ser6_recargo numeric,
    sdo_ser7_recargo numeric,
    sdo_ser8_recargo numeric,
    sdo_ser9_recargo numeric,
    sdo_ser10_recargo numeric,
    promedio numeric,
    tiene_descuento boolean
)
LANGUAGE plpgsql
AS $function$
DECLARE
    -- Codigo de ruta normalizado a 5 digitos (estandar V3 CC+LLL)
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
        -- codigo: en la version legacy era split_part(letracodigo,',',3); columna sigue
        -- existiendo en cliente_maestro pero en el flujo V3 ya no se usa. Devuelve 'A' por defecto.
        COALESCE(NULLIF(split_part(COALESCE(cm.letracodigo, ''), ',', 3), ''), 'A')::varchar,
        cm.maestro_cliente_rtn::text,

        -- ser1..ser10: en la version legacy se obtenian de configuracion_tasas + servicios_roles_ws.
        -- En V3 los servicios viven en adm_cliente_servicio y se entregan via snapshot offline.
        -- Aqui devolvemos false; el app V3 ignora estos campos.
        false::boolean AS ser1,
        false::boolean AS ser2,
        false::boolean AS ser3,
        false::boolean AS ser4,
        false::boolean AS ser5,
        false::boolean AS ser6,
        false::boolean AS ser7,
        false::boolean AS ser8,
        false::boolean AS ser9,
        false::boolean AS ser10,

        -- saldos anteriores: en la version legacy se obtenian via servicios_roles_ws
        -- + transaccion_abonado. En V3 los saldos previos se calculan en el snapshot
        -- (pendiente sprint 2: incluir saldo_anterior_total en snapshot).
        0::numeric AS agua_anterior,
        0::numeric AS alcantarillado_anterior,
        0::numeric AS ambiental_anterior,
        0::numeric AS convenio_anterior,
        0::numeric AS otro_anterior,
        0::numeric AS ersap_anterior,
        0::numeric AS gestion_legal_anterior,
        0::numeric AS sdo_ser6_anterior,
        0::numeric AS sdo_ser7_anterior,
        0::numeric AS sdo_ser8_anterior,
        0::numeric AS sdo_ser9_anterior,
        0::numeric AS sdo_ser10_anterior,

        -- recargos: idem
        0::numeric AS agua_recargo,
        0::numeric AS alcantarillado_recargo,
        0::numeric AS ambiental_recargo,
        0::numeric AS convenio_recargo,
        0::numeric AS otro_recargo,
        0::numeric AS ersap_recargo,
        0::numeric AS gestion_legal_recargo,
        0::numeric AS sdo_ser6_recargo,
        0::numeric AS sdo_ser7_recargo,
        0::numeric AS sdo_ser8_recargo,
        0::numeric AS sdo_ser9_recargo,
        0::numeric AS sdo_ser10_recargo,

        -- promedio sigue funcionando (sp_promedio_por_clave_ws solo usa historicomedicion)
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
    ORDER BY cm.maestro_cliente_secuencia, cm.maestro_cliente_clave;
END;
$function$;

ALTER FUNCTION public.sp_medidores_por_ruta_ws(character varying, integer, integer, integer) OWNER TO postgres;

COMMENT ON FUNCTION public.sp_medidores_por_ruta_ws(character varying, integer, integer, integer) IS
'Devuelve medidores activos de una ruta para un ciclo/anio/mes. Reescrito el 2026-05-05 sin dependencias legacy. Los campos ser1..ser10, saldos anteriores y recargos se devuelven en false/0 (legacy). El flujo V3 del app usa snapshot offline para esos datos.';
