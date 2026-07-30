\pset pager off
\timing on
set work_mem = '512MB';

\echo '=== muestra de facturas ==='
select recibo, trim(clave) clave, emision, vence, pago, banco, aplicado, total
from simafi_stg.facturas order by recibo desc limit 8;

\echo ''
\echo '=== pagadas vs impagas ==='
select (pago is not null) tiene_pago, count(*) n, round(sum(total),2) suma_total
from simafi_stg.facturas group by 1;

\echo ''
\echo '=== cartera impaga por cliente vs ledger ==='
drop table if exists simafi_stg._m2_fact cascade;
create table simafi_stg._m2_fact as
select trim(clave) c,
       round(sum(total) filter (where pago is null),2) impago,
       count(*) filter (where pago is null) n_impagas,
       round(sum(total),2) total_emitido
from simafi_stg.facturas
where clave is not null
group by 1;
create index _m2_fact_c on simafi_stg._m2_fact(c);

select count(*) clientes,
       round(sum(coalesce(f.impago,0)),2) cartera_facturas,
       round(sum(m.acum_final),2) cartera_ledger,
       round(sum(m.acum_final - coalesce(f.impago,0)),2) dif,
       count(*) filter (where abs(m.acum_final - coalesce(f.impago,0)) < 0.005) cuadran
from simafi_stg._m2_match m left join simafi_stg._m2_fact f on f.c = m.c;
