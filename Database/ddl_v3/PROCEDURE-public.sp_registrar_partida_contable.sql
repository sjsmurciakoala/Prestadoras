CREATE TYPE public.tipo_linea_partida AS (
    account_id int8,
    cost_center_id int8,
    description varchar(300),
    debit_amount numeric(18, 2),
    credit_amount numeric(18, 2),
    third_party_id int8,
    currency_code bpchar(3),
    exchange_rate numeric
);

-- DROP PROCEDURE public.sp_registrar_partida_contable(int8, int8, int8, varchar, varchar, varchar, varchar, timestamptz, varchar, varchar, int8, _tipo_linea_partida);

CREATE OR REPLACE PROCEDURE public.sp_registrar_partida_contable(IN p_company_id bigint, IN p_journal_id bigint, IN p_period_id bigint, IN p_module character varying, IN p_document_type character varying, IN p_document_number character varying, IN p_partida_number character varying, IN p_partida_date timestamp with time zone, IN p_description character varying, IN p_created_by character varying, IN p_type_id bigint, IN p_lineas tipo_linea_partida[])
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    v_partida_id int8;
    v_total_debit numeric(28, 2) := 0;
    v_total_credit numeric(28, 2) := 0;
    v_linea public.tipo_linea_partida;
    v_line_number int2 := 1;
BEGIN
    -- 1. Validación de cuadre y cálculo de totales
    FOR i IN array_lower(p_lineas, 1) .. array_upper(p_lineas, 1) LOOP
        v_total_debit := v_total_debit + COALESCE(p_lineas[i].debit_amount, 0);
        v_total_credit := v_total_credit + COALESCE(p_lineas[i].credit_amount, 0);
    END LOOP;

    IF v_total_debit <> v_total_credit THEN
        RAISE EXCEPTION 'La partida no está cuadrada. Débitos: %, Créditos: %', v_total_debit, v_total_credit;
    END IF;

    -- 2. Registro de Cabecera
    INSERT INTO public.con_partida_hdr (
        company_id, journal_id, period_id, "module", document_type, 
        document_number, poliza_number, poliza_date, description, 
        status, created_at, created_by, type_id, total_debit, total_credit
    ) VALUES (
        p_company_id, p_journal_id, p_period_id, p_module, p_document_type, 
        p_document_number, p_partida_number, p_partida_date, p_description, 
        0, now(), p_created_by, p_type_id, v_total_debit, v_total_credit
    ) RETURNING poliza_id INTO v_partida_id; -- Captura el ID para el detalle

    -- 3. Registro de Detalles (Múltiples contra-cuentas)
    FOREACH v_linea IN ARRAY p_lineas LOOP
        INSERT INTO public.con_partida_dtl (
            company_id, poliza_id, line_number, account_id, 
            cost_center_id, description, debit_amount, credit_amount, 
            third_party_id, currency_code, exchange_rate, source_document
        ) VALUES (
            p_company_id, v_partida_id, v_line_number, v_linea.account_id, 
            v_linea.cost_center_id, v_linea.description, v_linea.debit_amount, v_linea.credit_amount, 
            v_linea.third_party_id, v_linea.currency_code, v_linea.exchange_rate, p_document_number
        );
        v_line_number := v_line_number + 1;
    END LOOP;

    -- La transaccion se controla desde el servicio llamador.
END;
$procedure$
;

-- Permissions

ALTER PROCEDURE public.sp_registrar_partida_contable(int8, int8, int8, varchar, varchar, varchar, varchar, timestamptz, varchar, varchar, int8, _tipo_linea_partida) OWNER TO postgres;
GRANT ALL ON PROCEDURE public.sp_registrar_partida_contable(int8, int8, int8, varchar, varchar, varchar, varchar, timestamptz, varchar, varchar, int8, _tipo_linea_partida) TO postgres;
