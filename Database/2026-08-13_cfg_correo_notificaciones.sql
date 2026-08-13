-- =============================================================================
-- Correo y notificaciones por empresa: conexión SendGrid + enrutamiento por área
-- Fecha: 2026-08-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ ESTAS TABLAS
-- Hoy el envío de correo es un No-Op (no manda nada) y no hay dónde configurar la
-- conexión. Se quiere un mantenimiento por empresa, con VARIAS áreas de notificación
-- (administración, almacén, cobranza…), cada una con su propio remitente y sus
-- destinatarios, pero UNA sola conexión: la API key autentica la cuenta del proveedor;
-- el "de:" y el "para:" van por mensaje, no por credencial.
--
-- TRES TABLAS (separación conexión / enrutamiento)
--   cfg_correo                    -> la CONEXIÓN (1 por empresa): API key cifrada + remitente por defecto.
--   cfg_notificacion              -> el ÁREA/TIPO (N por empresa): remitente propio opcional.
--   cfg_notificacion_destinatario -> los DESTINATARIOS TO/CC (N por notificación).
--
-- LA API KEY VA CIFRADA
--   api_key_cifrada guarda el ciphertext de ASP.NET Core DataProtection (base64url), NUNCA la
--   clave en claro. La BD no puede descifrarla; solo la app con su key-ring. Un backup restaurado
--   en otra máquina no la descifra — es un rasgo buscado, no un defecto.
--
-- COMPANY_ID EN LAS TRES
--   Las tres implementan ICompanyScopedEntity (query filter global de EF). La hija lleva
--   company_id además de la FK, por el filtro y el stamping. La FK apunta a la PK SIMPLE del área;
--   el aislamiento entre empresas lo garantiza el filtro global. Mismo patrón que
--   prv_proveedor_contacto.
--
-- Cambio ADITIVO: tres tablas nuevas. NO altera ninguna tabla, columna ni dato existente.
-- Idempotente: CREATE ... IF NOT EXISTS. Sin siembra de datos (las filas las crea la pantalla:
-- el catálogo de tipos lo define el código, no la BD).
-- =============================================================================
BEGIN;

-- ── 1. Conexión: una por empresa ────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS cfg_correo (
    id                        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id                BIGINT       NOT NULL,
    proveedor                 VARCHAR(20)  NOT NULL DEFAULT 'SENDGRID',
    api_key_cifrada           TEXT,
    remitente_email_default   VARCHAR(200),
    remitente_nombre_default  VARCHAR(150),
    activo                    BOOLEAN      NOT NULL DEFAULT FALSE,
    usuariocreacion           VARCHAR(100),
    fechacreacion             TIMESTAMP WITHOUT TIME ZONE,
    usuariomodificacion       VARCHAR(100),
    fechamodificacion         TIMESTAMP WITHOUT TIME ZONE,
    CONSTRAINT ck_cfg_correo_proveedor CHECK (proveedor IN ('SENDGRID','SMTP'))
);

-- Una conexión por proveedor por empresa.
CREATE UNIQUE INDEX IF NOT EXISTS uq_cfg_correo_company_proveedor
    ON cfg_correo (company_id, proveedor);

-- ── 2. Área/tipo de notificación: N por empresa ─────────────────────────────
CREATE TABLE IF NOT EXISTS cfg_notificacion (
    id                    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id            BIGINT       NOT NULL,
    tipo                  VARCHAR(30)  NOT NULL,
    nombre                VARCHAR(120),
    remitente_email       VARCHAR(200),
    remitente_nombre      VARCHAR(150),
    activo                BOOLEAN      NOT NULL DEFAULT TRUE,
    usuariocreacion       VARCHAR(100),
    fechacreacion         TIMESTAMP WITHOUT TIME ZONE,
    usuariomodificacion   VARCHAR(100),
    fechamodificacion     TIMESTAMP WITHOUT TIME ZONE,
    -- Espejo de SIAD.Core.Constants.TipoNotificacion. Agregar un tipo = ampliar este CHECK.
    CONSTRAINT ck_cfg_notificacion_tipo
        CHECK (tipo IN ('ADMINISTRACION','ALMACEN','COBRANZA','SISTEMA'))
);

-- Un renglón por tipo por empresa.
CREATE UNIQUE INDEX IF NOT EXISTS uq_cfg_notificacion_company_tipo
    ON cfg_notificacion (company_id, tipo);

-- ── 3. Destinatarios TO/CC: N por notificación ──────────────────────────────
CREATE TABLE IF NOT EXISTS cfg_notificacion_destinatario (
    id                BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        BIGINT       NOT NULL,
    notificacion_id   BIGINT       NOT NULL,
    correo            VARCHAR(200) NOT NULL,
    clase             VARCHAR(4)   NOT NULL DEFAULT 'TO',
    activo            BOOLEAN      NOT NULL DEFAULT TRUE,
    usuariocreacion   VARCHAR(100),
    fechacreacion     TIMESTAMP WITHOUT TIME ZONE,

    CONSTRAINT ck_cfg_notif_dest_clase CHECK (clase IN ('TO','CC')),

    -- CASCADE: los destinatarios son hijos del área; al borrarla se van con ella.
    -- La FK NO lleva company_id (cfg_notificacion tiene PK simple); el aislamiento entre
    -- empresas lo garantiza el query filter global de EF. Mismo patrón que prv_proveedor_contacto.
    CONSTRAINT fk_cfg_notif_dest_notificacion
        FOREIGN KEY (notificacion_id)
        REFERENCES cfg_notificacion (id)
        ON DELETE CASCADE
);

-- Sirve al listado de destinatarios de un área.
CREATE INDEX IF NOT EXISTS ix_cfg_notif_dest_notificacion
    ON cfg_notificacion_destinatario (notificacion_id);

-- Sin duplicar un mismo destino en el mismo canal. Normaliza (sin distinguir mayúsculas ni
-- espacios al borde): "  A@x.com " y "a@x.com" son el mismo destinatario TO.
CREATE UNIQUE INDEX IF NOT EXISTS uq_cfg_notif_dest_correo
    ON cfg_notificacion_destinatario (notificacion_id, lower(btrim(correo)), clase);

-- ── Comentarios ─────────────────────────────────────────────────────────────
COMMENT ON TABLE  cfg_correo IS
    'Conexión de correo por empresa (1 por proveedor). API key cifrada + remitente por defecto. El enrutamiento por área vive en cfg_notificacion.';
COMMENT ON COLUMN cfg_correo.api_key_cifrada IS
    'Ciphertext de ASP.NET Core DataProtection (base64url). NUNCA en claro; solo la app con su key-ring la descifra.';
COMMENT ON COLUMN cfg_correo.remitente_email_default IS
    'Remitente por defecto; se usa cuando un área (cfg_notificacion) no define el suyo.';
COMMENT ON COLUMN cfg_correo.activo IS
    'Interruptor GLOBAL de envío. FALSE = no sale ningún correo de la empresa.';
COMMENT ON TABLE  cfg_notificacion IS
    'Área/tipo de notificación por empresa. El tipo lo define el código (quien dispara el evento); la pantalla solo asigna remitente y destinatarios.';
COMMENT ON COLUMN cfg_notificacion.remitente_email IS
    'Override del remitente para esta área. NULL = usa cfg_correo.remitente_email_default.';
COMMENT ON COLUMN cfg_notificacion.activo IS
    'Enciende/apaga este tipo sin borrarlo (distinto del interruptor global de cfg_correo).';
COMMENT ON TABLE  cfg_notificacion_destinatario IS
    'Destinatarios de un área (TO/CC). Se reemplazan como conjunto al guardar; por eso solo llevan auditoría de creación.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Columnas de las tres tablas:
-- SELECT table_name, column_name, data_type, character_maximum_length, is_nullable, column_default
--   FROM information_schema.columns
--  WHERE table_name IN ('cfg_correo','cfg_notificacion','cfg_notificacion_destinatario')
--  ORDER BY table_name, ordinal_position;
--   -> cfg_correo: 11 columnas (activo default false); cfg_notificacion: 11 (activo default true);
--      cfg_notificacion_destinatario: 8 (clase default 'TO', activo default true).
--
-- 2) Constraints (PK, CHECK, FK):
-- SELECT conrelid::regclass AS tabla, conname, contype FROM pg_constraint
--  WHERE conrelid IN ('cfg_correo'::regclass,'cfg_notificacion'::regclass,'cfg_notificacion_destinatario'::regclass)
--  ORDER BY 1, contype, conname;
--   -> ck_cfg_correo_proveedor(c), ck_cfg_notificacion_tipo(c), ck_cfg_notif_dest_clase(c),
--      fk_cfg_notif_dest_notificacion(f), y las tres *_pkey(p).
--
-- 3) Índices:
-- SELECT tablename, indexname FROM pg_indexes
--  WHERE tablename IN ('cfg_correo','cfg_notificacion','cfg_notificacion_destinatario')
--  ORDER BY 1, 2;
--   -> cfg_correo: cfg_correo_pkey, uq_cfg_correo_company_proveedor
--   -> cfg_notificacion: cfg_notificacion_pkey, uq_cfg_notificacion_company_tipo
--   -> cfg_notificacion_destinatario: cfg_notificacion_destinatario_pkey,
--        ix_cfg_notif_dest_notificacion, uq_cfg_notif_dest_correo
--
-- 4) El CHECK del tipo debe FALLAR con un valor fuera del catálogo (usar una empresa que exista):
-- INSERT INTO cfg_notificacion (company_id, tipo) VALUES (2, 'MARKETING');
--   -> ERROR: new row ... violates check constraint "ck_cfg_notificacion_tipo"
--
-- 5) La FK debe FALLAR con un área inexistente:
-- INSERT INTO cfg_notificacion_destinatario (company_id, notificacion_id, correo)
-- VALUES (2, 999999999, 'x@y.com');
--   -> ERROR: ... violates foreign key constraint "fk_cfg_notif_dest_notificacion"
--
-- 6) El destino duplicado por canal debe FALLAR (normalizando):
--    (crear antes un área real y usar su id)
-- INSERT INTO cfg_notificacion_destinatario (company_id, notificacion_id, correo, clase)
-- VALUES (2, <id_area>, '  A@x.com ', 'TO');   -- primero OK
-- INSERT INTO cfg_notificacion_destinatario (company_id, notificacion_id, correo, clase)
-- VALUES (2, <id_area>, 'a@x.com',   'TO');   -- debe FALLAR
--   -> ERROR: duplicate key value violates unique constraint "uq_cfg_notif_dest_correo"
--
-- 7) CASCADE: borrar el área se lleva sus destinatarios:
-- DELETE FROM cfg_notificacion WHERE id = <id_area>;
--   -> los cfg_notificacion_destinatario de ese id quedan en 0.
--
-- 8) Re-ejecución (idempotencia): volver a correr el script no debe fallar ni duplicar objetos.
-- =============================================================================
