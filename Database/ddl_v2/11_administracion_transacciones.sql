-- ================================================
-- 11_administracion_transacciones.sql
-- Tablas para cuentas por cobrar/pagar administrativas, interés de mora y ajustes fiscales
-- Requiere: 01_configuracion_base.sql, 02_contabilidad_core.sql, 04_ventas_core.sql, 10_administracion_maestros.sql
-- ================================================

BEGIN;

-- Tabla: adm_cxc_resumen (saldos consolidados por cliente)
CREATE TABLE IF NOT EXISTS public.adm_cxc_resumen
(
    cxc_id             bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    cliente_id         bigint         NOT NULL REFERENCES public.adm_cliente(cliente_id),
    saldo_inicial      numeric(18,2)  NOT NULL DEFAULT 0,
    cargos             numeric(18,2)  NOT NULL DEFAULT 0,
    abonos             numeric(18,2)  NOT NULL DEFAULT 0,
    saldo_actual       numeric(18,2)  NOT NULL DEFAULT 0,
    ultima_actualizacion timestamptz  NOT NULL DEFAULT now(),
    UNIQUE (company_id, cliente_id)
);

-- Tabla: adm_cxc_movimiento (Detalle administrativo complementario)
CREATE TABLE IF NOT EXISTS public.adm_cxc_movimiento
(
    cxc_mov_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cxc_id             bigint         NOT NULL REFERENCES public.adm_cxc_resumen(cxc_id) ON DELETE CASCADE,
    fecha_movimiento   date           NOT NULL,
    documento_tipo     varchar(30)    NOT NULL,
    documento_id       bigint,
    descripcion        varchar(300),
    cargo              numeric(18,2)  NOT NULL DEFAULT 0,
    abono              numeric(18,2)  NOT NULL DEFAULT 0,
    saldo_posterior    numeric(18,2)  NOT NULL DEFAULT 0,
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    creado_por         varchar(100)   NOT NULL DEFAULT current_user,
    creado_en          timestamptz    NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_adm_cxc_mov_cxc ON public.adm_cxc_movimiento (cxc_id);

-- Tabla: adm_cxp_resumen
CREATE TABLE IF NOT EXISTS public.adm_cxp_resumen
(
    cxp_id             bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    proveedor_id       bigint         NOT NULL REFERENCES public.adm_proveedor(proveedor_id),
    saldo_inicial      numeric(18,2)  NOT NULL DEFAULT 0,
    cargos             numeric(18,2)  NOT NULL DEFAULT 0,
    abonos             numeric(18,2)  NOT NULL DEFAULT 0,
    saldo_actual       numeric(18,2)  NOT NULL DEFAULT 0,
    ultima_actualizacion timestamptz  NOT NULL DEFAULT now(),
    UNIQUE (company_id, proveedor_id)
);

-- Tabla: adm_cxp_movimiento
CREATE TABLE IF NOT EXISTS public.adm_cxp_movimiento
(
    cxp_mov_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cxp_id             bigint         NOT NULL REFERENCES public.adm_cxp_resumen(cxp_id) ON DELETE CASCADE,
    fecha_movimiento   date           NOT NULL,
    documento_tipo     varchar(30)    NOT NULL,
    documento_id       bigint,
    descripcion        varchar(300),
    cargo              numeric(18,2)  NOT NULL DEFAULT 0,
    abono              numeric(18,2)  NOT NULL DEFAULT 0,
    saldo_posterior    numeric(18,2)  NOT NULL DEFAULT 0,
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    creado_por         varchar(100)   NOT NULL DEFAULT current_user,
    creado_en          timestamptz    NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_adm_cxp_mov_cxp ON public.adm_cxp_movimiento (cxp_id);

-- Tabla: adm_interes_mora
CREATE TABLE IF NOT EXISTS public.adm_interes_mora
(
    interes_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cxc_id             bigint         NOT NULL REFERENCES public.adm_cxc_resumen(cxc_id) ON DELETE CASCADE,
    factura_id         bigint         REFERENCES public.ven_factura(factura_id),
    periodo            varchar(7)     NOT NULL, -- YYYY-MM
    fecha_calculo      date           NOT NULL,
    tasa_anual         numeric(9,4)   NOT NULL,
    dias_mora          integer        NOT NULL,
    monto_calculado    numeric(18,2)  NOT NULL,
    estado             varchar(20)    NOT NULL DEFAULT 'PENDING', -- PENDING, APPLIED
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    creado_por         varchar(100)   NOT NULL DEFAULT current_user,
    creado_en          timestamptz    NOT NULL DEFAULT now(),
    UNIQUE (cxc_id, periodo, factura_id)
);

-- Tabla: adm_ajuste_fiscal (ajustes de impuestos y regularizaciones)
CREATE TABLE IF NOT EXISTS public.adm_ajuste_fiscal
(
    ajuste_id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    tipo_ajuste        varchar(30)    NOT NULL, -- ISV, ISR, OTRO
    descripcion        varchar(300),
    documento_tipo     varchar(30),
    documento_id       bigint,
    monto              numeric(18,2)  NOT NULL,
    fecha_ajuste       date           NOT NULL,
    estado             varchar(20)    NOT NULL DEFAULT 'PENDING',
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    creado_por         varchar(100)   NOT NULL DEFAULT current_user,
    creado_en          timestamptz    NOT NULL DEFAULT now()
);

-- Tabla: adm_operacion_log (bitácora de operaciones administrativas)
CREATE TABLE IF NOT EXISTS public.adm_operacion_log
(
    operacion_log_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    operacion_id       bigint         REFERENCES public.adm_operacion(operacion_id),
    company_id         bigint         REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    usuario            varchar(100)   NOT NULL,
    modulo             varchar(30)    NOT NULL,
    entidad            varchar(100)   NOT NULL,
    entidad_id         varchar(100),
    descripcion        varchar(500),
    creado_en          timestamptz    NOT NULL DEFAULT now()
);

COMMIT;
