\pset pager off
\timing on
set work_mem = '512MB';

\echo '=== veredicto con la regla REFINADA: excluir 01 y 12 ==='
with f as (
  select recibo,
         round(sum(valor) filter (where trim(codigo) not in ('01','12','15','16','17')),2) cargos_fact
  from simafi_stg.facturacion group by recibo
)
select case when l.cargos_led is null then 'sin movimientos en ledger'
            when abs(coalesce(l.cargos_led,0) - coalesce(f.cargos_fact,0)) < 0.005 then 'CUADRA exacto'
            when abs(coalesce(l.cargos_led,0) - coalesce(f.cargos_fact,0)) <= 0.05 then 'dif <= 5 centavos'
            else 'DIFIERE' end g,
       count(*) recibos,
       round(sum(f.cargos_fact),2) suma_facturacion,
       round(sum(l.cargos_led),2) suma_ledger,
       round(sum(coalesce(l.cargos_led,0)-coalesce(f.cargos_fact,0)),2) dif
from simafi_stg._m2_lin l join f on f.recibo = l.recibo
group by 1 order by 2 desc;

\echo ''
\echo '=== codigo 06 Gestion Legal: esta en el ledger? ==='
select f.recibo, f.valor, l.cargos_fact, l.cargos_led, l.saldo_anterior
from simafi_stg.facturacion f join simafi_stg._m2_lin l on l.recibo=f.recibo
where trim(f.codigo)='06';

\echo ''
\echo '=== que queda difiriendo tras excluir 01 y 12 ==='
with f as (
  select recibo, round(sum(valor) filter (where trim(codigo) not in ('01','12','15','16','17')),2) cargos_fact
  from simafi_stg.facturacion group by recibo
)
select l.recibo, f.cargos_fact, l.cargos_led, l.saldo_anterior,
       round(coalesce(l.cargos_led,0)-coalesce(f.cargos_fact,0),2) dif
from simafi_stg._m2_lin l join f on f.recibo=l.recibo
where l.cargos_led is not null and abs(coalesce(l.cargos_led,0)-coalesce(f.cargos_fact,0)) > 0.05
order by abs(coalesce(l.cargos_led,0)-coalesce(f.cargos_fact,0)) desc limit 15;

\echo ''
\echo '=== pagos: codigo 16 vs transaccion 201 (misma ventana) ==='
with f as (select recibo, round(-sum(valor) filter (where trim(codigo)='16'),2) pagos_fact from simafi_stg.facturacion group by recibo)
select count(*) filter (where coalesce(f.pagos_fact,0)<>0 or coalesce(l.pagos_led,0)<>0) recibos_con_pago,
       round(sum(coalesce(f.pagos_fact,0)),2) pagos_facturacion,
       round(sum(coalesce(l.pagos_led,0)),2) pagos_ledger
from simafi_stg._m2_lin l join f on f.recibo=l.recibo;
