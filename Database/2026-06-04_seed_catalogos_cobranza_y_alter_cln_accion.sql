-- =============================================================================
-- SEED: Catálogos de cobranza + ALTER cln_accion_cobranza
-- Fecha: 2026-06-04
-- Fuente: datos del sistema legacy bdsimafi
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
-- =============================================================================

BEGIN;

-- ── 1. axl_accion_cobranza ────────────────────────────────────────────────────
-- Tabla catálogo de tipos de acción de cobranza.
-- Estaba vacía; se pobla con los datos del sistema legacy.

TRUNCATE TABLE axl_accion_cobranza RESTART IDENTITY;

INSERT INTO axl_accion_cobranza (cod_accion, nombre) VALUES
(1,  'Envio de Carta de Cobranza Prejudicial'),
(2,  'Llamado Telefónico'),
(3,  'Cobranza presencial en terreno'),
(4,  'Envío Carta de Cobranza Judicial'),
(5,  'Demanda Legal'),
(6,  'Cobranza especial Institucionales'),
(7,  'Castigo de Deuda'),
(8,  'Otras Acciones de Cobranza'),
(9,  'Certificacion de Falta de pago'),
(10, 'Bloqueo y Desbloqueo Clientes');

-- ── 2. axl_observacion_cobranza ──────────────────────────────────────────────
-- Tabla catálogo de resultados/observaciones de una acción de cobranza.
-- Estaba vacía; se pobla con los datos del sistema legacy.

TRUNCATE TABLE axl_observacion_cobranza RESTART IDENTITY;

INSERT INTO axl_observacion_cobranza (observacion) VALUES
('Efectiva'),
('Solar Baldío'),
('No quiso Recibir Nota'),
('Se dejó en la Puerta'),
('Casa Abandonada'),
('Casa Deshabitada'),
('En Construcción'),
('Número Equivocado'),
('Número Desactivado'),
('Promesa de Pago'),
('No contestó'),
('Teléfono Apagado'),
('Se le dejó mensaje'),
('Prometió pagar'),
('Entregada por Lector');

-- ── 3. abogado ────────────────────────────────────────────────────────────────
-- Reemplaza los 3 registros de prueba por los 13 abogados reales.
-- Se confirma que ningún cliente_maestro tiene abogado asignado (count=0).

TRUNCATE TABLE abogado RESTART IDENTITY CASCADE;

INSERT INTO abogado
    (abogado_codigo, abogado_nombrecorto, abogado_nombrelargo,
     estado, codcuenta, usuariocreacion, fechacreacion)
VALUES
('01', 'Abogado No.1 Miguel',    'Abogado No.1 Miguel',    true, '113-10-00-01-01', 'sistema', NOW()),
('02', 'Abogado No.2 Colomer',   'Abogado No.2 Colomer',   true, '113-10-00-01-02', 'sistema', NOW()),
('03', 'Abogado No.3 Nancy',     'Abogado No.3 Nancy',     true, '113-10-00-01-03', 'sistema', NOW()),
('04', 'Abogado No.4 Dunia',     'Abogado No.4 Dunia',     true, '113-10-00-01-04', 'sistema', NOW()),
('05', 'Abogado No.5 Jose',      'Abogado No.5 Jose',      true, '113-10-00-01-05', 'sistema', NOW()),
('06', 'Abogado No.6 Siomy',     'Abogado No.6 Siomy',     true, '113-10-00-01-07', 'sistema', NOW()),
('07', 'Abogado No.7 Yeimi',     'Abogado No.7 Yeimi',     true, '113-10-00-01-06', 'sistema', NOW()),
('08', 'Abogado No.8 Carolina',  'Abogado No.8 Carolina',  true, '113-10-00-01-08', 'sistema', NOW()),
('09', 'Abogado No.9 Carlos',    'Abogado No.9 Carlos',    true, '113-10-00-01-09', 'sistema', NOW()),
('10', 'Martinez & Landaverry',  'Martinez & Landaverry',  true, '113-10-00-01-10', 'sistema', NOW()),
('11', 'Bonificacion Empleado',  'Bonificacion Empleado',  true, '113-10-00-01-11', 'sistema', NOW()),
('12', 'Abogado No.12 Alba',     'Abogado No.12 Alba',     true, '113-10-00-01-12', 'sistema', NOW()),
('13', 'Abogado No.13 Onan',     'Abogado No.13 Onan',     true, '113-10-00-01-13', 'sistema', NOW());

-- ── 4. ALTER cln_accion_cobranza ─────────────────────────────────────────────
-- Agrega columnas para soportar el nuevo módulo unificado de acciones.

ALTER TABLE cln_accion_cobranza
    ADD COLUMN IF NOT EXISTS cod_accion      INTEGER,
    ADD COLUMN IF NOT EXISTS cod_observacion INTEGER,
    ADD COLUMN IF NOT EXISTS abogado_id      INTEGER,
    ADD COLUMN IF NOT EXISTS ejecutado_por   VARCHAR(100);

-- Índices útiles para consultas del módulo
CREATE INDEX IF NOT EXISTS ix_cln_accion_cobranza_cod_accion
    ON cln_accion_cobranza(cod_accion);

COMMENT ON COLUMN cln_accion_cobranza.cod_accion
    IS 'FK → axl_accion_cobranza.cod_accion';
COMMENT ON COLUMN cln_accion_cobranza.cod_observacion
    IS 'FK → axl_observacion_cobranza.id';
COMMENT ON COLUMN cln_accion_cobranza.abogado_id
    IS 'FK → abogado.abogado_id (abogado asignado al momento de la acción)';
COMMENT ON COLUMN cln_accion_cobranza.ejecutado_por
    IS 'Usuario que ejecutó la acción (auto desde sesión)';

COMMIT;
