\pset pager off
\echo '=== A) ¿existe una fecha donde el acumulado igualó a totalmora? ==='
select case
         when totalmora = acum_final then 'CUADRA hoy'
         when f_match_mora is not null then 'cuadró en una fecha pasada'
         else 'NUNCA cuadró'
       end resultado,
       count(*) clientes
from simafi_stg._m2_match group by 1 order by 2 desc;

\echo ''
\echo '=== B) distribución de esa fecha (los que cuadraron en el pasado) ==='
select date_trunc('month', f_match_mora)::date mes, count(*) clientes
from simafi_stg._m2_match
where f_match_mora is not null and totalmora <> acum_final
group by 1 order by 2 desc limit 20;

\echo ''
\echo '=== C) lo mismo contra clientesaldos ==='
select case
         when saldo_cs is null then 'sin fila en clientesaldos'
         when saldo_cs = acum_final then 'CUADRA hoy'
         when f_match_cs is not null then 'cuadró en una fecha pasada'
         else 'NUNCA cuadró'
       end resultado,
       count(*) clientes
from simafi_stg._m2_match group by 1 order by 2 desc;

\echo ''
\echo '=== D) distribución de la fecha de match de clientesaldos ==='
select date_trunc('year', f_match_cs)::date anio, count(*) clientes
from simafi_stg._m2_match
where f_match_cs is not null and saldo_cs is distinct from acum_final
group by 1 order by 1;

\echo ''
\echo '=== E) los que NUNCA cuadraron contra totalmora: magnitud ==='
select count(*) clientes,
       round(sum(totalmora),2) suma_simafi,
       round(sum(acum_final),2) suma_calc,
       round(sum(acum_final-totalmora),2) dif
from simafi_stg._m2_match
where f_match_mora is null and totalmora <> acum_final;

\echo ''
\echo '=== F) muestra de los que nunca cuadraron ==='
select c, totalmora, acum_final, round(acum_final-totalmora,2) dif, f_ult
from simafi_stg._m2_match
where f_match_mora is null and totalmora <> acum_final
order by abs(acum_final-totalmora) desc limit 15;
