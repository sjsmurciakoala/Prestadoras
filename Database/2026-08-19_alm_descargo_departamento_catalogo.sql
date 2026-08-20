-- =============================================================================
-- Descargo: el departamento se elige del catálogo de Talento Humano (y es obligatorio)
-- Fecha: 2026-08-19
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- En el descargo directo y la entrega de requisición, "quién recibe" pasa a ser obligatorio
-- y se agrega la selección del DEPARTAMENTO donde se entrega, tomado del catálogo th_departamento
-- (el mismo del empleado y de la requisición), guardando el NOMBRE. Para que quepan los nombres
-- ("Almacén", "Contabilidad"...) se amplía la columna de VARCHAR(3) a VARCHAR(80).
--
-- Solo la CABECERA guarda el departamento (alm_descargo_hdr): las líneas planas (alm_descargo)
-- se dejan en NULL por diseño, así que no se tocan.
--
-- ADITIVO / bajo riesgo: WIDENING (3 → 80). No trunca ni reescribe datos: los códigos legacy de
-- 3 letras del histórico SIMAFI siguen cabiendo. Re-ejecutar al mismo tipo es un no-op.
-- =============================================================================
BEGIN;

ALTER TABLE alm_descargo_hdr ALTER COLUMN departamento TYPE VARCHAR(80);

COMMENT ON COLUMN alm_descargo_hdr.departamento IS 'Departamento donde se entrega. Se elige del catálogo th_departamento (se guarda el nombre); obligatorio en las entregas nuevas. El histórico SIMAFI conserva su código legacy de 3 letras.';

COMMIT;
