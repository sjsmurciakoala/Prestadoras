-- =============================================================================
-- Bitácora de maestros: alta de las dos tablas de contactos de proveedor
-- Fecha: 2026-07-27
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
-- Requiere: 2026-07-27_proveedor_contactos.sql (crea prv_proveedor_contacto y
--           prv_tipo_contacto) y 2026-07-17_bitacora_maestro_catalogo.sql.
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- prv_proveedor_contacto y prv_tipo_contacto ya entraron a la lista blanca de
-- código (SIAD.Core/Constants/AuditableMaestros.cs) y, al ser entidades CON clave
-- que se persisten con SaveChanges, el interceptor de EF las ve. Pero el catálogo
-- de entidades auditables vive en la BD (bitacora_maestro_catalogo) y el auto-seed
-- del backend (AuditoriaConfigService.AsegurarCatalogoAsync) solo corre cuando la
-- tabla está VACÍA para esa empresa: en una base que ya abrió la pantalla de
-- auditoría, agregar una entrada al código NO la hace aparecer sola. Este script
-- la agrega.
--
-- QUÉ HACE
--   1. bitacora_maestro_catalogo: una fila por empresa que ya tenga catálogo, para
--      cada una de las dos tablas nuevas. Así aparecen en la pantalla de
--      configuración de auditoría y se pueden habilitar desde la UI.
--   2. bitacora_maestro_config: hereda los flags de prv_proveedor_cuenta_bancaria
--      en la misma empresa. Es la tabla hermana (el otro detalle del proveedor):
--      si esa empresa ya audita las cuentas bancarias, auditar los contactos es la
--      expectativa razonable; si no la audita, no se enciende nada por sorpresa.
--      Donde no exista esa fila de referencia no se inserta config: la entrada de
--      catálogo basta para que el usuario lo prenda a mano.
--
-- Cambio de DATOS, aditivo. No crea ni altera estructura, no borra nada.
-- IDEMPOTENTE: los dos INSERT están guardados con NOT EXISTS sobre la clave
-- (company_id, tabla/entidad), que es además la que llevan los índices únicos.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Catálogo de entidades auditables
-- ---------------------------------------------------------------------------
INSERT INTO public.bitacora_maestro_catalogo
    (company_id, tabla, nombre, modulo, activo, usuariocreacion, fechacreacion)
SELECT c.company_id, t.tabla, t.nombre, 'Proveedores', TRUE, 'system', now()
FROM (SELECT DISTINCT company_id FROM public.bitacora_maestro_catalogo) c
CROSS JOIN (VALUES
        ('prv_proveedor_contacto', 'Contactos de proveedor'),
        ('prv_tipo_contacto',      'Tipos de contacto')
    ) AS t(tabla, nombre)
WHERE NOT EXISTS (
    SELECT 1 FROM public.bitacora_maestro_catalogo x
    WHERE x.company_id = c.company_id
      AND lower(x.tabla) = lower(t.tabla)
);

-- ---------------------------------------------------------------------------
-- 2) Configuración: hereda los flags de la tabla hermana del proveedor
-- ---------------------------------------------------------------------------
INSERT INTO public.bitacora_maestro_config
    (company_id, entidad, habilitado, audita_crear, audita_editar, audita_eliminar,
     usuariocreacion, fechacreacion)
SELECT ref.company_id, t.entidad,
       ref.habilitado, ref.audita_crear, ref.audita_editar, ref.audita_eliminar,
       'system', now()
FROM public.bitacora_maestro_config ref
CROSS JOIN (VALUES ('prv_proveedor_contacto'), ('prv_tipo_contacto')) AS t(entidad)
WHERE lower(ref.entidad) = 'prv_proveedor_cuenta_bancaria'
  AND NOT EXISTS (
      SELECT 1 FROM public.bitacora_maestro_config x
      WHERE x.company_id = ref.company_id
        AND lower(x.entidad) = lower(t.entidad)
  );

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Catálogo — debe haber una fila por empresa para cada tabla nueva:
-- SELECT company_id, tabla, nombre, modulo, activo
--   FROM public.bitacora_maestro_catalogo
--  WHERE tabla IN ('prv_proveedor_contacto','prv_tipo_contacto')
--  ORDER BY company_id, tabla;
--
-- 2) Config heredada — los flags deben coincidir con los de la hermana:
-- SELECT company_id, entidad, habilitado, audita_crear, audita_editar, audita_eliminar
--   FROM public.bitacora_maestro_config
--  WHERE entidad IN ('prv_proveedor_cuenta_bancaria','prv_proveedor_contacto','prv_tipo_contacto')
--  ORDER BY company_id, entidad;
--
-- 3) Idempotencia — volver a correr el script no debe mover estos conteos:
-- SELECT
--   (SELECT count(*) FROM public.bitacora_maestro_catalogo
--     WHERE tabla IN ('prv_proveedor_contacto','prv_tipo_contacto'))  AS filas_catalogo,
--   (SELECT count(*) FROM public.bitacora_maestro_config
--     WHERE entidad IN ('prv_proveedor_contacto','prv_tipo_contacto')) AS filas_config;
--
-- 4) Prueba funcional (con el portal corriendo y sesión iniciada, y la entidad
--    HABILITADA en Configuración > Auditoría): crear un tipo de contacto en
--    /mantenimientos/tipos-contacto y verificar la fila de bitácora:
-- SELECT fecha, entidad, accion, registro_id, registro_desc, usuario
--   FROM public.bitacora_maestros
--  WHERE entidad IN ('prv_tipo_contacto','prv_proveedor_contacto')
--  ORDER BY id DESC LIMIT 10;
--
-- 5) Si una empresa NO tiene fila de prv_proveedor_cuenta_bancaria en config,
--    las dos nuevas quedan SIN config (auditoría apagada) pero SÍ en el catálogo:
--    se habilitan desde Configuración > Auditoría. Detectar esas empresas:
-- SELECT DISTINCT company_id FROM public.bitacora_maestro_catalogo c
--  WHERE NOT EXISTS (SELECT 1 FROM public.bitacora_maestro_config g
--                     WHERE g.company_id = c.company_id
--                       AND lower(g.entidad) = 'prv_proveedor_cuenta_bancaria');
-- =============================================================================
