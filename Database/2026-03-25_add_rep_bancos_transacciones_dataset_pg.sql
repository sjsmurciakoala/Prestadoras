CREATE OR REPLACE FUNCTION public.rep_bancos_transacciones(
    p_company_id bigint,
    p_banco_cuenta_id bigint DEFAULT NULL,
    p_fecha_desde date DEFAULT NULL,
    p_fecha_hasta date DEFAULT NULL,
    p_incluir_anuladas boolean DEFAULT false
)
RETURNS TABLE (
    "BanKardexId" bigint,
    "BancoCuentaId" bigint,
    "BancoNombre" text,
    "CuentaNombre" text,
    "NumeroCuenta" text,
    "CuentaDisplay" text,
    "MonedaCodigo" text,
    "TipoTransaccion" text,
    "FechaMovimiento" date,
    "FechaRegistro" timestamp without time zone,
    "Descripcion" text,
    "Referencia" text,
    "Monto" numeric,
    "SaldoResultante" numeric,
    "Estado" text,
    "CreadoPor" text
)
LANGUAGE sql
STABLE
AS $$
WITH filtros AS (
    SELECT
        COALESCE(p_fecha_desde, date_trunc('month', current_date)::date) AS fecha_desde,
        COALESCE(p_fecha_hasta, current_date) AS fecha_hasta,
        COALESCE(p_incluir_anuladas, false) AS incluir_anuladas
),
base AS (
    SELECT
        k.ban_kardex_id,
        k.banco_cuenta_id,
        COALESCE(NULLIF(btrim(c.banco_nombre), ''), NULLIF(btrim(b.nombre), ''), '') AS banco_nombre,
        COALESCE(c.nombre, '') AS cuenta_nombre,
        COALESCE(c.numero_cuenta, '') AS numero_cuenta,
        CASE
            WHEN NULLIF(btrim(COALESCE(c.nombre, '')), '') IS NULL THEN COALESCE(c.numero_cuenta, '')
            WHEN NULLIF(btrim(COALESCE(c.numero_cuenta, '')), '') IS NULL THEN COALESCE(c.nombre, '')
            ELSE COALESCE(c.nombre, '') || ' / ' || COALESCE(c.numero_cuenta, '')
        END AS cuenta_display,
        COALESCE(NULLIF(btrim(m.codigo), ''), NULLIF(btrim(c.currency_code), ''), '') AS moneda_codigo,
        COALESCE(
            NULLIF(btrim(tt.nombre), ''),
            NULLIF(btrim(tt.tipo_transaccion), ''),
            k.id_tipo_transaccion::text) AS tipo_transaccion,
        k.fecha_movimiento,
        k.fecha_registro,
        COALESCE(k.descripcion, '') AS descripcion,
        COALESCE(k.referencia, '') AS referencia,
        COALESCE(k.monto, 0)::numeric AS monto,
        COALESCE(k.saldo, 0)::numeric AS saldo_resultante,
        COALESCE(k.created_by, '') AS creado_por
    FROM public.ban_kardex k
    INNER JOIN public.ban_cuenta c
        ON c.company_id = k.company_id
       AND c.banco_cuenta_id = k.banco_cuenta_id
    LEFT JOIN public.ban_banco b
        ON b.company_id = k.company_id
       AND b.ban_banco_id = COALESCE(k.ban_banco_id, c.ban_banco_id)
    LEFT JOIN public.ban_moneda m
        ON m.company_id = k.company_id
       AND m.ban_moneda_id = k.ban_moneda_id
    LEFT JOIN public.ban_tipos_transacciones tt
        ON tt.company_id = k.company_id
       AND tt.ban_tipo_transaccion_id = k.id_tipo_transaccion
    CROSS JOIN filtros f
    WHERE k.company_id = p_company_id
      AND (p_banco_cuenta_id IS NULL OR k.banco_cuenta_id = p_banco_cuenta_id)
      AND k.fecha_movimiento >= f.fecha_desde
      AND k.fecha_movimiento <= f.fecha_hasta
),
reversas_en_lista AS (
    SELECT b.ban_kardex_id, b.referencia
    FROM base b
    WHERE b.referencia ILIKE 'REV-%'
),
reversas_originales AS (
    SELECT
        b.ban_kardex_id AS original_id,
        k.ban_kardex_id AS anulacion_id,
        k.referencia
    FROM base b
    INNER JOIN public.ban_kardex k
        ON k.company_id = p_company_id
       AND k.referencia = 'REV-' || b.ban_kardex_id::text
),
estado AS (
    SELECT
        b.*,
        EXISTS (
            SELECT 1
            FROM reversas_en_lista r
            WHERE r.ban_kardex_id = b.ban_kardex_id) AS es_anulacion,
        EXISTS (
            SELECT 1
            FROM reversas_originales r
            WHERE r.original_id = b.ban_kardex_id) AS esta_anulada
    FROM base b
)
SELECT
    e.ban_kardex_id AS "BanKardexId",
    e.banco_cuenta_id AS "BancoCuentaId",
    e.banco_nombre AS "BancoNombre",
    e.cuenta_nombre AS "CuentaNombre",
    e.numero_cuenta AS "NumeroCuenta",
    e.cuenta_display AS "CuentaDisplay",
    e.moneda_codigo AS "MonedaCodigo",
    e.tipo_transaccion AS "TipoTransaccion",
    e.fecha_movimiento AS "FechaMovimiento",
    e.fecha_registro AS "FechaRegistro",
    e.descripcion AS "Descripcion",
    e.referencia AS "Referencia",
    e.monto AS "Monto",
    e.saldo_resultante AS "SaldoResultante",
    CASE
        WHEN e.es_anulacion THEN 'ANULACION'
        WHEN e.esta_anulada THEN 'ANULADA'
        ELSE 'ACTIVA'
    END AS "Estado",
    e.creado_por AS "CreadoPor"
FROM estado e
CROSS JOIN filtros f
WHERE f.incluir_anuladas
   OR (NOT e.es_anulacion AND NOT e.esta_anulada)
ORDER BY e.fecha_movimiento DESC, e.ban_kardex_id DESC;
$$;

UPDATE public.rep_catalogo_dataset
SET nombre = 'Dataset transacciones bancarias',
    descripcion = 'Fuente administrable para el reporte web de transacciones bancarias.',
    tipo_origen = 'STORED_PROCEDURE',
    origen_clave = 'public.rep_bancos_transacciones',
    sql_text = NULL,
    connection_name = 'DefaultConnection',
    is_active = TRUE,
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap'
WHERE codigo = 'bancos-transacciones';

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
ON CONFLICT (company_id, dataset_id, nombre) DO UPDATE
SET nombre_origen = EXCLUDED.nombre_origen,
    etiqueta = EXCLUDED.etiqueta,
    tipo_dato = EXCLUDED.tipo_dato,
    fuente_valor = EXCLUDED.fuente_valor,
    valor_default = EXCLUDED.valor_default,
    visible = EXCLUDED.visible,
    permite_nulo = EXCLUDED.permite_nulo,
    requerido = EXCLUDED.requerido,
    orden = EXCLUDED.orden,
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap';

UPDATE public.rep_catalogo_informe
SET consulta_clave = 'bancos-transacciones',
    updated_at = NOW(),
    updated_by = COALESCE(updated_by, 'reporteria-bootstrap')
WHERE codigo = 'bancos-transacciones'
  AND (consulta_clave IS NULL OR consulta_clave = '' OR consulta_clave = 'bancos-transacciones');

DO $$
BEGIN
    IF to_regclass('public.rep_catalogo_dataset') IS NULL THEN
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.rep_catalogo_dataset
        WHERE tipo_origen = 'OBJECT') THEN
        RAISE EXCEPTION 'Existen datasets OBJECT pendientes de migrar en public.rep_catalogo_dataset.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.rep_catalogo_dataset'::regclass
          AND conname = 'ck_rep_catalogo_dataset_tipo_origen') THEN
        ALTER TABLE public.rep_catalogo_dataset
            DROP CONSTRAINT ck_rep_catalogo_dataset_tipo_origen;
    END IF;

    ALTER TABLE public.rep_catalogo_dataset
        ADD CONSTRAINT ck_rep_catalogo_dataset_tipo_origen
        CHECK (tipo_origen IN ('STORED_PROCEDURE', 'VIEW', 'SQL'));
END $$;
