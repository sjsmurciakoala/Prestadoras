-- Fix: sp_medidores_por_ruta_ws debe filtrar por ruta usando el indicativo (libreta)
-- y aceptar rutas numéricas con/sin ceros a la izquierda.

CREATE OR REPLACE FUNCTION public.sp_medidores_por_ruta_ws(
    p_ruta character varying,
    p_ciclo integer,
    p_anio integer,
    p_mes integer)
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
        tiene_descuento boolean)
    LANGUAGE plpgsql
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$
DECLARE
    v_ruta text := COALESCE(btrim(p_ruta), '');
    v_ruta_num integer;
BEGIN
    IF v_ruta ~ '^[0-9]+$' THEN
        v_ruta_num := v_ruta::int;
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
        (select COALESCE(split_part(letracodigo, ',', 3), 'A')
         from cliente_maestro cf
         where cf.maestro_cliente_clave = cm.maestro_cliente_clave)::varchar,
        cm.maestro_cliente_rtn::text,
        COALESCE(SC.ser1, false)::boolean,
        COALESCE(SC.ser2, false)::boolean,
        COALESCE(SC.ser3, false)::boolean,
        COALESCE(SC.ser4, false)::boolean,
        COALESCE(SC.ser5, false)::boolean,
        COALESCE(SC.ser6, false)::boolean,
        COALESCE(SC.ser7, false)::boolean,
        COALESCE(SC.ser8, false)::boolean,
        COALESCE(SC.ser9, false)::boolean,
        COALESCE(SC.ser10, false)::boolean,
        COALESCE(SD.agua, 0)::numeric,
        COALESCE(SD.alcantarillado, 0)::numeric,
        COALESCE(SD.ambiental, 0)::numeric,
        COALESCE(SD.convenio, 0)::numeric,
        COALESCE(SD.otros, 0)::numeric,
        COALESCE(SD.ERSAPS, 0)::numeric,
        COALESCE(SD.gestionlegal, 0)::numeric,
        COALESCE(SD.SDO_SER6, 0)::numeric,
        COALESCE(SD.SDO_SER7, 0)::numeric,
        COALESCE(SD.SDO_SER8, 0)::numeric,
        COALESCE(SD.SDO_SER9, 0)::numeric,
        COALESCE(SD.SDO_SER10, 0)::numeric,
        0::numeric,
        0::numeric,
        COALESCE(SR.ambiental, 0)::numeric,
        COALESCE(SR.convenio, 0)::numeric,
        COALESCE(SR.otros, 0)::numeric,
        COALESCE(SR.ERSAPS, 0)::numeric,
        COALESCE(SR.gestionlegal, 0)::numeric,
        COALESCE(SR.SDO_SER6, 0)::numeric,
        COALESCE(SR.SDO_SER7, 0)::numeric,
        COALESCE(SR.SDO_SER8, 0)::numeric,
        COALESCE(SR.SDO_SER9, 0)::numeric,
        COALESCE(SR.SDO_SER10, 0)::numeric,
        COALESCE(sp_promedio_por_clave_ws(cm.maestro_cliente_clave, p_anio, p_mes), 0)::numeric,
        COALESCE(cm.maestro_cliente_tercera_edad, false)::boolean
    FROM cliente_maestro cm
    INNER JOIN cliente_detalle cd on cm.maestro_cliente_id = cd.maestro_cliente_id
    LEFT JOIN maestro_medidor mm on mm.maestro_medidor_id = cd.maestro_medidor_id
    INNER JOIN ciclos c on c.ciclos_id = p_ciclo

    LEFT JOIN (
        select
            cm2.maestro_cliente_clave,
            sum(case when sr.rol = 'agua' then ta.saldo_detalle else 0 end) agua,
            sum(case when sr.rol = 'alcantarillado' then ta.saldo_detalle else 0 end) alcantarillado,
            sum(case when sr.rol = 'ambiental' then ta.saldo_detalle else 0 end) ambiental,
            sum(case when sr.rol = 'convenio' then ta.saldo_detalle else 0 end) convenio,
            sum(case when sr.rol = 'otros' then ta.saldo_detalle else 0 end) otros,
            sum(case when sr.rol = 'ersaps' then ta.saldo_detalle else 0 end) ERSAPS,
            sum(case when sr.rol = 'gestion_legal' then ta.saldo_detalle else 0 end) gestionlegal,
            sum(case when sr.rol = 'sdo_ser6' then ta.saldo_detalle else 0 end) SDO_SER6,
            sum(case when sr.rol = 'sdo_ser7' then ta.saldo_detalle else 0 end) SDO_SER7,
            sum(case when sr.rol = 'sdo_ser8' then ta.saldo_detalle else 0 end) SDO_SER8,
            sum(case when sr.rol = 'sdo_ser9' then ta.saldo_detalle else 0 end) SDO_SER9,
            sum(case when sr.rol = 'sdo_ser10' then ta.saldo_detalle else 0 end) SDO_SER10
        from cliente_maestro cm2
        inner join transaccion_abonado ta on ta.cliente_clave = cm2.maestro_cliente_clave
        inner join servicios_roles_ws sr on sr.activo = true
            and sr.servicios_codigo = ta.tipo_servicio
            and sr.rol in ('agua','alcantarillado','ambiental','convenio','otros','ersaps','gestion_legal','sdo_ser6','sdo_ser7','sdo_ser8','sdo_ser9','sdo_ser10')
        where cm2.estado = true
          and (
            split_part(cm2.maestro_cliente_indicativo_ruta, '-', 3) = v_ruta
            or cm2.maestro_cliente_indicativo_ruta = v_ruta
            or (v_ruta_num is not null
                and split_part(cm2.maestro_cliente_indicativo_ruta, '-', 3) ~ '^[0-9]+$'
                and split_part(cm2.maestro_cliente_indicativo_ruta, '-', 3)::int = v_ruta_num)
          )
          and cm2.ciclos_id = p_ciclo
          and ta.estado = 'A'
        group by cm2.maestro_cliente_clave
    ) SD on SD.maestro_cliente_clave = cm.maestro_cliente_clave

    LEFT JOIN (
        select
            cm3.maestro_cliente_clave,
            bool_and(case when sr.rol = 'ser1' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser1,
            bool_and(case when sr.rol = 'ser2' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser2,
            bool_and(case when sr.rol = 'ser3' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser3,
            bool_and(case when sr.rol = 'ser4' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser4,
            bool_and(case when sr.rol = 'ser5' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser5,
            bool_and(case when sr.rol = 'ser6' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser6,
            bool_and(case when sr.rol = 'ser7' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser7,
            bool_and(case when sr.rol = 'ser8' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser8,
            bool_and(case when sr.rol = 'ser9' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser9,
            bool_and(case when sr.rol = 'ser10' then ctd.configuracion_tasas_detalle_aplicaservicio end) ser10
        from cliente_maestro cm3
        inner join configuracion_tasas ct on cm3.maestro_cliente_id = ct.maestro_cliente_id
        inner join configuracion_tasas_detalle ctd on ct.configuracion_tasas_id = ctd.configuracion_tasas_id
        inner join servicios s on s.servicios_id = ctd.servicios_id
        inner join servicios_roles_ws sr on sr.activo = true
            and sr.servicios_codigo = s.servicios_codigo
            and sr.rol in ('ser1','ser2','ser3','ser4','ser5','ser6','ser7','ser8','ser9','ser10')
        where cm3.estado = true
          and (
            split_part(cm3.maestro_cliente_indicativo_ruta, '-', 3) = v_ruta
            or cm3.maestro_cliente_indicativo_ruta = v_ruta
            or (v_ruta_num is not null
                and split_part(cm3.maestro_cliente_indicativo_ruta, '-', 3) ~ '^[0-9]+$'
                and split_part(cm3.maestro_cliente_indicativo_ruta, '-', 3)::int = v_ruta_num)
          )
          and cm3.ciclos_id = p_ciclo
        group by cm3.maestro_cliente_clave
    ) SC on SC.maestro_cliente_clave = cm.maestro_cliente_clave

    LEFT JOIN (
        select
            cm2.maestro_cliente_clave,
            sum(case when sr.rol = 'agua' then ta.saldo_detalle else 0 end) agua,
            sum(case when sr.rol = 'alcantarillado' then ta.saldo_detalle else 0 end) alcantarillado,
            sum(case when sr.rol = 'ambiental' then ta.saldo_detalle else 0 end) ambiental,
            sum(case when sr.rol = 'convenio' then ta.saldo_detalle else 0 end) convenio,
            sum(case when sr.rol = 'otros' then ta.saldo_detalle else 0 end) otros,
            sum(case when sr.rol = 'ersaps' then ta.saldo_detalle else 0 end) ERSAPS,
            sum(case when sr.rol = 'gestion_legal' then ta.saldo_detalle else 0 end) gestionlegal,
            sum(case when sr.rol = 'sdo_ser6' then ta.saldo_detalle else 0 end) SDO_SER6,
            sum(case when sr.rol = 'sdo_ser7' then ta.saldo_detalle else 0 end) SDO_SER7,
            sum(case when sr.rol = 'sdo_ser8' then ta.saldo_detalle else 0 end) SDO_SER8,
            sum(case when sr.rol = 'sdo_ser9' then ta.saldo_detalle else 0 end) SDO_SER9,
            sum(case when sr.rol = 'sdo_ser10' then ta.saldo_detalle else 0 end) SDO_SER10
        from cliente_maestro cm2
        inner join transaccion_abonado ta on ta.cliente_clave = cm2.maestro_cliente_clave
        inner join servicios_roles_ws sr on sr.activo = true
            and sr.servicios_codigo = ta.tipo_servicio
            and sr.rol in ('agua','alcantarillado','ambiental','convenio','otros','ersaps','gestion_legal','sdo_ser6','sdo_ser7','sdo_ser8','sdo_ser9','sdo_ser10')
        where cm2.estado = true
          and (
            split_part(cm2.maestro_cliente_indicativo_ruta, '-', 3) = v_ruta
            or cm2.maestro_cliente_indicativo_ruta = v_ruta
            or (v_ruta_num is not null
                and split_part(cm2.maestro_cliente_indicativo_ruta, '-', 3) ~ '^[0-9]+$'
                and split_part(cm2.maestro_cliente_indicativo_ruta, '-', 3)::int = v_ruta_num)
          )
          and cm2.ciclos_id = p_ciclo
          and ta.estado = 'A'
        group by cm2.maestro_cliente_clave
    ) SR on SR.maestro_cliente_clave = cm.maestro_cliente_clave

    LEFT JOIN historicomedicion hm
        on cm.maestro_cliente_clave = hm.clave
       and hm.ano = p_anio
       and hm.mes = p_mes

    WHERE cm.estado = true
      AND (
        split_part(cm.maestro_cliente_indicativo_ruta, '-', 3) = v_ruta
        OR cm.maestro_cliente_indicativo_ruta = v_ruta
        OR (v_ruta_num is not null
            AND split_part(cm.maestro_cliente_indicativo_ruta, '-', 3) ~ '^[0-9]+$'
            AND split_part(cm.maestro_cliente_indicativo_ruta, '-', 3)::int = v_ruta_num)
      )
      AND cm.ciclos_id = p_ciclo
    ORDER BY cm.maestro_cliente_secuencia, cm.maestro_cliente_clave;
END
$BODY$;

ALTER FUNCTION public.sp_medidores_por_ruta_ws(p_ruta character varying, p_ciclo integer, p_anio integer, p_mes integer)
    OWNER TO postgres;
