-- ================================================
-- 10_administracion_maestros.sql
-- Catálogos maestros centralizados (clientes, proveedores, rutas, servicios, reportes)
-- Requiere: 01_configuracion_base.sql
-- ================================================

BEGIN;

-- Tabla: adm_zona (zonas comerciales/logísticas)
CREATE TABLE IF NOT EXISTS public.adm_zona
(
    zona_id            bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(300),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_zona_company ON public.adm_zona (company_id);

-- Tabla: adm_ruta (catálogo de rutas)
CREATE TABLE IF NOT EXISTS public.adm_ruta
(
    ruta_id            bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    zona_id            bigint,
    branch_id          bigint         REFERENCES public.cfg_branch(branch_id) ON DELETE SET NULL,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(300),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_ruta_company ON public.adm_ruta (company_id);

ALTER TABLE public.adm_ruta
    ADD COLUMN IF NOT EXISTS zona_id bigint;

ALTER TABLE public.adm_ruta
    ADD COLUMN IF NOT EXISTS branch_id bigint;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_name = 'adm_ruta_zona_fk'
          AND table_name = 'adm_ruta'
          AND table_schema = 'public'
    ) THEN
        ALTER TABLE public.adm_ruta
            ADD CONSTRAINT adm_ruta_zona_fk
            FOREIGN KEY (zona_id) REFERENCES public.adm_zona(zona_id) ON DELETE SET NULL;
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_adm_ruta_zona ON public.adm_ruta (zona_id);
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_name = 'adm_ruta_branch_fk'
          AND table_name = 'adm_ruta'
          AND table_schema = 'public'
    ) THEN
        ALTER TABLE public.adm_ruta
            ADD CONSTRAINT adm_ruta_branch_fk
            FOREIGN KEY (branch_id) REFERENCES public.cfg_branch(branch_id) ON DELETE SET NULL;
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_adm_ruta_branch ON public.adm_ruta (branch_id);

-- Tabla: adm_cliente
CREATE TABLE IF NOT EXISTS public.adm_cliente
(
    cliente_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    ruta_id            bigint         REFERENCES public.adm_ruta(ruta_id) ON DELETE SET NULL,
    code               varchar(30)    NOT NULL,
    nombre             varchar(200)   NOT NULL,
    nombre_comercial   varchar(200),
    tax_id             varchar(30),
    customer_type      varchar(30)    NOT NULL DEFAULT 'LOCAL',
    tax_condition      varchar(30)    NOT NULL DEFAULT 'GRAVADO',
    email              varchar(150),
    phone              varchar(40),
    direccion          varchar(500),
    ciudad             varchar(120),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    credit_limit       numeric(18,2)  NOT NULL DEFAULT 0,
    credit_days        smallint       NOT NULL DEFAULT 0,
    balance_inicial    numeric(18,2)  NOT NULL DEFAULT 0,
    fecha_ingreso      date           NOT NULL DEFAULT current_date,
    comentario         varchar(300),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code),
    UNIQUE (company_id, tax_id)
);

CREATE INDEX IF NOT EXISTS ix_adm_cliente_company ON public.adm_cliente (company_id);
CREATE INDEX IF NOT EXISTS ix_adm_cliente_ruta ON public.adm_cliente (ruta_id);

ALTER TABLE public.adm_cliente
    ADD COLUMN IF NOT EXISTS balance_inicial numeric(18,2) NOT NULL DEFAULT 0;

ALTER TABLE public.adm_cliente
    ADD COLUMN IF NOT EXISTS comentario varchar(300);

-- Tabla: adm_proveedor
CREATE TABLE IF NOT EXISTS public.adm_proveedor
(
    proveedor_id       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(200)   NOT NULL,
    nombre_comercial   varchar(200),
    tax_id             varchar(30),
    supplier_type      varchar(30)    NOT NULL DEFAULT 'LOCAL',
    tax_condition      varchar(30)    NOT NULL DEFAULT 'GRAVADO',
    requiere_ret_isv   boolean        NOT NULL DEFAULT false,
    requiere_ret_isr   boolean        NOT NULL DEFAULT false,
    email              varchar(150),
    phone              varchar(40),
    direccion          varchar(500),
    ciudad             varchar(120),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    credit_days        smallint       NOT NULL DEFAULT 0,
    credit_limit       numeric(18,2)  NOT NULL DEFAULT 0,
    balance_inicial    numeric(18,2)  NOT NULL DEFAULT 0,
    comentario         varchar(300),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code),
    UNIQUE (company_id, tax_id)
);

CREATE INDEX IF NOT EXISTS ix_adm_proveedor_company ON public.adm_proveedor (company_id);

ALTER TABLE public.adm_proveedor
    ADD COLUMN IF NOT EXISTS requiere_ret_isv boolean NOT NULL DEFAULT false;

ALTER TABLE public.adm_proveedor
    ADD COLUMN IF NOT EXISTS requiere_ret_isr boolean NOT NULL DEFAULT false;

ALTER TABLE public.adm_proveedor
    ADD COLUMN IF NOT EXISTS comentario varchar(300);

-- Tabla: adm_servicio_categoria
CREATE TABLE IF NOT EXISTS public.adm_servicio_categoria
(
    servicio_categoria_id bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code                varchar(30)    NOT NULL,
    nombre              varchar(150)   NOT NULL,
    descripcion         varchar(300),
    estado              varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_servicio_categoria_company ON public.adm_servicio_categoria (company_id);

-- Tabla: adm_servicio (catálogo de servicios/no inventariables)
CREATE TABLE IF NOT EXISTS public.adm_servicio
(
    servicio_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    servicio_categoria_id bigint,
    code               varchar(50)    NOT NULL,
    nombre             varchar(200)   NOT NULL,
    descripcion        varchar(500),
    unidad_medida      varchar(20)    NOT NULL DEFAULT 'SERV',
    precio_unitario    numeric(18,4)  NOT NULL DEFAULT 0,
    impuesto_id        bigint         REFERENCES public.cfg_tax(tax_id),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_servicio_company ON public.adm_servicio (company_id);

ALTER TABLE public.adm_servicio
    ADD COLUMN IF NOT EXISTS servicio_categoria_id bigint;

ALTER TABLE public.adm_servicio
    ADD COLUMN IF NOT EXISTS precio_unitario numeric(18,4) NOT NULL DEFAULT 0;

ALTER TABLE public.adm_servicio
    ADD COLUMN IF NOT EXISTS impuesto_id bigint REFERENCES public.cfg_tax(tax_id);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_name = 'adm_servicio_categoria_fk'
          AND table_name = 'adm_servicio'
          AND table_schema = 'public'
    ) THEN
        ALTER TABLE public.adm_servicio
            ADD CONSTRAINT adm_servicio_categoria_fk
            FOREIGN KEY (servicio_categoria_id) REFERENCES public.adm_servicio_categoria(servicio_categoria_id) ON DELETE SET NULL;
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_adm_servicio_categoria ON public.adm_servicio (servicio_categoria_id);

-- Tabla: adm_vendedor / cuadrilla
CREATE TABLE IF NOT EXISTS public.adm_vendedor
(
    vendedor_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    ruta_id            bigint         REFERENCES public.adm_ruta(ruta_id) ON DELETE SET NULL,
    code               varchar(30)    NOT NULL,
    nombre             varchar(200)   NOT NULL,
    tipo               varchar(30)    NOT NULL DEFAULT 'EJECUTIVO', -- EJECUTIVO, CUADRILLA, ABOGADO
    telefono           varchar(40),
    email              varchar(150),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_vendedor_company ON public.adm_vendedor (company_id);

-- Tabla: adm_transporte (transportistas / vehículos)
CREATE TABLE IF NOT EXISTS public.adm_transporte
(
    transporte_id      bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(200)   NOT NULL,
    placa              varchar(20),
    tipo               varchar(30)    NOT NULL DEFAULT 'TERRESTRE',
    capacidad          numeric(18,2),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

-- Tabla: adm_producto_categoria
CREATE TABLE IF NOT EXISTS public.adm_producto_categoria
(
    producto_categoria_id bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code                varchar(30)    NOT NULL,
    nombre              varchar(150)   NOT NULL,
    descripcion         varchar(300),
    estado              varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at          timestamptz    NOT NULL DEFAULT now(),
    created_by          varchar(100)   NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_producto_categoria_company ON public.adm_producto_categoria (company_id);

-- Tabla: adm_producto (maestro general)
CREATE TABLE IF NOT EXISTS public.adm_producto
(
    producto_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    categoria_id       bigint         REFERENCES public.adm_producto_categoria(producto_categoria_id) ON DELETE SET NULL,
    code               varchar(50)    NOT NULL,
    nombre             varchar(200)   NOT NULL,
    descripcion        varchar(500),
    unidad_medida      varchar(20)    NOT NULL DEFAULT 'UNIDAD',
    tipo               varchar(20)    NOT NULL DEFAULT 'BIEN', -- BIEN, SERVICIO, INSUMO
    precio_base        numeric(18,4)  NOT NULL DEFAULT 0,
    impuesto_id        bigint         REFERENCES public.cfg_tax(tax_id),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_producto_company ON public.adm_producto (company_id);

-- Tabla: adm_deposito (equivalente maestro de bodegas)
CREATE TABLE IF NOT EXISTS public.adm_deposito
(
    deposito_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    branch_id          bigint         REFERENCES public.cfg_branch(branch_id) ON DELETE SET NULL,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    direccion          varchar(300),
    responsable        varchar(150),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_deposito_company ON public.adm_deposito (company_id);

-- Tabla: adm_instrumento_pago
CREATE TABLE IF NOT EXISTS public.adm_instrumento_pago
(
    instrumento_id     bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(300),
    requiere_referencia boolean      NOT NULL DEFAULT false,
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

-- Tabla: adm_operacion (tipos de operación heredados)
CREATE TABLE IF NOT EXISTS public.adm_operacion
(
    operacion_id       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(300),
    modulo_origen      varchar(30)    NOT NULL,
    requiere_autorizacion boolean     NOT NULL DEFAULT false,
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

-- Tabla: adm_oferta
CREATE TABLE IF NOT EXISTS public.adm_oferta
(
    oferta_id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(500),
    fecha_inicio       date           NOT NULL,
    fecha_fin          date           NOT NULL,
    descuento_pct      numeric(9,4)   NOT NULL DEFAULT 0,
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

-- Tabla: adm_retencion (catálogo de retenciones especiales)
CREATE TABLE IF NOT EXISTS public.adm_retencion
(
    retencion_id       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(300),
    tipo               varchar(30)    NOT NULL, -- ISV, ISR, MUNICIPAL
    porcentaje         numeric(9,4)   NOT NULL,
    aplica_a           varchar(30)    NOT NULL, -- VENTAS, COMPRAS
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

-- Tabla: adm_convenio (convenios/pagos programados)
CREATE TABLE IF NOT EXISTS public.adm_convenio
(
    convenio_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    cliente_id         bigint         REFERENCES public.adm_cliente(cliente_id) ON DELETE CASCADE,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(300),
    monto_total        numeric(18,2)  NOT NULL,
    cuotas             smallint       NOT NULL DEFAULT 1,
    tasa_interes       numeric(9,4)   NOT NULL DEFAULT 0,
    fecha_inicio       date           NOT NULL,
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100)
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'adm_convenio_company_nombre_key'
           AND conrelid = 'public.adm_convenio'::regclass
    ) THEN
        ALTER TABLE public.adm_convenio
            ADD CONSTRAINT adm_convenio_company_nombre_key
            UNIQUE (company_id, nombre);
    END IF;
END
$$;

-- Tabla: adm_factura_lote (parámetros para facturación masiva)
CREATE TABLE IF NOT EXISTS public.adm_factura_lote
(
    factura_lote_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(300),
    criterio_json      jsonb          NOT NULL,
    estado             varchar(20)    NOT NULL DEFAULT 'DRAFT',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, nombre)
);

CREATE INDEX IF NOT EXISTS ix_adm_factura_lote_company ON public.adm_factura_lote (company_id);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'adm_factura_lote_company_nombre_key'
           AND conrelid = 'public.adm_factura_lote'::regclass
    ) THEN
        ALTER TABLE public.adm_factura_lote
            ADD CONSTRAINT adm_factura_lote_company_nombre_key
            UNIQUE (company_id, nombre);
    END IF;
END
$$;


-- Tabla: adm_lista_precio (encabezado)
CREATE TABLE IF NOT EXISTS public.adm_lista_precio
(
    lista_precio_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    currency_code      char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    es_default         boolean        NOT NULL DEFAULT false,
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_lista_precio_company ON public.adm_lista_precio (company_id);

-- Tabla: adm_lista_precio_detalle
CREATE TABLE IF NOT EXISTS public.adm_lista_precio_detalle
(
    lista_precio_detalle_id bigint     GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    lista_precio_id    bigint         NOT NULL REFERENCES public.adm_lista_precio(lista_precio_id) ON DELETE CASCADE,
    item_tipo          varchar(20)    NOT NULL,
    item_codigo        varchar(50)    NOT NULL,
    descripcion        varchar(300),
    precio_unitario    numeric(18,4)  NOT NULL,
    descuento_maximo   numeric(5,2)   NOT NULL DEFAULT 0,
    impuesto_id        bigint         REFERENCES public.cfg_tax(tax_id),
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    producto_id        bigint,
    servicio_id        bigint,
    fecha_inicio       date           NOT NULL DEFAULT current_date,
    fecha_fin          date,
    UNIQUE (lista_precio_id, item_tipo, item_codigo),
    CHECK (item_tipo IN ('PRODUCTO', 'SERVICIO'))
);

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS item_tipo varchar(20);

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS item_codigo varchar(50);

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS descripcion varchar(300);

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS descuento_maximo numeric(5,2) NOT NULL DEFAULT 0;

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS impuesto_id bigint REFERENCES public.cfg_tax(tax_id);

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS estado varchar(20) NOT NULL DEFAULT 'ACTIVE';

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS created_at timestamptz NOT NULL DEFAULT now();

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS created_by varchar(100) NOT NULL DEFAULT current_user;

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS updated_at timestamptz;

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS updated_by varchar(100);

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS producto_id bigint;

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS servicio_id bigint;

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS fecha_inicio date NOT NULL DEFAULT current_date;

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS fecha_fin date;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'adm_lista_precio_detalle_item_tipo_check'
           AND conrelid = 'public.adm_lista_precio_detalle'::regclass
    ) THEN
        ALTER TABLE public.adm_lista_precio_detalle
            ADD CONSTRAINT adm_lista_precio_detalle_item_tipo_check
            CHECK (item_tipo IN ('PRODUCTO','SERVICIO'));
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'adm_lista_precio_detalle_producto_fk'
           AND conrelid = 'public.adm_lista_precio_detalle'::regclass
    ) THEN
        ALTER TABLE public.adm_lista_precio_detalle
            ADD CONSTRAINT adm_lista_precio_detalle_producto_fk
            FOREIGN KEY (producto_id) REFERENCES public.adm_producto(producto_id) ON DELETE CASCADE;
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'adm_lista_precio_detalle_servicio_fk'
           AND conrelid = 'public.adm_lista_precio_detalle'::regclass
    ) THEN
        ALTER TABLE public.adm_lista_precio_detalle
            ADD CONSTRAINT adm_lista_precio_detalle_servicio_fk
            FOREIGN KEY (servicio_id) REFERENCES public.adm_servicio(servicio_id) ON DELETE CASCADE;
    END IF;
END
$$;

-- Tabla: adm_reporte_config (configuración de reportes por módulo)
CREATE TABLE IF NOT EXISTS public.adm_reporte_config
(
    reporte_id         bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    modulo             varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    descripcion        varchar(300),
    parametros_json    jsonb,
    destino            varchar(30)    NOT NULL DEFAULT 'INTERNAL', -- INTERNAL, EXTERNAL, FISCAL
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, modulo, nombre)
);

COMMIT;
