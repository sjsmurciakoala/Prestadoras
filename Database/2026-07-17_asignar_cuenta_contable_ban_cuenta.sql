-- =============================================================================
-- Bancos: asignar la cuenta contable (cont_account_id) a las cuentas bancarias
--         que hoy la tienen en NULL, emparejando por NOMBRE del banco.
-- Fecha: 2026-07-17
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUE
-- El pago a proveedores (procesamiento "Emitir pago" y abonos) asienta el Banco al
-- Haber, y para eso necesita la cuenta contable de la cuenta bancaria de origen
-- (ban_cuenta.cont_account_id). El combo de "medio de pago" solo lista las cuentas
-- que tienen esa cuenta contable asignada (via GetCuentasContraProcesamientoAsync).
-- Las cuentas del tenant (company 2, modo consolidado) se migraron desde SIMAFI SIN
-- ese vinculo (cont_account_id NULL), por lo que el combo salia vacio.
--
-- CRITERIO (definido con el usuario): emparejar por nombre de banco. Solo se asignan
-- las que tienen una cuenta contable con ese nombre; las demas quedan sin asignar y
-- NO se mostraran (p.ej. "Banco Central", que no tiene cuenta contable en el plan).
-- Todas las cuentas de un mismo banco apuntan a la MISMA cuenta contable del banco
-- (el plan tiene una sola cuenta contable por banco, no una por cuenta bancaria).
--
-- Se resuelve la cuenta contable por CODIGO (no por account_id), porque los account_id
-- son identity y pueden diferir entre el mirror y el SRV.
--   11102010301 -> Banco Occidente
--   11102010501 -> Banco de los Trabajadores
--
-- Cambio ADITIVO y reversible: solo llena cont_account_id donde estaba NULL.
-- Idempotente: el WHERE cont_account_id IS NULL evita re-pisar en re-ejecuciones.
-- Ajustar company_id si el tenant en el SRV no fuera 2.
-- =============================================================================
BEGIN;

-- Banco de Occidente
UPDATE public.ban_cuenta AS bc
   SET cont_account_id = c.account_id,
       updated_at      = now(),
       updated_by      = 'asignacion-cta-contable'
  FROM public.con_plan_cuentas AS c
 WHERE bc.company_id      = 2
   AND bc.activo          = TRUE
   AND bc.cont_account_id IS NULL
   AND bc.banco_nombre ILIKE '%OCCIDENTE%'
   AND c.company_id       = bc.company_id
   AND btrim(c.code)      = '11102010301'
   AND c.allows_posting   = TRUE;

-- Banco de los Trabajadores
UPDATE public.ban_cuenta AS bc
   SET cont_account_id = c.account_id,
       updated_at      = now(),
       updated_by      = 'asignacion-cta-contable'
  FROM public.con_plan_cuentas AS c
 WHERE bc.company_id      = 2
   AND bc.activo          = TRUE
   AND bc.cont_account_id IS NULL
   AND bc.banco_nombre ILIKE '%TRABAJADORES%'
   AND c.company_id       = bc.company_id
   AND btrim(c.code)      = '11102010501'
   AND c.allows_posting   = TRUE;

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar)
-- =============================================================================
-- SELECT bc.banco_nombre, count(*) AS cuentas, count(bc.cont_account_id) AS con_contable
--   FROM public.ban_cuenta bc
--  WHERE bc.company_id = 2 AND bc.activo = TRUE
--  GROUP BY bc.banco_nombre ORDER BY bc.banco_nombre;
--   -> Occidente y Trabajadores con con_contable = cuentas; Banco Central con 0.
--
-- SELECT bc.nombre, c.code, c.name
--   FROM public.ban_cuenta bc
--   JOIN public.con_plan_cuentas c ON c.account_id = bc.cont_account_id
--  WHERE bc.company_id = 2 AND bc.activo = TRUE ORDER BY bc.nombre;
-- =============================================================================
