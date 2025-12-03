-- ================================================
-- 09_activos_fijos_core.sql
-- Tablas para el módulo de Activos Fijos
-- Requiere: 01_configuracion_base.sql, 02_contabilidad_core.sql
-- ================================================

BEGIN;

-- Tabla: af_categoria (configuración contable por tipo de activo)
CREATE TABLE IF NOT EXISTS public.af_categoria
(
    categoria_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code                varchar(30)    NOT NULL,
    nombre              varchar(150)   NOT NULL,
    descripcion         varchar(300),
    vida_util_meses     integer        NOT NULL DEFAULT 60,
    valor_residual_pct  numeric(9,4)   NOT NULL DEFAULT 0,
    metodo_depreciacion varchar(20)    NOT NULL DEFAULT 'LINEAL', -- LINEAL, DCL, SUMA_DIGITOS
    cuenta_activo_id    bigint         REFERENCES public.con_plan_cuentas(account_id),
    cuenta_dep_acum_id  bigint         REFERENCES public.con_plan_cuentas(account_id),
    cuenta_gasto_dep_id bigint         REFERENCES public.con_plan_cuentas(account_id),
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_af_categoria_company ON public.af_categoria (company_id);

-- Tabla: af_activo (maestro de activos)
CREATE TABLE IF NOT EXISTS public.af_activo
(
    activo_id           bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    categoria_id        bigint         REFERENCES public.af_categoria(categoria_id) ON DELETE SET NULL,
    code                varchar(50)    NOT NULL,
    descripcion         varchar(300)   NOT NULL,
    numero_serie        varchar(120),
    fecha_adquisicion   date           NOT NULL,
    costo_adquisicion   numeric(18,2)  NOT NULL,
    valor_residual      numeric(18,2)  NOT NULL DEFAULT 0,
    vida_util_meses     integer        NOT NULL,
    metodo_depreciacion varchar(20)    NOT NULL DEFAULT 'LINEAL',
    ubicacion           varchar(200),
    responsable         varchar(150),
    estado              varchar(20)    NOT NULL DEFAULT 'ACTIVO', -- ACTIVO, BAJA, VENDIDO
    con_poliza_alta_id bigint         REFERENCES public.con_poliza(poliza_id),
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_af_activo_company ON public.af_activo (company_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_categoria ON public.af_activo (categoria_id);

-- Tabla: af_depreciacion (registro histórico)
CREATE TABLE IF NOT EXISTS public.af_depreciacion
(
    depreciacion_id     bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    activo_id           bigint         NOT NULL REFERENCES public.af_activo(activo_id) ON DELETE CASCADE,
    periodo             varchar(7)     NOT NULL, -- formato YYYY-MM
    fecha_calculo       date           NOT NULL,
    monto_depreciacion  numeric(18,2)  NOT NULL,
    monto_acumulado     numeric(18,2)  NOT NULL,
    con_poliza_id      bigint         REFERENCES public.con_poliza(poliza_id),
    contabilizado       boolean        NOT NULL DEFAULT false,
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    UNIQUE (activo_id, periodo)
);

CREATE INDEX IF NOT EXISTS ix_af_depreciacion_activo ON public.af_depreciacion (activo_id);

-- Tabla: af_baja (disposición de activos)
CREATE TABLE IF NOT EXISTS public.af_baja
(
    baja_id             bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    activo_id           bigint         NOT NULL REFERENCES public.af_activo(activo_id) ON DELETE CASCADE,
    fecha_baja          date           NOT NULL,
    motivo              varchar(200),
    valor_venta         numeric(18,2)  NOT NULL DEFAULT 0,
    con_poliza_id      bigint         REFERENCES public.con_poliza(poliza_id),
    observaciones       varchar(300),
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user
);

CREATE INDEX IF NOT EXISTS ix_af_baja_activo ON public.af_baja (activo_id);

COMMIT;
