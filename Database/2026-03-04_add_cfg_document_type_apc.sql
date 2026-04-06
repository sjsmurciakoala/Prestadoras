-- 2026-03-04_add_cfg_document_type_apc.sql
-- Crea cfg_document_type y carga tipos base para APC (VENTAS)

BEGIN;

CREATE TABLE IF NOT EXISTS public.cfg_document_type
(
    document_type_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    module             varchar(30)    NOT NULL,
    code               varchar(20)    NOT NULL,
    name               varchar(120)   NOT NULL,
    description        varchar(300),
    requires_cai       boolean        NOT NULL DEFAULT false,
    is_active          boolean        NOT NULL DEFAULT true,
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, module, code)
);

CREATE INDEX IF NOT EXISTS ix_cfg_document_type_company ON public.cfg_document_type (company_id);
CREATE INDEX IF NOT EXISTS ix_cfg_document_type_module ON public.cfg_document_type (module);

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
        RAISE EXCEPTION 'Company APC no encontrada. Verifica cfg_company.code.';
    END IF;

    INSERT INTO public.cfg_document_type (company_id, module, code, name, description, requires_cai)
    VALUES
        (v_company_id, 'VENTAS', 'FAC', 'Factura de servicios', 'Factura por servicios/consumo de agua', true),
        (v_company_id, 'VENTAS', 'REC', 'Recibo de cobro', 'Aplicacion de cobros a clientes', false),
        (v_company_id, 'VENTAS', 'NC',  'Nota de credito',   'Disminucion de factura', true),
        (v_company_id, 'VENTAS', 'ND',  'Nota de debito',    'Aumento de factura', true)
    ON CONFLICT (company_id, module, code) DO UPDATE
        SET name = EXCLUDED.name,
            description = EXCLUDED.description,
            requires_cai = EXCLUDED.requires_cai,
            is_active = true,
            updated_at = now(),
            updated_by = current_user;
END
$$;

COMMIT;
