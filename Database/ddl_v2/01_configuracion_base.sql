-- ================================================
-- 01_configuracion_base.sql
-- Tablas maestras para Configuración (empresa, sucursales, monedas, impuestos, series)
-- Requiere: ejecutar en un esquema limpio o después de respaldar datos existentes.
-- ================================================

BEGIN;

-- Tabla: cfg_currency
CREATE TABLE IF NOT EXISTS public.cfg_currency
(
    currency_code      char(3)        PRIMARY KEY,
    name               varchar(80)    NOT NULL,
    symbol             varchar(10)    NOT NULL,
    decimal_places     smallint       NOT NULL DEFAULT 2,
    is_base_currency   boolean        NOT NULL DEFAULT false,
    status             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100)
);

CREATE INDEX IF NOT EXISTS ix_cfg_currency_status ON public.cfg_currency (status);

-- Tabla: cfg_company
CREATE TABLE IF NOT EXISTS public.cfg_company
(
    company_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code               varchar(20)    NOT NULL,
    commercial_name    varchar(200)   NOT NULL,
    legal_name         varchar(200)   NOT NULL,
    tax_id             varchar(30)    NOT NULL,
    email              varchar(120),
    phone              varchar(30),
    address            varchar(500),
    country_code       varchar(3)     NOT NULL DEFAULT 'HND',
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    timezone           varchar(60)    NOT NULL DEFAULT 'America/Tegucigalpa',
    status             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (code),
    UNIQUE (tax_id)
);

CREATE INDEX IF NOT EXISTS ix_cfg_company_status ON public.cfg_company (status);

-- Tabla: cfg_branch (sucursales)
CREATE TABLE IF NOT EXISTS public.cfg_branch
(
    branch_id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(20)    NOT NULL,
    name               varchar(200)   NOT NULL,
    address            varchar(500),
    phone              varchar(30),
    email              varchar(120),
    status             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_cfg_branch_company ON public.cfg_branch (company_id);
CREATE INDEX IF NOT EXISTS ix_cfg_branch_status ON public.cfg_branch (status);

-- Tabla: cfg_tax (impuestos / retenciones)
CREATE TABLE IF NOT EXISTS public.cfg_tax
(
    tax_id             bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    name               varchar(120)   NOT NULL,
    description        varchar(300),
    tax_type           varchar(30)    NOT NULL, -- VAT, SALES, WITHHOLDING, etc.
    rate               numeric(9,4)   NOT NULL,
    is_withholding     boolean        NOT NULL DEFAULT false,
    ledger_account_code varchar(50), -- referencia contable
    status             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, name)
);

CREATE INDEX IF NOT EXISTS ix_cfg_tax_company ON public.cfg_tax (company_id);
CREATE INDEX IF NOT EXISTS ix_cfg_tax_status ON public.cfg_tax (status);

-- Tabla: cfg_document_type (tipos de documentos fiscales/control interno)
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

-- Tabla: cfg_document_series
CREATE TABLE IF NOT EXISTS public.cfg_document_series
(
    series_id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    branch_id          bigint         REFERENCES public.cfg_branch(branch_id) ON DELETE SET NULL,
    module             varchar(30)    NOT NULL, -- VENTAS, COMPRAS, BANCOS, etc.
    document_type      varchar(50)    NOT NULL, -- FACTURA, NOTA_CREDITO, ORDEN_PAGO, etc.
    document_type_id   bigint         NOT NULL REFERENCES public.cfg_document_type(document_type_id) ON DELETE RESTRICT,
    prefix             varchar(20)    NOT NULL,
    next_number        bigint         NOT NULL,
    min_number         bigint,
    max_number         bigint,
    expires_on         date,
    authorization_code varchar(60),
    cai_number         varchar(60),
    status             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, module, document_type, prefix),
    UNIQUE (company_id, cai_number)
);

CREATE INDEX IF NOT EXISTS ix_cfg_document_series_company ON public.cfg_document_series (company_id);
CREATE INDEX IF NOT EXISTS ix_cfg_document_series_status ON public.cfg_document_series (status);
CREATE INDEX IF NOT EXISTS ix_cfg_document_series_doc_type ON public.cfg_document_series (document_type_id);

COMMIT;
