-- =============================================================================
-- Talento Humano: catálogo de empleados (th_empleado)
-- Fecha: 2026-08-19
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Catálogo base del nuevo módulo Talento Humano. Primer consumidor: el campo "Recibe"
-- del Descargo de almacén (alm_descargo_hdr.recibido_por), que pasa de texto 100% libre
-- a un combo con autocompletar alimentado por este catálogo — sin perder la opción de
-- texto libre: alm_descargo_hdr.recibido_por sigue siendo VARCHAR(120), sin FK.
--
-- ADITIVO / bajo riesgo: solo CREATE TABLE nueva + ADD COLUMN nullable. No toca ninguna
-- tabla existente. SIN seed: el catálogo arranca vacío; se llena a mano o importando un
-- Excel desde el maestro de empleados.
--
-- IDEMPOTENTE: CREATE/ADD IF [NOT] EXISTS. El bloque ALTER cubre las máquinas donde la tabla
-- ya se creó con la versión inicial (código + nombre + activo) antes de agregar identidad,
-- cargo y departamento; en una máquina nueva el CREATE ya trae las tres columnas.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS th_empleado (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    codigo              VARCHAR(20)   NOT NULL,
    codigo_simafi       VARCHAR(30)   NULL,
    nombre              VARCHAR(120)  NOT NULL,
    identidad           VARCHAR(20)   NULL,
    cargo               VARCHAR(80)   NULL,
    departamento        VARCHAR(80)   NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_th_empleado_company_codigo UNIQUE (company_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_th_empleado_company ON th_empleado(company_id);

-- Identidad, cargo, departamento y código SIMAFI (aditivo, para tablas ya creadas antes).
ALTER TABLE th_empleado ADD COLUMN IF NOT EXISTS identidad     VARCHAR(20) NULL;
ALTER TABLE th_empleado ADD COLUMN IF NOT EXISTS cargo         VARCHAR(80) NULL;
ALTER TABLE th_empleado ADD COLUMN IF NOT EXISTS departamento  VARCHAR(80) NULL;
ALTER TABLE th_empleado ADD COLUMN IF NOT EXISTS codigo_simafi VARCHAR(30) NULL;

-- El código SIMAFI, cuando viene, identifica al empleado en el origen (clave del upsert de la
-- importación Excel): único por empresa entre los que sí lo traen. Los creados a mano lo dejan NULL.
CREATE UNIQUE INDEX IF NOT EXISTS uq_th_empleado_company_simafi
    ON th_empleado(company_id, codigo_simafi) WHERE codigo_simafi IS NOT NULL;

COMMENT ON TABLE th_empleado IS 'Catálogo de empleados (módulo Talento Humano), por empresa. Primer consumidor: el campo "Recibe" del Descargo de almacén.';
COMMENT ON COLUMN th_empleado.codigo IS 'Código interno del empleado, AUTOGENERADO como correlativo por empresa (no lo escribe el usuario). Único por empresa.';
COMMENT ON COLUMN th_empleado.codigo_simafi IS 'Código del empleado en SIMAFI (origen). Solo lectura en la UI; solo se puebla al importar desde Excel. Único por empresa cuando no es NULL.';
COMMENT ON COLUMN th_empleado.identidad IS 'Número de identidad (DNI). Opcional; sin unicidad forzada para no bloquear importaciones con datos incompletos.';
COMMENT ON COLUMN th_empleado.cargo IS 'Cargo o puesto del empleado. Texto libre, opcional.';
COMMENT ON COLUMN th_empleado.departamento IS 'Departamento organizacional del empleado. Texto libre, opcional.';
COMMENT ON COLUMN th_empleado.activo IS 'Empleado disponible para elegir en los combos que consumen este catálogo.';

COMMIT;
