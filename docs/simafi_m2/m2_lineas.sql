\pset pager off
\timing on
set work_mem = '512MB';

\echo '=== ventana de facturacion ==='
select min(fecha) desde, max(fecha) hasta, count(distinct recibo) recibos from simafi_stg.facturacion;

\echo ''
\echo '=== cargos por recibo: facturacion (sin 01) vs ledger (tipo_partida 01) ==='
drop table if exists simafi_stg._m2_lin cascade;
create table simafi_stg._m2_lin as
with f as (
  select recibo,
         round(sum(valor) filter (where trim(codigo) not in ('01','15','16','17')),2) cargos_fact,
         round(sum(valor) filter (where trim(codigo) = '01'),2) saldo_anterior,
         round(-sum(valor) filter (where trim(codigo) = '16'),2) pagos_fact
  from simafi_stg.facturacion group by recibo
),
t as (
  select recibo,
         round(sum(debitos) filter (where tipo_partida='01'),2) cargos_led,
         round(sum(creditos) filter (where transaccion='201'),2) pagos_led
  from simafi_stg.transaccion_abonado
  where recibo in (select recibo from simafi_stg.facturacion)
  group by recibo
)
select f.recibo, f.cargos_fact, f.saldo_anterior, f.pagos_fact,
       t.cargos_led, t.pagos_led,
       round(coalesce(t.cargos_led,0) - coalesce(f.cargos_fact,0),2) dif_cargos
from f left join t on t.recibo = f.recibo;

\echo ''
\echo '=== veredicto: los cargos reales coinciden? ==='
select case when cargos_led is null then 'recibo sin movimientos en ledger'
            when abs(dif_cargos) < 0.005 then 'CUADRA exacto'
            when abs(dif_cargos) <= 0.05 then 'dif <= 5 centavos'
            else 'DIFIERE' end g,
       count(*) recibos,
       round(sum(cargos_fact),2) suma_facturacion,
       round(sum(cargos_led),2) suma_ledger,
       round(sum(dif_cargos),2) dif
from simafi_stg._m2_lin group by 1 order by 2 desc;

\echo ''
\echo '=== contraste: si NO se descartara el codigo 01 ==='
select round(sum(cargos_fact),2) cargos_reales,
       round(sum(saldo_anterior),2) arrastre_01,
       round(sum(cargos_fact + coalesce(saldo_anterior,0)),2) si_se_migrara_todo,
       round(sum(cargos_led),2) ledger
from simafi_stg._m2_lin;

\echo ''
\echo '=== muestra de recibos que difieren ==='
select * from simafi_stg._m2_lin
where cargos_led is not null and abs(dif_cargos) > 0.05
order by abs(dif_cargos) desc limit 12;
