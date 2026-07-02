-- Índice de apoyo para la consulta de cartera vencida (antigüedad por emisión).
-- Vista: Facturación → Cobranza → Cartera vencida.
-- La antigüedad se calcula por fecha de emisión (factura.fechavence no está poblada),
-- consistente con public.rep_analisis_antiguedad_cobros.
-- Regla mirror: aplicar en el mirror local (siad_v3_restore) y replicar en SRV (siad_v3).
CREATE INDEX IF NOT EXISTS ix_factura_cartera_vencida
    ON factura (company_id, fechaemision);
