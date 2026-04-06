CREATE TABLE IF NOT EXISTS public.rep_catalogo_dataset (
    dataset_id BIGSERIAL PRIMARY KEY,
    company_id BIGINT NOT NULL,
    codigo VARCHAR(80) NOT NULL,
    nombre VARCHAR(150) NOT NULL,
    descripcion VARCHAR(500) NULL,
    tipo_origen VARCHAR(30) NOT NULL,
    origen_clave VARCHAR(160) NULL,
    sql_text TEXT NULL,
    connection_name VARCHAR(100) NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    metadata_json TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMPTZ NULL,
    updated_by VARCHAR(100) NULL,
    CONSTRAINT fk_rep_catalogo_dataset_company
        FOREIGN KEY (company_id)
        REFERENCES public.cfg_company(company_id)
        ON DELETE CASCADE,
    CONSTRAINT ck_rep_catalogo_dataset_tipo_origen
        CHECK (tipo_origen IN ('STORED_PROCEDURE', 'VIEW', 'SQL'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_rep_catalogo_dataset_company_id_codigo
    ON public.rep_catalogo_dataset(company_id, codigo);

CREATE TABLE IF NOT EXISTS public.rep_dataset_parametro (
    dataset_parametro_id BIGSERIAL PRIMARY KEY,
    company_id BIGINT NOT NULL,
    dataset_id BIGINT NOT NULL,
    nombre VARCHAR(80) NOT NULL,
    nombre_origen VARCHAR(80) NULL,
    etiqueta VARCHAR(150) NOT NULL,
    tipo_dato VARCHAR(30) NOT NULL,
    fuente_valor VARCHAR(30) NOT NULL,
    valor_default VARCHAR(200) NULL,
    visible BOOLEAN NOT NULL DEFAULT TRUE,
    permite_nulo BOOLEAN NOT NULL DEFAULT FALSE,
    requerido BOOLEAN NOT NULL DEFAULT FALSE,
    orden INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMPTZ NULL,
    updated_by VARCHAR(100) NULL,
    CONSTRAINT fk_rep_dataset_parametro_company
        FOREIGN KEY (company_id)
        REFERENCES public.cfg_company(company_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_rep_dataset_parametro_dataset
        FOREIGN KEY (dataset_id)
        REFERENCES public.rep_catalogo_dataset(dataset_id)
        ON DELETE CASCADE,
    CONSTRAINT ck_rep_dataset_parametro_tipo_dato
        CHECK (tipo_dato IN ('TEXT', 'INT64', 'DECIMAL', 'DATE', 'DATETIME', 'BOOLEAN')),
    CONSTRAINT ck_rep_dataset_parametro_fuente_valor
        CHECK (fuente_valor IN ('REPORT', 'CURRENT_COMPANY', 'FIXED'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_rep_dataset_parametro_company_id_dataset_id_nombre
    ON public.rep_dataset_parametro(company_id, dataset_id, nombre);

CREATE UNIQUE INDEX IF NOT EXISTS ix_rep_dataset_parametro_company_id_dataset_id_orden
    ON public.rep_dataset_parametro(company_id, dataset_id, orden);

INSERT INTO public.rep_catalogo_dataset (
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
    created_by
)
SELECT
    c.company_id,
    'bancos-transacciones',
    'Dataset transacciones bancarias',
    'Fuente administrable para el reporte web de transacciones bancarias.',
    'STORED_PROCEDURE',
    'public.rep_bancos_transacciones',
    NULL,
    'DefaultConnection',
    TRUE,
    NOW(),
    'reporteria-bootstrap'
FROM public.cfg_company c
WHERE NOT EXISTS (
    SELECT 1
    FROM public.rep_catalogo_dataset d
    WHERE d.company_id = c.company_id
      AND d.codigo = 'bancos-transacciones'
);

INSERT INTO public.rep_dataset_parametro (
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
    created_by
)
SELECT
    d.company_id,
    d.dataset_id,
    seed.nombre,
    seed.nombre_origen,
    seed.etiqueta,
    seed.tipo_dato,
    seed.fuente_valor,
    seed.valor_default,
    seed.visible,
    seed.permite_nulo,
    seed.requerido,
    seed.orden,
    NOW(),
    'reporteria-bootstrap'
FROM public.rep_catalogo_dataset d
CROSS JOIN (
    VALUES
        ('CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, FALSE, FALSE, TRUE, 0),
        ('BancoCuentaId', 'p_banco_cuenta_id', 'Cuenta bancaria', 'INT64', 'REPORT', NULL, FALSE, TRUE, FALSE, 10),
        ('FechaDesde', 'p_fecha_desde', 'Fecha desde', 'DATE', 'REPORT', NULL, TRUE, TRUE, FALSE, 20),
        ('FechaHasta', 'p_fecha_hasta', 'Fecha hasta', 'DATE', 'REPORT', NULL, TRUE, TRUE, FALSE, 30),
        ('IncluirAnuladas', 'p_incluir_anuladas', 'Incluir anuladas', 'BOOLEAN', 'REPORT', 'false', TRUE, FALSE, FALSE, 40)
) AS seed(nombre, nombre_origen, etiqueta, tipo_dato, fuente_valor, valor_default, visible, permite_nulo, requerido, orden)
WHERE d.codigo = 'bancos-transacciones'
  AND NOT EXISTS (
      SELECT 1
      FROM public.rep_dataset_parametro p
      WHERE p.company_id = d.company_id
        AND p.dataset_id = d.dataset_id
        AND p.nombre = seed.nombre
  );

UPDATE public.rep_catalogo_informe
SET consulta_clave = 'bancos-transacciones',
    updated_at = NOW(),
    updated_by = COALESCE(updated_by, 'reporteria-bootstrap')
WHERE codigo = 'bancos-transacciones'
  AND (consulta_clave IS NULL OR consulta_clave = '');
