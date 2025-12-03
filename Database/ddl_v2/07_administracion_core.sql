-- ================================================
-- 07_administracion_core.sql
-- Tablas normalizadas para Administración: seguridad, maestros comunes y reportería corporativa
-- Requiere: 01_configuracion_base.sql
-- ================================================

BEGIN;

-- ================================================
-- Submódulo: Seguridad y control de acceso (ASP.NET Identity)
-- Las tablas de usuarios/roles se generan mediante las migraciones del proyecto apc (schema identity.*).
-- No se definen estructuras adicionales aquí para evitar duplicidad de catálogos.

-- ================================================
-- Submódulo: Maestros operativos (clientes, proveedores, rutas, servicios)
-- ================================================

CREATE TABLE IF NOT EXISTS public.adm_ruta
(
    ruta_id           bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code              varchar(30)    NOT NULL,
    nombre            varchar(150)   NOT NULL,
    descripcion       varchar(300),
    tipo              varchar(20)    NOT NULL DEFAULT 'DISTRIBUCION', -- DISTRIBUCION, COBRO, SERVICIO
    supervisor        varchar(150),
    estado            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_ruta_company ON public.adm_ruta (company_id);
CREATE INDEX IF NOT EXISTS ix_adm_ruta_estado ON public.adm_ruta (estado);

CREATE TABLE IF NOT EXISTS public.adm_servicio
(
    servicio_id       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code              varchar(30)    NOT NULL,
    nombre            varchar(200)   NOT NULL,
    descripcion       varchar(500),
    servicio_tipo     varchar(20)    NOT NULL DEFAULT 'SERVICIO', -- SERVICIO, MANTENIMIENTO, CONSULTORIA
    unidad_medida     varchar(20)    NOT NULL DEFAULT 'UNIDAD',
    tax_id            bigint         REFERENCES public.cfg_tax(tax_id),
    precio_base       numeric(18,4)  NOT NULL DEFAULT 0,
    requiere_programacion boolean    NOT NULL DEFAULT false,
    estado            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_servicio_company ON public.adm_servicio (company_id);
CREATE INDEX IF NOT EXISTS ix_adm_servicio_tax ON public.adm_servicio (tax_id);

CREATE TABLE IF NOT EXISTS public.adm_lista_precio
(
    lista_precio_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code              varchar(30)    NOT NULL,
    nombre            varchar(150)   NOT NULL,
    descripcion       varchar(300),
    aplica_a          varchar(20)    NOT NULL DEFAULT 'PRODUCTO', -- PRODUCTO, SERVICIO, MIXTO
    currency_code     char(3)        NOT NULL REFERENCES public.cfg_currency(currency_code),
    vigencia_desde    date,
    vigencia_hasta    date,
    es_default        boolean        NOT NULL DEFAULT false,
    estado            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_lista_precio_company ON public.adm_lista_precio (company_id);
CREATE INDEX IF NOT EXISTS ix_adm_lista_precio_estado ON public.adm_lista_precio (estado);

CREATE TABLE IF NOT EXISTS public.adm_lista_precio_detalle
(
    lista_precio_detalle_id bigint   GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    lista_precio_id   bigint         NOT NULL REFERENCES public.adm_lista_precio(lista_precio_id) ON DELETE CASCADE,
    item_tipo         varchar(20)    NOT NULL, -- PRODUCTO o SERVICIO
    item_codigo       varchar(50)    NOT NULL,
    descripcion       varchar(300),
    precio_unitario   numeric(18,4)  NOT NULL,
    descuento_maximo  numeric(5,2)   NOT NULL DEFAULT 0,
    impuesto_id       bigint         REFERENCES public.cfg_tax(tax_id),
    estado            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (lista_precio_id, item_tipo, item_codigo),
    CHECK (item_tipo IN ('PRODUCTO', 'SERVICIO'))
);

CREATE INDEX IF NOT EXISTS ix_adm_lista_precio_detalle_lista ON public.adm_lista_precio_detalle (lista_precio_id);
CREATE INDEX IF NOT EXISTS ix_adm_lista_precio_detalle_item ON public.adm_lista_precio_detalle (item_tipo, item_codigo);

CREATE TABLE IF NOT EXISTS public.adm_cliente
(
    cliente_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code              varchar(30)    NOT NULL,
    nombre            varchar(200)   NOT NULL,
    nombre_comercial  varchar(200),
    tax_id            varchar(30),
    customer_type     varchar(30)    NOT NULL DEFAULT 'LOCAL', -- LOCAL, EXENTO, GOBIERNO, EXTRANJERO
    tax_condition     varchar(30)    NOT NULL DEFAULT 'GRAVADO', -- GRAVADO, EXENTO, EXONERADO
    ruta_id           bigint         REFERENCES public.adm_ruta(ruta_id) ON DELETE SET NULL,
    lista_precio_id   bigint         REFERENCES public.adm_lista_precio(lista_precio_id) ON DELETE SET NULL,
    email             varchar(150),
    phone             varchar(40),
    direccion         varchar(500),
    ciudad            varchar(120),
    departamento      varchar(120),
    contacto_principal varchar(150),
    estado            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    credit_limit      numeric(18,2)  NOT NULL DEFAULT 0,
    credit_days       smallint       NOT NULL DEFAULT 0,
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code),
    UNIQUE (company_id, tax_id)
);

CREATE INDEX IF NOT EXISTS ix_adm_cliente_company ON public.adm_cliente (company_id);
CREATE INDEX IF NOT EXISTS ix_adm_cliente_ruta ON public.adm_cliente (ruta_id);
CREATE INDEX IF NOT EXISTS ix_adm_cliente_lista ON public.adm_cliente (lista_precio_id);

CREATE TABLE IF NOT EXISTS public.adm_cliente_contacto
(
    contacto_id       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cliente_id        bigint         NOT NULL REFERENCES public.adm_cliente(cliente_id) ON DELETE CASCADE,
    nombre            varchar(150)   NOT NULL,
    cargo             varchar(100),
    email             varchar(150),
    phone             varchar(40),
    es_principal      boolean        NOT NULL DEFAULT false,
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (cliente_id, email)
);

CREATE INDEX IF NOT EXISTS ix_adm_cliente_contacto_cliente ON public.adm_cliente_contacto (cliente_id);

CREATE TABLE IF NOT EXISTS public.adm_proveedor
(
    proveedor_id      bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code              varchar(30)    NOT NULL,
    nombre            varchar(200)   NOT NULL,
    nombre_comercial  varchar(200),
    tax_id            varchar(30),
    supplier_type     varchar(30)    NOT NULL DEFAULT 'LOCAL', -- LOCAL, EXTRANJERO
    tax_condition     varchar(30)    NOT NULL DEFAULT 'GRAVADO',
    ruta_id           bigint         REFERENCES public.adm_ruta(ruta_id) ON DELETE SET NULL,
    requiere_retencion_isv boolean   NOT NULL DEFAULT false,
    requiere_retencion_isr boolean   NOT NULL DEFAULT false,
    email             varchar(150),
    phone             varchar(40),
    direccion         varchar(500),
    ciudad            varchar(120),
    departamento      varchar(120),
    estado            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    credit_days       smallint       NOT NULL DEFAULT 0,
    credit_limit      numeric(18,2)  NOT NULL DEFAULT 0,
    metodo_pago_preferido varchar(30),
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code),
    UNIQUE (company_id, tax_id)
);

CREATE INDEX IF NOT EXISTS ix_adm_proveedor_company ON public.adm_proveedor (company_id);
CREATE INDEX IF NOT EXISTS ix_adm_proveedor_ruta ON public.adm_proveedor (ruta_id);

CREATE TABLE IF NOT EXISTS public.adm_proveedor_contacto
(
    contacto_id       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    proveedor_id      bigint         NOT NULL REFERENCES public.adm_proveedor(proveedor_id) ON DELETE CASCADE,
    nombre            varchar(150)   NOT NULL,
    cargo             varchar(100),
    email             varchar(150),
    phone             varchar(40),
    es_principal      boolean        NOT NULL DEFAULT false,
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (proveedor_id, email)
);

CREATE INDEX IF NOT EXISTS ix_adm_proveedor_contacto_proveedor ON public.adm_proveedor_contacto (proveedor_id);

-- ================================================
-- Submódulo: Repositorio corporativo de reportes
-- ================================================

CREATE TABLE IF NOT EXISTS public.adm_reporte_categoria
(
    reporte_categoria_id bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    module            varchar(30)    NOT NULL,
    code              varchar(30)    NOT NULL,
    nombre            varchar(150)   NOT NULL,
    descripcion       varchar(300),
    es_global         boolean        NOT NULL DEFAULT false,
    estado            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, module, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_reporte_categoria_company ON public.adm_reporte_categoria (company_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_categoria_module ON public.adm_reporte_categoria (module);

CREATE TABLE IF NOT EXISTS public.adm_reporte_definicion
(
    reporte_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    reporte_categoria_id bigint      REFERENCES public.adm_reporte_categoria(reporte_categoria_id) ON DELETE SET NULL,
    module            varchar(30)    NOT NULL,
    code              varchar(50)    NOT NULL,
    nombre            varchar(200)   NOT NULL,
    descripcion       varchar(500),
    engine            varchar(30)    NOT NULL DEFAULT 'SQL', -- SQL, EXTERNAL, DASHBOARD
    query_template    text           NOT NULL,
    version           integer        NOT NULL DEFAULT 1,
    es_activo         boolean        NOT NULL DEFAULT true,
    es_publico        boolean        NOT NULL DEFAULT false,
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_adm_reporte_definicion_company ON public.adm_reporte_definicion (company_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_definicion_module ON public.adm_reporte_definicion (module);

CREATE TABLE IF NOT EXISTS public.adm_reporte_parametro
(
    parametro_id      bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    reporte_id        bigint         NOT NULL REFERENCES public.adm_reporte_definicion(reporte_id) ON DELETE CASCADE,
    nombre            varchar(50)    NOT NULL,
    data_type         varchar(30)    NOT NULL,
    obligatorio       boolean        NOT NULL DEFAULT false,
    valor_default     varchar(200),
    posicion          smallint       NOT NULL,
    descripcion       varchar(300),
    UNIQUE (reporte_id, nombre)
);

CREATE INDEX IF NOT EXISTS ix_adm_reporte_parametro_reporte ON public.adm_reporte_parametro (reporte_id);

CREATE TABLE IF NOT EXISTS public.adm_reporte_programacion
(
    programacion_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    reporte_id        bigint         NOT NULL REFERENCES public.adm_reporte_definicion(reporte_id) ON DELETE CASCADE,
    frecuencia        varchar(20)    NOT NULL, -- ON_DEMAND, DAILY, WEEKLY, MONTHLY
    hora_ejecucion    time           NOT NULL DEFAULT '08:00',
    dia_semana        smallint,
    dia_mes           smallint,
    destinatarios     text,
    ultimo_envio      timestamptz,
    proximo_envio     timestamptz,
    estado            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    CHECK (frecuencia IN ('ON_DEMAND', 'DAILY', 'WEEKLY', 'MONTHLY'))
);

CREATE INDEX IF NOT EXISTS ix_adm_reporte_programacion_reporte ON public.adm_reporte_programacion (reporte_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_programacion_estado ON public.adm_reporte_programacion (estado);

CREATE TABLE IF NOT EXISTS public.adm_reporte_ejecucion
(
    ejecucion_id      bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    reporte_id        bigint         NOT NULL REFERENCES public.adm_reporte_definicion(reporte_id) ON DELETE CASCADE,
    programacion_id   bigint         REFERENCES public.adm_reporte_programacion(programacion_id) ON DELETE SET NULL,
    ejecutado_por     varchar(100),
    fecha_ejecucion   timestamptz    NOT NULL DEFAULT now(),
    parametros        jsonb          NOT NULL DEFAULT '{}'::jsonb,
    estado            varchar(20)    NOT NULL DEFAULT 'SUCCESS', -- SUCCESS, ERROR, PENDING
    mensaje           varchar(500)
);

CREATE INDEX IF NOT EXISTS ix_adm_reporte_ejecucion_reporte ON public.adm_reporte_ejecucion (reporte_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_ejecucion_estado ON public.adm_reporte_ejecucion (estado);

COMMIT;
