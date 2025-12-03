-- ================================================
-- 02_contabilidad_core.sql
-- Tablas para plan de cuentas, centros de costo y plantillas/pólizas contables
-- Requiere: 01_configuracion_base.sql (company, currency)
-- ================================================

BEGIN;

-- Tabla: con_plan_cuentas (catálogo general)
CREATE TABLE IF NOT EXISTS public.con_plan_cuentas
(
    account_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    parent_account_id  bigint         REFERENCES public.con_plan_cuentas(account_id) ON DELETE SET NULL,
    code               varchar(30)    NOT NULL,
    name               varchar(200)   NOT NULL,
    description        varchar(500),
    account_type       varchar(30)    NOT NULL, -- ACTIVO, PASIVO, CAPITAL, INGRESO, GASTO, MEMORANDA
    category           varchar(30),
    level              smallint       NOT NULL DEFAULT 1,
    allows_posting     boolean        NOT NULL DEFAULT true,
    currency_code      char(3)        REFERENCES public.cfg_currency(currency_code),
    status             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_con_plan_cuentas_company ON public.con_plan_cuentas (company_id);
CREATE INDEX IF NOT EXISTS ix_con_plan_cuentas_parent ON public.con_plan_cuentas (parent_account_id);

-- Tabla: con_centro_costo (opcional por compañía)
CREATE TABLE IF NOT EXISTS public.con_centro_costo
(
    cost_center_id     bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    name               varchar(150)   NOT NULL,
    description        varchar(300),
    status             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_con_centro_costo_company ON public.con_centro_costo (company_id);

-- Tabla: con_periodo_contable (control de períodos abiertos/cerrados)
CREATE TABLE IF NOT EXISTS public.con_periodo_contable
(
    period_id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(20)    NOT NULL,
    name               varchar(80)    NOT NULL,
    start_date         date           NOT NULL,
    end_date           date           NOT NULL,
    status             varchar(20)    NOT NULL DEFAULT 'OPEN', -- OPEN, CLOSED, LOCKED
    closed_at          timestamptz,
    closed_by          varchar(100),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_cont_periodo_company ON public.con_periodo_contable (company_id);
CREATE INDEX IF NOT EXISTS ix_cont_periodo_status ON public.con_periodo_contable (status);

-- Tabla: con_diario (catálogo de diarios contables)
CREATE TABLE IF NOT EXISTS public.con_diario
(
    journal_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(20)    NOT NULL,
    name               varchar(120)   NOT NULL,
    description        varchar(300),
    sequence_prefix    varchar(20),
    last_sequence      bigint         NOT NULL DEFAULT 0,
    is_active          boolean        NOT NULL DEFAULT true,
    allows_manual      boolean        NOT NULL DEFAULT true,
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_con_diario_company ON public.con_diario (company_id);

-- Tabla: con_regla_integracion (mapeo contable automático)
CREATE TABLE IF NOT EXISTS public.con_regla_integracion
(
    regla_id           bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    module             varchar(30)    NOT NULL,
    document_type_id   bigint         NOT NULL REFERENCES public.cfg_document_type(document_type_id) ON DELETE RESTRICT,
    scenario_code      varchar(50)    NOT NULL,
    description        varchar(300),
    debit_account_id   bigint         NOT NULL REFERENCES public.con_plan_cuentas(account_id),
    credit_account_id  bigint         NOT NULL REFERENCES public.con_plan_cuentas(account_id),
    cost_center_id     bigint         REFERENCES public.con_centro_costo(cost_center_id),
    is_active          boolean        NOT NULL DEFAULT true,
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, module, document_type_id, scenario_code)
);

CREATE INDEX IF NOT EXISTS ix_con_regla_integracion_company ON public.con_regla_integracion (company_id);
CREATE INDEX IF NOT EXISTS ix_con_regla_integracion_module ON public.con_regla_integracion (module);

-- Tabla: con_plantilla_poliza (definición de pólizas automáticas)
CREATE TABLE IF NOT EXISTS public.con_plantilla_poliza
(
    template_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    module             varchar(30)    NOT NULL, -- VENTAS, COMPRAS, BANCOS, etc.
    document_type      varchar(50)    NOT NULL, -- FACTURA, NOTA_CREDITO, ORDEN_PAGO
    name               varchar(150)   NOT NULL,
    description        varchar(500),
    is_active          boolean        NOT NULL DEFAULT true,
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, module, document_type, name)
);

CREATE INDEX IF NOT EXISTS ix_con_plantilla_poliza_company ON public.con_plantilla_poliza (company_id);

-- Tabla: con_plantilla_poliza_linea (detalle)
CREATE TABLE IF NOT EXISTS public.con_plantilla_poliza_linea
(
    template_line_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    template_id        bigint         NOT NULL REFERENCES public.con_plantilla_poliza(template_id) ON DELETE CASCADE,
    line_number        smallint       NOT NULL,
    account_id         bigint         NOT NULL REFERENCES public.con_plan_cuentas(account_id),
    cost_center_id     bigint         REFERENCES public.con_centro_costo(cost_center_id),
    debit_formula      varchar(200), -- puede contener tokens como {total}, {iva}
    credit_formula     varchar(200),
    description        varchar(300),
    UNIQUE (template_id, line_number)
);

CREATE INDEX IF NOT EXISTS ix_con_plantilla_poliza_linea_template ON public.con_plantilla_poliza_linea (template_id);

-- Tabla: con_poliza (encabezado de póliza)
CREATE TABLE IF NOT EXISTS public.con_poliza
(
    poliza_id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    journal_id         bigint         REFERENCES public.con_diario(journal_id) ON DELETE SET NULL,
    period_id          bigint         REFERENCES public.con_periodo_contable(period_id) ON DELETE SET NULL,
    template_id        bigint         REFERENCES public.con_plantilla_poliza(template_id),
    module             varchar(30)    NOT NULL,
    document_type      varchar(50)    NOT NULL,
    document_id        bigint,        -- referencia al documento origen
    document_number    varchar(50),
    poliza_number      varchar(50)    NOT NULL,
    sequence_number    bigint,
    poliza_date        date           NOT NULL,
    description        varchar(500),
    status             varchar(20)    NOT NULL DEFAULT 'POSTED', -- DRAFT, POSTED, VOID
    source_reference   varchar(120),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, poliza_number),
    UNIQUE (journal_id, sequence_number)
);

CREATE INDEX IF NOT EXISTS ix_con_poliza_company ON public.con_poliza (company_id);
CREATE INDEX IF NOT EXISTS ix_con_poliza_document ON public.con_poliza (module, document_type, document_id);
CREATE INDEX IF NOT EXISTS ix_con_poliza_journal ON public.con_poliza (journal_id);
CREATE INDEX IF NOT EXISTS ix_con_poliza_period ON public.con_poliza (period_id);

-- Tabla: con_poliza_linea
CREATE TABLE IF NOT EXISTS public.con_poliza_linea
(
    poliza_line_id     bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    poliza_id          bigint         NOT NULL REFERENCES public.con_poliza(poliza_id) ON DELETE CASCADE,
    line_number        smallint       NOT NULL,
    account_id         bigint         NOT NULL REFERENCES public.con_plan_cuentas(account_id),
    cost_center_id     bigint         REFERENCES public.con_centro_costo(cost_center_id),
    description        varchar(300),
    debit_amount       numeric(18,2)  NOT NULL DEFAULT 0,
    credit_amount      numeric(18,2)  NOT NULL DEFAULT 0,
    currency_code      char(3)        REFERENCES public.cfg_currency(currency_code),
    exchange_rate      numeric(18,6),
    source_document    varchar(120),
    UNIQUE (poliza_id, line_number)
);

CREATE INDEX IF NOT EXISTS ix_con_poliza_linea_poliza ON public.con_poliza_linea (poliza_id);

COMMIT;
