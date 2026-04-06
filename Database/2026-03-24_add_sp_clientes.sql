-- Prueba de reporteria web con Stored Procedure / funcion set-returning
-- Objetivo:
-- 1. Crear public.sp_clientes(p_company_id bigint)
-- 2. Registrar el dataset administrable "sp_clientes"
-- 3. Dejar listo el parametro CompanyId como CURRENT_COMPANY
--
-- Nota:
-- En PostgreSQL el preview actual de la reporteria espera una funcion
-- que retorne filas (RETURNS TABLE / SETOF), no un PROCEDURE sin resultset.

BEGIN;

DROP FUNCTION IF EXISTS public.sp_clientes(bigint);

CREATE OR REPLACE FUNCTION public.sp_clientes(
    p_company_id bigint
)
RETURNS TABLE
(
    maestro_cliente_id integer,
    company_id bigint,
    cliente_clave character varying(20),
    cliente_nombre text,
    cliente_identidad text,
    cliente_rtn text,
    telefono character varying(15),
    movil character varying(15),
    email character varying(100),
    direccion character varying(200),
    barrio_codigo character varying(7),
    ciclos_id integer,
    tipo_uso_codigo character varying(2),
    estado boolean,
    tiene_medidor boolean
)
LANGUAGE sql
STABLE
AS
$$
    SELECT
        cm.maestro_cliente_id,
        cm.company_id,
        cm.maestro_cliente_clave AS cliente_clave,
        cm.maestro_cliente_nombre AS cliente_nombre,
        cm.maestro_cliente_identidad AS cliente_identidad,
        cm.maestro_cliente_rtn AS cliente_rtn,
        cd.detalle_cliente_telefono AS telefono,
        cd.detalle_cliente_movil AS movil,
        cd.detalle_cliente_email AS email,
        cd.detalle_cliente_direccion AS direccion,
        cm.barrio_codigo,
        cm.ciclos_id,
        cm.tipo_uso_codigo,
        cm.estado,
        COALESCE(cm.maestro_cliente_tiene_medidor, false) AS tiene_medidor
    FROM public.cliente_maestro cm
    LEFT JOIN LATERAL
    (
        SELECT
            d.detalle_cliente_telefono,
            d.detalle_cliente_movil,
            d.detalle_cliente_email,
            d.detalle_cliente_direccion
        FROM public.cliente_detalle d
        WHERE d.company_id = cm.company_id
          AND d.maestro_cliente_id = cm.maestro_cliente_id
        ORDER BY d.detalle_cliente_id
        LIMIT 1
    ) cd ON true
    WHERE cm.company_id = p_company_id
    ORDER BY cm.maestro_cliente_clave;
$$;

COMMENT ON FUNCTION public.sp_clientes(bigint)
IS 'Retorna informacion basica de clientes filtrada por company_id para pruebas de reporteria web.';

INSERT INTO public.rep_catalogo_dataset
(
    company_id,
    codigo,
    nombre,
    descripcion,
    tipo_origen,
    origen_clave,
    sql_text,
    connection_name,
    is_active,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    c.company_id,
    'sp_clientes',
    'Clientes (Stored Procedure)',
    'Dataset de prueba basado en public.sp_clientes para validar reportería web con clientes.',
    'STORED_PROCEDURE',
    'public.sp_clientes',
    NULL,
    NULL,
    TRUE,
    NOW(),
    'reporteria-bootstrap',
    NOW(),
    'reporteria-bootstrap'
FROM public.cfg_company c
WHERE NOT EXISTS
(
    SELECT 1
    FROM public.rep_catalogo_dataset d
    WHERE d.company_id = c.company_id
      AND d.codigo = 'sp_clientes'
);

UPDATE public.rep_catalogo_dataset
SET
    nombre = 'Clientes (Stored Procedure)',
    descripcion = 'Dataset de prueba basado en public.sp_clientes para validar reportería web con clientes.',
    tipo_origen = 'STORED_PROCEDURE',
    origen_clave = 'public.sp_clientes',
    sql_text = NULL,
    connection_name = NULL,
    is_active = TRUE,
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap'
WHERE codigo = 'sp_clientes';

INSERT INTO public.rep_dataset_parametro
(
    company_id,
    dataset_id,
    nombre,
    nombre_origen,
    etiqueta,
    tipo_dato,
    fuente_valor,
    valor_default,
    visible,
    permite_nulo,
    requerido,
    orden,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    d.company_id,
    d.dataset_id,
    'CompanyId',
    'p_company_id',
    'Empresa actual',
    'INT64',
    'CURRENT_COMPANY',
    NULL,
    FALSE,
    FALSE,
    TRUE,
    10,
    NOW(),
    'reporteria-bootstrap',
    NOW(),
    'reporteria-bootstrap'
FROM public.rep_catalogo_dataset d
WHERE d.codigo = 'sp_clientes'
  AND NOT EXISTS
  (
      SELECT 1
      FROM public.rep_dataset_parametro p
      WHERE p.company_id = d.company_id
        AND p.dataset_id = d.dataset_id
        AND p.nombre = 'CompanyId'
  );

UPDATE public.rep_dataset_parametro p
SET
    nombre_origen = 'p_company_id',
    etiqueta = 'Empresa actual',
    tipo_dato = 'INT64',
    fuente_valor = 'CURRENT_COMPANY',
    valor_default = NULL,
    visible = FALSE,
    permite_nulo = FALSE,
    requerido = TRUE,
    orden = 10,
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap'
FROM public.rep_catalogo_dataset d
WHERE d.company_id = p.company_id
  AND d.dataset_id = p.dataset_id
  AND d.codigo = 'sp_clientes'
  AND p.nombre = 'CompanyId';

COMMIT;

-- Verificacion rapida
SELECT
    d.company_id,
    d.codigo,
    d.nombre,
    d.tipo_origen,
    d.origen_clave,
    p.nombre AS parametro,
    p.fuente_valor,
    p.tipo_dato
FROM public.rep_catalogo_dataset d
LEFT JOIN public.rep_dataset_parametro p
  ON p.company_id = d.company_id
 AND p.dataset_id = d.dataset_id
WHERE d.codigo = 'sp_clientes'
ORDER BY d.company_id, p.orden, p.nombre;


-- Preview rapido usando la primera empresa registrada
SELECT *
FROM public.sp_clientes(
    COALESCE(
        (
            SELECT company_id
            FROM public.cfg_company
            ORDER BY company_id
            LIMIT 1
        ),
        0
    )
)
LIMIT 50;

ROLLBACK

SELECT * FROM public.rep_catalogo_dataset --dataset registrado
SELECT * FROM public.rep_catalogo_informe -- informe registrado (si se ha creado alguno usando el dataset)
SELECT * FROM public.rep_reporte_layout -- layouts registrados (si se ha creado alguno usando el dataset)
SELECT * FROM public.rep_dataset_parametro -- parametros registrados para el dataset 


SELECT * FROM public.cliente_maestro limit 57890 order by maestro_cliente_id desc

delete from  public.rep_dataset_parametro

