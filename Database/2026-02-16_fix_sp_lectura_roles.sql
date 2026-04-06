-- Fix: sp_lectura debe mapear servicios por roles (servicios_roles_ws)
-- y no por codigos hardcodeados (101..104).
-- Roles soportados por el app (taservi1..4): agua, alcantarillado, ambiental, ersaps.

CREATE OR REPLACE PROCEDURE public.sp_lectura(
    IN p_anio integer,
    IN p_mes integer,
    IN p_contador character varying,
    IN p_fecha date,
    IN p_usuario character varying,
    IN p_lecturaactual numeric,
    IN p_consumo numeric,
    IN p_taservi1 numeric,
    IN p_taservi2 numeric,
    IN p_taservi3 numeric,
    IN p_taservi4 numeric,
    IN p_ser3 character,
    IN p_ser4 character,
    IN p_observacion character varying,
    IN p_condicionlectura character varying,
    IN p_lecturapromedio numeric,
    IN p_codigolectura character,
    IN p_codigoinfo character,
    IN p_numerofactura character varying,
    IN p_correlativocai integer,
    IN p_idcai integer,
    IN p_tienemedidor character,
    IN p_clave character varying,
    IN p_informativo character varying,
    IN p_imagen bytea,
    IN p_descuento numeric,
    IN p_categoria character
)
LANGUAGE 'plpgsql'
AS $BODY$
declare 
    temprow RECORD;
    v_maestro_cliente_id integer;
    v_fechavence date;
    v_ciclo character varying;
    v_recibo integer;
    v_facturaid integer;
    v_plazo integer;
    v_ruta character varying;
    v_secuencia character varying;
    v_saldoabonado  numeric;
    v_cai RECORD;
    v_monto numeric;
    v_saldo_total numeric;
    v_saldo_detalle numeric;
begin
  IF p_tienemedidor = 'N' 
  then
      INSERT INTO public.historicosinmedidor(
     cuenta, ano, mes, numerofactura, correlativocai, idcai, fecha, usuario)
    VALUES (p_clave, p_anio, p_mes, p_numerofactura, p_correlativocai, p_idcai, now(), p_usuario);   
  else
      UPDATE historicomedicion
        SET     
        fecha_lect_act  = now(),
        usuario         = p_usuario,
        lect_act        = p_lecturaactual,
        consumo         = p_consumo,
        taservi1        = p_taservi1,
        taservi2        = p_taservi2,
        taservi3        = p_taservi3,
        taservi4        = p_taservi4,
        ser3            = p_ser3,
        ser4            = p_ser4,
        observacion     = p_observacion,
        condicion       = p_condicionLectura,
        lec_prom        = p_lecturapromedio,
        numerofactura   = p_numerofactura,
        correlativocai  = p_correlativocai,
        idcai           = p_idcai,
        codinfo         = p_informativo,
        imagenmedidor   = p_imagen,
        descuentoaPP    = p_descuento,
        categoriacliente = p_categoria
        WHERE contador = p_contador AND ano = p_anio AND mes = p_mes;
  end if;

    --obtener CAI info
    select ide, ruta, cai, codigo_base, contador_actual
    into v_cai
    from cai
    where ide = p_idcai
    limit 1;
    
    select into v_maestro_cliente_id  maestro_cliente_id
    from cliente_maestro
    where maestro_cliente_clave = p_clave
    limit 1;
    
    select into v_ciclo  ciclo
    from historicomedicion
    where contador = p_contador AND ano = p_anio AND mes = p_mes
    limit 1;
    
    select into v_fechavence,v_plazo  fechavence, diasvence
    from calendariopro
    where ano = p_anio and mes = p_mes and ciclo = v_ciclo
    order by ide desc 
    limit 1;
    
    select into v_saldoabonado sp_obtener_cliente_saldo
    from sp_obtener_cliente_saldo(p_clave);
    if COALESCE(v_saldoabonado,0) = 0 then 
      v_saldoabonado:= 0;
     end if;
     
    --ESTADO A ACTIVO C CERRADO, cerrar la factura activa, para generar nueva con los saldos 
    UPDATE factura
    set estado = 'C'
    where clientecodigo = p_clave and tipofacturacion  = 'S' and estado = 'A';
    
    
    INSERT INTO public.factura(
    numfactura, 
    clientecodigo, 
    tipofactura, 
    ano, 
    mes, 
    fechaemision, 
    fechavence, 
    rtn, 
    periodo, 
    numdei, 
    saldototal, 
    usuario, 
    identidad,
    estado,
    tipofacturacion
    )
    VALUES ( 
        p_numerofactura, 
        p_clave, 
        'F', 
        p_anio, 
        p_mes, 
        now(), 
        v_fechavence,
        '',
        concat_ws('/', cast(p_anio as character varying ), cast(p_mes as character varying)), 
        concat(v_cai.codigo_base , '-' , cast(rpad('00', 8, cast(v_cai.contador_actual as character varying)) as character varying)),           --numdei
        v_saldoabonado + p_taservi1 + p_taservi2 +p_taservi3 + p_taservi4 , --saldo total
        p_usuario, 
        '', --identidad
        'A', --estado
        'S')
    RETURNING numrecibo, id  INTO v_recibo, v_facturaid;

   -- OBTENER LA CONFIGURACION DE TASAS ESPECIFICA DEL CLIENTE
   FOR temprow IN
    SELECT ct.configuracion_tasas_id,
           ctd.configuracion_tasas_detalle_id,
           ctd.configuracion_tasas_detalle_aplicaservicio,
           s.servicios_codigo,
           s.servicios_descripcioncorta,
           sr.rol
    FROM configuracion_tasas ct
    INNER JOIN configuracion_tasas_detalle ctd on ct.configuracion_tasas_id = ctd.configuracion_tasas_id
    INNER JOIN servicios s on ctd.servicios_id = s.servicios_id
    INNER JOIN servicios_roles_ws sr on sr.servicios_codigo = s.servicios_codigo and sr.activo = true
    WHERE ct.maestro_cliente_id = v_maestro_cliente_id
      and ctd.configuracion_tasas_detalle_aplicaservicio = true
      and sr.rol in ('agua','alcantarillado','ambiental','ersaps')
    LOOP
        v_monto := 0;
        v_saldo_total := v_saldoabonado;

        if temprow.rol = 'agua' then
            v_monto := p_taservi1;
            v_saldo_total := v_saldoabonado + p_taservi1;
        elsif temprow.rol = 'alcantarillado' then
            v_monto := p_taservi2;
            v_saldo_total := v_saldoabonado + p_taservi1 + p_taservi2;
        elsif temprow.rol = 'ambiental' then
            v_monto := p_taservi3;
            v_saldo_total := v_saldoabonado + p_taservi1 + p_taservi2 + p_taservi3;
        elsif temprow.rol = 'ersaps' then
            v_monto := p_taservi4;
            v_saldo_total := v_saldoabonado + p_taservi1 + p_taservi2 + p_taservi3 + p_taservi4;
        end if;

        v_saldo_detalle := COALESCE(sp_obtener_cliente_saldo_servicio_detalle(p_clave, temprow.servicios_codigo),0) + v_monto;

        INSERT INTO public.factura_detalle(
        numrecibo, codigo, tiposervicio, descripcion, montovalor, factura_id, montovalor_saldo)
        VALUES (v_recibo, '', temprow.servicios_codigo, temprow.servicios_descripcioncorta, v_monto, v_facturaid, COALESCE(sp_obtener_cliente_saldo_servicio_detalle(p_clave, temprow.servicios_codigo),0));
        
        INSERT INTO public.transaccion_abonado(
        cliente_clave, 
        recibo, 
        tipotransaccion, 
        docufuente, 
        docufuente2, 
        fecha_docu, 
        tipo_partida, 
        descripcion, 
        plazo, 
        docuaplicar, 
        trans_aplicar, 
        debitos, 
        creditos, 
        saldo, 
        tipo_servicio, 
        aplicar_alca, 
        periodo, 
        tasa, 
        estado, 
        fecha_registro, 
        ciclo, 
        ruta, 
        secuencia, 
        tiene_med, 
        codigoplan, 
        motivo, 
        usuario, 
        saldo_detalle)
    values(
        p_clave, 
        v_recibo,
        temprow.servicios_codigo,
        0,
        '',
        now(), 
        '01',
        concat('Factura Periodo ', cast(p_anio as character varying),'/', cast(p_mes as character varying)),
         v_plazo,
        0,   --docu_aplicar
        '',
        v_monto,                        -- debitos
        0,                               -- creditos
        v_saldo_total,                   -- saldo 
        temprow.servicios_codigo,
        '', 
        concat_ws('/', cast( p_anio as character varying), cast( p_mes as character varying)) ,
        0,
        'A',
        now(),
        v_ciclo,
        v_ruta,
        v_secuencia,
        p_tienemedidor,
        '', 
        '',
        p_usuario, 
        v_saldo_detalle);
    
   END LOOP;
    
    --Actualizar CAI--
    IF(v_cai.contador_actual <  p_correlativocai) THEN
        UPDATE cai 
        SET contador_actual = p_correlativocai
        WHERE ide = p_idcai ;
    END IF;
    
end
$BODY$;

ALTER PROCEDURE public.sp_lectura(integer, integer, character varying, date, character varying, numeric, numeric, numeric, numeric, numeric, numeric, character, character, character varying, character varying, numeric, character, character, character varying, integer, integer, character, character varying, character varying, bytea, numeric, character)
    OWNER TO postgres;
