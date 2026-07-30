-- =============================================================================
-- Índices para la búsqueda de la caja sobre la cartera migrada
-- =============================================================================
-- Con la migración total de SIMAFI, `factura` pasó de 124 filas a 3.9M y la
-- búsqueda de la caja (BuscarFacturasConSaldoAsync) tardaba 31.7 s: sus ILIKE
-- '%term%' forzaban recorrido completo. El servicio se reescribió para buscar
-- por igualdad indexable; estos índices la sirven:
--
--   * (company_id, clientecodigo): pendientes por cliente y búsqueda por clave.
--   * (company_id, numfactura) parcial: búsqueda por folio CAI. Hoy casi todo
--     numfactura es NULL (la cartera migrada no lleva CAI), por eso el parcial.
--
-- La búsqueda por numrecibo ya la cubre ix_factura_company_recibo_cliente (M3b).

\timing on
\set ON_ERROR_STOP on
SET maintenance_work_mem = '1536MB';

CREATE INDEX IF NOT EXISTS ix_factura_company_clientecodigo
    ON public.factura (company_id, clientecodigo);

CREATE INDEX IF NOT EXISTS ix_factura_company_numfactura
    ON public.factura (company_id, numfactura)
    WHERE numfactura IS NOT NULL;

ANALYZE public.factura;
