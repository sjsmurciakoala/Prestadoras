    -- ============================================================
    -- TEST NEGATIVO: Bloqueo de posteo en periodo en precierre/cerrado
    -- Fecha: 2026-03-10
    -- Objetivo:
    --   Confirmar que sp_con_postear_poliza bloquea el posteo cuando
    --   no existe periodo abierto para la fecha de la poliza.
    -- ============================================================
    --
    -- Uso:
    -- 1) Ajustar parametros en tmp_con_test_posteo_cerrado_params.
    -- 2) Ejecutar script completo en una misma sesion.
    -- 3) Guardar evidencia (mensaje de bloqueo + estado final DRAFT).
    --

    -- ------------------------------------------------------------
    -- Parametros
    -- ------------------------------------------------------------
    DROP TABLE IF EXISTS tmp_con_test_posteo_cerrado_params;
    CREATE TEMP TABLE tmp_con_test_posteo_cerrado_params (
        company_id bigint NOT NULL,
        user_name text NOT NULL,
        poliza_date date NULL,
        amount_total numeric(18,2) NOT NULL,
        keep_draft_after_test boolean NOT NULL DEFAULT false
    );

    INSERT INTO tmp_con_test_posteo_cerrado_params (
        company_id,
        user_name,
        poliza_date,
        amount_total,
        keep_draft_after_test
    ) VALUES (
        1,
        'jmurcia',
        NULL,   -- NULL = toma fecha de un periodo en precierre/cerrado existente
        99.99,
        false
    );

    -- ------------------------------------------------------------
    -- Seed de respaldo: crear periodo CERRADO (mes anterior) si no existe ninguno
    -- ------------------------------------------------------------
    DO $$
    DECLARE
        v_company_id bigint;
        v_user_name text;
        v_poliza_date date;
        v_has_exclusive_closed boolean;
        v_has_status_id boolean;
        v_prev_start date;
        v_prev_end date;
        v_prev_code text;
        v_prev_name text;
        v_min_start date;
    BEGIN
        SELECT
            p.company_id,
            COALESCE(NULLIF(btrim(p.user_name), ''), current_user),
            p.poliza_date
        INTO
            v_company_id,
            v_user_name,
            v_poliza_date
        FROM tmp_con_test_posteo_cerrado_params p
        LIMIT 1;

        -- Buscar un periodo en precierre/cerrado que NO comparta fecha con ningun abierto.
        SELECT EXISTS (
            SELECT 1
            FROM public.con_periodo_contable cp
            WHERE cp.company_id = v_company_id
            AND COALESCE(cp.status_id, 2) IN (1, 2)
            AND NOT EXISTS (
                SELECT 1
                FROM public.con_periodo_contable op
                WHERE op.company_id = cp.company_id
                  AND COALESCE(op.status_id, 2) = 0
                  AND cp.start_date::date BETWEEN op.start_date::date AND op.end_date::date
            )
        ) INTO v_has_exclusive_closed;

        IF v_has_exclusive_closed THEN
            RETURN;
        END IF;

        -- Solo auto-seed cuando la fecha es automatica (NULL).
        IF v_poliza_date IS NOT NULL THEN
            RETURN;
        END IF;

        -- Crear periodo cerrado aislado (un dia antes del primer periodo existente).
        SELECT COALESCE(MIN(cp.start_date)::date, current_date)
          INTO v_min_start
          FROM public.con_periodo_contable cp
         WHERE cp.company_id = v_company_id;

        v_prev_start := v_min_start - 1;
        v_prev_end := v_prev_start;
        v_prev_code := 'NEG' || to_char(v_prev_start, 'YYYYMMDD');
        v_prev_name := 'NEG CERRADO ' || to_char(v_prev_start, 'YYYY-MM-DD');

        SELECT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
            AND table_name = 'con_periodo_contable'
            AND column_name = 'status_id'
        ) INTO v_has_status_id;

        IF v_has_status_id THEN
        INSERT INTO public.con_periodo_contable (
            company_id, code, name, start_date, end_date,
            status, status_id, closed_at, closed_by, created_at, created_by
        ) VALUES (
            v_company_id, v_prev_code, v_prev_name, v_prev_start, v_prev_end,
            'CERRADO', 2, now(), v_user_name, now(), v_user_name
        )
        ON CONFLICT (company_id, code) DO UPDATE
            SET name = EXCLUDED.name,
                start_date = EXCLUDED.start_date,
                end_date = EXCLUDED.end_date,
                    status = 'CERRADO',
                    status_id = 2,
                    closed_at = COALESCE(public.con_periodo_contable.closed_at, now()),
                    closed_by = COALESCE(public.con_periodo_contable.closed_by, EXCLUDED.closed_by),
                    updated_at = now(),
                    updated_by = EXCLUDED.created_by;
        ELSE
        INSERT INTO public.con_periodo_contable (
            company_id, code, name, start_date, end_date,
            status, closed_at, closed_by, created_at, created_by
        ) VALUES (
            v_company_id, v_prev_code, v_prev_name, v_prev_start, v_prev_end,
            'CERRADO', now(), v_user_name, now(), v_user_name
        )
        ON CONFLICT (company_id, code) DO UPDATE
            SET name = EXCLUDED.name,
                start_date = EXCLUDED.start_date,
                end_date = EXCLUDED.end_date,
                    status = 'CERRADO',
                    closed_at = COALESCE(public.con_periodo_contable.closed_at, now()),
                    closed_by = COALESCE(public.con_periodo_contable.closed_by, EXCLUDED.closed_by),
                    updated_at = now(),
                    updated_by = EXCLUDED.created_by;
        END IF;
    END
    $$;

    -- ------------------------------------------------------------
    -- Resolver contexto
    -- ------------------------------------------------------------
    DROP TABLE IF EXISTS tmp_con_test_posteo_cerrado_ctx;
    CREATE TEMP TABLE tmp_con_test_posteo_cerrado_ctx AS
    WITH p AS (
        SELECT *
        FROM tmp_con_test_posteo_cerrado_params
        LIMIT 1
    ),
    period_target AS (
        SELECT
            cp.period_id,
            COALESCE(p.poliza_date, cp.start_date::date) AS poliza_date,
            cp.status_id,
            cp.status
        FROM p
        JOIN public.con_periodo_contable cp
        ON cp.company_id = p.company_id
        WHERE COALESCE(cp.status_id, 2) IN (1, 2)
        AND (
            p.poliza_date IS NULL
            OR p.poliza_date BETWEEN cp.start_date::date AND cp.end_date::date
        )
        AND NOT EXISTS (
            SELECT 1
            FROM public.con_periodo_contable op
            WHERE op.company_id = cp.company_id
              AND COALESCE(op.status_id, 2) = 0
              AND COALESCE(p.poliza_date, cp.start_date::date) BETWEEN op.start_date::date AND op.end_date::date
        )
        ORDER BY cp.start_date DESC
        LIMIT 1
    ),
    journal_resolved AS (
        SELECT d.journal_id
        FROM public.con_diario d
        JOIN p ON p.company_id = d.company_id
        WHERE d.is_active = true
        ORDER BY d.journal_id
        LIMIT 1
    ),
    type_resolved AS (
        SELECT t.type_id
        FROM public.con_tipo_transaccion t
        JOIN p ON p.company_id = t.company_id
        WHERE COALESCE(
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
        LIMIT 1
    ),
    accounts AS (
        SELECT
            MIN(a.account_id) AS acc_1,
            MIN(a.account_id) FILTER (
                WHERE a.account_id > (SELECT MIN(a2.account_id)
                                    FROM public.con_plan_cuentas a2
                                    JOIN p p2 ON p2.company_id = a2.company_id
                                    WHERE COALESCE(a2.allows_posting, false) = true)
            ) AS acc_2
        FROM public.con_plan_cuentas a
        JOIN p ON p.company_id = a.company_id
        WHERE COALESCE(a.allows_posting, false) = true
    )
    SELECT
        p.company_id,
        COALESCE(NULLIF(btrim(p.user_name), ''), current_user) AS user_name,
        p.amount_total,
        p.keep_draft_after_test,
        pt.period_id AS closed_period_id,
        pt.poliza_date,
        jr.journal_id,
        tr.type_id,
        a.acc_1 AS debit_account_id,
        a.acc_2 AS credit_account_id
    FROM p
    LEFT JOIN period_target pt ON true
    LEFT JOIN journal_resolved jr ON true
    LEFT JOIN type_resolved tr ON true
    LEFT JOIN accounts a ON true;

    SELECT
        company_id,
        user_name,
        closed_period_id,
        poliza_date,
        journal_id,
        type_id,
        debit_account_id,
        credit_account_id,
        amount_total
    FROM tmp_con_test_posteo_cerrado_ctx;

    DO $$
    DECLARE
        r record;
        v_open_count int;
    BEGIN
        SELECT *
        INTO r
        FROM tmp_con_test_posteo_cerrado_ctx
        LIMIT 1;

        IF r.closed_period_id IS NULL THEN
            RAISE EXCEPTION 'Precheck negativo: no existe periodo en precierre/cerrado para company % (o para la fecha indicada).', r.company_id;
        END IF;

        IF r.journal_id IS NULL THEN
            RAISE EXCEPTION 'Precheck negativo: no existe con_diario activo para company %.', r.company_id;
        END IF;

        IF r.type_id IS NULL THEN
            RAISE EXCEPTION 'Precheck negativo: no existe con_tipo_transaccion activo para company %.', r.company_id;
        END IF;

        IF r.debit_account_id IS NULL OR r.credit_account_id IS NULL THEN
            RAISE EXCEPTION 'Precheck negativo: no hay 2 cuentas posting disponibles para company %.', r.company_id;
        END IF;

        SELECT COUNT(*)
        INTO v_open_count
        FROM public.con_periodo_contable cp
        WHERE cp.company_id = r.company_id
        AND r.poliza_date BETWEEN cp.start_date::date AND cp.end_date::date
        AND COALESCE(cp.status_id, 2) = 0;

        IF v_open_count > 0 THEN
            RAISE EXCEPTION 'Precheck negativo: existe periodo abierto para la fecha %. Ajustar fecha para un periodo solo en precierre/cerrado.', r.poliza_date;
        END IF;
    END
    $$;

    -- ------------------------------------------------------------
    -- Baseline saldos de cuentas de prueba
    -- ------------------------------------------------------------
    DROP TABLE IF EXISTS tmp_con_test_posteo_cerrado_saldo_before;
    CREATE TEMP TABLE tmp_con_test_posteo_cerrado_saldo_before AS
    WITH r AS (
        SELECT *
        FROM tmp_con_test_posteo_cerrado_ctx
        LIMIT 1
    ),
    acc AS (
        SELECT 'DEBIT'::text AS rol, a.code AS codigo_cuenta
        FROM public.con_plan_cuentas a
        JOIN r ON a.account_id = r.debit_account_id
        UNION ALL
        SELECT 'CREDIT'::text AS rol, a.code AS codigo_cuenta
        FROM public.con_plan_cuentas a
        JOIN r ON a.account_id = r.credit_account_id
    )
    SELECT
        r.company_id,
        r.closed_period_id AS period_id,
        a.rol,
        a.codigo_cuenta,
        COALESCE(s.debitos, 0)::numeric(18,2) AS debitos_before,
        COALESCE(s.creditos, 0)::numeric(18,2) AS creditos_before
    FROM r
    JOIN acc a ON true
    LEFT JOIN public.con_saldo_cuenta s
    ON s.company_id = r.company_id
    AND s.periodo_id = r.closed_period_id
    AND s.codigo_cuenta = a.codigo_cuenta
    AND s.mes = 13
    AND s.tipo_transaccion = 0;

    -- ------------------------------------------------------------
    -- Crear poliza DRAFT en fecha cerrada (period_id NULL a proposito)
    -- ------------------------------------------------------------
    DROP TABLE IF EXISTS tmp_con_test_posteo_cerrado_poliza;
    CREATE TEMP TABLE tmp_con_test_posteo_cerrado_poliza AS
    WITH r AS (
        SELECT *
        FROM tmp_con_test_posteo_cerrado_ctx
        LIMIT 1
    ),
    new_hdr AS (
        INSERT INTO public.con_partida_hdr (
            company_id,
            journal_id,
            period_id,
            template_id,
            module,
            document_type,
            document_id,
            document_number,
            poliza_number,
            sequence_number,
            poliza_date,
            description,
            status,
            source_reference,
            created_at,
            created_by,
            type_id,
            total_debit,
            total_credit
        )
        SELECT
            r.company_id,
            r.journal_id,
            NULL, -- intencional para forzar resolucion por fecha en sp_con_postear_poliza
            NULL,
            'QA',
            'NEG',
            EXTRACT(EPOCH FROM clock_timestamp())::bigint,
            ('NEG-CLOSE-' || to_char(clock_timestamp(), 'YYYYMMDDHH24MISSMS')),
            ('NEG-CLOSE-' || to_char(clock_timestamp(), 'HH24MISSMS')),
            NULL,
            r.poliza_date,
            'TEST NEGATIVO periodo cerrado',
            0,
            'NEG-CLOSE',
            now(),
            r.user_name,
            r.type_id,
            0,
            0
        FROM r
        RETURNING poliza_id, company_id
    )
    SELECT *
    FROM new_hdr;

    INSERT INTO public.con_partida_dtl (
        company_id,
        poliza_id,
        line_number,
        account_id,
        cost_center_id,
        description,
        debit_amount,
        credit_amount,
        third_party_id,
        currency_code,
        exchange_rate,
        source_document
    )
    SELECT
        r.company_id,
        p.poliza_id,
        1,
        r.debit_account_id,
        NULL,
        'Negativo cerrado - debit',
        r.amount_total,
        0,
        NULL,
        'HNL',
        1,
        'NEG-CLOSE'
    FROM tmp_con_test_posteo_cerrado_ctx r
    JOIN tmp_con_test_posteo_cerrado_poliza p ON p.company_id = r.company_id;

    INSERT INTO public.con_partida_dtl (
        company_id,
        poliza_id,
        line_number,
        account_id,
        cost_center_id,
        description,
        debit_amount,
        credit_amount,
        third_party_id,
        currency_code,
        exchange_rate,
        source_document
    )
    SELECT
        r.company_id,
        p.poliza_id,
        2,
        r.credit_account_id,
        NULL,
        'Negativo cerrado - credit',
        0,
        r.amount_total,
        NULL,
        'HNL',
        1,
        'NEG-CLOSE'
    FROM tmp_con_test_posteo_cerrado_ctx r
    JOIN tmp_con_test_posteo_cerrado_poliza p ON p.company_id = r.company_id;

    -- ------------------------------------------------------------
    -- Intento de posteo (debe bloquear)
    -- ------------------------------------------------------------
    DROP TABLE IF EXISTS tmp_con_test_posteo_cerrado_result;
    CREATE TEMP TABLE tmp_con_test_posteo_cerrado_result (
        company_id bigint NOT NULL,
        poliza_id bigint NOT NULL,
        expected_blocked boolean NOT NULL,
        blocked boolean NOT NULL,
        error_message text NULL
    );

    DO $$
    DECLARE
        v_company_id bigint;
        v_poliza_id bigint;
    BEGIN
        SELECT c.company_id, p.poliza_id
        INTO v_company_id, v_poliza_id
        FROM tmp_con_test_posteo_cerrado_ctx c
        JOIN tmp_con_test_posteo_cerrado_poliza p ON p.company_id = c.company_id
        LIMIT 1;

        BEGIN
            PERFORM public.sp_con_postear_poliza(v_company_id, v_poliza_id, (SELECT user_name FROM tmp_con_test_posteo_cerrado_ctx LIMIT 1));

            INSERT INTO tmp_con_test_posteo_cerrado_result (
                company_id, poliza_id, expected_blocked, blocked, error_message
            ) VALUES (
                v_company_id, v_poliza_id, true, false,
                'ERROR: el posteo no debio permitirse en periodo cerrado.'
            );
        EXCEPTION WHEN OTHERS THEN
            INSERT INTO tmp_con_test_posteo_cerrado_result (
                company_id, poliza_id, expected_blocked, blocked, error_message
            ) VALUES (
                v_company_id, v_poliza_id, true, true, SQLERRM
            );
        END;
    END
    $$;

    SELECT * FROM tmp_con_test_posteo_cerrado_result;

    SELECT
        h.poliza_id,
        h.status,
        h.period_id,
        h.posted_at,
        h.total_debit,
        h.total_credit
    FROM public.con_partida_hdr h
    JOIN tmp_con_test_posteo_cerrado_poliza p ON p.poliza_id = h.poliza_id;

    WITH ctx AS (
        SELECT *
        FROM tmp_con_test_posteo_cerrado_ctx
        LIMIT 1
    ),
    after_saldo AS (
        SELECT
            s.codigo_cuenta,
            COALESCE(s.debitos, 0)::numeric(18,2) AS debitos_after,
            COALESCE(s.creditos, 0)::numeric(18,2) AS creditos_after
        FROM public.con_saldo_cuenta s
        JOIN ctx ON ctx.company_id = s.company_id
        WHERE s.periodo_id = ctx.closed_period_id
        AND s.mes = 13
        AND s.tipo_transaccion = 0
    )
    SELECT
        b.rol,
        b.codigo_cuenta,
        b.debitos_before,
        COALESCE(a.debitos_after, 0) AS debitos_after,
        (COALESCE(a.debitos_after, 0) - b.debitos_before)::numeric(18,2) AS delta_debitos,
        b.creditos_before,
        COALESCE(a.creditos_after, 0) AS creditos_after,
        (COALESCE(a.creditos_after, 0) - b.creditos_before)::numeric(18,2) AS delta_creditos,
        CASE
            WHEN abs(COALESCE(a.debitos_after, 0) - b.debitos_before) <= 0.01
            AND abs(COALESCE(a.creditos_after, 0) - b.creditos_before) <= 0.01
            THEN 'OK'
            ELSE 'ERROR'
        END AS saldos_sin_cambio
    FROM tmp_con_test_posteo_cerrado_saldo_before b
    LEFT JOIN after_saldo a ON a.codigo_cuenta = b.codigo_cuenta
    ORDER BY b.rol;

    -- ------------------------------------------------------------
    -- Limpieza opcional
    -- ------------------------------------------------------------
    DO $$
    DECLARE
        v_keep boolean;
        v_company_id bigint;
        v_poliza_id bigint;
        v_user text;
        v_status smallint;
    BEGIN
        SELECT c.keep_draft_after_test, c.company_id, p.poliza_id, c.user_name
        INTO v_keep, v_company_id, v_poliza_id, v_user
        FROM tmp_con_test_posteo_cerrado_ctx c
        JOIN tmp_con_test_posteo_cerrado_poliza p ON p.company_id = c.company_id
        LIMIT 1;

        IF COALESCE(v_keep, false) THEN
            RETURN;
        END IF;

        SELECT h.status
        INTO v_status
        FROM public.con_partida_hdr h
        WHERE h.poliza_id = v_poliza_id;

        IF v_status = 1 THEN
            PERFORM public.sp_con_revertir_poliza(v_company_id, v_poliza_id, v_user);
        END IF;

        DELETE FROM public.con_partida_dtl
        WHERE poliza_id = v_poliza_id;

        DELETE FROM public.con_partida_hdr
        WHERE poliza_id = v_poliza_id;
    END
    $$;

SELECT * FROM tmp_con_test_posteo_cerrado_result;
