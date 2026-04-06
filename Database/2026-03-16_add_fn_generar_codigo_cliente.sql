CREATE OR REPLACE FUNCTION public.fn_generar_codigo_cliente()
RETURNS character varying
LANGUAGE plpgsql
AS $$
DECLARE
    v_codigo_candidato text;
    v_intento integer := 0;
    v_caracteres text := 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
    v_codigo text;
    i integer;
BEGIN
    -- Generar código alfanumérico único de 10 caracteres
    LOOP
        v_codigo := '';
        -- Generar 10 caracteres aleatorios (A-Z y 0-9)
        FOR i IN 1..10 LOOP
            v_codigo := v_codigo || SUBSTR(v_caracteres, (FLOOR(RANDOM() * 36)::integer + 1), 1);
        END LOOP;

        v_codigo_candidato := v_codigo;

        -- Verificar que el código no existe
        EXIT WHEN NOT EXISTS (
            SELECT 1
              FROM public.cliente_maestro cm
             WHERE cm.maestro_cliente_clave = v_codigo_candidato
        );

        v_intento := v_intento + 1;
        IF v_intento > 1000 THEN
            RAISE EXCEPTION 'No fue posible generar un código único después de 1000 intentos';
        END IF;
    END LOOP;

    RETURN v_codigo_candidato;
END;
$$;

COMMENT ON FUNCTION public.fn_generar_codigo_cliente()
IS 'Genera un código Sistema único y aleatorio de 10 caracteres alfanuméricos (A-Z, 0-9) para cada cliente';
