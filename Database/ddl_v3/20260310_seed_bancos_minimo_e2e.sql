-- ============================================================
-- SEED MINIMO E2E: Catalogos base para pruebas de bancos
-- Fecha: 2026-03-10
-- Objetivo:
--   Crear (si hace falta) catalogos minimos para ejecutar
--   20260310_test_e2e_flujo_bancos.sql sin hardcodes de negocio.
-- ============================================================
--
-- Uso:
-- 1) Ajustar parametros en tmp_con_seed_bancos_params.
-- 2) Ejecutar script completo en una misma sesion.
-- 3) Guardar resultado final como evidencia.
--

-- ------------------------------------------------------------
-- Parametros
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_seed_bancos_params;
CREATE TEMP TABLE tmp_con_seed_bancos_params (
    company_id bigint NOT NULL,
    user_name text NOT NULL,
    tipo_transaccion_code varchar(3) NOT NULL,
    entra_sale bpchar(1) NOT NULL DEFAULT 'E'
);

INSERT INTO tmp_con_seed_bancos_params (
    company_id,
    user_name,
    tipo_transaccion_code,
    entra_sale
) VALUES (
    1,
    'jmurcia',
    'TRF',
    'E'
);

-- ------------------------------------------------------------
-- Resultado
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_seed_bancos_result;
CREATE TEMP TABLE tmp_con_seed_bancos_result (
    company_id bigint NOT NULL,
    user_name text NOT NULL,
    journal_id bigint NOT NULL,
    ban_moneda_id bigint NOT NULL,
    ban_banco_id bigint NOT NULL,
    banco_cuenta_id bigint NOT NULL,
    bank_account_id bigint NOT NULL,
    contra_account_id bigint NOT NULL,
    tipo_transaccion_code varchar(3) NOT NULL,
    ban_tipo_transaccion_id bigint NOT NULL,
    cod_tipopartida bpchar(3) NOT NULL,
    entra_sale bpchar(1) NOT NULL
);

DO $$
DECLARE
    v_company_id bigint;
    v_user_name text;
    v_tipo_transaccion_code varchar(3);
    v_entra_sale bpchar(1);

    v_ban_moneda_id bigint;
    v_ban_banco_id bigint;
    v_banco_cuenta_id bigint;
    v_ban_tipo_transaccion_id bigint;
    v_journal_id bigint;

    v_bank_account_id bigint;
    v_contra_account_id bigint;
    v_tipo_partida_id bigint;
    v_cod_tipopartida bpchar(3);

    v_banco_code text;
    v_cuenta_code text;
    v_numero_cuenta text;
BEGIN
    SELECT
        p.company_id,
        COALESCE(NULLIF(btrim(p.user_name), ''), current_user),
        upper(p.tipo_transaccion_code),
        COALESCE(NULLIF(upper(p.entra_sale::text), ''), 'E')::bpchar
    INTO
        v_company_id,
        v_user_name,
        v_tipo_transaccion_code,
        v_entra_sale
    FROM tmp_con_seed_bancos_params p
    LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Seed bancos E2E: company_id es requerido.';
    END IF;

    IF v_entra_sale NOT IN ('E', 'S') THEN
        RAISE EXCEPTION 'Seed bancos E2E: entra_sale debe ser E o S. Valor actual: %', v_entra_sale;
    END IF;

    -- 1) Moneda base HNL para bancos
    SELECT m.ban_moneda_id
      INTO v_ban_moneda_id
      FROM public.ban_moneda m
     WHERE m.company_id = v_company_id
       AND upper(m.codigo) = 'HNL'
     ORDER BY
       CASE WHEN COALESCE(m.es_base, false) THEN 0 ELSE 1 END,
       m.ban_moneda_id
     LIMIT 1;

    IF v_ban_moneda_id IS NULL THEN
        INSERT INTO public.ban_moneda (
            company_id, codigo, descripcion, pais, factor, es_base, created_by
        ) VALUES (
            v_company_id, 'HNL', 'LEMPIRA', 'HN', 1, true, v_user_name
        )
        RETURNING ban_moneda_id INTO v_ban_moneda_id;
    END IF;

    -- 2) Banco activo
    SELECT b.ban_banco_id
      INTO v_ban_banco_id
      FROM public.ban_banco b
     WHERE b.company_id = v_company_id
       AND COALESCE(b.activo, true) = true
     ORDER BY b.ban_banco_id
     LIMIT 1;

    IF v_ban_banco_id IS NULL THEN
        v_banco_code := 'E2E' || to_char(clock_timestamp(), 'HH24MISS');
        INSERT INTO public.ban_banco (
            company_id, code, nombre, activo, created_by
        ) VALUES (
            v_company_id, v_banco_code, 'BANCO E2E', true, v_user_name
        )
        RETURNING ban_banco_id INTO v_ban_banco_id;
    END IF;

    -- 3) Cuentas contables posting (banco + contra)
    SELECT a.account_id
      INTO v_bank_account_id
      FROM public.con_plan_cuentas a
     WHERE a.company_id = v_company_id
       AND COALESCE(a.allows_posting, false) = true
     ORDER BY a.account_id
     LIMIT 1;

    IF v_bank_account_id IS NULL THEN
        RAISE EXCEPTION 'Seed bancos E2E: no existe cuenta contable posting para company %.', v_company_id;
    END IF;

    SELECT a.account_id
      INTO v_contra_account_id
      FROM public.con_plan_cuentas a
     WHERE a.company_id = v_company_id
       AND COALESCE(a.allows_posting, false) = true
       AND a.account_id <> v_bank_account_id
     ORDER BY a.account_id
     LIMIT 1;

    IF v_contra_account_id IS NULL THEN
        RAISE EXCEPTION 'Seed bancos E2E: no existe contra cuenta posting para company %.', v_company_id;
    END IF;

    -- 4) Cuenta bancaria activa ligada a cuenta contable
    SELECT c.banco_cuenta_id
      INTO v_banco_cuenta_id
      FROM public.ban_cuenta c
     WHERE c.company_id = v_company_id
       AND COALESCE(c.activo, true) = true
       AND c.cont_account_id = v_bank_account_id
     ORDER BY c.banco_cuenta_id
     LIMIT 1;

    IF v_banco_cuenta_id IS NULL THEN
        v_cuenta_code := 'E2E-BC-' || to_char(clock_timestamp(), 'HH24MISS');
        v_numero_cuenta := 'E2E' || to_char(clock_timestamp(), 'DDHH24MISSMS');

        INSERT INTO public.ban_cuenta (
            company_id, code, nombre, banco_nombre, tipo, currency_code,
            numero_cuenta, saldo_inicial, estado, cont_account_id, ban_banco_id, activo, created_by
        ) VALUES (
            v_company_id, v_cuenta_code, 'CUENTA E2E', 'BANCO E2E', 'CHEQUES', 'HNL',
            v_numero_cuenta, 0, 'ACTIVE', v_bank_account_id, v_ban_banco_id, true, v_user_name
        )
        RETURNING banco_cuenta_id INTO v_banco_cuenta_id;
    END IF;

    -- 5) Tipo contable activo para resolver cod_tipopartida de bancos
    SELECT t.type_id
      INTO v_tipo_partida_id
      FROM public.con_tipo_transaccion t
     WHERE t.company_id = v_company_id
       AND COALESCE(
            t.status_id,
            CASE
                WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('ACTIVE', 'ACTIVO') THEN 1
                WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('INACTIVE', 'INACTIVO') THEN 0
                ELSE 1
            END
       ) = 1
     ORDER BY
       CASE WHEN COALESCE(t.is_default, false) THEN 0 ELSE 1 END,
       t.type_id
     LIMIT 1;

    IF v_tipo_partida_id IS NULL THEN
        RAISE EXCEPTION 'Seed bancos E2E: no existe con_tipo_transaccion activo para company %.', v_company_id;
    END IF;

    v_cod_tipopartida := right(lpad(v_tipo_partida_id::text, 3, '0'), 3)::bpchar;

    -- 6) Tipo de transaccion bancario activo (TRF por defecto)
    SELECT bt.ban_tipo_transaccion_id
      INTO v_ban_tipo_transaccion_id
      FROM public.ban_tipos_transacciones bt
     WHERE bt.company_id = v_company_id
       AND upper(bt.tipo_transaccion) = upper(v_tipo_transaccion_code)
     ORDER BY bt.ban_tipo_transaccion_id
     LIMIT 1;

    IF v_ban_tipo_transaccion_id IS NULL THEN
        INSERT INTO public.ban_tipos_transacciones (
            company_id, tipo_transaccion, cod_tipopartida, correlativo,
            nombre, entra_sale, del_sistema, estado, created_by
        ) VALUES (
            v_company_id, v_tipo_transaccion_code, v_cod_tipopartida, '000000',
            'E2E ' || v_tipo_transaccion_code, v_entra_sale, 'N', 'ACTIVE', v_user_name
        )
        RETURNING ban_tipo_transaccion_id INTO v_ban_tipo_transaccion_id;
    ELSE
        UPDATE public.ban_tipos_transacciones
           SET estado = 'ACTIVE',
               entra_sale = COALESCE(NULLIF(upper(btrim(entra_sale::text)), ''), v_entra_sale)::bpchar,
               updated_at = now(),
               updated_by = v_user_name
         WHERE ban_tipo_transaccion_id = v_ban_tipo_transaccion_id;
    END IF;

    -- 7) Diario contable BAN activo para flujo bancos
    INSERT INTO public.con_diario (
        company_id, code, name, description, sequence_prefix,
        last_sequence, is_active, allows_manual, created_at, created_by
    ) VALUES (
        v_company_id, 'BAN', 'Diario Bancos', 'E2E bancos', 'BAN',
        0, true, true, now(), v_user_name
    )
    ON CONFLICT (company_id, code) DO UPDATE
        SET last_sequence = COALESCE(public.con_diario.last_sequence, 0),
            is_active = true,
            allows_manual = true,
            updated_at = now(),
            updated_by = EXCLUDED.created_by
    RETURNING journal_id INTO v_journal_id;

    INSERT INTO tmp_con_seed_bancos_result (
        company_id,
        user_name,
        journal_id,
        ban_moneda_id,
        ban_banco_id,
        banco_cuenta_id,
        bank_account_id,
        contra_account_id,
        tipo_transaccion_code,
        ban_tipo_transaccion_id,
        cod_tipopartida,
        entra_sale
    ) VALUES (
        v_company_id,
        v_user_name,
        v_journal_id,
        v_ban_moneda_id,
        v_ban_banco_id,
        v_banco_cuenta_id,
        v_bank_account_id,
        v_contra_account_id,
        v_tipo_transaccion_code,
        v_ban_tipo_transaccion_id,
        v_cod_tipopartida,
        v_entra_sale
    );
END
$$;

SELECT * FROM tmp_con_seed_bancos_result;
