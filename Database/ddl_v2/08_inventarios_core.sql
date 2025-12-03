-- ================================================
-- 08_inventarios_core.sql
-- Tablas normalizadas para el módulo de Inventarios
-- Requiere: 01_configuracion_base.sql, 02_contabilidad_core.sql
-- ================================================

BEGIN;

-- Tabla: inv_categoria (agrupadores contables de productos)
CREATE TABLE IF NOT EXISTS public.inv_categoria
(
    categoria_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code                varchar(30)    NOT NULL,
    nombre              varchar(150)   NOT NULL,
    descripcion         varchar(300),
    cuenta_inventario_id bigint        REFERENCES public.con_plan_cuentas(account_id),
    cuenta_costo_id     bigint         REFERENCES public.con_plan_cuentas(account_id),
    cuenta_ingreso_id   bigint         REFERENCES public.con_plan_cuentas(account_id),
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_inv_categoria_company ON public.inv_categoria (company_id);

-- Tabla: inv_producto (maestro de ítems)
CREATE TABLE IF NOT EXISTS public.inv_producto
(
    producto_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    categoria_id        bigint         REFERENCES public.inv_categoria(categoria_id) ON DELETE SET NULL,
    code                varchar(50)    NOT NULL,
    nombre              varchar(200)   NOT NULL,
    descripcion         varchar(500),
    unidad_medida       varchar(20)    NOT NULL DEFAULT 'UNIDAD',
    es_inventariable    boolean        NOT NULL DEFAULT true,
    metodo_costeo       varchar(20)    NOT NULL DEFAULT 'PROMEDIO', -- PROMEDIO, FIFO, PEPS
    costo_promedio      numeric(18,6)  NOT NULL DEFAULT 0,
    costo_ultimo        numeric(18,6)  NOT NULL DEFAULT 0,
    impuesto_venta_id   bigint         REFERENCES public.cfg_tax(tax_id),
    cuenta_inventario_id bigint        REFERENCES public.con_plan_cuentas(account_id),
    cuenta_costo_id     bigint         REFERENCES public.con_plan_cuentas(account_id),
    cuenta_ingreso_id   bigint         REFERENCES public.con_plan_cuentas(account_id),
    estado              varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_inv_producto_company ON public.inv_producto (company_id);
CREATE INDEX IF NOT EXISTS ix_inv_producto_categoria ON public.inv_producto (categoria_id);

-- Tabla: inv_almacen (almacenes/bodegas)
CREATE TABLE IF NOT EXISTS public.inv_almacen
(
    almacen_id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    branch_id           bigint         REFERENCES public.cfg_branch(branch_id) ON DELETE SET NULL,
    code                varchar(30)    NOT NULL,
    nombre              varchar(150)   NOT NULL,
    direccion           varchar(300),
    responsable         varchar(150),
    estado              varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_inv_almacen_company ON public.inv_almacen (company_id);

-- Tabla: inv_existencia (saldos por producto y almacén)
CREATE TABLE IF NOT EXISTS public.inv_existencia
(
    existencia_id       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    producto_id         bigint         NOT NULL REFERENCES public.inv_producto(producto_id) ON DELETE CASCADE,
    almacen_id          bigint         NOT NULL REFERENCES public.inv_almacen(almacen_id) ON DELETE CASCADE,
    cantidad_disponible numeric(18,4)  NOT NULL DEFAULT 0,
    cantidad_reservada  numeric(18,4)  NOT NULL DEFAULT 0,
    costo_promedio      numeric(18,6)  NOT NULL DEFAULT 0,
    updated_at          timestamptz    NOT NULL DEFAULT now(),
    UNIQUE (producto_id, almacen_id)
);

CREATE INDEX IF NOT EXISTS ix_inv_existencia_producto ON public.inv_existencia (producto_id);
CREATE INDEX IF NOT EXISTS ix_inv_existencia_almacen ON public.inv_existencia (almacen_id);

-- Tabla: inv_movimiento (encabezado de movimientos)
CREATE TABLE IF NOT EXISTS public.inv_movimiento
(
    movimiento_id       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    document_type_id    bigint         REFERENCES public.cfg_document_type(document_type_id) ON DELETE SET NULL,
    document_series_id  bigint         REFERENCES public.cfg_document_series(series_id) ON DELETE SET NULL,
    numero_documento    varchar(50),
    tipo_movimiento     varchar(20)    NOT NULL, -- INGRESO, EGRESO, AJUSTE, TRASLADO
    fecha_movimiento    date           NOT NULL,
    almacen_origen_id   bigint         REFERENCES public.inv_almacen(almacen_id) ON DELETE SET NULL,
    almacen_destino_id  bigint         REFERENCES public.inv_almacen(almacen_id) ON DELETE SET NULL,
    referencia          varchar(200),
    origen_modulo       varchar(30),
    origen_documento_id bigint,
    con_poliza_id      bigint         REFERENCES public.con_poliza(poliza_id),
    estado              varchar(20)    NOT NULL DEFAULT 'DRAFT', -- DRAFT, POSTED, VOID
    monto_total         numeric(18,2)  NOT NULL DEFAULT 0,
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100)
);

CREATE INDEX IF NOT EXISTS ix_inv_movimiento_tipo ON public.inv_movimiento (tipo_movimiento);
CREATE INDEX IF NOT EXISTS ix_inv_movimiento_fecha ON public.inv_movimiento (fecha_movimiento);
CREATE INDEX IF NOT EXISTS ix_inv_movimiento_origen ON public.inv_movimiento (origen_modulo, origen_documento_id);

-- Tabla: inv_movimiento_linea (detalle)
CREATE TABLE IF NOT EXISTS public.inv_movimiento_linea
(
    movimiento_linea_id bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    movimiento_id       bigint         NOT NULL REFERENCES public.inv_movimiento(movimiento_id) ON DELETE CASCADE,
    line_number         smallint       NOT NULL,
    producto_id         bigint         NOT NULL REFERENCES public.inv_producto(producto_id),
    almacen_id          bigint         REFERENCES public.inv_almacen(almacen_id) ON DELETE SET NULL,
    almacen_destino_id  bigint         REFERENCES public.inv_almacen(almacen_id) ON DELETE SET NULL,
    cantidad            numeric(18,4)  NOT NULL,
    costo_unitario      numeric(18,6)  NOT NULL DEFAULT 0,
    costo_total         numeric(18,2)  NOT NULL DEFAULT 0,
    cost_center_id      bigint         REFERENCES public.con_centro_costo(cost_center_id),
    descripcion         varchar(300),
    UNIQUE (movimiento_id, line_number)
);

CREATE INDEX IF NOT EXISTS ix_inv_mov_linea_mov ON public.inv_movimiento_linea (movimiento_id);
CREATE INDEX IF NOT EXISTS ix_inv_mov_linea_producto ON public.inv_movimiento_linea (producto_id);

COMMIT;
