-- =============================================================================
-- Almacén: soft-delete del artículo (alm_articulo.activo)
-- Fecha: 2026-07-29
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- Hasta ahora el maestro de artículos era el único catálogo de Almacén que borraba
-- FÍSICAMENTE (ArticulosService.DeleteAsync hacía DELETE). Sus hermanas ya usan
-- soft-delete con la columna 'activo': alm_bodega, alm_tipo_articulo, alm_grupo,
-- alm_unidad_medida, alm_articulo_bodega y alm_articulo_proveedor. Un artículo
-- borrado de verdad se lleva consigo la posibilidad de consultar su historia, y
-- deja huérfanas las referencias por código que todavía existen en documentos
-- viejos (compras/requisiciones/descargos migrados de SIMAFI referencian por
-- codigo_articulo, no por id).
--
-- A partir de este cambio el artículo se DESCONTINÚA: deja de ofrecerse para
-- documentos nuevos, pero se conserva y su kardex sigue consultable.
--
-- QUÉ HACE
--   1. alm_articulo.activo BOOLEAN NOT NULL DEFAULT true. Todos los artículos
--      existentes quedan ACTIVOS (nada cambia de comportamiento al aplicarlo).
--   2. Índice parcial por empresa para la lista filtrada (el caso normal: solo
--      activos). Convive con el ix_alm_articulo_company que ya existe.
--
-- NO HACE FALTA DDL PARA:
--   - Concurrencia optimista: se usa la columna de sistema xmin de Postgres, que
--     ya existe en toda tabla. Solo se mapea en EF (SIAD.Data/SiadDbContext.Almacen.cs).
--   - Auditoría: alm_articulo ya está en la lista blanca (AuditableMaestros) y el
--     interceptor de bitácora ya interpreta activo true->false como "Eliminación",
--     así que descontinuar queda registrado como borrado sin tocar nada más.
--
-- Cambio ADITIVO de estructura. No borra ni modifica ningún dato de negocio.
-- IDEMPOTENTE: ADD COLUMN IF NOT EXISTS + CREATE INDEX IF NOT EXISTS.
-- REVERSIBLE: DROP COLUMN activo (ver ROLLBACK al final).
--
-- Nota sobre el código: el índice único uq_alm_articulo_company_codigo sigue
-- aplicando a los descontinuados, o sea que un artículo descontinuado CONSERVA su
-- código y este no se puede reutilizar en otro artículo. Es intencional: reusar el
-- código de un artículo con historia haría ambiguo el kardex migrado.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Columna de soft-delete
-- ---------------------------------------------------------------------------
ALTER TABLE public.alm_articulo
    ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT true;

COMMENT ON COLUMN public.alm_articulo.activo IS
    'Soft-delete: false = artículo descontinuado (se conserva para histórico y su kardex sigue consultable, pero no se ofrece para documentos nuevos). El maestro solo lista los activos salvo que el usuario pida ver los descontinuados.';

-- ---------------------------------------------------------------------------
-- 2) Índice parcial para el caso normal (listar solo activos por empresa)
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_alm_articulo_company_activo
    ON public.alm_articulo(company_id) WHERE activo;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) La columna existe, es NOT NULL y su default es true:
-- SELECT column_name, data_type, is_nullable, column_default
--   FROM information_schema.columns
--  WHERE table_name = 'alm_articulo' AND column_name = 'activo';
--
-- 2) Ningún artículo quedó descontinuado por la migración (descontinuados = 0):
-- SELECT count(*) AS total,
--        count(*) FILTER (WHERE activo)     AS activos,
--        count(*) FILTER (WHERE NOT activo) AS descontinuados
--   FROM public.alm_articulo;
--
-- 3) El índice parcial existe:
-- SELECT indexname, indexdef FROM pg_indexes
--  WHERE tablename = 'alm_articulo' AND indexname = 'ix_alm_articulo_company_activo';
--
-- 4) Idempotencia: volver a correr el script no debe fallar ni cambiar conteos.
--
-- 5) Prueba funcional (portal corriendo, sesión iniciada): en /almacen/articulos
--    descontinuar un artículo de prueba y comprobar que (a) desaparece de la lista
--    normal, (b) aparece al activar "Ver descontinuados", (c) su kardex sigue
--    consultable y (d) quedó registrado en la bitácora como Eliminación:
-- SELECT fecha, entidad, accion, registro_id, usuario
--   FROM public.bitacora_maestros
--  WHERE tabla = 'alm_articulo'
--  ORDER BY id DESC LIMIT 10;
--
-- =============================================================================
-- ROLLBACK (solo si hay que revertir; se pierde qué artículos estaban descontinuados)
-- =============================================================================
-- BEGIN;
-- DROP INDEX IF EXISTS ix_alm_articulo_company_activo;
-- ALTER TABLE public.alm_articulo DROP COLUMN IF EXISTS activo;
-- COMMIT;
-- =============================================================================
