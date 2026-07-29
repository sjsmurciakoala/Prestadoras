-- =============================================================================
-- Proveedores: contactos por proveedor + catálogo de tipos de contacto
-- Fecha: 2026-07-27
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ ESTAS TABLAS
-- Un proveedor tenía UN solo contacto, guardado como columnas sueltas en
-- prv_proveedores (nombre_contacto, telefono, email). En la práctica un proveedor
-- tiene varias personas — ventas, cobros, soporte — y no había dónde registrarlas.
--
-- COMPANY_ID EN LA TABLA HIJA
-- El correlativo del proveedor se genera por empresa, así que cod_proveedor se
-- REPITE entre empresas. Colgar los contactos solo de cod_proveedor los volvería
-- visibles entre tenants. Por eso company_id va en la hija y las entidades
-- implementan ICompanyScopedEntity (query filter global de SiadDbContext).
--
-- SIN FK A prv_proveedores: esa tabla no declara PK (entidad keyless en EF), así
-- que no hay a qué apuntar. Mismo caso que prv_proveedor_cuenta_bancaria.
--
-- CAMPOS LEGACY: prv_proveedores.nombre_contacto/telefono/email NO se tocan. El
-- servicio los mantiene sincronizados con el contacto de orden = 1.
--
-- Cambio aditivo: dos tablas nuevas. No altera ninguna tabla ni dato existente.
-- Re-ejecutable de punta a punta: los CREATE llevan IF NOT EXISTS y los dos
-- INSERT están guardados con NOT EXISTS.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS prv_tipo_contacto (
    tipo_contacto_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         BIGINT       NOT NULL,
    nombre             VARCHAR(60)  NOT NULL,
    observaciones      VARCHAR(250),
    activo             BOOLEAN      NOT NULL DEFAULT TRUE,
    fecha_creacion     TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    usuario_creo       VARCHAR(100) NOT NULL,
    fecha_modificacion TIMESTAMP WITHOUT TIME ZONE,
    usuario_modifica   VARCHAR(100),
    rowid              UUID         NOT NULL DEFAULT gen_random_uuid()
);

-- Nombre único por empresa, sin distinguir mayúsculas ni espacios al borde: el
-- usuario que teclea " ventas " no debe poder duplicar "Ventas". Es índice de
-- expresión (no UNIQUE constraint) precisamente porque normaliza el valor.
CREATE UNIQUE INDEX IF NOT EXISTS uq_prv_tipo_contacto_nombre
    ON prv_tipo_contacto (company_id, upper(btrim(nombre)));

CREATE TABLE IF NOT EXISTS prv_proveedor_contacto (
    proveedor_contacto_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id            BIGINT       NOT NULL,
    cod_proveedor         VARCHAR(20)  NOT NULL,
    tipo_contacto_id      BIGINT,
    nombre                VARCHAR(150) NOT NULL,
    cargo                 VARCHAR(100),
    telefono              VARCHAR(30),
    extension             VARCHAR(10),
    celular               VARCHAR(30),
    email                 VARCHAR(150),
    observaciones         VARCHAR(500),
    orden                 INT          NOT NULL DEFAULT 1,
    fecha_creacion        TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    usuario_creo          VARCHAR(100) NOT NULL,
    fecha_modificacion    TIMESTAMP WITHOUT TIME ZONE,
    usuario_modifica      VARCHAR(100),
    rowid                 UUID         NOT NULL DEFAULT gen_random_uuid(),

    -- RESTRICT: no se borra un tipo que esté asignado a algún contacto.
    -- El servicio da el mensaje amigable; esto es defensa en profundidad.
    -- La FK NO lleva company_id porque prv_tipo_contacto tiene PK simple; el
    -- aislamiento entre empresas lo garantiza el query filter global de EF.
    CONSTRAINT fk_prv_proveedor_contacto_tipo
        FOREIGN KEY (tipo_contacto_id)
        REFERENCES prv_tipo_contacto (tipo_contacto_id)
        ON DELETE RESTRICT
);

-- Sirve al listado de contactos de un proveedor ya ordenado (la consulta del
-- detalle filtra por empresa + código y ordena por orden).
CREATE INDEX IF NOT EXISTS ix_prv_proveedor_contacto_proveedor
    ON prv_proveedor_contacto (company_id, cod_proveedor, orden);

COMMENT ON TABLE  prv_tipo_contacto IS
    'Catálogo de tipos de contacto de proveedor (Ventas, Cobros, ...), por empresa.';
COMMENT ON COLUMN prv_tipo_contacto.activo IS
    'FALSE retira el tipo de los combos sin borrarlo ni afectar contactos ya asignados.';
COMMENT ON TABLE  prv_proveedor_contacto IS
    'Contactos de un proveedor. Sin FK a prv_proveedores porque esa tabla no declara PK. El contacto de orden=1 se replica en prv_proveedores.nombre_contacto/telefono/email.';
COMMENT ON COLUMN prv_proveedor_contacto.company_id IS
    'Obligatorio: cod_proveedor se repite entre empresas (correlativo por empresa).';
COMMENT ON COLUMN prv_proveedor_contacto.orden IS
    'Posición en el grid del formulario. El orden 1 alimenta los campos legacy del proveedor.';
COMMENT ON COLUMN prv_proveedor_contacto.tipo_contacto_id IS
    'Opcional. NULL = contacto sin clasificar.';

-- Semilla del catálogo: una fila por empresa que ya tenga proveedores. Se siembra
-- por empresa (y no global) porque el catálogo es tenant-scoped: cada empresa
-- edita y desactiva los suyos sin afectar a las demás.
-- Los 5 nombres del VALUES son distintos entre sí, así que el NOT EXISTS (que ve
-- la tabla como estaba al inicio del statement) alcanza para la idempotencia.
INSERT INTO prv_tipo_contacto (company_id, nombre, usuario_creo)
SELECT c.company_id, t.nombre, 'system'
FROM (SELECT DISTINCT company_id FROM prv_proveedores) c
CROSS JOIN (VALUES ('Ventas'), ('Cobros'), ('Gerencia'), ('Soporte técnico'), ('Administración')) AS t(nombre)
WHERE NOT EXISTS (
    SELECT 1 FROM prv_tipo_contacto x
    WHERE x.company_id = c.company_id
      AND upper(btrim(x.nombre)) = upper(btrim(t.nombre))
);

-- Migración: el contacto que hoy vive en las columnas sueltas pasa a ser el #1.
-- Idempotente: no inserta si el proveedor ya tiene contactos.
-- BORDE CONOCIDO: el guard mira "¿tiene contactos?", no "¿ya se migró?". Si alguien
-- borra a propósito TODOS los contactos de un proveedor cuyo nombre_contacto legacy
-- sigue con valor, una re-corrida del script le recrea el contacto migrado. Es
-- aceptable porque el script se corre una vez por ambiente, pero conviene saberlo.
-- usuario_creo = 'migracion' para distinguir en la bitácora lo que vino del
-- legacy de lo que capturó una persona (y para que la verificación (5) pueda
-- contar solo lo migrado).
-- El cod_proveedor se copia TAL CUAL (sin btrim) para que la fila hija apunte
-- exactamente al mismo valor que tiene el padre: si hubiera códigos con espacios
-- al borde, recortarlos aquí desacoplaría el hijo del padre. Ver verificación (5).
INSERT INTO prv_proveedor_contacto
    (company_id, cod_proveedor, nombre, telefono, email, orden, fecha_creacion, usuario_creo)
SELECT p.company_id,
       p.cod_proveedor,
       btrim(p.nombre_contacto),
       NULLIF(btrim(COALESCE(p.telefono, '')), ''),
       NULLIF(btrim(COALESCE(p.email, '')), ''),
       1,
       COALESCE(p.fecha_modificacion, p.fecha_creacion, now()),
       'migracion'
FROM prv_proveedores p
WHERE btrim(COALESCE(p.nombre_contacto, '')) <> ''
  AND NOT EXISTS (
      SELECT 1 FROM prv_proveedor_contacto d
      WHERE d.company_id = p.company_id
        AND d.cod_proveedor = p.cod_proveedor
  );

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Columnas de la tabla hija:
-- SELECT column_name, data_type, character_maximum_length, is_nullable, column_default
--   FROM information_schema.columns
--  WHERE table_name='prv_proveedor_contacto' ORDER BY ordinal_position;
--   -> 17 columnas; proveedor_contacto_id identity, orden default 1,
--      fecha_creacion default now(), rowid default gen_random_uuid().
--
-- 1b) Columnas del catálogo:
-- SELECT column_name, data_type, character_maximum_length, is_nullable, column_default
--   FROM information_schema.columns
--  WHERE table_name='prv_tipo_contacto' ORDER BY ordinal_position;
--   -> 10 columnas; activo default true.
--
-- 2) Constraints:
-- SELECT conname, contype FROM pg_constraint
--  WHERE conrelid='prv_proveedor_contacto'::regclass ORDER BY contype, conname;
--   -> fk_prv_proveedor_contacto_tipo(f), prv_proveedor_contacto_pkey(p)
-- SELECT conname, contype FROM pg_constraint
--  WHERE conrelid='prv_tipo_contacto'::regclass ORDER BY contype, conname;
--   -> prv_tipo_contacto_pkey(p)
--
-- 3) Índices de ambas tablas:
-- SELECT tablename, indexname FROM pg_indexes
--  WHERE tablename IN ('prv_proveedor_contacto','prv_tipo_contacto') ORDER BY 1, 2;
--   -> prv_proveedor_contacto: ix_prv_proveedor_contacto_proveedor, prv_proveedor_contacto_pkey
--   -> prv_tipo_contacto:      prv_tipo_contacto_pkey, uq_prv_tipo_contacto_nombre
--
-- 4) Semilla del catálogo — 5 filas por cada empresa que tenga proveedores:
-- SELECT company_id, count(*) FROM prv_tipo_contacto GROUP BY 1 ORDER BY 1;
--
-- 5) Migración — los dos conteos deben COINCIDIR.
--    El filtro usuario_creo='migracion' es lo que hace válida esta comprobación en
--    cualquier momento, no solo recién aplicado el script: sin él, el primer contacto
--    que capture un usuario desde la app ya rompería la igualdad de forma legítima.
-- SELECT
--   (SELECT count(*) FROM prv_proveedor_contacto WHERE usuario_creo = 'migracion')       AS contactos_migrados,
--   (SELECT count(*) FROM prv_proveedores
--     WHERE btrim(COALESCE(nombre_contacto,'')) <> '')                                   AS proveedores_con_contacto;
--   Si "contactos_migrados" sale MAYOR, es porque hay (company_id, cod_proveedor)
--   repetidos en prv_proveedores (esa tabla no tiene PK ni unique). Detectarlos con:
-- SELECT company_id, cod_proveedor, count(*)
--   FROM prv_proveedores GROUP BY 1,2 HAVING count(*) > 1;
--   -> 0 filas = sin duplicados.
--
-- 5b) Diagnóstico opcional: códigos con espacios al borde (el servicio busca por
--     el código recortado, así que estos proveedores ya vienen rotos de antes):
-- SELECT company_id, cod_proveedor FROM prv_proveedores
--  WHERE cod_proveedor <> btrim(cod_proveedor);
--   -> 0 filas = nada que revisar.
--
-- 6) Re-ejecución (idempotencia): volver a correr el script no debe mover estos
--    números. Comparar (4) y (5) antes y después.
--
-- 7) El nombre duplicado por empresa debe FALLAR (usar una empresa que exista):
-- INSERT INTO prv_tipo_contacto (company_id, nombre, usuario_creo)
-- VALUES (2, '  ventas  ', 'test');
--   -> ERROR: duplicate key value violates unique constraint "uq_prv_tipo_contacto_nombre"
--
-- 8) La FK del tipo debe FALLAR con un id inexistente:
-- INSERT INTO prv_proveedor_contacto (company_id, cod_proveedor, tipo_contacto_id, nombre, usuario_creo)
-- VALUES (2, 'XXX', 999999999, 'Prueba', 'test');
--   -> ERROR: insert or update on table "prv_proveedor_contacto" violates foreign key
--             constraint "fk_prv_proveedor_contacto_tipo"
-- =============================================================================
