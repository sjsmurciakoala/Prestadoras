-- ================================================
-- 05_com_core.sql
-- Tablas normalizadas para el módulo de Compras (órdenes, facturas y pagos)
-- Requiere: 01_configuracion_base.sql, 02_contabilidad_core.sql, 07_administracion_core.sql
-- ================================================

BEGIN;

-- Tabla: com_orden (orden de compra)
CREATE TABLE IF NOT EXISTS public.com_orden
(
    orden_id           bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    branch_id          bigint         REFERENCES public.cfg_branch(branch_id),
    proveedor_id       bigint         NOT NULL REFERENCES public.adm_proveedor(proveedor_id),
    numero_orden       varchar(50)    NOT NULL,
    fecha_orden        date           NOT NULL,
    fecha_entrega      date,
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    exchange_rate      numeric(18,6)  NOT NULL DEFAULT 1,
    estado             varchar(20)    NOT NULL DEFAULT 'DRAFT', -- DRAFT, APPROVED, CLOSED, CANCELLED
    subtotal           numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_total     numeric(18,2)  NOT NULL DEFAULT 0,
    total              numeric(18,2)  NOT NULL DEFAULT 0,
    observaciones      varchar(500),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, numero_orden)
);

CREATE INDEX IF NOT EXISTS ix_com_orden_estado ON public.com_orden (estado);
CREATE INDEX IF NOT EXISTS ix_com_orden_proveedor ON public.com_orden (proveedor_id);

-- Tabla: com_orden_linea
CREATE TABLE IF NOT EXISTS public.com_orden_linea
(
    orden_linea_id     bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    orden_id           bigint         NOT NULL REFERENCES public.com_orden(orden_id) ON DELETE CASCADE,
    line_number        smallint       NOT NULL,
    producto_codigo    varchar(50)    NOT NULL,
    descripcion        varchar(300)   NOT NULL,
    cantidad           numeric(18,4)  NOT NULL,
    costo_unitario     numeric(18,4)  NOT NULL,
    descuento          numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_monto     numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_id        bigint         REFERENCES public.cfg_tax(tax_id),
    total_linea        numeric(18,2)  NOT NULL,
    UNIQUE (orden_id, line_number)
);

CREATE INDEX IF NOT EXISTS ix_com_orden_linea_orden ON public.com_orden_linea (orden_id);

-- Tabla: com_factura (factura proveedor)
CREATE TABLE IF NOT EXISTS public.com_factura
(
    factura_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    proveedor_id       bigint         NOT NULL REFERENCES public.adm_proveedor(proveedor_id),
    document_type_id   bigint         NOT NULL REFERENCES public.cfg_document_type(document_type_id),
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    exchange_rate      numeric(18,6)  NOT NULL DEFAULT 1,
    numero_documento   varchar(50)    NOT NULL,
    numero_fiscal      varchar(25),
    fecha_emision      date           NOT NULL,
    fecha_vencimiento  date,
    proveedor_nombre   varchar(200)   NOT NULL,
    proveedor_tax_id   varchar(30),
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
    estado             varchar(20)    NOT NULL DEFAULT 'PENDING', -- PENDING, PAID, CANCELLED
    orden_id           bigint         REFERENCES public.com_orden(orden_id),
    con_poliza_id     bigint         REFERENCES public.con_poliza(poliza_id),
    observaciones      varchar(500),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, proveedor_id, numero_documento)
);

CREATE INDEX IF NOT EXISTS ix_com_factura_proveedor ON public.com_factura (proveedor_id);
CREATE INDEX IF NOT EXISTS ix_com_factura_doc_type ON public.com_factura (document_type_id);
CREATE INDEX IF NOT EXISTS ix_com_factura_estado ON public.com_factura (estado);

-- Tabla: com_factura_linea
CREATE TABLE IF NOT EXISTS public.com_factura_linea
(
    factura_linea_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    factura_id         bigint         NOT NULL REFERENCES public.com_factura(factura_id) ON DELETE CASCADE,
    line_number        smallint       NOT NULL,
    producto_codigo    varchar(50)    NOT NULL,
    descripcion        varchar(300)   NOT NULL,
    cantidad           numeric(18,4)  NOT NULL,
    costo_unitario     numeric(18,4)  NOT NULL,
    descuento          numeric(18,2)  NOT NULL DEFAULT 0,
    base_imponible     numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_monto     numeric(18,2)  NOT NULL DEFAULT 0,
    impuesto_id        bigint         REFERENCES public.cfg_tax(tax_id),
    tratamiento_impuesto varchar(20)  NOT NULL DEFAULT 'GRAVADO',
    total_linea        numeric(18,2)  NOT NULL,
    UNIQUE (factura_id, line_number)
);

CREATE INDEX IF NOT EXISTS ix_com_factura_linea_factura ON public.com_factura_linea (factura_id);
CREATE INDEX IF NOT EXISTS ix_com_factura_linea_tax ON public.com_factura_linea (impuesto_id);

-- Tabla: com_pago (pagos a proveedor)
CREATE TABLE IF NOT EXISTS public.com_pago
(
    pago_id            bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    proveedor_id       bigint         NOT NULL REFERENCES public.adm_proveedor(proveedor_id),
    document_type_id   bigint         NOT NULL REFERENCES public.cfg_document_type(document_type_id),
    document_series_id bigint         REFERENCES public.cfg_document_series(series_id),
    numero_pago        varchar(50)    NOT NULL,
    banco_cuenta_id    bigint,
    fecha_pago         date           NOT NULL,
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    exchange_rate      numeric(18,6)  NOT NULL DEFAULT 1,
    monto_pagado       numeric(18,2)  NOT NULL,
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
    UNIQUE (company_id, numero_pago)
);

CREATE INDEX IF NOT EXISTS ix_com_pago_proveedor ON public.com_pago (proveedor_id);
CREATE INDEX IF NOT EXISTS ix_com_pago_doc_type ON public.com_pago (document_type_id);
CREATE INDEX IF NOT EXISTS ix_com_pago_estado ON public.com_pago (estado);
CREATE INDEX IF NOT EXISTS ix_com_pago_series ON public.com_pago (document_series_id);

-- Tabla: com_pago_detalle (aplicación de pagos a facturas)
CREATE TABLE IF NOT EXISTS public.com_pago_detalle
(
    pago_detalle_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    pago_id            bigint         NOT NULL REFERENCES public.com_pago(pago_id) ON DELETE CASCADE,
    factura_id         bigint         NOT NULL REFERENCES public.com_factura(factura_id) ON DELETE CASCADE,
    monto_aplicado     numeric(18,2)  NOT NULL,
    monto_retencion    numeric(18,2)  NOT NULL DEFAULT 0,
    retencion_tax_id   bigint         REFERENCES public.cfg_tax(tax_id),
    descripcion        varchar(200),
    UNIQUE (pago_id, factura_id)
);

CREATE INDEX IF NOT EXISTS ix_com_pago_detalle_pago ON public.com_pago_detalle (pago_id);
CREATE INDEX IF NOT EXISTS ix_com_pago_detalle_factura ON public.com_pago_detalle (factura_id);

COMMIT;
