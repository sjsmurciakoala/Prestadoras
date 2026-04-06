-- ================================================
-- 20260113_contabilidad_tipos_transaccion.sql
-- Tipo de transaccion + reglas (legacy C01TransClass / C01TransClassRule)
-- ================================================

BEGIN;

ALTER TABLE IF EXISTS public.con_tipo_transaccion
    ADD COLUMN IF NOT EXISTS type_trans smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS type_oper smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS frequency smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS max_entries integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS allows_cash_flow boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS allows_account_limit boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS is_default boolean NOT NULL DEFAULT false;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_con_tipo_transaccion_type_trans'
          AND conrelid = 'public.con_tipo_transaccion'::regclass
    ) THEN
        ALTER TABLE public.con_tipo_transaccion
            ADD CONSTRAINT ck_con_tipo_transaccion_type_trans
            CHECK (type_trans >= 0 AND type_trans <= 5);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_con_tipo_transaccion_type_oper'
          AND conrelid = 'public.con_tipo_transaccion'::regclass
    ) THEN
        ALTER TABLE public.con_tipo_transaccion
            ADD CONSTRAINT ck_con_tipo_transaccion_type_oper
            CHECK (type_oper >= 0 AND type_oper <= 10);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_con_tipo_transaccion_frequency'
          AND conrelid = 'public.con_tipo_transaccion'::regclass
    ) THEN
        ALTER TABLE public.con_tipo_transaccion
            ADD CONSTRAINT ck_con_tipo_transaccion_frequency
            CHECK (frequency >= 0 AND frequency <= 2);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_con_tipo_transaccion_max_entries'
          AND conrelid = 'public.con_tipo_transaccion'::regclass
    ) THEN
        ALTER TABLE public.con_tipo_transaccion
            ADD CONSTRAINT ck_con_tipo_transaccion_max_entries
            CHECK (max_entries >= 0);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_con_tipo_transaccion_default
    ON public.con_tipo_transaccion (company_id)
    WHERE is_default;

CREATE TABLE IF NOT EXISTS public.con_tipo_transaccion_rule
(
    rule_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id bigint NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    type_id bigint NOT NULL REFERENCES public.con_tipo_transaccion(type_id) ON DELETE CASCADE,
    line_number int NOT NULL DEFAULT 1,
    account_code_from varchar(30),
    account_code_to varchar(30),
    cost_center_code_from varchar(30),
    cost_center_code_to varchar(30),
    third_party_code_from varchar(30),
    third_party_code_to varchar(30),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(100) NOT NULL DEFAULT current_user,
    updated_at timestamptz,
    updated_by varchar(100),
    UNIQUE (company_id, type_id, line_number)
);

CREATE INDEX IF NOT EXISTS ix_con_tipo_transaccion_rule_company
    ON public.con_tipo_transaccion_rule (company_id);

CREATE INDEX IF NOT EXISTS ix_con_tipo_transaccion_rule_type
    ON public.con_tipo_transaccion_rule (type_id);

COMMIT;
