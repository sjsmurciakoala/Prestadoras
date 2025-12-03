-- ================================================
-- 06_ban_core.sql
-- Tablas normalizadas para el módulo de Bancos
-- Requiere: 01_configuracion_base.sql, 02_contabilidad_core.sql
-- ================================================

BEGIN;

-- Tabla: ban_cuenta
CREATE TABLE IF NOT EXISTS public.ban_cuenta
(
    banco_cuenta_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    banco_nombre       varchar(150),
    branch_id          bigint         REFERENCES public.cfg_branch(branch_id) ON DELETE SET NULL,
    tipo               varchar(20)    NOT NULL DEFAULT 'CHEQUES', -- CHEQUES, AHORRO, TARJETA, CAJA
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    numero_cuenta      varchar(50)    NOT NULL,
    saldo_inicial      numeric(18,2)  NOT NULL DEFAULT 0,
    fecha_saldo        date,
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    allow_reconciliation boolean      NOT NULL DEFAULT true,
    cont_account_id    bigint         REFERENCES public.con_plan_cuentas(account_id),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code),
    UNIQUE (company_id, numero_cuenta)
);

CREATE INDEX IF NOT EXISTS ix_ban_cuenta_company ON public.ban_cuenta (company_id);

-- Tabla: ban_movimiento (encabezado)
CREATE TABLE IF NOT EXISTS public.ban_movimiento
(
    movimiento_id      bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    banco_cuenta_id    bigint         NOT NULL REFERENCES public.ban_cuenta(banco_cuenta_id) ON DELETE CASCADE,
    tipo               varchar(20)    NOT NULL, -- INGRESO, EGRESO, TRANSFERENCIA
    fecha_movimiento   date           NOT NULL,
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    exchange_rate      numeric(18,6)  NOT NULL DEFAULT 1,
    monto              numeric(18,2)  NOT NULL,
    monto_local        numeric(18,2)  NOT NULL DEFAULT 0,
    descripcion        varchar(300),
    referencia         varchar(100),
    origen_modulo      varchar(30),
    origen_documento_id bigint,
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    estado             varchar(20)    NOT NULL DEFAULT 'POSTED', -- DRAFT, POSTED, VOID
    conciliado         boolean        NOT NULL DEFAULT false,
    fecha_conciliacion date,
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100)
);

CREATE INDEX IF NOT EXISTS ix_ban_movimiento_cuenta ON public.ban_movimiento (banco_cuenta_id);
CREATE INDEX IF NOT EXISTS ix_ban_movimiento_origen ON public.ban_movimiento (origen_modulo, origen_documento_id);

-- Tabla: ban_conciliacion (encabezado)
CREATE TABLE IF NOT EXISTS public.ban_conciliacion
(
    conciliacion_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    banco_cuenta_id    bigint         NOT NULL REFERENCES public.ban_cuenta(banco_cuenta_id) ON DELETE CASCADE,
    periodo            varchar(10)    NOT NULL, -- formato YYYY-MM
    fecha_inicio       date           NOT NULL,
    fecha_fin          date           NOT NULL,
    saldo_estado_cuenta numeric(18,2) NOT NULL,
    saldo_libros       numeric(18,2)  NOT NULL,
    diferencia         numeric(18,2)  NOT NULL DEFAULT 0,
    estado             varchar(20)    NOT NULL DEFAULT 'OPEN', -- OPEN, CLOSED
    observaciones      varchar(300),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (banco_cuenta_id, periodo)
);

CREATE INDEX IF NOT EXISTS ix_ban_conciliacion_cuenta ON public.ban_conciliacion (banco_cuenta_id);

-- Tabla: ban_conciliacion_detalle
CREATE TABLE IF NOT EXISTS public.ban_conciliacion_detalle
(
    conciliacion_detalle_id bigint     GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    conciliacion_id    bigint         NOT NULL REFERENCES public.ban_conciliacion(conciliacion_id) ON DELETE CASCADE,
    movimiento_id      bigint         NOT NULL REFERENCES public.ban_movimiento(movimiento_id) ON DELETE CASCADE,
    aplicado           boolean        NOT NULL DEFAULT false,
    comentario         varchar(200),
    UNIQUE (conciliacion_id, movimiento_id)
);

CREATE INDEX IF NOT EXISTS ix_ban_conciliacion_detalle_conc ON public.ban_conciliacion_detalle (conciliacion_id);

-- Relacionar movimientos de ventas y compras con cuentas bancarias (se ejecuta si las tablas existen)
DO $$
BEGIN
    IF to_regclass('public.ven_cobro') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM pg_constraint
             WHERE conname = 'fk_ven_cobro_banco'
               AND conrelid = 'public.ven_cobro'::regclass
       ) THEN
        ALTER TABLE public.ven_cobro
        ADD CONSTRAINT fk_ven_cobro_banco
        FOREIGN KEY (banco_cuenta_id)
        REFERENCES public.ban_cuenta(banco_cuenta_id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
        NOT VALID;
        ALTER TABLE public.ven_cobro VALIDATE CONSTRAINT fk_ven_cobro_banco;
    END IF;

    IF to_regclass('public.com_pago') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM pg_constraint
             WHERE conname = 'fk_com_pago_banco'
               AND conrelid = 'public.com_pago'::regclass
       ) THEN
        ALTER TABLE public.com_pago
        ADD CONSTRAINT fk_com_pago_banco
        FOREIGN KEY (banco_cuenta_id)
        REFERENCES public.ban_cuenta(banco_cuenta_id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
        NOT VALID;
        ALTER TABLE public.com_pago VALIDATE CONSTRAINT fk_com_pago_banco;
    END IF;
END $$;

COMMIT;
