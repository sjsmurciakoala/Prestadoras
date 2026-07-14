-- =============================================================================
-- Almacén: FK compuestas tenant-safe (company_id, id) hacia alm_bodega y alm_articulo
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- POR QUÉ
-- El sistema es multiempresa: toda tabla funcional se filtra por company_id
-- (SiadDbContext.Tenancy.cs aplica un global query filter y estampa company_id al
-- insertar). Ese filtro protege las LECTURAS, no la integridad referencial.
--
-- Hasta hoy las FK del módulo apuntaban solo al id del padre:
--     FOREIGN KEY (bodega_id)   REFERENCES alm_bodega(id)
--     FOREIGN KEY (articulo_id) REFERENCES alm_articulo(id)
-- Es decir: la BD acepta un bodega_id (o articulo_id) que pertenece a OTRA empresa.
-- Basta un bug de servicio, un DTO que se cuele con el id del cliente equivocado o
-- una carga/script manual para postear existencia y asientos de kardex contra la
-- bodega de otro cliente. En un motor de stock eso es corrupción silenciosa y
-- prácticamente irreversible (el kardex es inmutable).
--
-- Con la FK compuesta (company_id, columna) -> padre(company_id, id), la BD garantiza
-- que el hijo y su padre viven SIEMPRE en la misma empresa. Es la contraparte en BD
-- del query filter: defensa en profundidad.
--
-- Verificado contra el mirror antes de escribir este script: CERO violaciones
-- cross-tenant hoy (ver consulta de verificación al final), así que las constraints
-- se crean sin conflicto y sin tocar un solo dato.
--
-- QUÉ HACE
--   1. Claves alternas UNIQUE (company_id, id) en alm_bodega y alm_articulo — un
--      padre solo puede ser referenciado por una FK compuesta si expone esas dos
--      columnas como única.
--   2. Reemplaza 11 FK simples por su equivalente compuesta, PRESERVANDO la regla
--      ON DELETE de cada una y el NOMBRE de la constraint (para no romper nada que
--      los referencie: scripts, mensajes de error, mapeos).
--
-- REGLAS ON DELETE PRESERVADAS (tal como están hoy en la BD):
--   alm_kardex.bodega_id ................ RESTRICT   (2026-07-09_alm_kardex_bodega_id.sql)
--   alm_kardex.bodega_destino_id ........ NO ACTION  (2026-07-14_alm_kardex_trazabilidad.sql: sin cláusula)
--   alm_kardex.articulo_id .............. RESTRICT   (2026-07-14_alm_kardex_fk_articulo_restrict.sql)
--   alm_articulo_bodega.bodega_id ....... RESTRICT   (2026-07-07_alm_articulo_bodega.sql)
--   alm_articulo_bodega.articulo_id ..... CASCADE    (2026-07-07_alm_articulo_bodega.sql)
--   alm_compra.bodega_id ................ RESTRICT   (2026-07-14_alm_documentos_bodega_posteo.sql)
--   alm_compra.articulo_id .............. SET NULL   (2026-07-13_alm_movimientos_articulo_id.sql)
--   alm_descargo.bodega_id .............. RESTRICT   (2026-07-14_alm_documentos_bodega_posteo.sql)
--   alm_descargo.articulo_id ............ SET NULL   (2026-07-13_alm_movimientos_articulo_id.sql)
--   alm_requisicion.bodega_id ........... RESTRICT   (2026-07-14_alm_documentos_bodega_posteo.sql)
--   alm_requisicion.articulo_id ......... SET NULL   (2026-07-13_alm_movimientos_articulo_id.sql)
--
-- EL PROBLEMA DEL "SET NULL" COMPUESTO (y cómo se resuelve)
-- company_id es BIGINT NOT NULL. Un ON DELETE SET NULL clásico sobre una FK
-- compuesta intentaría anular TODAS las columnas de la FK — incluida company_id —
-- y reventaría con violación de NOT NULL al borrar el artículo padre.
-- PostgreSQL 15 introdujo la lista de columnas en la acción referencial:
--     ON DELETE SET NULL (articulo_id)
-- que anula SOLO la columna indicada y deja company_id intacto. La BD es PG 15, así
-- que se usa esa forma en las tres FK articulo_id de compras/descargos/requisiciones.
-- El resultado post-borrado, (company_id, NULL), satisface la FK por MATCH SIMPLE
-- (si alguna columna de la FK es NULL, la restricción se da por cumplida).
-- >>> Si esto se aplicara alguna vez a un servidor PG < 15, el ADD CONSTRAINT fallaría
--     con error de sintaxis. Verificar con: SHOW server_version;
--
-- ORDEN DE LOS STATEMENTS (por qué es re-ejecutable)
-- Primero se sueltan las 11 FK, luego se recrean las claves alternas, y al final se
-- crean las FK compuestas. Hacerlo en ese orden es lo que permite re-correr el script:
-- si las FK ya existieran, un DROP CONSTRAINT sobre uq_alm_bodega_company_id fallaría
-- ("otros objetos dependen de él"). Sueltas primero las FK, la clave alterna queda sin
-- dependientes y se puede recrear limpia.
--
-- TRIGGER DE INMUTABILIDAD DEL KARDEX (trg_alm_kardex_inmutable, SQLSTATE K0001)
-- No interfiere. Es un trigger BEFORE UPDATE OR DELETE FOR EACH ROW: solo se dispara
-- si alguien modifica o borra FILAS. ALTER TABLE ... ADD CONSTRAINT FOREIGN KEY solo
-- VALIDA las filas existentes (un SELECT interno contra el padre); no las reescribe,
-- así que no hay evento UPDATE/DELETE que dispare el trigger. Lo mismo aplica al DROP.
-- Nota de diseño relacionada: las FK de alm_kardex hacia articulo/bodega son RESTRICT
-- justamente para no caer nunca en un SET NULL, que SÍ sería un UPDATE y SÍ chocaría
-- contra el trigger.
--
-- No altera datos: solo cambia la forma de las FK y agrega dos claves alternas.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Soltar las FK simples (antes que las claves alternas: ver nota de orden arriba)
-- -----------------------------------------------------------------------------
ALTER TABLE alm_kardex          DROP CONSTRAINT IF EXISTS alm_kardex_bodega_id_fkey;
ALTER TABLE alm_kardex          DROP CONSTRAINT IF EXISTS alm_kardex_bodega_destino_id_fkey;
ALTER TABLE alm_kardex          DROP CONSTRAINT IF EXISTS alm_kardex_articulo_id_fkey;
ALTER TABLE alm_articulo_bodega DROP CONSTRAINT IF EXISTS alm_articulo_bodega_bodega_id_fkey;
ALTER TABLE alm_articulo_bodega DROP CONSTRAINT IF EXISTS alm_articulo_bodega_articulo_id_fkey;
ALTER TABLE alm_compra          DROP CONSTRAINT IF EXISTS alm_compra_bodega_id_fkey;
ALTER TABLE alm_compra          DROP CONSTRAINT IF EXISTS alm_compra_articulo_id_fkey;
ALTER TABLE alm_descargo        DROP CONSTRAINT IF EXISTS alm_descargo_bodega_id_fkey;
ALTER TABLE alm_descargo        DROP CONSTRAINT IF EXISTS alm_descargo_articulo_id_fkey;
ALTER TABLE alm_requisicion     DROP CONSTRAINT IF EXISTS alm_requisicion_bodega_id_fkey;
ALTER TABLE alm_requisicion     DROP CONSTRAINT IF EXISTS alm_requisicion_articulo_id_fkey;

-- -----------------------------------------------------------------------------
-- 2) Claves alternas en los padres: (company_id, id) UNIQUE
--    Son el destino de las FK compuestas. La PK (id) se conserva intacta.
-- -----------------------------------------------------------------------------
ALTER TABLE alm_bodega   DROP CONSTRAINT IF EXISTS uq_alm_bodega_company_id;
ALTER TABLE alm_bodega   ADD  CONSTRAINT uq_alm_bodega_company_id   UNIQUE (company_id, id);

ALTER TABLE alm_articulo DROP CONSTRAINT IF EXISTS uq_alm_articulo_company_id;
ALTER TABLE alm_articulo ADD  CONSTRAINT uq_alm_articulo_company_id UNIQUE (company_id, id);

COMMENT ON CONSTRAINT uq_alm_bodega_company_id   ON alm_bodega   IS 'Clave alterna tenant-safe: destino de las FK compuestas (company_id, bodega_id) del módulo Almacén. No borrar sin recrear esas FK.';
COMMENT ON CONSTRAINT uq_alm_articulo_company_id ON alm_articulo IS 'Clave alterna tenant-safe: destino de las FK compuestas (company_id, articulo_id) del módulo Almacén. No borrar sin recrear esas FK.';

-- -----------------------------------------------------------------------------
-- 3) FK compuestas (mismos nombres, mismas reglas ON DELETE)
-- -----------------------------------------------------------------------------

-- ── alm_kardex ──────────────────────────────────────────────────────────────
ALTER TABLE alm_kardex
    ADD CONSTRAINT alm_kardex_bodega_id_fkey
    FOREIGN KEY (company_id, bodega_id) REFERENCES alm_bodega(company_id, id)
    ON DELETE RESTRICT;

-- bodega_destino_id nació sin cláusula ON DELETE => NO ACTION. Se preserva tal cual
-- (NO ACTION se diferencia de RESTRICT solo en que su chequeo puede diferirse).
ALTER TABLE alm_kardex
    ADD CONSTRAINT alm_kardex_bodega_destino_id_fkey
    FOREIGN KEY (company_id, bodega_destino_id) REFERENCES alm_bodega(company_id, id);

ALTER TABLE alm_kardex
    ADD CONSTRAINT alm_kardex_articulo_id_fkey
    FOREIGN KEY (company_id, articulo_id) REFERENCES alm_articulo(company_id, id)
    ON DELETE RESTRICT;

-- ── alm_articulo_bodega ─────────────────────────────────────────────────────
ALTER TABLE alm_articulo_bodega
    ADD CONSTRAINT alm_articulo_bodega_bodega_id_fkey
    FOREIGN KEY (company_id, bodega_id) REFERENCES alm_bodega(company_id, id)
    ON DELETE RESTRICT;

ALTER TABLE alm_articulo_bodega
    ADD CONSTRAINT alm_articulo_bodega_articulo_id_fkey
    FOREIGN KEY (company_id, articulo_id) REFERENCES alm_articulo(company_id, id)
    ON DELETE CASCADE;

-- ── alm_compra ──────────────────────────────────────────────────────────────
ALTER TABLE alm_compra
    ADD CONSTRAINT alm_compra_bodega_id_fkey
    FOREIGN KEY (company_id, bodega_id) REFERENCES alm_bodega(company_id, id)
    ON DELETE RESTRICT;

-- SET NULL con lista de columnas (PG 15+): anula SOLO articulo_id; company_id es
-- NOT NULL y no debe tocarse. Sin la lista, el borrado del artículo fallaría con
-- violación de NOT NULL sobre company_id.
ALTER TABLE alm_compra
    ADD CONSTRAINT alm_compra_articulo_id_fkey
    FOREIGN KEY (company_id, articulo_id) REFERENCES alm_articulo(company_id, id)
    ON DELETE SET NULL (articulo_id);

-- ── alm_descargo ────────────────────────────────────────────────────────────
ALTER TABLE alm_descargo
    ADD CONSTRAINT alm_descargo_bodega_id_fkey
    FOREIGN KEY (company_id, bodega_id) REFERENCES alm_bodega(company_id, id)
    ON DELETE RESTRICT;

ALTER TABLE alm_descargo
    ADD CONSTRAINT alm_descargo_articulo_id_fkey
    FOREIGN KEY (company_id, articulo_id) REFERENCES alm_articulo(company_id, id)
    ON DELETE SET NULL (articulo_id);

-- ── alm_requisicion ─────────────────────────────────────────────────────────
ALTER TABLE alm_requisicion
    ADD CONSTRAINT alm_requisicion_bodega_id_fkey
    FOREIGN KEY (company_id, bodega_id) REFERENCES alm_bodega(company_id, id)
    ON DELETE RESTRICT;

ALTER TABLE alm_requisicion
    ADD CONSTRAINT alm_requisicion_articulo_id_fkey
    FOREIGN KEY (company_id, articulo_id) REFERENCES alm_articulo(company_id, id)
    ON DELETE SET NULL (articulo_id);

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano después de aplicar; NO forma parte de la transacción)
-- =============================================================================
--
-- 0) La BD debe ser PostgreSQL 15 o superior (por ON DELETE SET NULL (columna)).
-- SHOW server_version;
--
-- 1) Cero violaciones cross-tenant. Con las FK compuestas ya creadas debe dar 0 en
--    las 11 filas: si algo aparece aquí, es que la constraint NO se creó.
-- WITH pares AS (
--     SELECT 'alm_kardex.bodega_id'            AS fk, k.company_id, k.bodega_id         AS hijo_id, b.company_id AS padre_company FROM alm_kardex          k JOIN alm_bodega   b ON b.id = k.bodega_id
--     UNION ALL
--     SELECT 'alm_kardex.bodega_destino_id',        k.company_id, k.bodega_destino_id,     b.company_id            FROM alm_kardex          k JOIN alm_bodega   b ON b.id = k.bodega_destino_id
--     UNION ALL
--     SELECT 'alm_kardex.articulo_id',              k.company_id, k.articulo_id,           a.company_id            FROM alm_kardex          k JOIN alm_articulo a ON a.id = k.articulo_id
--     UNION ALL
--     SELECT 'alm_articulo_bodega.bodega_id',       x.company_id, x.bodega_id,             b.company_id            FROM alm_articulo_bodega x JOIN alm_bodega   b ON b.id = x.bodega_id
--     UNION ALL
--     SELECT 'alm_articulo_bodega.articulo_id',     x.company_id, x.articulo_id,           a.company_id            FROM alm_articulo_bodega x JOIN alm_articulo a ON a.id = x.articulo_id
--     UNION ALL
--     SELECT 'alm_compra.bodega_id',                x.company_id, x.bodega_id,             b.company_id            FROM alm_compra          x JOIN alm_bodega   b ON b.id = x.bodega_id
--     UNION ALL
--     SELECT 'alm_compra.articulo_id',              x.company_id, x.articulo_id,           a.company_id            FROM alm_compra          x JOIN alm_articulo a ON a.id = x.articulo_id
--     UNION ALL
--     SELECT 'alm_descargo.bodega_id',              x.company_id, x.bodega_id,             b.company_id            FROM alm_descargo        x JOIN alm_bodega   b ON b.id = x.bodega_id
--     UNION ALL
--     SELECT 'alm_descargo.articulo_id',            x.company_id, x.articulo_id,           a.company_id            FROM alm_descargo        x JOIN alm_articulo a ON a.id = x.articulo_id
--     UNION ALL
--     SELECT 'alm_requisicion.bodega_id',           x.company_id, x.bodega_id,             b.company_id            FROM alm_requisicion     x JOIN alm_bodega   b ON b.id = x.bodega_id
--     UNION ALL
--     SELECT 'alm_requisicion.articulo_id',         x.company_id, x.articulo_id,           a.company_id            FROM alm_requisicion     x JOIN alm_articulo a ON a.id = x.articulo_id
-- )
-- SELECT fk, count(*) AS violaciones
-- FROM pares
-- WHERE company_id <> padre_company
-- GROUP BY fk
-- ORDER BY fk;   -- resultado esperado: 0 filas
--
-- 2) Forma final de las FK: deben ser todas compuestas (company_id, ...) y conservar
--    su regla de borrado (RESTRICT / CASCADE / SET NULL (columna) / sin cláusula = NO ACTION).
-- SELECT conrelid::regclass AS tabla, conname, pg_get_constraintdef(oid) AS definicion
-- FROM pg_constraint
-- WHERE conrelid IN ('alm_kardex'::regclass, 'alm_articulo_bodega'::regclass,
--                    'alm_compra'::regclass, 'alm_descargo'::regclass,
--                    'alm_requisicion'::regclass)
--   AND contype = 'f'
-- ORDER BY conrelid::text, conname;
--
-- 3) Las claves alternas de los padres existen.
-- SELECT conrelid::regclass AS tabla, conname, pg_get_constraintdef(oid) AS definicion
-- FROM pg_constraint
-- WHERE conname IN ('uq_alm_bodega_company_id', 'uq_alm_articulo_company_id');
-- =============================================================================
