\pset pager off
\timing on
set work_mem='1GB';
with sinaplicar as (
  select p.pago_id, p.cliente_clave, round(p.monto_total - coalesce(a.aplicado,0),2) resto
  from public.adm_pago p
  left join (select pago_id, round(sum(monto_aplicado),2) aplicado
               from public.adm_pago_aplicacion where company_id=2 group by 1) a on a.pago_id=p.pago_id
  where p.company_id=2 and round(p.monto_total - coalesce(a.aplicado,0),2) > 0
),
saldocli as (
  select trim(cliente) c, round(sum(debitos)-sum(creditos),2) saldo
  from simafi_stg.transaccion_abonado where trim(coalesce(cliente,''))<>'' group by 1
)
select count(*) pagos_incompletos,
       count(distinct s.cliente_clave) clientes,
       round(sum(s.resto),2) monto_sin_aplicar,
       count(*) filter (where sc.saldo < 0) en_clientes_con_saldo_a_favor,
       round(sum(s.resto) filter (where sc.saldo < 0),2) monto_en_saldo_a_favor
from sinaplicar s join saldocli sc on sc.c = s.cliente_clave;

\echo ''
\echo '=== total de saldos a favor en el origen ==='
select count(*) clientes, round(sum(saldo),2) total
from (select trim(cliente) c, round(sum(debitos)-sum(creditos),2) saldo
        from simafi_stg.transaccion_abonado where trim(coalesce(cliente,''))<>'' group by 1) x
where saldo < 0;
