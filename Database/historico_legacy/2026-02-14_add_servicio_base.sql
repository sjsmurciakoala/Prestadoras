-- Agregar bandera de servicio base global y actualizar SP de configuración de tasas
-- Ejecutar en PostgreSQL (schema public)

-- 1) Columna + índice único (solo un servicio base)
ALTER TABLE IF EXISTS public.servicios
    ADD COLUMN IF NOT EXISTS es_servicio_base boolean NOT NULL DEFAULT false;

CREATE UNIQUE INDEX IF NOT EXISTS ux_servicios_base
    ON public.servicios (es_servicio_base)
    WHERE es_servicio_base;

-- 2) Marcar servicio base (elige UNO)
-- UPDATE public.servicios SET es_servicio_base = false;
-- UPDATE public.servicios SET es_servicio_base = true WHERE servicios_id = <ID>;
-- o por código:
-- UPDATE public.servicios SET es_servicio_base = true WHERE servicios_codigo = '<CODIGO>';

-- 3) SP: usar servicio base global (sin hardcode)
CREATE OR REPLACE PROCEDURE public.sp_generar_configuracion_tasas_cliente(
    IN p_cliente_clave character varying,
    IN p_usuario character varying
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    v_configuracion_tasas_id integer;
    v_maestro_cliente_id integer;
    v_categoria_id integer;
    v_tipo_uso_codigo integer;
    v_servicio_primario integer;
    v_servicio_primario_monto numeric;
    v_porcentaje numeric;
    v_total numeric;
    temprow RECORD;
    temprow2 RECORD;
    v_letra character varying;
    v_tipo_text text;
    v_categoria_text text;
    v_codigo text;
    v_tipo integer;
    v_categoria integer;
BEGIN
    SELECT
        maestro_cliente_id,
        categoria_servicio_id,
        tipo_uso_codigo,
        letracodigo
    INTO
        v_maestro_cliente_id,
        v_categoria_id,
        v_tipo_uso_codigo,
        v_letra
    FROM cliente_maestro
    WHERE maestro_cliente_clave = p_cliente_clave
    LIMIT 1;

    SELECT servicios_id
    INTO v_servicio_primario
    FROM servicios
    WHERE es_servicio_base = true AND estado = true
    LIMIT 1;

    IF v_servicio_primario IS NULL THEN
        RAISE EXCEPTION 'Servicio base no configurado en servicios';
    END IF;

    v_letra := btrim(coalesce(v_letra, ''));

    IF v_letra = '' THEN
        v_servicio_primario_monto := 0;
    ELSE
        IF position(',' in v_letra) > 0 THEN
            v_tipo_text := split_part(v_letra, ',', 1);
            v_categoria_text := split_part(v_letra, ',', 2);
            v_codigo := split_part(v_letra, ',', 3);

            IF v_tipo_text ~ '^[0-9]+$' AND v_categoria_text ~ '^[0-9]+$' THEN
                v_tipo := v_tipo_text::integer;
                v_categoria := v_categoria_text::integer;
            ELSE
                v_tipo := v_tipo_uso_codigo;
                v_categoria := v_categoria_id;
                v_codigo := v_letra;
            END IF;
        ELSE
            v_tipo := v_tipo_uso_codigo;
            v_categoria := v_categoria_id;
            v_codigo := v_letra;
        END IF;

        SELECT valor
        INTO v_servicio_primario_monto
        FROM tarifas t
        WHERE t.tipo = v_tipo
          AND t.categoria_id = v_categoria
          AND t.codigo = v_codigo
        LIMIT 1;

        v_servicio_primario_monto := COALESCE(v_servicio_primario_monto, 0);
    END IF;

    INSERT INTO configuracion_tasas
        (maestro_cliente_id, estado, usuariocreacion, fechacreacion)
    VALUES
        (v_maestro_cliente_id, true, p_usuario, now())
    RETURNING configuracion_tasas_id INTO v_configuracion_tasas_id;

    INSERT INTO public.configuracion_tasas_detalle(
        configuracion_tasas_id,
        servicios_id,
        configuracion_tasas_detalle_aplicaservicio,
        configuracion_tasas_detalle_monto,
        estado,
        usuariocreacion,
        fechacreacion)
    VALUES
        (v_configuracion_tasas_id, v_servicio_primario, true, v_servicio_primario_monto, true, p_usuario, now());

    FOR temprow IN
        SELECT a.ide, a.servicio_id
        FROM configuracion_cobros_adicionales a
        INNER JOIN concepto_cobro_adicional c ON c.ide = a.concepto_id
        WHERE a.categoria_id = v_categoria_id
    LOOP
        v_total := 0.0;

        FOR temprow2 IN
            SELECT *
            FROM configuracion_cobros_adicionales_detalle d
            WHERE d.configuracion_cobro_adicional_ide = temprow.ide
        LOOP
            v_porcentaje := temprow2.porcentaje;
            v_total := v_total + (v_servicio_primario_monto * v_porcentaje);
        END LOOP;

        INSERT INTO public.configuracion_tasas_detalle(
            configuracion_tasas_id,
            servicios_id,
            configuracion_tasas_detalle_aplicaservicio,
            configuracion_tasas_detalle_monto,
            estado,
            usuariocreacion,
            fechacreacion)
        VALUES
            (v_configuracion_tasas_id, temprow.servicio_id, true, COALESCE(v_total, 0), true, p_usuario, now());
    END LOOP;
END
$BODY$;

ALTER PROCEDURE public.sp_generar_configuracion_tasas_cliente(character varying, character varying)
    OWNER TO postgres;
