-- 12_contabilidad_configuracion_empresa.sql
-- Define la tabla de perfil/configuración de la empresa para el módulo de contabilidad.
-- Requiere: 01_configuracion_base.sql (cfg_company).

BEGIN;

CREATE TABLE IF NOT EXISTS public.con_empresa_configuracion
(
    company_id          bigint       NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    tipo_empresa        varchar(60),
    id_fiscal_siglas    varchar(10),
    id_fiscal_valor     varchar(40),
    tamano              varchar(30),
    capital             varchar(30),
    fecha_constitucion  date,
    contacto            varchar(120),
    direccion           varchar(500),
    telefonos           varchar(120),
    ciudad              varchar(120),
    pais                varchar(120),
    email               varchar(160),
    pagina_web          varchar(200),
    created_at          timestamptz  NOT NULL DEFAULT now(),
    created_by          varchar(100) NOT NULL DEFAULT current_user,
    updated_at          timestamptz,
    updated_by          varchar(100),
    CONSTRAINT pk_con_empresa_configuracion PRIMARY KEY (company_id)
);

COMMIT;
