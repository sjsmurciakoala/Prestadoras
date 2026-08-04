-- Backlog pruebas operativas: "Cambio de categoria (Domestico->Comercial) debe
-- generar partida contable" — cierre del flujo REAL de operacion.
--
-- El sistema tiene DOS categorias:
--   * adm_categoria_regulatoria (V3 tarifario, via adm_cliente_servicio):
--     define la TARIFA. Se cambia en /tarifario/cliente-servicio-v3.
--   * categoria_servicio (cliente_maestro.categoria_servicio_id): define la
--     cuenta CxC contable (matriz con_integracion_cuenta) y el snapshot de
--     factura. Se cambia en Editar Cliente.
--
-- Sin puente, cambiar la categoria en tarifario dejaba la contabilidad
-- posteando con la categoria vieja y sin reclasificacion. Esta columna declara
-- la equivalencia contable de cada categoria regulatoria; el guardado de
-- /tarifario/cliente-servicio-v3 sincroniza cliente_maestro y reclasifica CxC.

BEGIN;

ALTER TABLE public.adm_categoria_regulatoria
    ADD COLUMN IF NOT EXISTS categoria_servicio_id integer NULL;

COMMENT ON COLUMN public.adm_categoria_regulatoria.categoria_servicio_id IS
    'Equivalente contable en categoria_servicio. Al cambiar la categoria regulatoria de un cliente, su cliente_maestro.categoria_servicio_id se sincroniza a este valor y el saldo CxC pendiente se reclasifica (cln_cliente_recategorizacion). NULL = sin sincronizacion contable.';

-- Semilla por codigo (todas las empresas): equivalencias canonicas.
UPDATE public.adm_categoria_regulatoria SET categoria_servicio_id = 1
 WHERE categoria_servicio_id IS NULL AND upper(btrim(codigo)) = 'DOMESTICO';
UPDATE public.adm_categoria_regulatoria SET categoria_servicio_id = 2
 WHERE categoria_servicio_id IS NULL AND upper(btrim(codigo)) = 'COMERCIAL';
UPDATE public.adm_categoria_regulatoria SET categoria_servicio_id = 3
 WHERE categoria_servicio_id IS NULL AND upper(btrim(codigo)) = 'INDUSTRIAL';
UPDATE public.adm_categoria_regulatoria SET categoria_servicio_id = 4
 WHERE categoria_servicio_id IS NULL AND upper(btrim(codigo)) = 'GUBERNAMENTAL';

COMMIT;
