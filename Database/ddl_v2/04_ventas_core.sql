-- ================================================
-- 04_ven_core.sql
-- Tablas normalizadas para el módulo de Ventas (facturación y cobranzas)
-- Requiere: 01_configuracion_base.sql, 02_contabilidad_core.sql, 07_administracion_core.sql
-- ================================================

BEGIN;

-- Tabla: ven_factura (encabezado)
CREATE TABLE IF NOT EXISTS public.ven_factura
(
    factura_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    branch_id          bigint         REFERENCES public.cfg_branch(branch_id),
    cliente_id         bigint         NOT NULL REFERENCES public.adm_cliente(cliente_id),
    document_type_id   bigint         NOT NULL REFERENCES public.cfg_document_type(document_type_id),
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    exchange_rate      numeric(18,6)  NOT NULL DEFAULT 1,
    document_series_id bigint         REFERENCES public.cfg_document_series(series_id),
    numero_documento   varchar(50)    NOT NULL,
    numero_fiscal      varchar(25)    NOT NULL,
    cai_number         varchar(60),
    fecha_emision      date           NOT NULL,
    fecha_vencimiento  date,
    cliente_nombre     varchar(200)   NOT NULL,
    cliente_tax_id     varchar(30),
    subtotal           numeric(18,2)  NOT NULL DEFAULT 0,
    subtotal_gravado   numeric(18,2)  NOT NULL DEFAULT 0,
    subtotal_exento    numeric(18,2)  NOT NULL DEFAULT 0,
    subtotal_exonerado numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_isv       numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_otros     numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_total     numeric(18,2)  NOT NULL DEFAULT 0,
    monto_retenciones  numeric(18,2)  NOT NULL DEFAULT 0,
    total              numeric(18,2)  NOT NULL DEFAULT 0,
    saldo_actual       numeric(18,2)  NOT NULL DEFAULT 0,
    estatus            varchar(20)    NOT NULL DEFAULT 'PENDING', -- PENDING, PAID, CANCELLED
    referencia         varchar(200),
    observaciones      varchar(500),
    enviada_sar        boolean        NOT NULL DEFAULT false,
    fecha_envio_sar    timestamptz,
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, numero_documento),
    UNIQUE (company_id, numero_fiscal)
);

CREATE INDEX IF NOT EXISTS ix_ven_factura_cliente ON public.ven_factura (cliente_id);
CREATE INDEX IF NOT EXISTS ix_ven_factura_estado ON public.ven_factura (estatus);
CREATE INDEX IF NOT EXISTS ix_ven_factura_doc_type ON public.ven_factura (document_type_id);
CREATE INDEX IF NOT EXISTS ix_ven_factura_series ON public.ven_factura (document_series_id);

-- Tabla: ven_factura_linea
CREATE TABLE IF NOT EXISTS public.ven_factura_linea
(
    factura_linea_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    factura_id         bigint         NOT NULL REFERENCES public.ven_factura(factura_id) ON DELETE CASCADE,
    line_number        smallint       NOT NULL,
    producto_codigo    varchar(50)    NOT NULL,
    descripcion        varchar(300)   NOT NULL,
    item_tipo          varchar(20)    NOT NULL DEFAULT 'PRODUCTO', -- PRODUCTO, SERVICIO
    cantidad           numeric(18,4)  NOT NULL,
    precio_unitario    numeric(18,4)  NOT NULL,
    descuento          numeric(18,2)  NOT NULL DEFAULT 0,
    base_imponible     numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_monto     numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_id        bigint         REFERENCES public.cfg_tax(tax_id),
    tratamiento_impuesto varchar(20)  NOT NULL DEFAULT 'GRAVADO', -- GRAVADO, EXENTO, EXONERADO
    total_linea        numeric(18,2)  NOT NULL,
    UNIQUE (factura_id, line_number),
    CHECK (item_tipo IN ('PRODUCTO', 'SERVICIO'))
);

CREATE INDEX IF NOT EXISTS ix_ven_factura_linea_factura ON public.ven_factura_linea (factura_id);
CREATE INDEX IF NOT EXISTS ix_ven_factura_linea_tax ON public.ven_factura_linea (impuesto_id);

-- Tabla: ven_nota (crédito/débito)
CREATE TABLE IF NOT EXISTS public.ven_nota
(
    nota_id            bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    factura_id         bigint         NOT NULL REFERENCES public.ven_factura(factura_id) ON DELETE CASCADE,
    tipo               varchar(10)    NOT NULL, -- CREDITO / DEBITO
    document_type_id   bigint         NOT NULL REFERENCES public.cfg_document_type(document_type_id),
    numero_documento   varchar(50)    NOT NULL,
    numero_fiscal      varchar(25),
    fecha_emision      date           NOT NULL,
    motivo             varchar(200),
    subtotal           numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_total     numeric(18,2)  NOT NULL DEFAULT 0,
    monto              numeric(18,2)  NOT NULL,
    saldo_afectado     numeric(18,2)  NOT NULL,
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (factura_id, numero_documento)
);

CREATE INDEX IF NOT EXISTS ix_ven_nota_factura ON public.ven_nota (factura_id);
CREATE INDEX IF NOT EXISTS ix_ven_nota_doc_type ON public.ven_nota (document_type_id);

-- Tabla: ven_cobro (pagos/recibos)
CREATE TABLE IF NOT EXISTS public.ven_cobro
(
    cobro_id           bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    cliente_id         bigint         NOT NULL REFERENCES public.adm_cliente(cliente_id),
    document_type_id   bigint         NOT NULL REFERENCES public.cfg_document_type(document_type_id),
    document_series_id bigint         REFERENCES public.cfg_document_series(series_id),
    numero_recibo      varchar(50)    NOT NULL,
    banco_cuenta_id    bigint,
    fecha_cobro        date           NOT NULL,
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    exchange_rate      numeric(18,6)  NOT NULL DEFAULT 1,
    monto_cobrado      numeric(18,2)  NOT NULL,
    monto_retenciones  numeric(18,2)  NOT NULL DEFAULT 0,
    metodo             varchar(30)    NOT NULL, -- EFECTIVO, CHEQUE, TRANSFERENCIA
    referencia_bancaria varchar(100),
    observaciones      varchar(300),
    estado             varchar(20)    NOT NULL DEFAULT 'POSTED', -- POSTED, VOID
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, numero_recibo)
);

CREATE INDEX IF NOT EXISTS ix_ven_cobro_cliente ON public.ven_cobro (cliente_id);
CREATE INDEX IF NOT EXISTS ix_ven_cobro_doc_type ON public.ven_cobro (document_type_id);
CREATE INDEX IF NOT EXISTS ix_ven_cobro_estado ON public.ven_cobro (estado);
CREATE INDEX IF NOT EXISTS ix_ven_cobro_series ON public.ven_cobro (document_series_id);

-- Tabla: ven_cobro_detalle (aplicación de recibos a facturas)
CREATE TABLE IF NOT EXISTS public.ven_cobro_detalle
(
    cobro_detalle_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cobro_id           bigint         NOT NULL REFERENCES public.ven_cobro(cobro_id) ON DELETE CASCADE,
    factura_id         bigint         NOT NULL REFERENCES public.ven_factura(factura_id) ON DELETE CASCADE,
    monto_aplicado     numeric(18,2)  NOT NULL,
    monto_retencion    numeric(18,2)  NOT NULL DEFAULT 0,
    retencion_tax_id   bigint         REFERENCES public.cfg_tax(tax_id),
    descripcion        varchar(200),
    UNIQUE (cobro_id, factura_id)
);

CREATE INDEX IF NOT EXISTS ix_ven_cobro_detalle_cobro ON public.ven_cobro_detalle (cobro_id);
CREATE INDEX IF NOT EXISTS ix_ven_cobro_detalle_factura ON public.ven_cobro_detalle (factura_id);

COMMIT;
