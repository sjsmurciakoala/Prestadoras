-- =============================================================================
-- Saneo de libretas fantasma detectadas al aplicar las libretas globales
-- Fecha: 2026-07-30
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
-- Requiere: 2026-07-16_libretas_globales.sql (crea adm_libreta y deriva las rutas
--           del ciclo desde los CLIENTES, no del catálogo rutas).
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- Al sembrar adm_libreta desde el catálogo `rutas` aparecieron SEIS libretas donde
-- el negocio tiene CINCO (00L1..00L5). La sexta es 'OOL1': escrita con la letra O
-- en vez de ceros. Su propia descripción la delata — "LIBRO 001" — - es un error de
-- digitación en `rutas` (id 120), no una libreta real:
--   - NINGÚN cliente la usa (verificado: 0 filas);
--   - NINGUNA credencial de lector la tiene asignada;
--   - pero saldría en el mantenimiento de libretas como si fuera real, invitando a
--     asignarle un lector o clientes por error.
--
-- Desde las libretas globales las rutas de un ciclo se derivan de los CLIENTES, así
-- que una libreta de catálogo sin clientes es inerte para abrir/cerrar ciclo. El daño
-- es de confusión, no funcional — por eso esto es saneo y no urgencia.
--
-- QUÉ HACE
--   1. Borra la fila 'OOL1' de adm_libreta (la sembró el seed; no la referencia nadie).
--   2. Desactiva la ruta 'OOL1' en el catálogo legacy `rutas` para que un futuro
--      re-seed no la vuelva a traer. NO se borra: `rutas` se conserva para rollback
--      de las libretas globales.
--
-- Cambio de DATOS, acotado y verificable. No toca clientes, lecturas ni credenciales.
-- IDEMPOTENTE: los dos statements son no-ops si ya se corrió.
-- REVERSIBLE: ver ROLLBACK al final.
--
--   3. Desactiva al cliente 09013580, que arrastra la otra libreta fantasma.
--
-- EL CLIENTE 09013580 (JENNIFER SARAHI MATUTE MEJIA), indicativo '10-018-10000-00100':
-- la libreta '10000' es el fallback del formulario viejo (ciclo 10 + '000'), el mismo
-- patrón que las libretas globales corrigieron a mano para el cliente 090806560
-- ('19000'). **NO se le asigna una libreta real** porque no hay forma de deducirla:
--   - 0 lecturas en historicomedicion,
--   - 0 facturas,
--   - es el ÚNICO cliente del ciclo 10 (activos e inactivos), así que no hay vecinos.
-- Elegir una al azar decidiría quién le lee el medidor. Con ese perfil —sin lecturas,
-- sin facturas y solo en su ciclo— es casi con certeza un registro de prueba, así que
-- (decisión del usuario, 2026-07-30) se DESACTIVA en vez de inventarle una ruta.
-- Importa porque, desde las libretas globales, un cliente activo con libreta fantasma
-- genera una ruta que ningún lector puede atender y que bloquearía el cierre del ciclo.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Fuera del catálogo nuevo de libretas
-- ---------------------------------------------------------------------------
DELETE FROM public.adm_libreta
 WHERE upper(btrim(codigo)) = 'OOL1'
   -- Guarda de seguridad: sólo si de verdad no la usa ningún cliente.
   AND NOT EXISTS (
       SELECT 1 FROM public.cliente_maestro cm
        WHERE cm.company_id = adm_libreta.company_id
          AND upper(btrim(split_part(cm.maestro_cliente_indicativo_ruta, '-', 3))) = 'OOL1'
   );

-- ---------------------------------------------------------------------------
-- 2) Desactivar el origen en el catálogo legacy, para que no vuelva en un re-seed
-- ---------------------------------------------------------------------------
UPDATE public.rutas
   SET estado = false
 WHERE upper(btrim(codruta)) = 'OOL1'
   AND estado = true;

-- ---------------------------------------------------------------------------
-- 3) Desactivar al cliente con la libreta fantasma '10000'
--    Guardas: sólo ese cliente, sólo con ESE indicativo, y sólo si sigue sin
--    lecturas y sin facturas (si aparecieran, es un cliente real y hay que
--    asignarle libreta a mano en vez de desactivarlo).
-- ---------------------------------------------------------------------------
UPDATE public.cliente_maestro cm
   SET estado = false
 WHERE cm.maestro_cliente_clave = '09013580'
   AND cm.maestro_cliente_indicativo_ruta = '10-018-10000-00100'
   AND cm.estado = true
   AND NOT EXISTS (SELECT 1 FROM public.historicomedicion h WHERE btrim(h.clave) = '09013580')
   AND NOT EXISTS (SELECT 1 FROM public.factura f WHERE btrim(f.clientecodigo) = '09013580');

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Quedan exactamente las 5 libretas reales por empresa:
-- SELECT company_id, string_agg(codigo, ',' ORDER BY codigo) AS libretas, count(*)
--   FROM public.adm_libreta GROUP BY company_id;
--   -- esperado: 00L1,00L2,00L3,00L4,00L5  (5)
--
-- 2) La ruta legacy quedó inactiva:
-- SELECT codruta, descripcion, estado FROM public.rutas WHERE upper(btrim(codruta)) = 'OOL1';
--
-- 3) NINGÚN cliente ACTIVO queda con libreta fuera del catálogo (debe dar 0 filas):
-- SELECT cm.maestro_cliente_clave, cm.maestro_cliente_indicativo_ruta
--   FROM public.cliente_maestro cm
--  WHERE cm.estado = true
--    AND cm.maestro_cliente_indicativo_ruta IS NOT NULL
--    AND NOT EXISTS (SELECT 1 FROM public.adm_libreta l
--                     WHERE l.company_id = cm.company_id
--                       AND l.codigo = upper(split_part(cm.maestro_cliente_indicativo_ruta, '-', 3)));
--
-- 4) El cliente quedó inactivo (conserva su indicativo, por si hay que reactivarlo
--    con la libreta correcta):
-- SELECT maestro_cliente_clave, maestro_cliente_nombre, estado, maestro_cliente_indicativo_ruta
--   FROM public.cliente_maestro WHERE maestro_cliente_clave = '09013580';

-- =============================================================================
-- ROLLBACK
-- =============================================================================
-- BEGIN;
-- UPDATE public.cliente_maestro SET estado = true WHERE maestro_cliente_clave = '09013580';
-- UPDATE public.rutas SET estado = true WHERE upper(btrim(codruta)) = 'OOL1';
-- INSERT INTO public.adm_libreta (company_id, codigo, descripcion, created_by)
-- SELECT DISTINCT cm.company_id, 'OOL1', 'LIBRO 001', 'rollback-saneo'
--   FROM public.cliente_maestro cm
-- ON CONFLICT (company_id, codigo) DO NOTHING;
-- COMMIT;
--
-- Si más adelante resulta que el cliente 09013580 SÍ es real, la corrección es
-- reactivarlo y darle su libreta verdadera (no volver a '10000'):
-- UPDATE public.cliente_maestro
--    SET estado = true, maestro_cliente_indicativo_ruta = '10-018-00Lx-00100'
--  WHERE maestro_cliente_clave = '09013580';   -- reemplazar 00Lx por la libreta real
-- =============================================================================
