-- =============================================================================
-- Bitácora de maestros: alta del catálogo de formatos fiscales
-- Fecha: 2026-08-22
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
-- Requiere: 2026-08-22_cfg_formato_fiscal.sql (crea la tabla) y
--           2026-07-17_bitacora_maestro_catalogo.sql.
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- cfg_formato_fiscal ya entró a la lista blanca de código
-- (SIAD.Core/Constants/AuditableMaestros.cs) y, al ser una entidad CON clave que se
-- persiste con SaveChanges, el interceptor de EF la ve. Pero el catálogo de entidades
-- auditables vive en la BD (bitacora_maestro_catalogo) y el auto-seed del backend
-- (AuditoriaConfigService.AsegurarCatalogoAsync) solo corre cuando la tabla está VACÍA:
-- en una base que ya abrió la pantalla de auditoría, agregar una entrada al código NO
-- la hace aparecer sola. Este script la agrega.
--
-- QUÉ HACE
--   1. bitacora_maestro_catalogo: una fila por empresa que ya tenga catálogo.
--   2. bitacora_maestro_config: los cuatro flags en TRUE para esas mismas empresas.
--      A diferencia del script de contactos (que hereda de una tabla hermana), aquí se
--      enciende directo: el historial de cambios es un requisito explícito del
--      mantenimiento, no una expectativa razonable. Se puede apagar después desde
--      Configuración > Auditoría.
--
-- Cambio de DATOS, aditivo. No crea ni altera estructura, no borra nada.
-- IDEMPOTENTE: los dos INSERT están guardados con NOT EXISTS sobre la clave
-- (company_id, tabla/entidad), que es además la que llevan los índices únicos.
--
-- ⚠️ CACHE: AuditableCatalogProvider y AuditConfigProvider cachean en IMemoryCache con
-- TTL de 30 MINUTOS. Un insert hecho directo en SQL tarda hasta media hora en surtir
-- efecto, o hay que reiniciar el host apc.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Catálogo de entidades auditables
-- ---------------------------------------------------------------------------
INSERT INTO public.bitacora_maestro_catalogo
    (company_id, tabla, nombre, modulo, activo, usuariocreacion, fechacreacion)
SELECT c.company_id, 'cfg_formato_fiscal', 'Formatos fiscales', 'Configuración', TRUE, 'system', now()
FROM (SELECT DISTINCT company_id FROM public.bitacora_maestro_catalogo) c
WHERE NOT EXISTS (
    SELECT 1 FROM public.bitacora_maestro_catalogo x
    WHERE x.company_id = c.company_id
      AND lower(x.tabla) = 'cfg_formato_fiscal'
);

-- ---------------------------------------------------------------------------
-- 2) Configuración: auditoría encendida en las tres acciones
-- ---------------------------------------------------------------------------
INSERT INTO public.bitacora_maestro_config
    (company_id, entidad, habilitado, audita_crear, audita_editar, audita_eliminar,
     usuariocreacion, fechacreacion)
SELECT c.company_id, 'cfg_formato_fiscal', TRUE, TRUE, TRUE, TRUE, 'system', now()
FROM (SELECT DISTINCT company_id FROM public.bitacora_maestro_catalogo) c
WHERE NOT EXISTS (
    SELECT 1 FROM public.bitacora_maestro_config x
    WHERE x.company_id = c.company_id
      AND lower(x.entidad) = 'cfg_formato_fiscal'
);

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Catálogo — una fila por empresa:
-- SELECT company_id, tabla, nombre, modulo, activo
--   FROM public.bitacora_maestro_catalogo
--  WHERE tabla = 'cfg_formato_fiscal'
--  ORDER BY company_id;
--
-- 2) Config — los cuatro flags en true:
-- SELECT company_id, entidad, habilitado, audita_crear, audita_editar, audita_eliminar
--   FROM public.bitacora_maestro_config
--  WHERE entidad = 'cfg_formato_fiscal'
--  ORDER BY company_id;
--
-- 3) Idempotencia — volver a correr no debe mover estos conteos:
-- SELECT
--   (SELECT count(*) FROM public.bitacora_maestro_catalogo WHERE tabla   = 'cfg_formato_fiscal') AS filas_catalogo,
--   (SELECT count(*) FROM public.bitacora_maestro_config   WHERE entidad = 'cfg_formato_fiscal') AS filas_config;
--
-- 4) Si el catálogo está VACÍO en esta base, los dos INSERT no hacen nada (0 filas) y
--    es lo correcto: el auto-seed del backend sembrará las 12 entradas la primera vez
--    que se abra Configuración > Auditoría. Detectarlo:
-- SELECT count(*) AS filas_en_catalogo FROM public.bitacora_maestro_catalogo;
--
-- 5) Prueba funcional (portal corriendo, sesión iniciada): editar la máscara en
--    /mantenimientos/formatos-fiscales y verificar la fila de bitácora:
-- SELECT fecha, tabla, entidad, accion, registro_id, usuario, valores_anteriores, valores_nuevos
--   FROM public.bitacora_maestros
--  WHERE tabla = 'cfg_formato_fiscal'
--  ORDER BY bitacora_maestro_id DESC LIMIT 10;
-- =============================================================================
