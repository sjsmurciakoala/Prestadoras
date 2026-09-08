-- =============================================================================
-- Módulo ACTIVOS FIJOS (af_) — Fase 1: REGISTRO
-- Fecha: 2026-09-08
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y luego en el
-- servidor (lo aplica el usuario, ver runbook).
--
-- QUÉ HACE
--   Levanta el módulo de Activos Fijos sobre la tabla af_activo_fijo que ya trae
--   el histórico migrado de SIMAFI (MySQL bdsimafi.inventario, script
--   2026-07-01_alm_almacen_af_activo_fijo.sql). NO crea un maestro paralelo: el
--   histórico y el registro nuevo viven en la misma tabla.
--
--   Catálogos nuevos     : af_metodo_depreciacion, af_estado_activo,
--                          af_tipo_activo, af_ubicacion
--   Tablas hijas nuevas  : af_activo_asignacion, af_activo_componente
--   Maestro extendido    : af_activo_fijo + columnas normalizadas y de auditoría
--   Acceso a datos       : funciones fn_af_* y procedimientos sp_af_*
--                          (regla del proyecto: el SQL vive en la BD, no en C#)
--
-- ADITIVO / bajo riesgo: solo CREATE TABLE nuevas, ADD COLUMN nullable, CREATE
-- INDEX y CREATE OR REPLACE de funciones/SPs nuevos. NO hay DROP, ni DELETE, ni
-- cambios de tipo, ni NOT NULL sobre columnas existentes. Ninguna fila del
-- histórico se modifica.
--
-- IDEMPOTENTE: todo va con IF NOT EXISTS / CREATE OR REPLACE; se puede correr
-- varias veces sin efecto adicional.
-- =============================================================================

BEGIN;

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. CATÁLOGOS DE SISTEMA (sin company_id: vocabulario común a todas las empresas)
-- ─────────────────────────────────────────────────────────────────────────────

-- 1.1 Método de depreciación. Ids fijos y estables: se referencian desde C#
--     por constante, nunca por texto (regla de estados numéricos del proyecto).
CREATE TABLE IF NOT EXISTS af_metodo_depreciacion (
    id           SMALLINT     PRIMARY KEY,
    nombre       VARCHAR(60)  NOT NULL,
    descripcion  VARCHAR(254) NULL,
    implementado BOOLEAN      NOT NULL DEFAULT false,
    activo       BOOLEAN      NOT NULL DEFAULT true
);

INSERT INTO af_metodo_depreciacion (id, nombre, descripcion, implementado, activo) VALUES
    (1, 'Línea recta',            'Cuota constante: (valor de compra - valor residual) / vida útil.', true,  true),
    (2, 'Saldo decreciente',      'Porcentaje fijo sobre el valor en libros de cada período.',        false, true),
    (3, 'Suma de dígitos',        'Cuota decreciente ponderada por los años de vida restantes.',      false, true),
    (4, 'Unidades de producción', 'Cuota proporcional al uso (horas, kilómetros, unidades).',         false, true)
ON CONFLICT (id) DO NOTHING;

COMMENT ON TABLE af_metodo_depreciacion IS 'Métodos de depreciación disponibles. Solo "Línea recta" está implementado en el motor; el resto queda declarado para fases posteriores.';
COMMENT ON COLUMN af_metodo_depreciacion.implementado IS 'false = el método se puede ver pero la UI no deja elegirlo todavía; evita registrar activos que el motor no sabe depreciar.';

-- 1.2 Estado del activo. Reemplaza af_activo_fijo.estado (SMALLINT sin catálogo)
--     y los pares de banderas descargado/vendido del origen SIMAFI.
CREATE TABLE IF NOT EXISTS af_estado_activo (
    id                SMALLINT     PRIMARY KEY,
    nombre            VARCHAR(60)  NOT NULL,
    descripcion       VARCHAR(254) NULL,
    permite_depreciar BOOLEAN      NOT NULL DEFAULT true,
    es_final          BOOLEAN      NOT NULL DEFAULT false,
    activo            BOOLEAN      NOT NULL DEFAULT true
);

INSERT INTO af_estado_activo (id, nombre, descripcion, permite_depreciar, es_final, activo) VALUES
    (1, 'En uso',           'Asignado a un responsable y en operación.',                      true,  false, true),
    (2, 'Disponible',       'En bodega, sin asignar, listo para entregarse.',                 true,  false, true),
    (3, 'En mantenimiento', 'Temporalmente fuera de operación por reparación.',               true,  false, true),
    (4, 'Fuera de servicio','Inservible pero aún en el patrimonio; no se deprecia.',           false, false, true),
    (5, 'Dado de baja',     'Retirado del patrimonio por descargo. Estado final.',            false, true,  true),
    (6, 'Vendido',          'Retirado del patrimonio por venta. Estado final.',               false, true,  true),
    (7, 'Extraviado',       'Pérdida o robo reportado. Estado final hasta que se regularice.', false, true,  true)
ON CONFLICT (id) DO NOTHING;

COMMENT ON TABLE af_estado_activo IS 'Estados del activo fijo. La UI muestra SIEMPRE el nombre, nunca el id (regla del proyecto: los códigos internos no llegan al usuario).';
COMMENT ON COLUMN af_estado_activo.permite_depreciar IS 'false = el motor de depreciación salta el activo aunque tenga depreciar = true.';
COMMENT ON COLUMN af_estado_activo.es_final IS 'true = el activo salió del patrimonio; no admite nuevas asignaciones ni depreciación.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. CATÁLOGOS POR EMPRESA
-- ─────────────────────────────────────────────────────────────────────────────

-- 2.1 Tipo de activo. Es el corazón de la mejora: agrupa los activos y les
--     PRESTA sus valores por defecto (vida útil, método, % residual y las cuatro
--     cuentas contables). Mismo patrón que alm_tipo_articulo en almacén.
CREATE TABLE IF NOT EXISTS af_tipo_activo (
    id                            SERIAL        PRIMARY KEY,
    company_id                    BIGINT        NOT NULL,
    codigo                        VARCHAR(20)   NOT NULL,
    nombre                        VARCHAR(120)  NOT NULL,
    descripcion                   VARCHAR(254)  NULL,
    prefijo_codigo                VARCHAR(6)    NULL,
    vida_util_anios               NUMERIC(4,1)  NULL,
    metodo_depreciacion_id        SMALLINT      NOT NULL DEFAULT 1 REFERENCES af_metodo_depreciacion(id),
    porcentaje_residual           NUMERIC(5,2)  NOT NULL DEFAULT 0,
    cuenta_activo                 VARCHAR(30)   NULL,
    cuenta_depreciacion_acumulada VARCHAR(30)   NULL,
    cuenta_gasto_depreciacion     VARCHAR(30)   NULL,
    cuenta_perdida_baja           VARCHAR(30)   NULL,
    activo                        BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion               VARCHAR(100)  NULL,
    fechacreacion                 TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion           VARCHAR(100)  NULL,
    fechamodificacion             TIMESTAMP     NULL,
    CONSTRAINT uq_af_tipo_activo_company_codigo UNIQUE (company_id, codigo),
    CONSTRAINT ck_af_tipo_activo_residual  CHECK (porcentaje_residual >= 0 AND porcentaje_residual <= 100),
    CONSTRAINT ck_af_tipo_activo_vida_util CHECK (vida_util_anios IS NULL OR vida_util_anios > 0)
);
CREATE INDEX IF NOT EXISTS ix_af_tipo_activo_company ON af_tipo_activo(company_id);

COMMENT ON TABLE af_tipo_activo IS 'Tipo de activo fijo por empresa (Mobiliario, Vehículos, Equipo de cómputo...). Presta al activo su vida útil, método, % residual y cuentas contables; el activo puede sobrescribirlas.';
COMMENT ON COLUMN af_tipo_activo.prefijo_codigo IS 'Prefijo del código autogenerado del activo (p. ej. VEH -> VEH-000045). NULL = correlativo simple por empresa.';
COMMENT ON COLUMN af_tipo_activo.porcentaje_residual IS 'Valor residual sugerido como porcentaje del valor de compra. El activo puede capturar un monto propio.';
COMMENT ON COLUMN af_tipo_activo.cuenta_activo IS 'Código del plan de cuentas donde se capitaliza el activo. Se hereda al activo si este no trae cuenta propia.';
COMMENT ON COLUMN af_tipo_activo.cuenta_perdida_baja IS 'Cuenta de pérdida en retiro o venta. La usa la Fase 3 (bajas); en la Fase 1 solo se captura.';

-- 2.2 Ubicación física, jerárquica. Reemplaza af_activo_fijo.ubicacion (texto libre)
--     y aplana en UNA tabla las cuatro del desarrollo de Merendon
--     (sucursal / edificio / piso / oficina), que allá son tablas separadas.
CREATE TABLE IF NOT EXISTS af_ubicacion (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    codigo              VARCHAR(20)   NOT NULL,
    nombre              VARCHAR(120)  NOT NULL,
    padre_id            INTEGER       NULL REFERENCES af_ubicacion(id),
    direccion           VARCHAR(254)  NULL,
    responsable         VARCHAR(120)  NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_af_ubicacion_company_codigo UNIQUE (company_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_af_ubicacion_company ON af_ubicacion(company_id);
CREATE INDEX IF NOT EXISTS ix_af_ubicacion_padre   ON af_ubicacion(padre_id);

COMMENT ON TABLE af_ubicacion IS 'Ubicación física del activo, jerárquica por padre_id (sede, edificio, piso, oficina). Una sola tabla en vez de las cuatro del desarrollo de Merendon.';
COMMENT ON COLUMN af_ubicacion.padre_id IS 'Ubicación contenedora. NULL = nivel raíz. La profundidad no está limitada por la BD.';

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. MAESTRO: af_activo_fijo — columnas nuevas (todas nullable, aditivas)
-- ─────────────────────────────────────────────────────────────────────────────
-- Las columnas de texto libre que trae el histórico (tipo, clase, ubicacion,
-- responsable, proveedor, cuenta_contable...) NO se tocan: quedan como respaldo
-- del dato de origen. Las nuevas columnas *_id son las que usa el registro nuevo.

ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS tipo_activo_id            INTEGER      NULL REFERENCES af_tipo_activo(id);
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS estado_activo_id          SMALLINT     NULL REFERENCES af_estado_activo(id);
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS ubicacion_id              INTEGER      NULL REFERENCES af_ubicacion(id);
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS metodo_depreciacion_id    SMALLINT     NULL REFERENCES af_metodo_depreciacion(id);
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS empleado_id               INTEGER      NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS cod_proveedor             VARCHAR(20)  NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS centro_costo_id           BIGINT       NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS marca                     VARCHAR(60)  NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS placa                     VARCHAR(30)  NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS codigo_barra              VARCHAR(50)  NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS fecha_inicio_depreciacion DATE         NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS fecha_fin_depreciacion    DATE         NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS poliza_seguro             VARCHAR(60)  NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS poliza_vence              DATE         NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS garantia_vence            DATE         NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS usuariocreacion           VARCHAR(100) NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS fechacreacion             TIMESTAMP    NULL DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS usuariomodificacion       VARCHAR(100) NULL;
ALTER TABLE af_activo_fijo ADD COLUMN IF NOT EXISTS fechamodificacion         TIMESTAMP    NULL;

CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_tipo      ON af_activo_fijo(company_id, tipo_activo_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_estado    ON af_activo_fijo(company_id, estado_activo_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_ubicacion ON af_activo_fijo(company_id, ubicacion_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_fijo_empleado  ON af_activo_fijo(company_id, empleado_id);

-- El código de barras o placa de inventario, cuando se usa, identifica al activo.
CREATE UNIQUE INDEX IF NOT EXISTS uq_af_activo_fijo_company_barra
    ON af_activo_fijo(company_id, codigo_barra) WHERE codigo_barra IS NOT NULL;

COMMENT ON COLUMN af_activo_fijo.tipo_activo_id IS 'Tipo del catálogo af_tipo_activo. Sustituye a la columna de texto libre tipo, que queda como respaldo del dato migrado de SIMAFI.';
COMMENT ON COLUMN af_activo_fijo.estado_activo_id IS 'Estado del catálogo af_estado_activo. Sustituye a la columna SMALLINT estado (sin catálogo) y a las banderas descargado/vendido.';
COMMENT ON COLUMN af_activo_fijo.empleado_id IS 'Responsable actual, referencia lógica a th_empleado.id. Sin FK física para no bloquear el histórico migrado sin empleado en el catálogo; la integridad la valida el SP.';
COMMENT ON COLUMN af_activo_fijo.cod_proveedor IS 'Proveedor de la compra, referencia lógica a prv_proveedores.cod_proveedor. Sustituye a la columna de texto libre proveedor.';
COMMENT ON COLUMN af_activo_fijo.centro_costo_id IS 'Centro de costo actual, referencia lógica a con_centro_costo.cost_center_id. Lo usa la Fase 2 para repartir el gasto de depreciación.';
COMMENT ON COLUMN af_activo_fijo.fecha_inicio_depreciacion IS 'Fecha desde la que se deprecia. Puede diferir de fecha_compra (activo comprado en diciembre que entra en servicio en enero).';
COMMENT ON COLUMN af_activo_fijo.fecha_fin_depreciacion IS 'Fecha calculada de fin de vida útil. La escribe el SP de guardado, no el usuario.';
COMMENT ON COLUMN af_activo_fijo.codigo_barra IS 'Placa o etiqueta de inventario físico. Único por empresa cuando no es NULL.';

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. TABLAS HIJAS NUEVAS
-- ─────────────────────────────────────────────────────────────────────────────

-- 4.1 Historial de asignación. MEJORA sobre Merendon y SIMAFI, que solo guardan
--     el responsable ACTUAL: aquí queda la traza de quién tuvo el activo, dónde
--     y desde cuándo. Una sola fila vigente por activo (fecha_hasta IS NULL).
CREATE TABLE IF NOT EXISTS af_activo_asignacion (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    activo_fijo_id      INTEGER       NOT NULL REFERENCES af_activo_fijo(id) ON DELETE CASCADE,
    fecha_desde         DATE          NOT NULL,
    fecha_hasta         DATE          NULL,
    empleado_id         INTEGER       NULL,
    responsable         VARCHAR(120)  NULL,
    cargo_responsable   VARCHAR(80)   NULL,
    ubicacion_id        INTEGER       NULL REFERENCES af_ubicacion(id),
    centro_costo_id     BIGINT        NULL,
    motivo              VARCHAR(254)  NULL,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT ck_af_asignacion_rango CHECK (fecha_hasta IS NULL OR fecha_hasta >= fecha_desde)
);
CREATE INDEX IF NOT EXISTS ix_af_activo_asignacion_company ON af_activo_asignacion(company_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_asignacion_activo  ON af_activo_asignacion(activo_fijo_id, fecha_desde DESC);

CREATE UNIQUE INDEX IF NOT EXISTS uq_af_activo_asignacion_vigente
    ON af_activo_asignacion(activo_fijo_id) WHERE fecha_hasta IS NULL;

COMMENT ON TABLE af_activo_asignacion IS 'Historial de asignación del activo (responsable, ubicación, centro de costo) con vigencia. La fila con fecha_hasta NULL es la vigente y es la que refleja af_activo_fijo.';
COMMENT ON COLUMN af_activo_asignacion.responsable IS 'Nombre del responsable en el momento de la asignación. Se copia del empleado o se captura libre si no está en el catálogo.';

-- 4.2 Componentes y accesorios. MEJORA sobre Merendon, donde Accesorios y
--     Componentes son dos campos de texto libre en la ficha del activo.
CREATE TABLE IF NOT EXISTS af_activo_componente (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    activo_fijo_id      INTEGER       NOT NULL REFERENCES af_activo_fijo(id) ON DELETE CASCADE,
    descripcion         VARCHAR(200)  NOT NULL,
    marca               VARCHAR(60)   NULL,
    modelo              VARCHAR(60)   NULL,
    serie               VARCHAR(60)   NULL,
    cantidad            NUMERIC(10,2) NOT NULL DEFAULT 1,
    valor               NUMERIC(14,2) NOT NULL DEFAULT 0,
    observacion         VARCHAR(254)  NULL,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT ck_af_componente_cantidad CHECK (cantidad > 0),
    CONSTRAINT ck_af_componente_valor    CHECK (valor >= 0)
);
CREATE INDEX IF NOT EXISTS ix_af_activo_componente_company ON af_activo_componente(company_id);
CREATE INDEX IF NOT EXISTS ix_af_activo_componente_activo  ON af_activo_componente(activo_fijo_id);

COMMENT ON TABLE af_activo_componente IS 'Componentes y accesorios que forman parte de un activo (el disco de una computadora, la cama de un camión). Su valor es informativo en la Fase 1: no altera el valor depreciable del activo.';

-- ─────────────────────────────────────────────────────────────────────────────
-- 3.1 AMPLIACIONES DE PRECISIÓN en af_activo_fijo
-- ─────────────────────────────────────────────────────────────────────────────
-- ATENCIÓN: este es el único bloque del script que NO es puramente aditivo —
-- cambia el tipo de tres columnas. Las tres son AMPLIACIONES: el rango nuevo
-- contiene al viejo, así que ninguna fila existente se pierde ni se redondea.
-- Postgres las resuelve sin reescribir la tabla cuando solo crece la precisión.
--
-- Por qué son necesarias (límites heredados del MySQL de SIMAFI):
--   valor_rescate    NUMERIC(7,2)  -> tope L. 99,999.99. Un vehículo de
--                                    L. 800,000 con 20% de valor residual
--                                    (L. 160,000) NO cabe: el registro fallaría.
--   vida_util_anios  NUMERIC(3,0)  -> solo enteros. Impide vidas útiles de
--                                    2.5 o 7.5 años, comunes en equipo liviano.
--   valor_venta      NUMERIC(11,2) -> se alinea con valor_compra NUMERIC(12,2)
--                                    para que un activo no pueda venderse por
--                                    más de lo que la columna admite.

ALTER TABLE af_activo_fijo ALTER COLUMN valor_rescate   TYPE NUMERIC(14,2);
ALTER TABLE af_activo_fijo ALTER COLUMN vida_util_anios TYPE NUMERIC(4,1);
ALTER TABLE af_activo_fijo ALTER COLUMN valor_venta     TYPE NUMERIC(14,2);

COMMENT ON COLUMN af_activo_fijo.valor_rescate IS 'Valor residual estimado al final de la vida útil. Ampliado de NUMERIC(7,2) a NUMERIC(14,2) el 2026-09-08: el tope heredado de SIMAFI (L. 99,999.99) no admitía el residual de un vehículo o de maquinaria.';
COMMENT ON COLUMN af_activo_fijo.vida_util_anios IS 'Vida útil en años. Ampliado de NUMERIC(3,0) a NUMERIC(4,1) el 2026-09-08 para admitir medios años (2.5, 7.5).';

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. ACCESO A DATOS: funciones y procedimientos
-- ─────────────────────────────────────────────────────────────────────────────
-- Regla del proyecto: la lógica de consulta vive en la BD; C# solo invoca.
-- Los DROP de abajo son sobre objetos de ESTE mismo módulo, creados por este
-- script: sirven para poder cambiar la firma sin dejar sobrecargas colgando.

DROP FUNCTION  IF EXISTS public.fn_af_metodo_depreciacion_listar();
DROP FUNCTION  IF EXISTS public.fn_af_estado_activo_listar();
DROP FUNCTION  IF EXISTS public.fn_af_tipo_activo_listar(BIGINT, BOOLEAN, TEXT);
DROP FUNCTION  IF EXISTS public.fn_af_tipo_activo_obtener(BIGINT, INTEGER);
DROP FUNCTION  IF EXISTS public.fn_af_ubicacion_listar(BIGINT, BOOLEAN, TEXT);
DROP FUNCTION  IF EXISTS public.fn_af_ubicacion_obtener(BIGINT, INTEGER);
DROP FUNCTION  IF EXISTS public.fn_af_activo_siguiente_codigo(BIGINT, INTEGER);
DROP FUNCTION  IF EXISTS public.fn_af_activo_listar(BIGINT, TEXT, INTEGER, SMALLINT, INTEGER, INTEGER, BOOLEAN);
DROP FUNCTION  IF EXISTS public.fn_af_activo_obtener(BIGINT, INTEGER);
DROP FUNCTION  IF EXISTS public.fn_af_activo_resumen(BIGINT);
DROP FUNCTION  IF EXISTS public.fn_af_activo_asignacion_listar(BIGINT, INTEGER);
DROP FUNCTION  IF EXISTS public.fn_af_activo_componente_listar(BIGINT, INTEGER);

-- 5.1 Catálogos de sistema ───────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.fn_af_metodo_depreciacion_listar()
RETURNS TABLE (id SMALLINT, nombre VARCHAR, descripcion VARCHAR, implementado BOOLEAN)
LANGUAGE sql STABLE AS $$
    SELECT m.id, m.nombre, m.descripcion, m.implementado
      FROM public.af_metodo_depreciacion m
     WHERE m.activo
     ORDER BY m.id;
$$;

CREATE OR REPLACE FUNCTION public.fn_af_estado_activo_listar()
RETURNS TABLE (id SMALLINT, nombre VARCHAR, descripcion VARCHAR,
               permite_depreciar BOOLEAN, es_final BOOLEAN)
LANGUAGE sql STABLE AS $$
    SELECT e.id, e.nombre, e.descripcion, e.permite_depreciar, e.es_final
      FROM public.af_estado_activo e
     WHERE e.activo
     ORDER BY e.id;
$$;

-- 5.2 Tipo de activo ─────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.fn_af_tipo_activo_listar(
    p_company_id   BIGINT,
    p_solo_activos BOOLEAN DEFAULT NULL,
    p_search       TEXT    DEFAULT NULL)
RETURNS TABLE (id INTEGER, codigo VARCHAR, nombre VARCHAR, descripcion VARCHAR,
               prefijo_codigo VARCHAR, vida_util_anios NUMERIC,
               metodo_depreciacion_id SMALLINT, metodo_depreciacion VARCHAR,
               porcentaje_residual NUMERIC,
               cuenta_activo VARCHAR, cuenta_depreciacion_acumulada VARCHAR,
               cuenta_gasto_depreciacion VARCHAR, cuenta_perdida_baja VARCHAR,
               activo BOOLEAN, activos_registrados BIGINT)
LANGUAGE sql STABLE AS $$
    SELECT t.id, t.codigo, t.nombre, t.descripcion, t.prefijo_codigo, t.vida_util_anios,
           t.metodo_depreciacion_id, m.nombre, t.porcentaje_residual,
           t.cuenta_activo, t.cuenta_depreciacion_acumulada,
           t.cuenta_gasto_depreciacion, t.cuenta_perdida_baja, t.activo,
           (SELECT count(*) FROM public.af_activo_fijo a
             WHERE a.company_id = t.company_id AND a.tipo_activo_id = t.id)
      FROM public.af_tipo_activo t
      JOIN public.af_metodo_depreciacion m ON m.id = t.metodo_depreciacion_id
     WHERE t.company_id = p_company_id
       AND (p_solo_activos IS NULL OR t.activo = p_solo_activos)
       AND (p_search IS NULL OR t.codigo ILIKE p_search OR t.nombre ILIKE p_search)
     ORDER BY t.nombre;
$$;

CREATE OR REPLACE FUNCTION public.fn_af_tipo_activo_obtener(p_company_id BIGINT, p_id INTEGER)
RETURNS TABLE (id INTEGER, codigo VARCHAR, nombre VARCHAR, descripcion VARCHAR,
               prefijo_codigo VARCHAR, vida_util_anios NUMERIC,
               metodo_depreciacion_id SMALLINT, porcentaje_residual NUMERIC,
               cuenta_activo VARCHAR, cuenta_depreciacion_acumulada VARCHAR,
               cuenta_gasto_depreciacion VARCHAR, cuenta_perdida_baja VARCHAR,
               activo BOOLEAN)
LANGUAGE sql STABLE AS $$
    SELECT t.id, t.codigo, t.nombre, t.descripcion, t.prefijo_codigo, t.vida_util_anios,
           t.metodo_depreciacion_id, t.porcentaje_residual, t.cuenta_activo,
           t.cuenta_depreciacion_acumulada, t.cuenta_gasto_depreciacion,
           t.cuenta_perdida_baja, t.activo
      FROM public.af_tipo_activo t
     WHERE t.company_id = p_company_id AND t.id = p_id;
$$;

CREATE OR REPLACE PROCEDURE public.sp_af_tipo_activo_guardar(
    INOUT p_id                            INTEGER,
    IN    p_company_id                    BIGINT,
    IN    p_codigo                        VARCHAR,
    IN    p_nombre                        VARCHAR,
    IN    p_descripcion                   VARCHAR,
    IN    p_prefijo_codigo                VARCHAR,
    IN    p_vida_util_anios               NUMERIC,
    IN    p_metodo_depreciacion_id        SMALLINT,
    IN    p_porcentaje_residual           NUMERIC,
    IN    p_cuenta_activo                 VARCHAR,
    IN    p_cuenta_depreciacion_acumulada VARCHAR,
    IN    p_cuenta_gasto_depreciacion     VARCHAR,
    IN    p_cuenta_perdida_baja           VARCHAR,
    IN    p_activo                        BOOLEAN,
    IN    p_usuario                       VARCHAR)
LANGUAGE plpgsql AS $$
DECLARE
    v_codigo  VARCHAR := upper(btrim(coalesce(p_codigo, '')));
    v_nombre  VARCHAR := btrim(coalesce(p_nombre, ''));
    v_metodo  SMALLINT := coalesce(p_metodo_depreciacion_id, 1);
    v_ahora   TIMESTAMP := (now() AT TIME ZONE 'utc');
BEGIN
    IF v_codigo = '' THEN RAISE EXCEPTION 'El código del tipo de activo es obligatorio.'; END IF;
    IF v_nombre = '' THEN RAISE EXCEPTION 'El nombre del tipo de activo es obligatorio.'; END IF;

    IF p_vida_util_anios IS NOT NULL AND p_vida_util_anios <= 0 THEN
        RAISE EXCEPTION 'La vida útil debe ser mayor que cero.';
    END IF;

    IF coalesce(p_porcentaje_residual, 0) < 0 OR coalesce(p_porcentaje_residual, 0) > 100 THEN
        RAISE EXCEPTION 'El porcentaje residual debe estar entre 0 y 100.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.af_metodo_depreciacion m
                    WHERE m.id = v_metodo AND m.implementado) THEN
        RAISE EXCEPTION 'El método de depreciación seleccionado todavía no está disponible.';
    END IF;

    IF EXISTS (SELECT 1 FROM public.af_tipo_activo t
                WHERE t.company_id = p_company_id AND t.codigo = v_codigo
                  AND (p_id IS NULL OR t.id <> p_id)) THEN
        RAISE EXCEPTION 'Ya existe un tipo de activo con el código %.', v_codigo;
    END IF;

    IF p_id IS NULL OR p_id <= 0 THEN
        INSERT INTO public.af_tipo_activo
               (company_id, codigo, nombre, descripcion, prefijo_codigo, vida_util_anios,
                metodo_depreciacion_id, porcentaje_residual, cuenta_activo,
                cuenta_depreciacion_acumulada, cuenta_gasto_depreciacion, cuenta_perdida_baja,
                activo, usuariocreacion, fechacreacion)
        VALUES (p_company_id, v_codigo, v_nombre, p_descripcion,
                nullif(upper(btrim(coalesce(p_prefijo_codigo, ''))), ''), p_vida_util_anios,
                v_metodo, coalesce(p_porcentaje_residual, 0), p_cuenta_activo,
                p_cuenta_depreciacion_acumulada, p_cuenta_gasto_depreciacion, p_cuenta_perdida_baja,
                coalesce(p_activo, true), p_usuario, v_ahora)
        RETURNING id INTO p_id;
    ELSE
        UPDATE public.af_tipo_activo t
           SET codigo = v_codigo, nombre = v_nombre, descripcion = p_descripcion,
               prefijo_codigo = nullif(upper(btrim(coalesce(p_prefijo_codigo, ''))), ''),
               vida_util_anios = p_vida_util_anios,
               metodo_depreciacion_id = v_metodo,
               porcentaje_residual = coalesce(p_porcentaje_residual, 0),
               cuenta_activo = p_cuenta_activo,
               cuenta_depreciacion_acumulada = p_cuenta_depreciacion_acumulada,
               cuenta_gasto_depreciacion = p_cuenta_gasto_depreciacion,
               cuenta_perdida_baja = p_cuenta_perdida_baja,
               activo = coalesce(p_activo, true),
               usuariomodificacion = p_usuario, fechamodificacion = v_ahora
         WHERE t.company_id = p_company_id AND t.id = p_id;

        IF NOT FOUND THEN RAISE EXCEPTION 'No se encontró el tipo de activo solicitado.'; END IF;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE public.sp_af_tipo_activo_desactivar(
    IN p_company_id BIGINT, IN p_id INTEGER, IN p_usuario VARCHAR)
LANGUAGE plpgsql AS $$
BEGIN
    UPDATE public.af_tipo_activo t
       SET activo = false, usuariomodificacion = p_usuario,
           fechamodificacion = (now() AT TIME ZONE 'utc')
     WHERE t.company_id = p_company_id AND t.id = p_id;

    IF NOT FOUND THEN RAISE EXCEPTION 'No se encontró el tipo de activo solicitado.'; END IF;
END;
$$;

-- 5.3 Ubicación ──────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.fn_af_ubicacion_listar(
    p_company_id   BIGINT,
    p_solo_activos BOOLEAN DEFAULT NULL,
    p_search       TEXT    DEFAULT NULL)
RETURNS TABLE (id INTEGER, codigo VARCHAR, nombre VARCHAR, padre_id INTEGER,
               padre_nombre VARCHAR, ruta TEXT, direccion VARCHAR,
               responsable VARCHAR, activo BOOLEAN, activos_registrados BIGINT)
LANGUAGE sql STABLE AS $$
    WITH RECURSIVE arbol AS (
        SELECT u.id, u.company_id, u.codigo, u.nombre, u.padre_id,
               u.nombre::TEXT AS ruta
          FROM public.af_ubicacion u
         WHERE u.company_id = p_company_id AND u.padre_id IS NULL
        UNION ALL
        SELECT h.id, h.company_id, h.codigo, h.nombre, h.padre_id,
               a.ruta || ' / ' || h.nombre
          FROM public.af_ubicacion h
          JOIN arbol a ON a.id = h.padre_id AND a.company_id = h.company_id
    )
    SELECT u.id, u.codigo, u.nombre, u.padre_id, p.nombre, a.ruta, u.direccion,
           u.responsable, u.activo,
           (SELECT count(*) FROM public.af_activo_fijo af
             WHERE af.company_id = u.company_id AND af.ubicacion_id = u.id)
      FROM public.af_ubicacion u
      JOIN arbol a                    ON a.id = u.id
      LEFT JOIN public.af_ubicacion p ON p.id = u.padre_id
     WHERE u.company_id = p_company_id
       AND (p_solo_activos IS NULL OR u.activo = p_solo_activos)
       AND (p_search IS NULL OR u.codigo ILIKE p_search OR u.nombre ILIKE p_search
                             OR a.ruta ILIKE p_search)
     ORDER BY a.ruta;
$$;

CREATE OR REPLACE FUNCTION public.fn_af_ubicacion_obtener(p_company_id BIGINT, p_id INTEGER)
RETURNS TABLE (id INTEGER, codigo VARCHAR, nombre VARCHAR, padre_id INTEGER,
               direccion VARCHAR, responsable VARCHAR, activo BOOLEAN)
LANGUAGE sql STABLE AS $$
    SELECT u.id, u.codigo, u.nombre, u.padre_id, u.direccion, u.responsable, u.activo
      FROM public.af_ubicacion u
     WHERE u.company_id = p_company_id AND u.id = p_id;
$$;

CREATE OR REPLACE PROCEDURE public.sp_af_ubicacion_guardar(
    INOUT p_id          INTEGER,
    IN    p_company_id  BIGINT,
    IN    p_codigo      VARCHAR,
    IN    p_nombre      VARCHAR,
    IN    p_padre_id    INTEGER,
    IN    p_direccion   VARCHAR,
    IN    p_responsable VARCHAR,
    IN    p_activo      BOOLEAN,
    IN    p_usuario     VARCHAR)
LANGUAGE plpgsql AS $$
DECLARE
    v_codigo VARCHAR := upper(btrim(coalesce(p_codigo, '')));
    v_nombre VARCHAR := btrim(coalesce(p_nombre, ''));
    v_ahora  TIMESTAMP := (now() AT TIME ZONE 'utc');
    v_cursor INTEGER;
BEGIN
    IF v_codigo = '' THEN RAISE EXCEPTION 'El código de la ubicación es obligatorio.'; END IF;
    IF v_nombre = '' THEN RAISE EXCEPTION 'El nombre de la ubicación es obligatorio.'; END IF;

    IF EXISTS (SELECT 1 FROM public.af_ubicacion u
                WHERE u.company_id = p_company_id AND u.codigo = v_codigo
                  AND (p_id IS NULL OR u.id <> p_id)) THEN
        RAISE EXCEPTION 'Ya existe una ubicación con el código %.', v_codigo;
    END IF;

    IF p_padre_id IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM public.af_ubicacion u
                        WHERE u.company_id = p_company_id AND u.id = p_padre_id) THEN
            RAISE EXCEPTION 'La ubicación contenedora seleccionada no existe.';
        END IF;

        -- Un ciclo (A dentro de B dentro de A) dejaría el árbol irrecorrible.
        IF p_id IS NOT NULL AND p_padre_id = p_id THEN
            RAISE EXCEPTION 'Una ubicación no puede estar dentro de sí misma.';
        END IF;

        IF p_id IS NOT NULL THEN
            v_cursor := p_padre_id;
            WHILE v_cursor IS NOT NULL LOOP
                IF v_cursor = p_id THEN
                    RAISE EXCEPTION 'La ubicación contenedora ya depende de esta ubicación.';
                END IF;
                SELECT u.padre_id INTO v_cursor FROM public.af_ubicacion u WHERE u.id = v_cursor;
            END LOOP;
        END IF;
    END IF;

    IF p_id IS NULL OR p_id <= 0 THEN
        INSERT INTO public.af_ubicacion
               (company_id, codigo, nombre, padre_id, direccion, responsable, activo,
                usuariocreacion, fechacreacion)
        VALUES (p_company_id, v_codigo, v_nombre, p_padre_id, p_direccion, p_responsable,
                coalesce(p_activo, true), p_usuario, v_ahora)
        RETURNING id INTO p_id;
    ELSE
        UPDATE public.af_ubicacion u
           SET codigo = v_codigo, nombre = v_nombre, padre_id = p_padre_id,
               direccion = p_direccion, responsable = p_responsable,
               activo = coalesce(p_activo, true),
               usuariomodificacion = p_usuario, fechamodificacion = v_ahora
         WHERE u.company_id = p_company_id AND u.id = p_id;

        IF NOT FOUND THEN RAISE EXCEPTION 'No se encontró la ubicación solicitada.'; END IF;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE public.sp_af_ubicacion_desactivar(
    IN p_company_id BIGINT, IN p_id INTEGER, IN p_usuario VARCHAR)
LANGUAGE plpgsql AS $$
BEGIN
    UPDATE public.af_ubicacion u
       SET activo = false, usuariomodificacion = p_usuario,
           fechamodificacion = (now() AT TIME ZONE 'utc')
     WHERE u.company_id = p_company_id AND u.id = p_id;

    IF NOT FOUND THEN RAISE EXCEPTION 'No se encontró la ubicación solicitada.'; END IF;
END;
$$;

-- 5.4 Maestro de activos ─────────────────────────────────────────────────────
--
-- OJO con la depreciación acumulada del histórico. En las 829 filas migradas de SIMAFI
-- la columna `depreciacion_acumulada` está en 0 y el acumulado real vive en
-- `valor_depreciado` (se comprobó que valor_libros = valor_compra - valor_depreciado).
-- Leer solo la primera mostraría cero depreciación sobre L. 16 millones ya depreciados.
-- Por eso las funciones de lectura devuelven el MAYOR de las dos. Los registros nuevos
-- escriben las dos columnas con el mismo valor, así que para ellos da igual. Unificar el
-- dato de origen es una decisión aparte (D8 del plan): implica tocar el histórico.

-- Correlativo por empresa y por prefijo del tipo: VEH-000045. Solo mira los
-- códigos que siguen ese patrón, así que los códigos libres del histórico
-- SIMAFI no interfieren con la numeración nueva.
CREATE OR REPLACE FUNCTION public.fn_af_activo_siguiente_codigo(
    p_company_id BIGINT, p_tipo_activo_id INTEGER)
RETURNS VARCHAR
LANGUAGE plpgsql STABLE AS $$
DECLARE
    v_prefijo VARCHAR;
    v_max     INTEGER;
BEGIN
    SELECT coalesce(nullif(btrim(coalesce(t.prefijo_codigo, '')), ''), 'AF')
      INTO v_prefijo
      FROM public.af_tipo_activo t
     WHERE t.company_id = p_company_id AND t.id = p_tipo_activo_id;

    v_prefijo := coalesce(v_prefijo, 'AF');

    SELECT coalesce(max(substring(a.codigo_activo from '[0-9]+$')::INTEGER), 0)
      INTO v_max
      FROM public.af_activo_fijo a
     WHERE a.company_id = p_company_id
       AND a.codigo_activo ~ ('^' || v_prefijo || '-[0-9]{6}$');

    RETURN v_prefijo || '-' || lpad((v_max + 1)::TEXT, 6, '0');
END;
$$;

CREATE OR REPLACE FUNCTION public.fn_af_activo_listar(
    p_company_id   BIGINT,
    p_search       TEXT     DEFAULT NULL,
    p_tipo_id      INTEGER  DEFAULT NULL,
    p_estado_id    SMALLINT DEFAULT NULL,
    p_ubicacion_id INTEGER  DEFAULT NULL,
    p_empleado_id  INTEGER  DEFAULT NULL,
    p_pendientes   BOOLEAN  DEFAULT NULL)
RETURNS TABLE (id INTEGER, codigo_activo VARCHAR, descripcion VARCHAR,
               tipo_activo_id INTEGER, tipo_activo VARCHAR,
               estado_activo_id SMALLINT, estado_activo VARCHAR, estado_es_final BOOLEAN,
               ubicacion_id INTEGER, ubicacion VARCHAR,
               empleado_id INTEGER, responsable VARCHAR,
               marca VARCHAR, modelo VARCHAR, serie VARCHAR, placa VARCHAR,
               fecha_compra DATE, valor_compra NUMERIC, valor_rescate NUMERIC,
               depreciacion_acumulada NUMERIC, valor_libros NUMERIC,
               depreciar BOOLEAN, pendiente_completar BOOLEAN)
LANGUAGE sql STABLE AS $$
    SELECT a.id, a.codigo_activo, a.descripcion,
           a.tipo_activo_id, t.nombre,
           a.estado_activo_id, e.nombre, coalesce(e.es_final, false),
           a.ubicacion_id, u.nombre,
           a.empleado_id, coalesce(nullif(btrim(coalesce(emp.nombre, '')), ''), a.responsable),
           a.marca, a.modelo, a.serie, a.placa,
           a.fecha_compra, a.valor_compra, a.valor_rescate,
           GREATEST(coalesce(a.depreciacion_acumulada, 0), coalesce(a.valor_depreciado, 0)),
           a.valor_libros, a.depreciar,
           (a.tipo_activo_id IS NULL OR a.estado_activo_id IS NULL)
      FROM public.af_activo_fijo a
      LEFT JOIN public.af_tipo_activo    t   ON t.company_id = a.company_id AND t.id = a.tipo_activo_id
      LEFT JOIN public.af_estado_activo  e   ON e.id = a.estado_activo_id
      LEFT JOIN public.af_ubicacion      u   ON u.company_id = a.company_id AND u.id = a.ubicacion_id
      LEFT JOIN public.th_empleado       emp ON emp.company_id = a.company_id AND emp.id = a.empleado_id
     WHERE a.company_id = p_company_id
       AND (p_tipo_id      IS NULL OR a.tipo_activo_id   = p_tipo_id)
       AND (p_estado_id    IS NULL OR a.estado_activo_id = p_estado_id)
       AND (p_ubicacion_id IS NULL OR a.ubicacion_id     = p_ubicacion_id)
       AND (p_empleado_id  IS NULL OR a.empleado_id      = p_empleado_id)
       AND (p_pendientes   IS NULL OR p_pendientes = false
            OR a.tipo_activo_id IS NULL OR a.estado_activo_id IS NULL)
       AND (p_search IS NULL
            OR a.codigo_activo ILIKE p_search
            OR a.descripcion   ILIKE p_search
            OR coalesce(a.serie, '')        ILIKE p_search
            OR coalesce(a.placa, '')        ILIKE p_search
            OR coalesce(a.codigo_barra, '') ILIKE p_search
            OR coalesce(a.responsable, '')  ILIKE p_search)
     ORDER BY a.codigo_activo;
$$;

CREATE OR REPLACE FUNCTION public.fn_af_activo_obtener(p_company_id BIGINT, p_id INTEGER)
RETURNS TABLE (id INTEGER, codigo_activo VARCHAR, descripcion VARCHAR, clase VARCHAR,
               tipo_activo_id INTEGER, estado_activo_id SMALLINT, ubicacion_id INTEGER,
               empleado_id INTEGER, responsable VARCHAR, cargo_responsable VARCHAR,
               cod_proveedor VARCHAR, centro_costo_id BIGINT,
               marca VARCHAR, modelo VARCHAR, serie VARCHAR, placa VARCHAR, codigo_barra VARCHAR,
               numero_factura VARCHAR, fecha_compra DATE, fecha_inicio_depreciacion DATE,
               fecha_fin_depreciacion DATE, valor_compra NUMERIC, valor_rescate NUMERIC,
               vida_util_anios NUMERIC, metodo_depreciacion_id SMALLINT, depreciar BOOLEAN,
               depreciacion_acumulada NUMERIC, depreciacion_mensual NUMERIC,
               depreciacion_diaria NUMERIC, valor_libros NUMERIC,
               cuenta_contable VARCHAR, cuenta_depreciacion VARCHAR, cuenta_gasto VARCHAR,
               poliza_seguro VARCHAR, poliza_vence DATE, garantia_vence DATE,
               propiedades_especiales VARCHAR, observacion VARCHAR,
               tipo_activo VARCHAR, estado_activo VARCHAR, ubicacion VARCHAR,
               proveedor_nombre VARCHAR, centro_costo VARCHAR)
LANGUAGE sql STABLE AS $$
    SELECT a.id, a.codigo_activo, a.descripcion, a.clase,
           a.tipo_activo_id, a.estado_activo_id, a.ubicacion_id,
           a.empleado_id, a.responsable, a.cargo_responsable,
           a.cod_proveedor, a.centro_costo_id,
           a.marca, a.modelo, a.serie, a.placa, a.codigo_barra,
           a.numero_factura, a.fecha_compra, a.fecha_inicio_depreciacion,
           a.fecha_fin_depreciacion, a.valor_compra, a.valor_rescate,
           a.vida_util_anios, a.metodo_depreciacion_id, a.depreciar,
           GREATEST(coalesce(a.depreciacion_acumulada, 0), coalesce(a.valor_depreciado, 0)),
           a.depreciacion_mensual,
           a.depreciacion_diaria, a.valor_libros,
           a.cuenta_contable, a.cuenta_depreciacion, a.cuenta_gasto,
           a.poliza_seguro, a.poliza_vence, a.garantia_vence,
           a.propiedades_especiales, a.observacion,
           t.nombre, e.nombre, u.nombre, pr.nombre, cc.name
      FROM public.af_activo_fijo a
      LEFT JOIN public.af_tipo_activo   t  ON t.company_id = a.company_id AND t.id = a.tipo_activo_id
      LEFT JOIN public.af_estado_activo e  ON e.id = a.estado_activo_id
      LEFT JOIN public.af_ubicacion     u  ON u.company_id = a.company_id AND u.id = a.ubicacion_id
      LEFT JOIN public.prv_proveedores   pr ON pr.cod_proveedor = a.cod_proveedor
      LEFT JOIN public.con_centro_costo cc ON cc.company_id = a.company_id AND cc.cost_center_id = a.centro_costo_id
     WHERE a.company_id = p_company_id AND a.id = p_id;
$$;

-- Tarjetas de resumen de la pantalla de listado.
CREATE OR REPLACE FUNCTION public.fn_af_activo_resumen(p_company_id BIGINT)
RETURNS TABLE (total_activos BIGINT, en_patrimonio BIGINT, pendientes_completar BIGINT,
               sin_responsable BIGINT, valor_compra NUMERIC,
               depreciacion_acumulada NUMERIC, valor_libros NUMERIC)
LANGUAGE sql STABLE AS $$
    SELECT count(*),
           count(*) FILTER (WHERE coalesce(e.es_final, false) = false),
           count(*) FILTER (WHERE a.tipo_activo_id IS NULL OR a.estado_activo_id IS NULL),
           count(*) FILTER (WHERE a.empleado_id IS NULL
                              AND btrim(coalesce(a.responsable, '')) = ''),
           coalesce(sum(a.valor_compra), 0),
           coalesce(sum(GREATEST(coalesce(a.depreciacion_acumulada, 0),
                                 coalesce(a.valor_depreciado, 0))), 0),
           coalesce(sum(a.valor_libros), 0)
      FROM public.af_activo_fijo a
      LEFT JOIN public.af_estado_activo e ON e.id = a.estado_activo_id
     WHERE a.company_id = p_company_id;
$$;

CREATE OR REPLACE PROCEDURE public.sp_af_activo_guardar(
    INOUT p_id                        INTEGER,
    INOUT p_codigo_activo             VARCHAR,
    IN    p_company_id                BIGINT,
    IN    p_descripcion               VARCHAR,
    IN    p_clase                     VARCHAR,
    IN    p_tipo_activo_id            INTEGER,
    IN    p_estado_activo_id          SMALLINT,
    IN    p_ubicacion_id              INTEGER,
    IN    p_empleado_id               INTEGER,
    IN    p_responsable               VARCHAR,
    IN    p_cargo_responsable         VARCHAR,
    IN    p_cod_proveedor             VARCHAR,
    IN    p_centro_costo_id           BIGINT,
    IN    p_marca                     VARCHAR,
    IN    p_modelo                    VARCHAR,
    IN    p_serie                     VARCHAR,
    IN    p_placa                     VARCHAR,
    IN    p_codigo_barra              VARCHAR,
    IN    p_numero_factura            VARCHAR,
    IN    p_fecha_compra              DATE,
    IN    p_fecha_inicio_depreciacion DATE,
    IN    p_valor_compra              NUMERIC,
    IN    p_valor_rescate             NUMERIC,
    IN    p_vida_util_anios           NUMERIC,
    IN    p_metodo_depreciacion_id    SMALLINT,
    IN    p_depreciar                 BOOLEAN,
    IN    p_depreciacion_acumulada    NUMERIC,
    IN    p_cuenta_contable           VARCHAR,
    IN    p_cuenta_depreciacion       VARCHAR,
    IN    p_cuenta_gasto              VARCHAR,
    IN    p_poliza_seguro             VARCHAR,
    IN    p_poliza_vence              DATE,
    IN    p_garantia_vence            DATE,
    IN    p_propiedades_especiales    VARCHAR,
    IN    p_observacion               VARCHAR,
    IN    p_usuario                   VARCHAR)
LANGUAGE plpgsql AS $$
DECLARE
    v_tipo            RECORD;
    v_estado          RECORD;
    v_es_nuevo        BOOLEAN := (p_id IS NULL OR p_id <= 0);
    v_codigo          VARCHAR := upper(btrim(coalesce(p_codigo_activo, '')));
    v_descripcion     VARCHAR := btrim(coalesce(p_descripcion, ''));
    v_barra           VARCHAR := nullif(upper(btrim(coalesce(p_codigo_barra, ''))), '');
    v_vida            NUMERIC;
    v_metodo          SMALLINT;
    v_rescate         NUMERIC;
    v_compra          NUMERIC := coalesce(p_valor_compra, 0);
    v_acumulada       NUMERIC := coalesce(p_depreciacion_acumulada, 0);
    v_a_depreciar     NUMERIC;
    v_mensual         NUMERIC := 0;
    v_diaria          NUMERIC := 0;
    v_periodos        NUMERIC;
    v_inicio          DATE;
    v_fin             DATE;
    v_responsable     VARCHAR := nullif(btrim(coalesce(p_responsable, '')), '');
    v_cargo           VARCHAR := nullif(btrim(coalesce(p_cargo_responsable, '')), '');
    v_cuenta_activo   VARCHAR;
    v_cuenta_dep      VARCHAR;
    v_cuenta_gasto    VARCHAR;
    v_ahora           TIMESTAMP := (now() AT TIME ZONE 'utc');
    v_asig            RECORD;
BEGIN
    -- ── Validaciones de negocio ──────────────────────────────────────────────
    IF v_descripcion = '' THEN
        RAISE EXCEPTION 'La descripción del activo es obligatoria.';
    END IF;

    IF p_tipo_activo_id IS NULL THEN
        RAISE EXCEPTION 'Debe seleccionar el tipo de activo.';
    END IF;

    SELECT t.* INTO v_tipo
      FROM public.af_tipo_activo t
     WHERE t.company_id = p_company_id AND t.id = p_tipo_activo_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El tipo de activo seleccionado no existe.';
    END IF;

    IF NOT v_tipo.activo AND v_es_nuevo THEN
        RAISE EXCEPTION 'El tipo de activo % está inactivo y no admite registros nuevos.', v_tipo.nombre;
    END IF;

    IF p_estado_activo_id IS NULL THEN
        RAISE EXCEPTION 'Debe seleccionar el estado del activo.';
    END IF;

    SELECT e.* INTO v_estado FROM public.af_estado_activo e WHERE e.id = p_estado_activo_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El estado del activo seleccionado no existe.';
    END IF;

    IF v_compra <= 0 THEN
        RAISE EXCEPTION 'El valor de compra debe ser mayor que cero.';
    END IF;

    IF p_fecha_compra IS NULL THEN
        RAISE EXCEPTION 'La fecha de compra es obligatoria.';
    END IF;

    IF p_fecha_compra > current_date THEN
        RAISE EXCEPTION 'La fecha de compra no puede ser futura.';
    END IF;

    -- Herencia del tipo: lo que el usuario no captura lo presta el tipo.
    v_vida   := coalesce(p_vida_util_anios, v_tipo.vida_util_anios);
    v_metodo := coalesce(p_metodo_depreciacion_id, v_tipo.metodo_depreciacion_id, 1::SMALLINT);

    v_rescate := p_valor_rescate;
    IF v_rescate IS NULL THEN
        v_rescate := round(v_compra * coalesce(v_tipo.porcentaje_residual, 0) / 100, 2);
    END IF;

    IF v_rescate < 0 THEN
        RAISE EXCEPTION 'El valor residual no puede ser negativo.';
    END IF;

    IF v_rescate >= v_compra THEN
        RAISE EXCEPTION 'El valor residual (%) debe ser menor que el valor de compra (%).', v_rescate, v_compra;
    END IF;

    IF coalesce(p_depreciar, false) THEN
        IF v_vida IS NULL OR v_vida <= 0 THEN
            RAISE EXCEPTION 'Un activo que se deprecia necesita una vida útil mayor que cero.';
        END IF;

        IF NOT EXISTS (SELECT 1 FROM public.af_metodo_depreciacion m
                        WHERE m.id = v_metodo AND m.implementado) THEN
            RAISE EXCEPTION 'El método de depreciación seleccionado todavía no está disponible.';
        END IF;

        IF NOT v_estado.permite_depreciar THEN
            RAISE EXCEPTION 'Un activo en estado % no se deprecia. Desmarque la casilla o cambie el estado.', v_estado.nombre;
        END IF;
    END IF;

    v_a_depreciar := v_compra - v_rescate;

    IF v_acumulada < 0 THEN
        RAISE EXCEPTION 'La depreciación acumulada no puede ser negativa.';
    END IF;

    IF v_acumulada > v_a_depreciar THEN
        RAISE EXCEPTION 'La depreciación acumulada (%) no puede superar el valor depreciable (%).', v_acumulada, v_a_depreciar;
    END IF;

    v_inicio := coalesce(p_fecha_inicio_depreciacion, p_fecha_compra);
    IF v_inicio < p_fecha_compra THEN
        RAISE EXCEPTION 'La depreciación no puede empezar antes de la fecha de compra.';
    END IF;

    IF p_empleado_id IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM public.th_empleado emp
                        WHERE emp.company_id = p_company_id AND emp.id = p_empleado_id) THEN
        RAISE EXCEPTION 'El responsable seleccionado no existe en el catálogo de empleados.';
    END IF;

    IF p_ubicacion_id IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM public.af_ubicacion u
                        WHERE u.company_id = p_company_id AND u.id = p_ubicacion_id) THEN
        RAISE EXCEPTION 'La ubicación seleccionada no existe.';
    END IF;

    IF p_cod_proveedor IS NOT NULL AND btrim(p_cod_proveedor) <> ''
       AND NOT EXISTS (SELECT 1 FROM public.prv_proveedores pr
                        WHERE pr.cod_proveedor = btrim(p_cod_proveedor)) THEN
        RAISE EXCEPTION 'El proveedor seleccionado no existe.';
    END IF;

    -- ── Derivados de depreciación (línea recta) ──────────────────────────────
    IF v_vida IS NOT NULL AND v_vida > 0 THEN
        v_periodos := round(v_vida * 12);
        v_mensual  := round(v_a_depreciar / (v_vida * 12), 2);
        v_diaria   := round(v_a_depreciar / (v_vida * 365), 2);
        v_fin      := (v_inicio + (v_periodos::INTEGER * INTERVAL '1 month'))::DATE;
    END IF;

    -- Cuentas: las del activo mandan; si vienen vacías, hereda las del tipo.
    v_cuenta_activo := coalesce(nullif(btrim(coalesce(p_cuenta_contable, '')), ''),     v_tipo.cuenta_activo);
    v_cuenta_dep    := coalesce(nullif(btrim(coalesce(p_cuenta_depreciacion, '')), ''), v_tipo.cuenta_depreciacion_acumulada);
    v_cuenta_gasto  := coalesce(nullif(btrim(coalesce(p_cuenta_gasto, '')), ''),        v_tipo.cuenta_gasto_depreciacion);

    -- El nombre del responsable se copia del catálogo cuando se eligió de ahí.
    IF p_empleado_id IS NOT NULL THEN
        SELECT emp.nombre INTO v_responsable
          FROM public.th_empleado emp
         WHERE emp.company_id = p_company_id AND emp.id = p_empleado_id;
    END IF;

    -- ── Código: autogenerado si el usuario no lo escribió ────────────────────
    IF v_codigo = '' THEN
        IF v_es_nuevo THEN
            v_codigo := public.fn_af_activo_siguiente_codigo(p_company_id, p_tipo_activo_id);
        ELSE
            SELECT a.codigo_activo INTO v_codigo
              FROM public.af_activo_fijo a
             WHERE a.company_id = p_company_id AND a.id = p_id;
        END IF;
    END IF;

    IF EXISTS (SELECT 1 FROM public.af_activo_fijo a
                WHERE a.company_id = p_company_id AND a.codigo_activo = v_codigo
                  AND (v_es_nuevo OR a.id <> p_id)) THEN
        RAISE EXCEPTION 'Ya existe un activo con el código %.', v_codigo;
    END IF;

    IF v_barra IS NOT NULL
       AND EXISTS (SELECT 1 FROM public.af_activo_fijo a
                    WHERE a.company_id = p_company_id AND a.codigo_barra = v_barra
                      AND (v_es_nuevo OR a.id <> p_id)) THEN
        RAISE EXCEPTION 'Ya existe un activo con el código de barras %.', v_barra;
    END IF;

    -- ── Escritura ────────────────────────────────────────────────────────────
    IF v_es_nuevo THEN
        -- Las columnas de texto libre heredadas de SIMAFI (tipo, ubicacion, proveedor)
        -- NO se escriben: son respaldo del dato migrado y su ancho no da para los
        -- nombres del catálogo (tipo es VARCHAR(15) y "Equipos de transporte" no cabe).
        -- Para los registros nuevos el texto sale del JOIN con el catálogo.
        INSERT INTO public.af_activo_fijo
               (company_id, codigo_activo, descripcion, clase,
                tipo_activo_id, estado_activo_id, ubicacion_id, metodo_depreciacion_id,
                empleado_id, responsable, cargo_responsable, cod_proveedor,
                centro_costo_id, marca, modelo, serie, placa, codigo_barra, numero_factura,
                fecha_compra, fecha_inicio_depreciacion, fecha_fin_depreciacion,
                valor_compra, valor_rescate, vida_util_anios, vida_util_periodos,
                depreciar, valor_a_depreciar, valor_depreciado, depreciacion_acumulada,
                depreciacion_mensual, depreciacion_diaria, valor_libros,
                cuenta_contable, cuenta_depreciacion, cuenta_gasto,
                poliza_seguro, poliza_vence, garantia_vence,
                propiedades_especiales, observacion, descargado, vendido,
                usuariocreacion, fechacreacion)
        VALUES (p_company_id, v_codigo, v_descripcion, p_clase,
                p_tipo_activo_id, p_estado_activo_id, p_ubicacion_id, v_metodo,
                p_empleado_id, left(v_responsable, 80), left(v_cargo, 50),
                nullif(btrim(coalesce(p_cod_proveedor, '')), ''),
                p_centro_costo_id, p_marca, p_modelo, p_serie, p_placa, v_barra, p_numero_factura,
                p_fecha_compra, v_inicio, v_fin,
                v_compra, v_rescate, v_vida, v_periodos,
                coalesce(p_depreciar, false), v_a_depreciar, v_acumulada, v_acumulada,
                v_mensual, v_diaria, v_compra - v_acumulada,
                v_cuenta_activo, v_cuenta_dep, v_cuenta_gasto,
                p_poliza_seguro, p_poliza_vence, p_garantia_vence,
                p_propiedades_especiales, p_observacion,
                coalesce(v_estado.es_final, false) AND p_estado_activo_id = 5,
                coalesce(v_estado.es_final, false) AND p_estado_activo_id = 6,
                p_usuario, v_ahora)
        RETURNING id INTO p_id;
    ELSE
        UPDATE public.af_activo_fijo a
           SET codigo_activo = v_codigo, descripcion = v_descripcion, clase = p_clase,
               tipo_activo_id = p_tipo_activo_id, estado_activo_id = p_estado_activo_id,
               ubicacion_id = p_ubicacion_id, metodo_depreciacion_id = v_metodo,
               empleado_id = p_empleado_id,
               responsable = left(v_responsable, 80), cargo_responsable = left(v_cargo, 50),
               cod_proveedor = nullif(btrim(coalesce(p_cod_proveedor, '')), ''),
               centro_costo_id = p_centro_costo_id,
               marca = p_marca, modelo = p_modelo, serie = p_serie, placa = p_placa,
               codigo_barra = v_barra, numero_factura = p_numero_factura,
               fecha_compra = p_fecha_compra, fecha_inicio_depreciacion = v_inicio,
               fecha_fin_depreciacion = v_fin,
               valor_compra = v_compra, valor_rescate = v_rescate,
               vida_util_anios = v_vida, vida_util_periodos = v_periodos,
               depreciar = coalesce(p_depreciar, false),
               valor_a_depreciar = v_a_depreciar,
               depreciacion_acumulada = v_acumulada, valor_depreciado = v_acumulada,
               depreciacion_mensual = v_mensual, depreciacion_diaria = v_diaria,
               valor_libros = v_compra - v_acumulada,
               cuenta_contable = v_cuenta_activo, cuenta_depreciacion = v_cuenta_dep,
               cuenta_gasto = v_cuenta_gasto,
               poliza_seguro = p_poliza_seguro, poliza_vence = p_poliza_vence,
               garantia_vence = p_garantia_vence,
               propiedades_especiales = p_propiedades_especiales, observacion = p_observacion,
               descargado = (p_estado_activo_id = 5), vendido = (p_estado_activo_id = 6),
               usuariomodificacion = p_usuario, fechamodificacion = v_ahora
         WHERE a.company_id = p_company_id AND a.id = p_id;

        IF NOT FOUND THEN RAISE EXCEPTION 'No se encontró el activo solicitado.'; END IF;
    END IF;

    p_codigo_activo := v_codigo;

    -- ── Asignación vigente: se abre o se reabre si cambió algo de la tercia ──
    SELECT s.* INTO v_asig
      FROM public.af_activo_asignacion s
     WHERE s.company_id = p_company_id AND s.activo_fijo_id = p_id AND s.fecha_hasta IS NULL;

    IF NOT FOUND THEN
        IF p_empleado_id IS NOT NULL OR v_responsable IS NOT NULL OR p_ubicacion_id IS NOT NULL THEN
            INSERT INTO public.af_activo_asignacion
                   (company_id, activo_fijo_id, fecha_desde, empleado_id, responsable,
                    cargo_responsable, ubicacion_id, centro_costo_id, motivo,
                    usuariocreacion, fechacreacion)
            VALUES (p_company_id, p_id, coalesce(p_fecha_compra, current_date), p_empleado_id,
                    v_responsable, v_cargo, p_ubicacion_id, p_centro_costo_id,
                    'Asignación inicial del registro', p_usuario, v_ahora);
        END IF;
    ELSIF v_asig.empleado_id     IS DISTINCT FROM p_empleado_id
       OR v_asig.ubicacion_id    IS DISTINCT FROM p_ubicacion_id
       OR v_asig.centro_costo_id IS DISTINCT FROM p_centro_costo_id
       OR v_asig.responsable     IS DISTINCT FROM v_responsable THEN

        UPDATE public.af_activo_asignacion s
           SET fecha_hasta = current_date
         WHERE s.id = v_asig.id;

        INSERT INTO public.af_activo_asignacion
               (company_id, activo_fijo_id, fecha_desde, empleado_id, responsable,
                cargo_responsable, ubicacion_id, centro_costo_id, motivo,
                usuariocreacion, fechacreacion)
        VALUES (p_company_id, p_id, current_date, p_empleado_id, v_responsable, v_cargo,
                p_ubicacion_id, p_centro_costo_id, 'Cambio registrado desde la ficha del activo',
                p_usuario, v_ahora);
    END IF;
END;
$$;

-- 5.5 Asignaciones ───────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.fn_af_activo_asignacion_listar(
    p_company_id BIGINT, p_activo_fijo_id INTEGER)
RETURNS TABLE (id INTEGER, fecha_desde DATE, fecha_hasta DATE, vigente BOOLEAN,
               empleado_id INTEGER, responsable VARCHAR, cargo_responsable VARCHAR,
               ubicacion_id INTEGER, ubicacion VARCHAR, centro_costo VARCHAR,
               motivo VARCHAR, usuariocreacion VARCHAR)
LANGUAGE sql STABLE AS $$
    SELECT s.id, s.fecha_desde, s.fecha_hasta, (s.fecha_hasta IS NULL),
           s.empleado_id,
           coalesce(nullif(btrim(coalesce(emp.nombre, '')), ''), s.responsable),
           s.cargo_responsable, s.ubicacion_id, u.nombre, cc.name, s.motivo, s.usuariocreacion
      FROM public.af_activo_asignacion s
      LEFT JOIN public.th_empleado      emp ON emp.company_id = s.company_id AND emp.id = s.empleado_id
      LEFT JOIN public.af_ubicacion     u   ON u.company_id = s.company_id AND u.id = s.ubicacion_id
      LEFT JOIN public.con_centro_costo cc  ON cc.company_id = s.company_id AND cc.cost_center_id = s.centro_costo_id
     WHERE s.company_id = p_company_id AND s.activo_fijo_id = p_activo_fijo_id
     ORDER BY s.fecha_desde DESC, s.id DESC;
$$;

CREATE OR REPLACE PROCEDURE public.sp_af_activo_asignar(
    IN p_company_id     BIGINT,
    IN p_activo_fijo_id INTEGER,
    IN p_fecha          DATE,
    IN p_empleado_id    INTEGER,
    IN p_ubicacion_id   INTEGER,
    IN p_centro_costo_id BIGINT,
    IN p_motivo         VARCHAR,
    IN p_usuario        VARCHAR)
LANGUAGE plpgsql AS $$
DECLARE
    v_estado_final BOOLEAN;
    v_fecha        DATE := coalesce(p_fecha, current_date);
    v_responsable  VARCHAR;
    v_cargo        VARCHAR;
    v_vigente      RECORD;
    v_ahora        TIMESTAMP := (now() AT TIME ZONE 'utc');
BEGIN
    SELECT coalesce(e.es_final, false) INTO v_estado_final
      FROM public.af_activo_fijo a
      LEFT JOIN public.af_estado_activo e ON e.id = a.estado_activo_id
     WHERE a.company_id = p_company_id AND a.id = p_activo_fijo_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No se encontró el activo solicitado.';
    END IF;

    IF v_estado_final THEN
        RAISE EXCEPTION 'El activo ya salió del patrimonio y no admite nuevas asignaciones.';
    END IF;

    IF v_fecha > current_date THEN
        RAISE EXCEPTION 'La fecha de asignación no puede ser futura.';
    END IF;

    IF p_empleado_id IS NOT NULL THEN
        SELECT emp.nombre, c.nombre INTO v_responsable, v_cargo
          FROM public.th_empleado emp
          LEFT JOIN public.th_cargo c ON c.company_id = emp.company_id AND c.id = emp.cargo_id
         WHERE emp.company_id = p_company_id AND emp.id = p_empleado_id;

        IF v_responsable IS NULL THEN
            RAISE EXCEPTION 'El responsable seleccionado no existe en el catálogo de empleados.';
        END IF;
    END IF;

    SELECT s.* INTO v_vigente
      FROM public.af_activo_asignacion s
     WHERE s.company_id = p_company_id AND s.activo_fijo_id = p_activo_fijo_id
       AND s.fecha_hasta IS NULL;

    IF FOUND THEN
        IF v_fecha < v_vigente.fecha_desde THEN
            RAISE EXCEPTION 'La fecha no puede ser anterior al inicio de la asignación vigente (%).', v_vigente.fecha_desde;
        END IF;

        UPDATE public.af_activo_asignacion s SET fecha_hasta = v_fecha WHERE s.id = v_vigente.id;
    END IF;

    INSERT INTO public.af_activo_asignacion
           (company_id, activo_fijo_id, fecha_desde, empleado_id, responsable,
            cargo_responsable, ubicacion_id, centro_costo_id, motivo, usuariocreacion, fechacreacion)
    VALUES (p_company_id, p_activo_fijo_id, v_fecha, p_empleado_id, v_responsable, v_cargo,
            p_ubicacion_id, p_centro_costo_id, nullif(btrim(coalesce(p_motivo, '')), ''),
            p_usuario, v_ahora);

    UPDATE public.af_activo_fijo a
       SET empleado_id = p_empleado_id,
           responsable = left(v_responsable, 80),
           cargo_responsable = left(v_cargo, 50),
           ubicacion_id = p_ubicacion_id,
           centro_costo_id = p_centro_costo_id,
           fecha_asignacion = v_fecha,
           usuariomodificacion = p_usuario,
           fechamodificacion = v_ahora
     WHERE a.company_id = p_company_id AND a.id = p_activo_fijo_id;
END;
$$;

-- 5.6 Componentes ────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.fn_af_activo_componente_listar(
    p_company_id BIGINT, p_activo_fijo_id INTEGER)
RETURNS TABLE (id INTEGER, descripcion VARCHAR, marca VARCHAR, modelo VARCHAR,
               serie VARCHAR, cantidad NUMERIC, valor NUMERIC, observacion VARCHAR)
LANGUAGE sql STABLE AS $$
    SELECT c.id, c.descripcion, c.marca, c.modelo, c.serie, c.cantidad, c.valor, c.observacion
      FROM public.af_activo_componente c
     WHERE c.company_id = p_company_id AND c.activo_fijo_id = p_activo_fijo_id
     ORDER BY c.descripcion;
$$;

CREATE OR REPLACE PROCEDURE public.sp_af_activo_componente_guardar(
    INOUT p_id             INTEGER,
    IN    p_company_id     BIGINT,
    IN    p_activo_fijo_id INTEGER,
    IN    p_descripcion    VARCHAR,
    IN    p_marca          VARCHAR,
    IN    p_modelo         VARCHAR,
    IN    p_serie          VARCHAR,
    IN    p_cantidad       NUMERIC,
    IN    p_valor          NUMERIC,
    IN    p_observacion    VARCHAR,
    IN    p_usuario        VARCHAR)
LANGUAGE plpgsql AS $$
DECLARE
    v_descripcion VARCHAR := btrim(coalesce(p_descripcion, ''));
    v_cantidad    NUMERIC := coalesce(p_cantidad, 1);
    v_valor       NUMERIC := coalesce(p_valor, 0);
BEGIN
    IF v_descripcion = '' THEN
        RAISE EXCEPTION 'La descripción del componente es obligatoria.';
    END IF;

    IF v_cantidad <= 0 THEN
        RAISE EXCEPTION 'La cantidad del componente debe ser mayor que cero.';
    END IF;

    IF v_valor < 0 THEN
        RAISE EXCEPTION 'El valor del componente no puede ser negativo.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.af_activo_fijo a
                    WHERE a.company_id = p_company_id AND a.id = p_activo_fijo_id) THEN
        RAISE EXCEPTION 'No se encontró el activo solicitado.';
    END IF;

    IF p_id IS NULL OR p_id <= 0 THEN
        INSERT INTO public.af_activo_componente
               (company_id, activo_fijo_id, descripcion, marca, modelo, serie,
                cantidad, valor, observacion, usuariocreacion, fechacreacion)
        VALUES (p_company_id, p_activo_fijo_id, v_descripcion, p_marca, p_modelo, p_serie,
                v_cantidad, v_valor, p_observacion, p_usuario, (now() AT TIME ZONE 'utc'))
        RETURNING id INTO p_id;
    ELSE
        UPDATE public.af_activo_componente c
           SET descripcion = v_descripcion, marca = p_marca, modelo = p_modelo, serie = p_serie,
               cantidad = v_cantidad, valor = v_valor, observacion = p_observacion
         WHERE c.company_id = p_company_id AND c.id = p_id AND c.activo_fijo_id = p_activo_fijo_id;

        IF NOT FOUND THEN RAISE EXCEPTION 'No se encontró el componente solicitado.'; END IF;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE public.sp_af_activo_componente_eliminar(
    IN p_company_id BIGINT, IN p_id INTEGER)
LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM public.af_activo_componente c
     WHERE c.company_id = p_company_id AND c.id = p_id;

    IF NOT FOUND THEN RAISE EXCEPTION 'No se encontró el componente solicitado.'; END IF;
END;
$$;

COMMIT;
