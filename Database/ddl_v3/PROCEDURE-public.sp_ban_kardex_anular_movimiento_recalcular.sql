-- ============================================================
-- Procedimiento: sp_ban_kardex_anular_movimiento_recalcular
-- Descripción:   Anula un movimiento creando reversa e
--                inmediatamente recalcula los saldos históricos
--                desde la fecha afectada hacia adelante.
-- Regla:         reversa = (-1) * monto_original
-- Tabla kardex:  public.ban_kardex (id_tipo_transaccion)
-- ============================================================

-- Cambio de firma: fecha de anulacion automatica (se elimina parametro).
-- Eliminar firma anterior con p_fecha_anulacion si existe.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
          FROM pg_proc p
          JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = 'public'
           AND p.proname = 'sp_ban_kardex_anular_movimiento_recalcular'
           AND p.prokind = 'p'
           AND pg_get_function_identity_arguments(p.oid) =
               'bigint, bigint, bigint, date, character varying, character varying'
    ) THEN
        EXECUTE 'DROP PROCEDURE public.sp_ban_kardex_anular_movimiento_recalcular(' ||
                'bigint, bigint, bigint, date, character varying, character varying)';
    END IF;
END $$;

CREATE OR REPLACE PROCEDURE public.sp_ban_kardex_anular_movimiento_recalcular(
    p_company_id               bigint,
    p_banco_cuenta_id          bigint,
    p_ban_kardex_id_original   bigint,
    p_motivo                   character varying(500),
    p_usuario                  character varying(100),
    OUT p_ban_kardex_id_anulacion bigint,
    OUT p_saldo_resultante     numeric(28,4)
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_ban_banco_id        bigint;
    v_ban_moneda_id       bigint;
    v_currency_code       varchar(3);

    v_saldo_inicial       numeric(28,4);

    v_monto_original      numeric(28,4);
    v_monto_reversa       numeric(28,4);
    v_fecha_original      date;
    v_ref_original        varchar(100);
    v_estado_conciliacion varchar(3);
    v_tipo_transaccion    varchar(3);
    v_tipo_transaccion_id bigint;
    v_kardex_tipo_col     text;
    v_kardex_tipo_is_bigint boolean;

    v_referencia_reversa  varchar(100);

    v_fecha_anulacion     date;
    v_fecha_recalculo     date;
    v_saldo_base          numeric(28,4);

    v_ctx                 text;
BEGIN
    p_ban_kardex_id_anulacion := NULL;
    p_saldo_resultante := NULL;

    BEGIN
        -- 0) Bloquear cuenta para evitar carreras (misma cuenta recalculándose en paralelo)
        PERFORM 1
          FROM public.ban_cuenta c
         WHERE c.company_id = p_company_id
           AND c.banco_cuenta_id = p_banco_cuenta_id
         FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION USING ERRCODE='P0001',
                MESSAGE = format('Cuenta bancaria no encontrada: %s (company_id=%s)',
                                 p_banco_cuenta_id, p_company_id);
        END IF;

        -- 1) Obtener datos cuenta (banco/moneda/saldo inicial)
        SELECT c.ban_banco_id, c.currency_code, COALESCE(c.saldo_inicial,0)
          INTO v_ban_banco_id, v_currency_code, v_saldo_inicial
        FROM public.ban_cuenta c
        WHERE c.company_id = p_company_id
          AND c.banco_cuenta_id = p_banco_cuenta_id;

        SELECT m.ban_moneda_id
          INTO v_ban_moneda_id
        FROM public.ban_moneda m
        WHERE m.company_id = p_company_id
          AND m.codigo = v_currency_code;

        IF v_ban_moneda_id IS NULL THEN
            RAISE EXCEPTION USING ERRCODE='P0001',
                MESSAGE = format('Moneda no encontrada para company_id=%s, codigo=%s',
                                 p_company_id, v_currency_code);
        END IF;

        -- 2) Determinar columna/tipo para el id de transaccion en ban_kardex
        SELECT a.attname,
               (a.atttypid = 'int8'::regtype)
          INTO v_kardex_tipo_col, v_kardex_tipo_is_bigint
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'ban_kardex'
          AND a.attname IN ('tipo_movimiento', 'id_tipo_transaccion')
          AND a.attnum > 0
          AND NOT a.attisdropped
        ORDER BY CASE a.attname WHEN 'tipo_movimiento' THEN 1 ELSE 2 END
        LIMIT 1;

        IF v_kardex_tipo_col IS NULL THEN
            RAISE EXCEPTION USING ERRCODE='P0001',
                MESSAGE = 'No existe columna tipo_movimiento ni id_tipo_transaccion en ban_kardex';
        END IF;

        -- 3) Leer kardex original
        IF v_kardex_tipo_col = 'tipo_movimiento' THEN
            IF v_kardex_tipo_is_bigint THEN
                SELECT k.monto, k.fecha_movimiento, k.referencia, k.estado_conciliacion, k.tipo_movimiento, t.tipo_transaccion
                  INTO v_monto_original, v_fecha_original, v_ref_original, v_estado_conciliacion, v_tipo_transaccion_id, v_tipo_transaccion
                FROM public.ban_kardex k
                LEFT JOIN public.ban_tipos_transacciones t
                  ON t.company_id = k.company_id
                 AND t.ban_tipo_transaccion_id = k.tipo_movimiento
                WHERE k.company_id = p_company_id
                  AND k.banco_cuenta_id = p_banco_cuenta_id
                  AND k.ban_kardex_id = p_ban_kardex_id_original;
            ELSE
                SELECT k.monto, k.fecha_movimiento, k.referencia, k.estado_conciliacion, k.tipo_movimiento, t.ban_tipo_transaccion_id
                  INTO v_monto_original, v_fecha_original, v_ref_original, v_estado_conciliacion, v_tipo_transaccion, v_tipo_transaccion_id
                FROM public.ban_kardex k
                LEFT JOIN public.ban_tipos_transacciones t
                  ON t.company_id = k.company_id
                 AND t.tipo_transaccion = k.tipo_movimiento
                WHERE k.company_id = p_company_id
                  AND k.banco_cuenta_id = p_banco_cuenta_id
                  AND k.ban_kardex_id = p_ban_kardex_id_original;
            END IF;
        ELSIF v_kardex_tipo_col = 'id_tipo_transaccion' THEN
            IF v_kardex_tipo_is_bigint THEN
                SELECT k.monto, k.fecha_movimiento, k.referencia, k.estado_conciliacion, k.id_tipo_transaccion, t.tipo_transaccion
                  INTO v_monto_original, v_fecha_original, v_ref_original, v_estado_conciliacion, v_tipo_transaccion_id, v_tipo_transaccion
                FROM public.ban_kardex k
                LEFT JOIN public.ban_tipos_transacciones t
                  ON t.company_id = k.company_id
                 AND t.ban_tipo_transaccion_id = k.id_tipo_transaccion
                WHERE k.company_id = p_company_id
                  AND k.banco_cuenta_id = p_banco_cuenta_id
                  AND k.ban_kardex_id = p_ban_kardex_id_original;
            ELSE
                SELECT k.monto, k.fecha_movimiento, k.referencia, k.estado_conciliacion, k.id_tipo_transaccion, t.ban_tipo_transaccion_id
                  INTO v_monto_original, v_fecha_original, v_ref_original, v_estado_conciliacion, v_tipo_transaccion, v_tipo_transaccion_id
                FROM public.ban_kardex k
                LEFT JOIN public.ban_tipos_transacciones t
                  ON t.company_id = k.company_id
                 AND t.tipo_transaccion = k.id_tipo_transaccion
                WHERE k.company_id = p_company_id
                  AND k.banco_cuenta_id = p_banco_cuenta_id
                  AND k.ban_kardex_id = p_ban_kardex_id_original;
            END IF;
        END IF;

        IF NOT FOUND THEN
            RAISE EXCEPTION USING ERRCODE='P0001',
                MESSAGE = format('Kardex original no encontrado: %s (company_id=%s, cuenta=%s)',
                                 p_ban_kardex_id_original, p_company_id, p_banco_cuenta_id);
        END IF;

        IF UPPER(COALESCE(v_estado_conciliacion, 'NOC')) = 'CON' THEN
            RAISE EXCEPTION USING ERRCODE='P0001',
                MESSAGE = format(
                    'No se puede anular la transaccion bancaria %s porque ya esta conciliada.',
                    p_ban_kardex_id_original);
        END IF;

        IF v_kardex_tipo_is_bigint AND v_tipo_transaccion_id IS NULL THEN
            RAISE EXCEPTION USING ERRCODE='P0001',
                MESSAGE = format('No se pudo resolver ban_tipo_transaccion_id para el kardex %s (company_id=%s, cuenta=%s)',
                                 p_ban_kardex_id_original, p_company_id, p_banco_cuenta_id);
        END IF;

        IF v_tipo_transaccion IS NULL AND v_tipo_transaccion_id IS NOT NULL THEN
            v_tipo_transaccion := v_tipo_transaccion_id::varchar;
        END IF;

        v_fecha_anulacion := current_date;

        -- 3) Evitar duplicidad de reversa por convención referencia = REV-<id>
        v_referencia_reversa := left(format('REV-%s', p_ban_kardex_id_original), 100);

        IF EXISTS (
            SELECT 1
              FROM public.ban_kardex kx
             WHERE kx.company_id = p_company_id
               AND kx.banco_cuenta_id = p_banco_cuenta_id
               AND kx.referencia = v_referencia_reversa
        ) THEN
            RAISE EXCEPTION USING ERRCODE='P0001',
                MESSAGE = format('El kardex %s ya tiene reversa registrada (referencia=%s).',
                                 p_ban_kardex_id_original, v_referencia_reversa);
        END IF;

        -- 3.1) Eliminar partida contable asociada (si existe)
        IF v_ref_original IS NOT NULL AND btrim(v_ref_original) <> '' THEN
            -- con_partida_hdr / con_partida_dtl
            IF EXISTS (
                SELECT 1
                  FROM information_schema.tables
                 WHERE table_schema = 'public'
                   AND table_name = 'con_partida_hdr'
            ) THEN
                -- eliminar detalle primero (si existe)
                IF EXISTS (
                    SELECT 1
                      FROM information_schema.tables
                     WHERE table_schema = 'public'
                       AND table_name = 'con_partida_dtl'
                ) THEN
                    WITH partidas AS (
                        SELECT poliza_id
                          FROM public.con_partida_hdr
                         WHERE company_id = p_company_id
                           AND "module" = 'BANCOS'
                           AND document_type = v_tipo_transaccion
                           AND (document_number = v_ref_original OR poliza_number = v_ref_original)
                    )
                    DELETE FROM public.con_partida_dtl d
                    USING partidas p
                    WHERE d.poliza_id = p.poliza_id;
                END IF;

                DELETE FROM public.con_partida_hdr h
                 WHERE h.company_id = p_company_id
                   AND h."module" = 'BANCOS'
                   AND h.document_type = v_tipo_transaccion
                   AND (h.document_number = v_ref_original OR h.poliza_number = v_ref_original);
            END IF;

            -- con_partida_hdr / con_partida_dtl
            IF EXISTS (
                SELECT 1
                  FROM information_schema.tables
                 WHERE table_schema = 'public'
                   AND table_name = 'con_partida_hdr'
            ) THEN
                IF EXISTS (
                    SELECT 1
                      FROM information_schema.tables
                     WHERE table_schema = 'public'
                       AND table_name = 'con_partida_dtl'
                ) THEN
                    WITH polizas AS (
                        SELECT poliza_id
                          FROM public.con_partida_hdr
                         WHERE company_id = p_company_id
                           AND "module" = 'BANCOS'
                           AND document_type = v_tipo_transaccion
                           AND (document_number = v_ref_original OR poliza_number = v_ref_original OR source_reference = v_ref_original)
                    )
                    DELETE FROM public.con_partida_dtl l
                    USING polizas p
                    WHERE l.poliza_id = p.poliza_id;
                END IF;

                DELETE FROM public.con_partida_hdr pz
                 WHERE pz.company_id = p_company_id
                   AND pz."module" = 'BANCOS'
                   AND pz.document_type = v_tipo_transaccion
                   AND (pz.document_number = v_ref_original OR pz.poliza_number = v_ref_original OR pz.source_reference = v_ref_original);
            END IF;
        END IF;

        -- 4) Insertar reversa
        v_monto_reversa := -1 * COALESCE(v_monto_original,0);

        IF v_kardex_tipo_col = 'tipo_movimiento' THEN
            IF v_kardex_tipo_is_bigint THEN
                INSERT INTO public.ban_kardex (
                    company_id, banco_cuenta_id, ban_banco_id, ban_moneda_id,
                    tipo_movimiento, fecha_movimiento, fecha_registro,
                    descripcion, referencia, monto, saldo,
                    created_at, created_by
                )
                VALUES (
                    p_company_id, p_banco_cuenta_id, v_ban_banco_id, v_ban_moneda_id,
                    v_tipo_transaccion_id, v_fecha_anulacion, now(),
                    left(
                        format(
                            'ANULACI?N ban_kardex_id=%s | fecha_orig=%s | ref_orig=%s | %s',
                            p_ban_kardex_id_original,
                            v_fecha_original,
                            COALESCE(v_ref_original,''),
                            COALESCE(p_motivo,'')
                        ),
                        500
                    ),
                    v_referencia_reversa,
                    v_monto_reversa,
                    0, -- se recalcula despu?s
                    now(), p_usuario
                )
                RETURNING ban_kardex_id INTO p_ban_kardex_id_anulacion;
            ELSE
                INSERT INTO public.ban_kardex (
                    company_id, banco_cuenta_id, ban_banco_id, ban_moneda_id,
                    tipo_movimiento, fecha_movimiento, fecha_registro,
                    descripcion, referencia, monto, saldo,
                    created_at, created_by
                )
                VALUES (
                    p_company_id, p_banco_cuenta_id, v_ban_banco_id, v_ban_moneda_id,
                    v_tipo_transaccion, v_fecha_anulacion, now(),
                    left(
                        format(
                            'ANULACI?N ban_kardex_id=%s | fecha_orig=%s | ref_orig=%s | %s',
                            p_ban_kardex_id_original,
                            v_fecha_original,
                            COALESCE(v_ref_original,''),
                            COALESCE(p_motivo,'')
                        ),
                        500
                    ),
                    v_referencia_reversa,
                    v_monto_reversa,
                    0, -- se recalcula despu?s
                    now(), p_usuario
                )
                RETURNING ban_kardex_id INTO p_ban_kardex_id_anulacion;
            END IF;
        ELSIF v_kardex_tipo_col = 'id_tipo_transaccion' THEN
            IF v_kardex_tipo_is_bigint THEN
                INSERT INTO public.ban_kardex (
                    company_id, banco_cuenta_id, ban_banco_id, ban_moneda_id,
                    id_tipo_transaccion, fecha_movimiento, fecha_registro,
                    descripcion, referencia, monto, saldo,
                    created_at, created_by
                )
                VALUES (
                    p_company_id, p_banco_cuenta_id, v_ban_banco_id, v_ban_moneda_id,
                    v_tipo_transaccion_id, v_fecha_anulacion, now(),
                    left(
                        format(
                            'ANULACI?N ban_kardex_id=%s | fecha_orig=%s | ref_orig=%s | %s',
                            p_ban_kardex_id_original,
                            v_fecha_original,
                            COALESCE(v_ref_original,''),
                            COALESCE(p_motivo,'')
                        ),
                        500
                    ),
                    v_referencia_reversa,
                    v_monto_reversa,
                    0, -- se recalcula despu?s
                    now(), p_usuario
                )
                RETURNING ban_kardex_id INTO p_ban_kardex_id_anulacion;
            ELSE
                INSERT INTO public.ban_kardex (
                    company_id, banco_cuenta_id, ban_banco_id, ban_moneda_id,
                    id_tipo_transaccion, fecha_movimiento, fecha_registro,
                    descripcion, referencia, monto, saldo,
                    created_at, created_by
                )
                VALUES (
                    p_company_id, p_banco_cuenta_id, v_ban_banco_id, v_ban_moneda_id,
                    v_tipo_transaccion, v_fecha_anulacion, now(),
                    left(
                        format(
                            'ANULACI?N ban_kardex_id=%s | fecha_orig=%s | ref_orig=%s | %s',
                            p_ban_kardex_id_original,
                            v_fecha_original,
                            COALESCE(v_ref_original,''),
                            COALESCE(p_motivo,'')
                        ),
                        500
                    ),
                    v_referencia_reversa,
                    v_monto_reversa,
                    0, -- se recalcula despu?s
                    now(), p_usuario
                )
                RETURNING ban_kardex_id INTO p_ban_kardex_id_anulacion;
            END IF;
        END IF;

        -- 5) Fecha desde donde recalcular (la menor afectada)
        v_fecha_recalculo := LEAST(v_fecha_original, v_fecha_anulacion);

        -- 6) Saldo base = saldo del último movimiento ANTES de v_fecha_recalculo
        --    IMPORTANTE: ordenar también por fecha_registro para consistencia intra-día
        SELECT k.saldo
          INTO v_saldo_base
        FROM public.ban_kardex k
        WHERE k.company_id = p_company_id
          AND k.banco_cuenta_id = p_banco_cuenta_id
          AND k.fecha_movimiento < v_fecha_recalculo
        ORDER BY k.fecha_movimiento DESC, k.fecha_registro DESC, k.ban_kardex_id DESC
        LIMIT 1;

        v_saldo_base := COALESCE(v_saldo_base, v_saldo_inicial);

        -- 7) Recalcular saldos desde v_fecha_recalculo hacia adelante
        --    IMPORTANTE: ordenar también por fecha_registro para consistencia intra-día
        WITH movs AS (
            SELECT
                k.ban_kardex_id,
                k.monto,
                SUM(k.monto) OVER (
                    ORDER BY k.fecha_movimiento, k.fecha_registro, k.ban_kardex_id
                    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                ) AS running_monto
            FROM public.ban_kardex k
            WHERE k.company_id = p_company_id
              AND k.banco_cuenta_id = p_banco_cuenta_id
              AND k.fecha_movimiento >= v_fecha_recalculo
        ),
        nuevos AS (
            SELECT
                ban_kardex_id,
                (v_saldo_base + running_monto)::numeric(28,4) AS nuevo_saldo
            FROM movs
        )
        UPDATE public.ban_kardex k
           SET saldo = n.nuevo_saldo,
               updated_at = now(),
               updated_by = p_usuario
          FROM nuevos n
         WHERE k.ban_kardex_id = n.ban_kardex_id;

        -- 8) Saldo resultante = saldo del último movimiento de la cuenta (o saldo_inicial si no hay)
        SELECT k.saldo
          INTO p_saldo_resultante
        FROM public.ban_kardex k
        WHERE k.company_id = p_company_id
          AND k.banco_cuenta_id = p_banco_cuenta_id
        ORDER BY k.fecha_movimiento DESC, k.fecha_registro DESC, k.ban_kardex_id DESC
        LIMIT 1;

        p_saldo_resultante := COALESCE(p_saldo_resultante, v_saldo_inicial);

        -- 9) Actualizar saldo_actual en cuenta
        UPDATE public.ban_cuenta
           SET saldo_actual = p_saldo_resultante,
               updated_at   = now(),
               updated_by   = p_usuario
         WHERE company_id = p_company_id
           AND banco_cuenta_id = p_banco_cuenta_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION USING ERRCODE='P0001',
                MESSAGE = format('No se pudo actualizar saldo_actual. Cuenta no encontrada: %s (company_id=%s)',
                                 p_banco_cuenta_id, p_company_id);
        END IF;

        RAISE NOTICE
            'SUCCESS sp_ban_kardex_anular_movimiento_recalcular: original=%, anulacion=%, fecha_recalculo=%, saldo_final=%',
            p_ban_kardex_id_original, p_ban_kardex_id_anulacion, v_fecha_recalculo, p_saldo_resultante;

    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS v_ctx = PG_EXCEPTION_CONTEXT;

        p_ban_kardex_id_anulacion := NULL;
        p_saldo_resultante := NULL;

        RAISE EXCEPTION
            USING
                ERRCODE = SQLSTATE,
                MESSAGE = format(
                    'ERROR sp_ban_kardex_anular_movimiento_recalcular | %s | company_id=%s | cuenta=%s | kardex_original=%s',
                    SQLERRM, p_company_id, p_banco_cuenta_id, p_ban_kardex_id_original
                ),
                DETAIL = v_ctx;
    END;
END;
$$;


