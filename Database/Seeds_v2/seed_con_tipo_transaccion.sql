-- Seed: Tipos de Transacción (Catálogo esencial para pólizas contables)
-- Aplica para empresa_demo (company_id = 1)

BEGIN;

INSERT INTO public.con_tipo_transaccion 
  (company_id, code, name, description, category, is_automatic, allows_cost_center, allows_third_party, status, created_at, created_by)
VALUES
  -- Tipos básicos: manual (diario, ajuste) + automático (cierre, apertura)
  (1, 'DIARIO',        'Diario',          'Polizas de diario contable',          'DIARIO',    false, true,  false, 'ACTIVE', now(), 'seeder'),
  (1, 'AJUSTE',        'Ajuste',          'Ajustes y correcciones contables',    'AJUSTE',    false, true,  false, 'ACTIVE', now(), 'seeder'),
  (1, 'CIERRE',        'Cierre',          'Cierre automático de período',        'CIERRE',    true,  false, false, 'ACTIVE', now(), 'seeder'),
  (1, 'APERTURA',      'Apertura',        'Saldos iniciales del período',        'APERTURA',  true,  false, false, 'ACTIVE', now(), 'seeder'),
  (1, 'FACTURACION',   'Facturación',     'Polizas generadas de facturas',       'FACTURACION',false, true,  true,  'ACTIVE', now(), 'seeder'),
  (1, 'PAGO',          'Pago',            'Polizas de pagos y recibos',          'PAGO',      false, true,  true,  'ACTIVE', now(), 'seeder'),
  (1, 'CONSOLIDACION', 'Consolidación',   'Consolidación entre empresas',        'CONSOLIDACION',true,false,false, 'ACTIVE', now(), 'seeder');

-- Crear índices adicionales para búsquedas frecuentes (opcional, EF ya lo hace)
-- CREATE INDEX IF NOT EXISTS ix_con_tipo_transaccion_category_status ON public.con_tipo_transaccion(company_id, category, status);

COMMIT;

-- Verificar inserción
-- SELECT * FROM public.con_tipo_transaccion WHERE company_id = 1 ORDER BY code;
