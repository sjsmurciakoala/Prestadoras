-- Nuevo contrato: detalle por servicio (JSONB)
-- sp_lectura_v2 registra lectura + factura usando montos por servicio.

CREATE OR REPLACE PROCEDURE public.sp_lectura_v2(
    IN p_anio integer,
    IN p_mes integer,
    IN p_contador character varying,
    IN p_fecha date,
    IN p_usuario character varying,
    IN p_lecturaactual numeric,
    IN p_consumo numeric,
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
    IN p_categoria character,
    IN p_detalle jsonb
)
LANGUAGE 'plpgsql'
AS $BODY$
declare
    v_maestro_cliente_id integer;
    v_fechavence date;
    v_ciclo character varying;
    v_recibo integer;
    v_facturaid integer;
    v_plazo integer;
    v_ruta character varying;
    v_secuencia character varying;
    v_saldoabonado numeric;
    v_cai RECORD;
    v_monto numeric;
    v_total numeric := 0;
    v_saldo_total numeric;
    v_saldo_detalle numeric;
    v_line RECORD;
    v_descripcion character varying;
begin
    if p_tienemedidor = 'N' then
        INSERT INTO public.historicosinmedidor(
            cuenta, ano, mes, numerofactura, correlativocai, idcai, fecha, usuario)
        VALUES (p_clave, p_anio, p_mes, p_numerofactura, p_correlativocai, p_idcai, now(), p_usuario);
    else
        UPDATE historicomedicion
            SET
                fecha_lect_act   = now(),
                usuario          = p_usuario,
                lect_act         = p_lecturaactual,
                consumo          = p_consumo,
                taservi1         = 0,
                taservi2         = 0,
                taservi3         = 0,
                taservi4         = 0,
                ser3             = p_ser3,
                ser4             = p_ser4,
                observacion      = p_observacion,
                condicion        = p_condicionlectura,
                lec_prom         = p_lecturapromedio,
                numerofactura    = p_numerofactura,
                correlativocai   = p_correlativocai,
                idcai            = p_idcai,
                codinfo          = p_informativo,
                imagenmedidor    = p_imagen,
                descuentoaPP     = p_descuento,
                categoriacliente = p_categoria
        WHERE contador = p_contador AND ano = p_anio AND mes = p_mes;
    end if;

    -- total desde detalle
    if p_detalle is not null then
        select coalesce(sum((d->>'Monto')::numeric), 0)
          into v_total
        from jsonb_array_elements(p_detalle) d;
    end if;

    -- obtener CAI
    select ide, ruta, cai, codigo_base, contador_actual
      into v_cai
      from cai
     where ide = p_idcai
     limit 1;

    select into v_maestro_cliente_id maestro_cliente_id
      from cliente_maestro
     where maestro_cliente_clave = p_clave
     limit 1;

    select into v_ciclo ciclo
      from historicomedicion
     where contador = p_contador AND ano = p_anio AND mes = p_mes
     limit 1;

    select into v_fechavence, v_plazo fechavence, diasvence
      from calendariopro
     where ano = p_anio and mes = p_mes and ciclo = v_ciclo
     order by ide desc
     limit 1;

    select into v_saldoabonado sp_obtener_cliente_saldo
      from sp_obtener_cliente_saldo(p_clave);
    if coalesce(v_saldoabonado, 0) = 0 then
        v_saldoabonado := 0;
    end if;

    -- cerrar factura activa
    UPDATE factura
       set estado = 'C'
     where clientecodigo = p_clave and tipofacturacion = 'S' and estado = 'A';

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
        concat(v_cai.codigo_base , '-' , cast(rpad('00', 8, cast(v_cai.contador_actual as character varying)) as character varying)),
        v_saldoabonado + v_total,
        p_usuario,
        '',
        'A',
        'S'
    )
    RETURNING numrecibo, id INTO v_recibo, v_facturaid;

    v_saldo_total := v_saldoabonado;

    -- detalle por servicio (orden del JSON)
    FOR v_line IN
        SELECT
            (d->>'ServicioCodigo')::varchar AS servicio_codigo,
            nullif(trim(d->>'Descripcion'), '') AS descripcion,
            coalesce((d->>'Monto')::numeric, 0) AS monto
        FROM jsonb_array_elements(p_detalle) d
        WHERE coalesce((d->>'Monto')::numeric, 0) <> 0
    LOOP
        v_monto := v_line.monto;

        v_descripcion := v_line.descripcion;
        if v_descripcion is null then
            select s.servicios_descripcioncorta
              into v_descripcion
              from servicios s
             where s.servicios_codigo = v_line.servicio_codigo
             limit 1;
        end if;

        v_saldo_total := v_saldo_total + v_monto;
        v_saldo_detalle := coalesce(sp_obtener_cliente_saldo_servicio_detalle(p_clave, v_line.servicio_codigo), 0) + v_monto;

        INSERT INTO public.factura_detalle(
            numrecibo, codigo, tiposervicio, descripcion, montovalor, factura_id, montovalor_saldo)
        VALUES (
            v_recibo, '', v_line.servicio_codigo, v_descripcion, v_monto, v_facturaid,
            coalesce(sp_obtener_cliente_saldo_servicio_detalle(p_clave, v_line.servicio_codigo), 0)
        );

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
        VALUES (
            p_clave,
            v_recibo,
            v_line.servicio_codigo,
            0,
            '',
            now(),
            '01',
            concat('Factura Periodo ', cast(p_anio as character varying), '/', cast(p_mes as character varying)),
            v_plazo,
            0,
            '',
            v_monto,
            0,
            v_saldo_total,
            v_line.servicio_codigo,
            '',
            concat_ws('/', cast(p_anio as character varying), cast(p_mes as character varying)),
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
            v_saldo_detalle
        );
    END LOOP;

    -- actualizar CAI
    IF (v_cai.contador_actual < p_correlativocai) THEN
        UPDATE cai
           SET contador_actual = p_correlativocai
         WHERE ide = p_idcai;
    END IF;
end
$BODY$;

ALTER PROCEDURE public.sp_lectura_v2(
    integer, integer, character varying, date, character varying, numeric, numeric,
    character, character, character varying, character varying, numeric, character, character,
    character varying, integer, integer, character, character varying, character varying, bytea,
    numeric, character, jsonb)
    OWNER TO postgres;

