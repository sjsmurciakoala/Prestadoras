-- =============================================================================
-- Talento Humano: catálogos de Cargos y Departamentos (th_cargo, th_departamento)
-- Fecha: 2026-08-19
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- El empleado deja de guardar cargo/departamento como texto libre y pasa a elegirlos de
-- un catálogo (selección ESTRICTA en el formulario). th_empleado gana cargo_id y
-- departamento_id (FK compuestas por empresa, nullable = "sin asignar").
--
-- Migración incluida (idempotente): siembra cada catálogo con los valores DISTINTOS que
-- ya están en uso en th_empleado.cargo / th_empleado.departamento y enlaza cada empleado
-- a su id. Las columnas de texto cargo/departamento se CONSERVAN por ahora (snapshot/legacy);
-- su DROP se decidirá aparte.
--
-- ADITIVO / bajo riesgo: CREATE TABLE nuevas + ADD COLUMN nullable con FK + datos derivados
-- de lo ya existente. IDEMPOTENTE: CREATE/ADD IF [NOT] EXISTS, INSERT ... ON CONFLICT DO
-- NOTHING, UPDATE por coincidencia de nombre.
-- =============================================================================
BEGIN;

-- 1. Catálogo de cargos -------------------------------------------------------
CREATE TABLE IF NOT EXISTS th_cargo (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    nombre              VARCHAR(80)   NOT NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_th_cargo_company_nombre UNIQUE (company_id, nombre),
    CONSTRAINT uq_th_cargo_tenant         UNIQUE (company_id, id)
);
CREATE INDEX IF NOT EXISTS ix_th_cargo_company ON th_cargo(company_id);

-- 2. Catálogo de departamentos ------------------------------------------------
CREATE TABLE IF NOT EXISTS th_departamento (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    nombre              VARCHAR(80)   NOT NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_th_departamento_company_nombre UNIQUE (company_id, nombre),
    CONSTRAINT uq_th_departamento_tenant         UNIQUE (company_id, id)
);
CREATE INDEX IF NOT EXISTS ix_th_departamento_company ON th_departamento(company_id);

-- 3. FK en el empleado (nullable = "sin asignar") -----------------------------
ALTER TABLE th_empleado ADD COLUMN IF NOT EXISTS cargo_id        INTEGER NULL;
ALTER TABLE th_empleado ADD COLUMN IF NOT EXISTS departamento_id INTEGER NULL;

-- FK compuestas por empresa: un empleado no puede apuntar a un cargo de otra empresa.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_th_empleado_cargo') THEN
        ALTER TABLE th_empleado
            ADD CONSTRAINT fk_th_empleado_cargo
            FOREIGN KEY (company_id, cargo_id) REFERENCES th_cargo (company_id, id) ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_th_empleado_departamento') THEN
        ALTER TABLE th_empleado
            ADD CONSTRAINT fk_th_empleado_departamento
            FOREIGN KEY (company_id, departamento_id) REFERENCES th_departamento (company_id, id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_th_empleado_cargo        ON th_empleado(cargo_id);
CREATE INDEX IF NOT EXISTS ix_th_empleado_departamento ON th_empleado(departamento_id);

-- 4. Migración: sembrar catálogos con lo ya en uso y enlazar ------------------
INSERT INTO th_cargo (company_id, nombre)
SELECT DISTINCT e.company_id, btrim(e.cargo)
  FROM th_empleado e
 WHERE e.cargo IS NOT NULL AND btrim(e.cargo) <> ''
ON CONFLICT (company_id, nombre) DO NOTHING;

INSERT INTO th_departamento (company_id, nombre)
SELECT DISTINCT e.company_id, btrim(e.departamento)
  FROM th_empleado e
 WHERE e.departamento IS NOT NULL AND btrim(e.departamento) <> ''
ON CONFLICT (company_id, nombre) DO NOTHING;

UPDATE th_empleado e
   SET cargo_id = c.id
  FROM th_cargo c
 WHERE c.company_id = e.company_id
   AND lower(c.nombre) = lower(btrim(e.cargo))
   AND e.cargo IS NOT NULL AND btrim(e.cargo) <> ''
   AND e.cargo_id IS NULL;

UPDATE th_empleado e
   SET departamento_id = d.id
  FROM th_departamento d
 WHERE d.company_id = e.company_id
   AND lower(d.nombre) = lower(btrim(e.departamento))
   AND e.departamento IS NOT NULL AND btrim(e.departamento) <> ''
   AND e.departamento_id IS NULL;

COMMENT ON TABLE th_cargo IS 'Catálogo de cargos de empleados (Talento Humano), por empresa. Se elige de forma estricta en el maestro de empleados.';
COMMENT ON TABLE th_departamento IS 'Catálogo de departamentos de empleados (Talento Humano), por empresa. Se elige de forma estricta en el maestro de empleados.';
COMMENT ON COLUMN th_empleado.cargo_id IS 'Cargo del empleado (FK th_cargo, por empresa). NULL = sin asignar.';
COMMENT ON COLUMN th_empleado.departamento_id IS 'Departamento del empleado (FK th_departamento, por empresa). NULL = sin asignar.';

COMMIT;
