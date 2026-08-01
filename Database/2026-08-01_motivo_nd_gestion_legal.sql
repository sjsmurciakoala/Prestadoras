-- =============================================================================
-- Pruebas operativas jul-2026 (lote 4 convenios): "Cargar gestión legal al
-- cliente mediante nota de débito".
--
-- El mecanismo YA existe: /facturacion/notas emite ND contra una factura del
-- cliente con un motivo de aumento del catálogo. Lo que faltaba era el MOTIVO
-- específico para honorarios/gastos de la gestión legal (el catálogo solo
-- traía cobro extemporáneo, mora, ajuste de tarifa, interés y OTRO).
--
-- Idempotente por código.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

INSERT INTO public.cfg_motivo_aumento (motivo_aumento_id, codigo, descripcion, activo)
SELECT (SELECT COALESCE(MAX(motivo_aumento_id), 0) + 1 FROM public.cfg_motivo_aumento),
       'GESTION_LEGAL',
       'Gestión legal de cobranza (honorarios y gastos legales cargados al cliente)',
       true
WHERE NOT EXISTS (
    SELECT 1 FROM public.cfg_motivo_aumento WHERE codigo = 'GESTION_LEGAL'
);

COMMIT;
