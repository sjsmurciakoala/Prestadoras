-- ============================================================
-- Agrega company_id y cont_account_id a servicios
-- Company por defecto: cfg_company.code = 'APC'
-- Fecha: 2026-03-04
-- ============================================================

BEGIN;

ALTER TABLE public.servicios
    ADD COLUMN IF NOT EXISTS company_id bigint;

ALTER TABLE public.servicios
    ADD COLUMN IF NOT EXISTS cont_account_id bigint;

DO $$
DECLARE
    v_company_id bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'APC'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No existe cfg_company con code = APC. Cree la empresa antes de migrar servicios.';
    END IF;

    UPDATE public.servicios
       SET company_id = v_company_id
     WHERE company_id IS NULL;
END $$;

ALTER TABLE public.servicios
    ALTER COLUMN company_id SET NOT NULL;

ALTER TABLE public.servicios
    ADD CONSTRAINT servicios_company_id_fkey
        FOREIGN KEY (company_id)
        REFERENCES public.cfg_company (company_id);

ALTER TABLE public.servicios
    ADD CONSTRAINT servicios_cont_account_id_fkey
        FOREIGN KEY (cont_account_id)
        REFERENCES public.con_plan_cuentas (account_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_servicios_company_codigo
    ON public.servicios (company_id, servicios_codigo);

CREATE INDEX IF NOT EXISTS ix_servicios_company
    ON public.servicios (company_id);

CREATE INDEX IF NOT EXISTS ix_servicios_cont_account
    ON public.servicios (cont_account_id);

COMMIT;
