-- ================================================
-- 20260115_contabilidad_centros_costo.sql
-- Cambios legado centros de costo + apertura centro costo
-- ================================================

BEGIN;

ALTER TABLE IF EXISTS public.con_centro_costo
    ADD COLUMN IF NOT EXISTS legacy_key_cost integer,
    ADD COLUMN IF NOT EXISTS legacy_type_trans smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS legacy_parent_code varchar(24),
    ADD COLUMN IF NOT EXISTS allows_movement boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS start_date timestamptz NOT NULL DEFAULT now(),
    ADD COLUMN IF NOT EXISTS end_date timestamptz NOT NULL DEFAULT now(),
    ADD COLUMN IF NOT EXISTS legacy_notes text,
    ADD COLUMN IF NOT EXISTS is_periodic boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS legacy_status boolean NOT NULL DEFAULT false;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_con_centro_costo_legacy_type_trans'
          AND conrelid = 'public.con_centro_costo'::regclass
    ) THEN
        ALTER TABLE public.con_centro_costo
            ADD CONSTRAINT ck_con_centro_costo_legacy_type_trans
            CHECK (legacy_type_trans >= 0 AND legacy_type_trans <= 5);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS public.con_apertura_centro_costo
(
    opening_id       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id       bigint NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    period_id        bigint NOT NULL REFERENCES public.con_periodo_contable(period_id) ON DELETE CASCADE,
    account_id       bigint NOT NULL REFERENCES public.con_plan_cuentas(account_id) ON DELETE RESTRICT,
    cost_center_id   bigint NOT NULL REFERENCES public.con_centro_costo(cost_center_id) ON DELETE RESTRICT,
    tipo_transaccion smallint NOT NULL DEFAULT 0,
    debit_amount     numeric(18,2) NOT NULL DEFAULT 0,
    credit_amount    numeric(18,2) NOT NULL DEFAULT 0,
    currency_code    char(3) REFERENCES public.cfg_currency(currency_code),
    exchange_rate    numeric(18,9) DEFAULT 1.0,
    notes            varchar(300),
    created_at       timestamptz NOT NULL DEFAULT now(),
    created_by       varchar(100) NOT NULL DEFAULT current_user,
    updated_at       timestamptz,
    updated_by       varchar(100),
    CONSTRAINT ck_con_apertura_centro_costo_tipo_transaccion
        CHECK (tipo_transaccion >= 0 AND tipo_transaccion <= 5),
    UNIQUE (company_id, period_id, account_id, cost_center_id, tipo_transaccion)
);

CREATE INDEX IF NOT EXISTS ix_con_apertura_centro_costo_account
    ON public.con_apertura_centro_costo (account_id);

CREATE INDEX IF NOT EXISTS ix_con_apertura_centro_costo_cost_center
    ON public.con_apertura_centro_costo (cost_center_id);

CREATE INDEX IF NOT EXISTS ix_con_apertura_centro_costo_period
    ON public.con_apertura_centro_costo (period_id);

COMMIT;
