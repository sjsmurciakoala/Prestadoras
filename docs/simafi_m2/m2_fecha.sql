\timing on
set work_mem = '512MB';
set maintenance_work_mem = '1GB';

-- 1) neto por cliente y día
drop table if exists simafi_stg._m2_dia cascade;
create table simafi_stg._m2_dia as
select trim(cliente) c, fecha_docu f, sum(debitos-creditos) neto, count(*) n
from simafi_stg.transaccion_abonado
where fecha_docu is not null
group by 1,2;
create index _m2_dia_cf on simafi_stg._m2_dia(c,f);
select 'dia' paso, count(*) filas, count(distinct c) clientes from simafi_stg._m2_dia;

-- 2) saldo acumulado por cliente y día
drop table if exists simafi_stg._m2_acum cascade;
create table simafi_stg._m2_acum as
select c, f, round(sum(neto) over (partition by c order by f rows unbounded preceding),2) acum
from simafi_stg._m2_dia;
create index _m2_acum_c on simafi_stg._m2_acum(c);
select 'acum' paso, count(*) filas from simafi_stg._m2_acum;

-- 3) oraculos por cliente
drop table if exists simafi_stg._m2_oraculo cascade;
create table simafi_stg._m2_oraculo as
select trim(m.clave) c,
       coalesce(m.totalmora,0) totalmora,
       (select sum(s.saldo) from simafi_stg.clientesaldos s where trim(s.cliente)=trim(m.clave)) saldo_cs
from simafi_stg.maestrosep m;
create index _m2_or_c on simafi_stg._m2_oraculo(c);

-- 4) ultima fecha en que el acumulado igualo al oraculo (totalmora)
drop table if exists simafi_stg._m2_match cascade;
create table simafi_stg._m2_match as
select o.c, o.totalmora, o.saldo_cs,
       (select max(f) from simafi_stg._m2_acum a where a.c=o.c and a.acum = o.totalmora) f_match_mora,
       (select max(f) from simafi_stg._m2_acum a where a.c=o.c and a.acum = o.saldo_cs)  f_match_cs,
       (select max(f) from simafi_stg._m2_acum a where a.c=o.c) f_ult,
       (select acum from simafi_stg._m2_acum a where a.c=o.c order by f desc limit 1) acum_final
from simafi_stg._m2_oraculo o;
select 'match' paso, count(*) from simafi_stg._m2_match;
