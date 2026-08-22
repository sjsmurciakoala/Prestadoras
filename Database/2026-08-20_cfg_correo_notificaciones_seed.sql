-- =============================================================================
-- Correo / SendGrid: migración de la configuración de notificaciones
-- Fecha: 2026-08-20  ·  Fase C.3
--
-- Base objetivo: siad_v4 @ 172.16.0.9  (la base ACTIVA; NO siad_v3)
--
-- Migra a producción las 4 filas de configuración de correo que existen en local:
--   cfg_correo                    1 fila  (proveedor SENDGRID + remitente por defecto)
--   cfg_notificacion              1 fila  (área ALMACEN)
--   cfg_notificacion_destinatario 2 filas (los correos que reciben esa notificación)
--
-- Las 3 tablas ya existen en siad_v4 (las creó 2026-08-13_cfg_correo_notificaciones.sql)
-- y están VACÍAS.
--
-- El `id` de las 3 es GENERATED ALWAYS AS IDENTITY: **no se puede forzar** (Postgres lo
-- rechaza sin OVERRIDING SYSTEM VALUE, que además dejaría la secuencia desincronizada).
-- Por eso el script NO copia los ids de local; deja que la identity los genere y enlaza
-- los destinatarios con la notificación por búsqueda, no por id fijo.
--
-- -----------------------------------------------------------------------------
-- ⚠️ LA API KEY DE SENDGRID **NO** SE MIGRA — Y NO ES UN DESCUIDO
--
--   cfg_correo.api_key_cifrada guarda la key cifrada con ASP.NET DataProtection.
--   El ciphertext de local NO se puede descifrar en el servidor, por dos motivos
--   que se acumulan (ver apc/Program.cs, bloque de DataProtection):
--
--     1. SetApplicationName distinto: "HODSOFT.Prestadoras.Development" en local
--        contra "HODSOFT.Prestadoras" en producción. El ApplicationName es parte
--        del discriminador de propósito, así que el mismo texto cifrado en un
--        entorno no abre en el otro.
--
--     2. ProtectKeysWithDpapi(protectToLocalMachine: true) en producción: el
--        key-ring queda **atado a esa máquina**. Copiar el ciphertext (o incluso
--        las llaves) desde otra máquina no sirve.
--
--   Copiar el valor dejaría una key que no descifra y **el envío se caería en
--   silencio**. Por eso api_key_cifrada queda en NULL y cfg_correo.activo en FALSE.
--
--   ✅ QUÉ HACER DESPUÉS DE APLICAR ESTE SCRIPT:
--      Entrar al portal EN EL SERVIDOR → mantenimiento de Correo → pegar la API key
--      de SendGrid y guardar. Eso la cifra con el key-ring correcto y activa el envío.
--      Al guardarla, marcar `activo`.
-- -----------------------------------------------------------------------------
--
-- ADITIVO: solo INSERT sobre 3 tablas vacías. No toca nada existente.
-- IDEMPOTENTE: INSERT … WHERE NOT EXISTS.
--
-- ¿YA APLICADO?
--   SELECT (SELECT count(*) FROM cfg_correo)                    AS correo,
--          (SELECT count(*) FROM cfg_notificacion)              AS notif,
--          (SELECT count(*) FROM cfg_notificacion_destinatario) AS dest;
--   -- esperado tras aplicar: 1 | 1 | 2
--
-- REVERSIBLE:
--   DELETE FROM cfg_notificacion_destinatario WHERE company_id = 2;
--   DELETE FROM cfg_notificacion              WHERE company_id = 2 AND tipo = 'ALMACEN';
--   DELETE FROM cfg_correo                    WHERE company_id = 2;
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Conexión de correo (SENDGRID) — SIN la API key, y desactivada
-- ---------------------------------------------------------------------------
INSERT INTO cfg_correo
    (company_id, proveedor, api_key_cifrada,
     remitente_email_default, remitente_nombre_default, activo,
     usuariocreacion, fechacreacion)
SELECT 2, 'SENDGRID', NULL,
       'egaray@koalaoutsourcing.com', 'Emilio Garay', false,
       'migracion_2026-08-20', now()
WHERE NOT EXISTS (SELECT 1 FROM cfg_correo WHERE company_id = 2);

-- ---------------------------------------------------------------------------
-- 2. Notificación por área — ALMACEN
-- ---------------------------------------------------------------------------
INSERT INTO cfg_notificacion
    (company_id, tipo, nombre, remitente_email, remitente_nombre, activo,
     usuariocreacion, fechacreacion)
SELECT 2, 'ALMACEN', 'Almacen', NULL, NULL, true,
       'migracion_2026-08-20', now()
WHERE NOT EXISTS (SELECT 1 FROM cfg_notificacion WHERE company_id = 2 AND tipo = 'ALMACEN');

-- ---------------------------------------------------------------------------
-- 3. Destinatarios de esa notificación
-- ---------------------------------------------------------------------------
INSERT INTO cfg_notificacion_destinatario
    (company_id, notificacion_id, correo, clase, activo, usuariocreacion, fechacreacion)
SELECT 2, n.id, d.correo, 'TO', true, 'migracion_2026-08-20', now()
  FROM cfg_notificacion n
 CROSS JOIN (VALUES ('egaray@koalaoutsourcing.com'),
                    ('srivera@koalaoutsourcing.com')) AS d(correo)
 WHERE n.company_id = 2 AND n.tipo = 'ALMACEN'
   AND NOT EXISTS (SELECT 1 FROM cfg_notificacion_destinatario x
                    WHERE x.company_id = 2 AND x.notificacion_id = n.id
                      AND x.correo = d.correo);

-- ---------------------------------------------------------------------------
-- Verificación
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    v_correo int; v_notif int; v_dest int; v_sinkey int;
BEGIN
    SELECT count(*) INTO v_correo FROM cfg_correo                    WHERE company_id = 2;
    SELECT count(*) INTO v_notif  FROM cfg_notificacion              WHERE company_id = 2;
    SELECT count(*) INTO v_dest   FROM cfg_notificacion_destinatario WHERE company_id = 2;
    SELECT count(*) INTO v_sinkey FROM cfg_correo
     WHERE company_id = 2 AND api_key_cifrada IS NULL;

    RAISE NOTICE 'cfg_correo=% cfg_notificacion=% destinatarios=%', v_correo, v_notif, v_dest;

    IF v_correo <> 1 OR v_notif <> 1 OR v_dest <> 2 THEN
        RAISE EXCEPTION 'Verificacion fallida: correo=% notif=% dest=%. Se revierte.', v_correo, v_notif, v_dest;
    END IF;

    IF v_sinkey > 0 THEN
        RAISE NOTICE 'PENDIENTE: capturar la API key de SendGrid desde la pantalla de Correo EN EL SERVIDOR y marcar cfg_correo.activo = true. Hasta entonces no se envia ningun correo.';
    END IF;
END $$;

COMMIT;
