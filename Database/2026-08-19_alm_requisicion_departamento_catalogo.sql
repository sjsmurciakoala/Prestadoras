-- =============================================================================
-- Requisiciones: el departamento pasa a elegirse del catálogo de Talento Humano
-- Fecha: 2026-08-19
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- El control "Departamento" de la requisición deja de ser un código libre de 3 letras y pasa
-- a listar los departamentos del catálogo th_departamento (mismo que usa el empleado), guardando
-- el NOMBRE. Para que quepan los nombres ("Almacén", "Contabilidad"...) se amplía la columna de
-- VARCHAR(3) a VARCHAR(80).
--
-- ADITIVO / bajo riesgo: es un WIDENING (3 → 80). No trunca ni reescribe datos: los códigos
-- legacy de 3 letras del histórico SIMAFI siguen cabiendo tal cual. IDEMPOTENTE de facto
-- (re-ejecutar un ALTER al mismo tipo es un no-op).
--
-- NO cambia el histórico ni agrega FK: las requisiciones ya capturadas conservan su texto; las
-- nuevas guardan el nombre del departamento elegido del catálogo.
--
-- Se amplían LAS DOS tablas: la cabecera (alm_requisicion_hdr) y la tabla plana de renglones
-- (alm_requisicion), porque el servicio copia el departamento de la cabecera a cada renglón.
-- =============================================================================
BEGIN;

ALTER TABLE alm_requisicion_hdr ALTER COLUMN departamento TYPE VARCHAR(80);
ALTER TABLE alm_requisicion     ALTER COLUMN departamento TYPE VARCHAR(80);

COMMENT ON COLUMN alm_requisicion_hdr.departamento IS 'Departamento de la requisición. Se elige del catálogo th_departamento (se guarda el nombre). El histórico SIMAFI conserva su código legacy de 3 letras.';
COMMENT ON COLUMN alm_requisicion.departamento IS 'Departamento del renglón (copiado de la cabecera). Ampliado a 80 para el nombre del catálogo th_departamento.';

COMMIT;
