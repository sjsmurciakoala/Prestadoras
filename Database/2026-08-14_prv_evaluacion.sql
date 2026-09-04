-- =============================================================================
-- Evaluación de proveedores — F0: estructura, semilla y función de métricas
-- Fecha: 2026-08-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ
-- No existe nada de evaluación/calificación de proveedores en la base: lo único
-- parecido es el estado de cuenta (fn_prv_estado_cuenta_*, 2026-08-13), que mide
-- deuda, no desempeño. Este script crea el modelo para calificar a cada proveedor
-- por período combinando lo que ya registran órdenes y recepciones con lo que
-- califica el comprador. Diseño en docs/plans/2026-08-14-evaluacion-proveedores-plan.md
-- (prototipo aprobado por el usuario).
--
-- QUÉ SE CREA
--   1) prv_evaluacion_periodo    -> período evaluado (rango de fechas con nombre).
--   2) prv_evaluacion_criterio   -> catálogo por empresa: peso, origen, meta.
--   3) prv_evaluacion_clase      -> escala A/B/C/D por puntaje.
--   4) prv_evaluacion_hdr        -> evaluación de un proveedor en un período.
--   5) prv_evaluacion_dtl        -> un renglón por criterio, con SNAPSHOT del peso.
--   6) prv_recepcion_incidencia  -> incidencias por recepción (alimentan CALIDAD).
--   7) fn_prv_evaluacion_metricas-> función de LECTURA: numeradores/denominadores.
--   8) Semilla: 6 criterios y 4 clases por empresa (idempotente).
--
-- DECISIONES (usuario, 2026-08-14):
--   - La PERIODICIDAD es un dato (rango con nombre), no estructura: trimestral,
--     mensual o anual sin tocar el esquema.
--   - Los PESOS son configurables y el detalle guarda SNAPSHOT del peso y nombre:
--     repesar el catálogo no reescribe la historia ya calculada.
--   - Un criterio SIN datos en el período no puntúa cero: el servicio lo excluye y
--     reparte su peso (por eso dtl.logro y dtl.peso_efectivo admiten NULL).
--   - La semilla de criterios/clases es una PROPUESTA (D4): se edita desde la
--     pantalla del catálogo, sin SQL.
--
-- Cambio ADITIVO: 6 tablas nuevas (vacías), 1 función de lectura y una semilla que
-- sólo llena las tablas recién creadas. NO altera ninguna tabla existente.
-- Revertir: DROP de las 6 tablas (en orden inverso) y de la función.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Período evaluado
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prv_evaluacion_periodo (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    codigo              VARCHAR(20)    NOT NULL,            -- '2026-T2'
    nombre              VARCHAR(100)   NOT NULL,            -- 'Trimestre II 2026'
    fecha_desde         DATE           NOT NULL,
    fecha_hasta         DATE           NOT NULL,
    estado              SMALLINT       NOT NULL DEFAULT 1,  -- 1 Abierto · 2 Cerrado
    fecha_calculo       TIMESTAMP      NULL,
    usuario_calculo     VARCHAR(100)   NULL,
    fecha_cierre        TIMESTAMP      NULL,
    usuario_cierre      VARCHAR(100)   NULL,
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    CONSTRAINT uq_prv_evaluacion_periodo_codigo UNIQUE (company_id, codigo),
    -- AK para las FK compuestas tenant-safe (hdr -> periodo).
    CONSTRAINT uq_prv_evaluacion_periodo_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_prv_evaluacion_periodo_estado CHECK (estado IN (1, 2)),
    CONSTRAINT ck_prv_evaluacion_periodo_rango  CHECK (fecha_hasta >= fecha_desde)
);
CREATE INDEX IF NOT EXISTS ix_prv_evaluacion_periodo_company ON prv_evaluacion_periodo(company_id);

COMMENT ON TABLE  prv_evaluacion_periodo IS 'Período de evaluación de proveedores (rango de fechas con nombre). La periodicidad es un dato: trimestral, mensual o anual sin cambiar el esquema.';
COMMENT ON COLUMN prv_evaluacion_periodo.estado IS '1 Abierto (se puede recalcular) · 2 Cerrado (congelado, es historia).';

-- -----------------------------------------------------------------------------
-- 2) Catálogo de criterios (por empresa)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prv_evaluacion_criterio (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    codigo              VARCHAR(20)    NOT NULL,
    nombre              VARCHAR(100)   NOT NULL,
    descripcion         VARCHAR(300)   NULL,
    peso                NUMERIC(6,2)   NOT NULL DEFAULT 0,  -- porcentaje; la suma debe dar 100
    origen              SMALLINT       NOT NULL DEFAULT 1,  -- 1 Automático · 2 Manual (lo captura el comprador)
    metrica             VARCHAR(20)    NULL,                -- qué métrica lo alimenta; NULL si es manual
    meta                NUMERIC(6,2)   NULL,                -- % objetivo, informativo en la ficha
    parametro           NUMERIC(10,4)  NULL,                -- p. ej. tolerancia de precio en %
    orden               SMALLINT       NOT NULL DEFAULT 0,
    activo              BOOLEAN        NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    CONSTRAINT uq_prv_evaluacion_criterio_codigo UNIQUE (company_id, codigo),
    CONSTRAINT ck_prv_evaluacion_criterio_origen CHECK (origen IN (1, 2)),
    CONSTRAINT ck_prv_evaluacion_criterio_peso   CHECK (peso >= 0 AND peso <= 100)
);
CREATE INDEX IF NOT EXISTS ix_prv_evaluacion_criterio_company ON prv_evaluacion_criterio(company_id);

COMMENT ON TABLE  prv_evaluacion_criterio IS 'Criterios de evaluación por empresa: peso, origen y meta. Editable desde la pantalla del catálogo; el detalle de cada evaluación guarda un snapshot, así que repesar no reescribe la historia.';
COMMENT ON COLUMN prv_evaluacion_criterio.metrica IS 'Métrica que lo alimenta: ENTREGA · COMPLETO · PRECIO · CALIDAD · DOCUMENTO (ver fn_prv_evaluacion_metricas). NULL = criterio manual.';
COMMENT ON COLUMN prv_evaluacion_criterio.parametro IS 'Parámetro de la métrica. Hoy sólo lo usa PRECIO: tolerancia en % sobre el costo pactado.';

-- -----------------------------------------------------------------------------
-- 3) Escala de clasificación
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prv_evaluacion_clase (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    codigo              VARCHAR(10)    NOT NULL,            -- 'A'
    nombre              VARCHAR(60)    NOT NULL,            -- 'Confiable'
    descripcion         VARCHAR(300)   NULL,                -- qué acción dispara
    puntaje_desde       NUMERIC(6,2)   NOT NULL,
    puntaje_hasta       NUMERIC(6,2)   NOT NULL,
    orden               SMALLINT       NOT NULL DEFAULT 0,
    activo              BOOLEAN        NOT NULL DEFAULT true,
    CONSTRAINT uq_prv_evaluacion_clase_codigo UNIQUE (company_id, codigo),
    -- AK para la FK compuesta tenant-safe (hdr -> clase).
    CONSTRAINT uq_prv_evaluacion_clase_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_prv_evaluacion_clase_rango  CHECK (puntaje_hasta >= puntaje_desde)
);
CREATE INDEX IF NOT EXISTS ix_prv_evaluacion_clase_company ON prv_evaluacion_clase(company_id);

COMMENT ON TABLE  prv_evaluacion_clase IS 'Escala de clasificación por puntaje (A/B/C/D). La clase se resuelve como la de mayor puntaje_desde <= puntaje, así un redondeo no deja al proveedor sin clase.';

-- -----------------------------------------------------------------------------
-- 4) Evaluación de un proveedor en un período
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prv_evaluacion_hdr (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    periodo_id          INTEGER        NOT NULL,
    cod_proveedor       VARCHAR(20)    NOT NULL,            -- prv_proveedores (keyless: sin FK, se valida en el servicio)
    proveedor_nombre    VARCHAR(200)   NULL,                -- snapshot al calcular
    puntaje             NUMERIC(6,2)   NULL,                -- NULL mientras no haya ningún criterio con datos
    clase_id            INTEGER        NULL,
    clase_codigo        VARCHAR(10)    NULL,                -- snapshot, para no depender del catálogo al leer
    compras_periodo     NUMERIC(14,2)  NOT NULL DEFAULT 0,
    recepciones         INTEGER        NOT NULL DEFAULT 0,
    ordenes             INTEGER        NOT NULL DEFAULT 0,
    estado              SMALLINT       NOT NULL DEFAULT 1,  -- 1 Calculada · 2 Cerrada
    observaciones       VARCHAR(1000)  NULL,                -- plan de acción
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    CONSTRAINT uq_prv_evaluacion_hdr_proveedor UNIQUE (company_id, periodo_id, cod_proveedor),
    -- AK para la FK compuesta tenant-safe (dtl -> hdr).
    CONSTRAINT uq_prv_evaluacion_hdr_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_prv_evaluacion_hdr_estado CHECK (estado IN (1, 2)),
    CONSTRAINT fk_prv_evaluacion_hdr_periodo
        FOREIGN KEY (company_id, periodo_id)
        REFERENCES prv_evaluacion_periodo (company_id, id)
        ON DELETE CASCADE,
    -- clase_id es NULL mientras no hay puntaje; con MATCH SIMPLE la FK no valida esas filas.
    CONSTRAINT fk_prv_evaluacion_hdr_clase
        FOREIGN KEY (company_id, clase_id)
        REFERENCES prv_evaluacion_clase (company_id, id)
        ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_prv_evaluacion_hdr_company   ON prv_evaluacion_hdr(company_id);
CREATE INDEX IF NOT EXISTS ix_prv_evaluacion_hdr_periodo   ON prv_evaluacion_hdr(company_id, periodo_id);
CREATE INDEX IF NOT EXISTS ix_prv_evaluacion_hdr_proveedor ON prv_evaluacion_hdr(company_id, cod_proveedor);

COMMENT ON TABLE  prv_evaluacion_hdr IS 'Evaluación de un proveedor en un período: puntaje ponderado, clase y contexto (compras, recepciones, órdenes).';
COMMENT ON COLUMN prv_evaluacion_hdr.puntaje IS 'Σ de puntos sobre 100. NULL = el período no dejó ningún criterio con datos para este proveedor.';

-- -----------------------------------------------------------------------------
-- 5) Detalle por criterio (con snapshot)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prv_evaluacion_dtl (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    evaluacion_id       INTEGER        NOT NULL,
    criterio_id         INTEGER        NULL,                -- referencia blanda al catálogo (puede borrarse)
    criterio_codigo     VARCHAR(20)    NOT NULL,            -- SNAPSHOT
    criterio_nombre     VARCHAR(100)   NOT NULL,            -- SNAPSHOT
    peso                NUMERIC(6,2)   NOT NULL,            -- SNAPSHOT del peso configurado
    origen              SMALLINT       NOT NULL DEFAULT 1,
    metrica             VARCHAR(20)    NULL,
    numerador           NUMERIC(14,4)  NULL,
    denominador         NUMERIC(14,4)  NULL,
    logro               NUMERIC(6,2)   NULL,                -- % 0..100. NULL = sin datos en el período
    peso_efectivo       NUMERIC(6,2)   NULL,                -- peso tras redistribuir el de los criterios sin datos
    puntos              NUMERIC(6,2)   NULL,                -- peso_efectivo × logro
    detalle             VARCHAR(300)   NULL,                -- evidencia legible: "12 de 16 órdenes a tiempo"
    usuario_captura     VARCHAR(100)   NULL,                -- sólo criterios manuales
    fecha_captura       TIMESTAMP      NULL,
    CONSTRAINT uq_prv_evaluacion_dtl_criterio UNIQUE (company_id, evaluacion_id, criterio_codigo),
    CONSTRAINT ck_prv_evaluacion_dtl_logro CHECK (logro IS NULL OR (logro >= 0 AND logro <= 100)),
    CONSTRAINT fk_prv_evaluacion_dtl_hdr
        FOREIGN KEY (company_id, evaluacion_id)
        REFERENCES prv_evaluacion_hdr (company_id, id)
        ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_prv_evaluacion_dtl_company ON prv_evaluacion_dtl(company_id);
CREATE INDEX IF NOT EXISTS ix_prv_evaluacion_dtl_hdr     ON prv_evaluacion_dtl(company_id, evaluacion_id);

COMMENT ON TABLE  prv_evaluacion_dtl IS 'Un renglón por criterio evaluado. Guarda SNAPSHOT de código, nombre y peso: una evaluación cerrada sigue explicando su propio puntaje aunque el catálogo cambie.';
COMMENT ON COLUMN prv_evaluacion_dtl.logro IS 'Porcentaje de logro 0..100. NULL = el criterio no tuvo denominador en el período; su peso se redistribuye y NO cuenta como cero.';
COMMENT ON COLUMN prv_evaluacion_dtl.peso_efectivo IS 'Peso que realmente se usó, después de repartir el de los criterios sin datos. Σ peso_efectivo de la evaluación = 100.';

-- -----------------------------------------------------------------------------
-- 6) Incidencias de recepción (alimentan el criterio CALIDAD)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prv_recepcion_incidencia (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    compra_hdr_id       INTEGER        NOT NULL,            -- la recepción afectada
    fecha               DATE           NOT NULL,
    tipo                SMALLINT       NOT NULL,            -- 1 Devolución · 2 Daño · 3 Especificación distinta · 4 Faltante · 9 Otro
    articulo_id         INTEGER        NULL,                -- opcional: si la incidencia es de un artículo puntual
    cantidad            NUMERIC(14,4)  NULL,
    monto               NUMERIC(14,2)  NULL,
    descripcion         VARCHAR(500)   NOT NULL,
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    CONSTRAINT ck_prv_recepcion_incidencia_tipo CHECK (tipo IN (1, 2, 3, 4, 9)),
    CONSTRAINT fk_prv_recepcion_incidencia_compra
        FOREIGN KEY (company_id, compra_hdr_id)
        REFERENCES alm_compra_hdr (company_id, id)
        ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_prv_recepcion_incidencia_company ON prv_recepcion_incidencia(company_id);
CREATE INDEX IF NOT EXISTS ix_prv_recepcion_incidencia_compra  ON prv_recepcion_incidencia(company_id, compra_hdr_id);
CREATE INDEX IF NOT EXISTS ix_prv_recepcion_incidencia_fecha   ON prv_recepcion_incidencia(company_id, fecha);

COMMENT ON TABLE prv_recepcion_incidencia IS 'Incidencias detectadas al recibir (devolución, daño, especificación distinta, faltante). Es lo que hace medible el criterio CALIDAD: sin filas aquí, ese criterio queda sin datos y su peso se redistribuye.';

-- -----------------------------------------------------------------------------
-- 7) Métricas automáticas por proveedor (SOLO LECTURA)
-- -----------------------------------------------------------------------------
-- Devuelve numerador y denominador de cada métrica, no el porcentaje: la ficha
-- necesita mostrar la evidencia ("12 de 16"), y el servicio decide qué hacer
-- cuando el denominador es 0 (redistribuir el peso, no puntuar cero).
CREATE OR REPLACE FUNCTION fn_prv_evaluacion_metricas(
    p_company_id  BIGINT,
    p_desde       DATE,
    p_hasta       DATE,
    p_tolerancia  NUMERIC DEFAULT 2.0
)
RETURNS TABLE (
    cod_proveedor  VARCHAR,
    compras        NUMERIC,
    recepciones    INTEGER,
    ordenes        INTEGER,
    entrega_num    NUMERIC,
    entrega_den    NUMERIC,
    completo_num   NUMERIC,
    completo_den   NUMERIC,
    precio_num     NUMERIC,
    precio_den     NUMERIC,
    calidad_num    NUMERIC,
    calidad_den    NUMERIC,
    documento_num  NUMERIC,
    documento_den  NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    -- Universo: recepciones NO anuladas del período. El histórico SIMAFI queda fuera
    -- por construcción (vive en alm_compra sin cabecera).
    WITH rec AS (
        SELECT h.id, h.company_id, h.cod_proveedor, h.fecha, h.total, h.orden_compra_id,
               (COALESCE(h.cai, '') <> '' AND COALESCE(h.numero_factura_sar, '') <> '') AS doc_ok
          FROM alm_compra_hdr h
         WHERE h.company_id = p_company_id
           AND h.estado = 1
           AND h.fecha BETWEEN p_desde AND p_hasta
    ),
    base AS (
        SELECT r.cod_proveedor,
               SUM(r.total)                                      AS compras,
               COUNT(*)::int                                     AS recepciones,
               COUNT(DISTINCT r.orden_compra_id)::int            AS ordenes,
               SUM(CASE WHEN r.doc_ok THEN 1 ELSE 0 END)::numeric AS doc_num,
               COUNT(*)::numeric                                 AS doc_den
          FROM rec r
         GROUP BY r.cod_proveedor
    ),
    -- Líneas recibidas contra una O/C: base de ENTREGA y PRECIO.
    lin AS (
        SELECT r.cod_proveedor,
               r.fecha AS fecha_recepcion,
               c.precio_unitario,
               d.costo_unitario,
               -- La fecha del renglón manda; si no tiene, rige la de la cabecera.
               COALESCE(d.fecha_entrega_pactada, o.fecha_entrega_pactada) AS pactada
          FROM rec r
          JOIN alm_compra c
            ON c.company_id = r.company_id AND c.compra_hdr_id = r.id
          JOIN alm_orden_compra_detalle d
            ON d.company_id = c.company_id AND d.id = c.orden_compra_detalle_id
          JOIN alm_orden_compra o
            ON o.company_id = d.company_id AND o.id = d.orden_compra_id
    ),
    ent AS (
        SELECT l.cod_proveedor,
               SUM(CASE WHEN l.fecha_recepcion <= l.pactada THEN 1 ELSE 0 END)::numeric AS num,
               COUNT(*)::numeric AS den
          FROM lin l
         WHERE l.pactada IS NOT NULL      -- órdenes anteriores al 2026-08-14: no evaluables
         GROUP BY l.cod_proveedor
    ),
    pre AS (
        SELECT l.cod_proveedor,
               SUM(CASE
                     WHEN l.costo_unitario > 0
                      AND ABS(l.precio_unitario - l.costo_unitario) / l.costo_unitario * 100 <= p_tolerancia THEN 1
                     WHEN l.costo_unitario = 0 AND l.precio_unitario = 0 THEN 1
                     ELSE 0
                   END)::numeric AS num,
               COUNT(*)::numeric AS den
          FROM lin l
         GROUP BY l.cod_proveedor
    ),
    -- Completitud: sobre TODOS los renglones de las O/C recibidas en el período.
    -- LEAST evita que recibir de más en un renglón compense un faltante en otro.
    com AS (
        SELECT o.cod_proveedor,
               SUM(LEAST(d.cantidad_aplicada, d.cantidad_pedida)) AS num,
               SUM(d.cantidad_pedida)                             AS den
          FROM (SELECT DISTINCT r.company_id, r.orden_compra_id, r.cod_proveedor
                  FROM rec r WHERE r.orden_compra_id IS NOT NULL) o
          JOIN alm_orden_compra_detalle d
            ON d.company_id = o.company_id AND d.orden_compra_id = o.orden_compra_id
         GROUP BY o.cod_proveedor
    ),
    -- ¿La empresa registra incidencias? Mientras NADIE haya registrado una sola, no se
    -- puede distinguir "no hubo incidencias" de "nadie las captura": dar 100% ahí regalaría
    -- el peso completo del criterio a todos los proveedores. Hasta la primera incidencia,
    -- CALIDAD se reporta SIN DATOS (denominador 0) y el servicio redistribuye su peso.
    -- En cuanto exista una fila en prv_recepcion_incidencia, el criterio se activa solo.
    usa_incidencias AS (
        SELECT EXISTS (SELECT 1 FROM prv_recepcion_incidencia x
                        WHERE x.company_id = p_company_id) AS activo
    ),
    cal AS (
        SELECT r.cod_proveedor,
               SUM(CASE WHEN i.compra_hdr_id IS NULL THEN 1 ELSE 0 END)::numeric AS num,
               COUNT(*)::numeric AS den
          FROM rec r
          LEFT JOIN (SELECT DISTINCT x.company_id, x.compra_hdr_id
                       FROM prv_recepcion_incidencia x
                      WHERE x.company_id = p_company_id) i
            ON i.company_id = r.company_id AND i.compra_hdr_id = r.id
         GROUP BY r.cod_proveedor
    )
    SELECT b.cod_proveedor::varchar,
           b.compras,
           b.recepciones,
           b.ordenes,
           COALESCE(ent.num, 0), COALESCE(ent.den, 0),
           COALESCE(com.num, 0), COALESCE(com.den, 0),
           COALESCE(pre.num, 0), COALESCE(pre.den, 0),
           CASE WHEN u.activo THEN COALESCE(cal.num, 0) ELSE 0 END,
           CASE WHEN u.activo THEN COALESCE(cal.den, 0) ELSE 0 END,
           b.doc_num,            b.doc_den
      FROM base b
      CROSS JOIN usa_incidencias u
      LEFT JOIN ent ON ent.cod_proveedor = b.cod_proveedor
      LEFT JOIN com ON com.cod_proveedor = b.cod_proveedor
      LEFT JOIN pre ON pre.cod_proveedor = b.cod_proveedor
      LEFT JOIN cal ON cal.cod_proveedor = b.cod_proveedor
     ORDER BY b.compras DESC;
$$;

COMMENT ON FUNCTION fn_prv_evaluacion_metricas(BIGINT, DATE, DATE, NUMERIC) IS
    'Métricas automáticas de evaluación por proveedor en un rango. Sólo lectura. Devuelve numerador/denominador por métrica: denominador 0 significa "sin datos", y el servicio redistribuye ese peso en vez de puntuar cero.';

-- -----------------------------------------------------------------------------
-- 8) Semilla por empresa (idempotente) — PROPUESTA, editable desde la pantalla
-- -----------------------------------------------------------------------------
INSERT INTO prv_evaluacion_criterio
       (company_id, codigo, nombre, descripcion, peso, origen, metrica, meta, parametro, orden, usuariocreacion)
SELECT c.company_id::bigint, v.codigo, v.nombre, v.descripcion, v.peso, v.origen, v.metrica, v.meta, v.parametro, v.orden, 'semilla'
  FROM cfg_company c
 CROSS JOIN (VALUES
    ('ENTREGA',   'Cumplimiento de entrega', 'Recepciones dentro de la fecha pactada con el proveedor.',        25.00::numeric, 1::smallint, 'ENTREGA',   95.00::numeric, NULL::numeric,        1::smallint),
    ('COMPLETO',  'Completitud del pedido',  'Cantidad recibida contra cantidad pedida en la orden.',           20.00,          1,           'COMPLETO',  98.00,          NULL,                 2),
    ('CALIDAD',   'Calidad de lo recibido',  'Recepciones sin incidencias (devolución, daño, faltante).',       20.00,          1,           'CALIDAD',   98.00,          NULL,                 3),
    ('PRECIO',    'Exactitud de precio',     'Lo facturado coincide con el costo pactado en la orden.',         15.00,          1,           'PRECIO',    98.00,          2.0000,               4),
    ('DOCUMENTO', 'Documentación fiscal',    'Facturas con CAI y número SAR válidos.',                          10.00,          1,           'DOCUMENTO', 100.00,         NULL,                 5),
    ('SERVICIO',  'Servicio y respuesta',    'Atención, tiempo de respuesta y soporte. Lo califica compras.',   10.00,          2,           NULL,        80.00,          NULL,                 6)
 ) AS v(codigo, nombre, descripcion, peso, origen, metrica, meta, parametro, orden)
 WHERE NOT EXISTS (
    SELECT 1 FROM prv_evaluacion_criterio e
     WHERE e.company_id = c.company_id::bigint AND e.codigo = v.codigo);

INSERT INTO prv_evaluacion_clase
       (company_id, codigo, nombre, descripcion, puntaje_desde, puntaje_hasta, orden)
SELECT c.company_id::bigint, v.codigo, v.nombre, v.descripcion, v.desde, v.hasta, v.orden
  FROM cfg_company c
 CROSS JOIN (VALUES
    ('A', 'Confiable',    'Compra directa autorizada. Elegible para acuerdos de precio anual.', 90.00::numeric, 100.00::numeric, 1::smallint),
    ('B', 'Aceptable',    'Opera normal. Se le comunican las desviaciones del período.',        75.00,          89.99,           2),
    ('C', 'Condicionado', 'Requiere plan de acción y segunda cotización obligatoria.',          60.00,          74.99,           3),
    ('D', 'No aceptable', 'Se suspende la emisión de órdenes hasta reevaluación.',               0.00,          59.99,           4)
 ) AS v(codigo, nombre, descripcion, desde, hasta, orden)
 WHERE NOT EXISTS (
    SELECT 1 FROM prv_evaluacion_clase e
     WHERE e.company_id = c.company_id::bigint AND e.codigo = v.codigo);

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Las 6 tablas existen:
-- SELECT table_name FROM information_schema.tables
--  WHERE table_name IN ('prv_evaluacion_periodo','prv_evaluacion_criterio','prv_evaluacion_clase',
--                       'prv_evaluacion_hdr','prv_evaluacion_dtl','prv_recepcion_incidencia')
--  ORDER BY table_name;   -- 6 filas
--
-- 2) La semilla, y que los pesos sumen 100 por empresa:
-- SELECT company_id, count(*) AS criterios, sum(peso) AS peso_total
--   FROM prv_evaluacion_criterio GROUP BY company_id ORDER BY company_id;   -- 6 / 100.00
-- SELECT company_id, count(*) AS clases FROM prv_evaluacion_clase GROUP BY company_id;  -- 4
--
-- 3) Las FK compuestas tenant-safe:
-- SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
--  WHERE conrelid IN ('prv_evaluacion_hdr'::regclass,'prv_evaluacion_dtl'::regclass,
--                     'prv_recepcion_incidencia'::regclass) AND contype='f';
--
-- 4) La función corre y no escribe nada (sustituir company y fechas):
-- SELECT * FROM fn_prv_evaluacion_metricas(2, '2026-07-01', '2026-09-30');
-- =============================================================================
