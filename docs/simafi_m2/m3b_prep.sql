\timing on
set work_mem='512MB';

-- Cabecera de factura por (cliente, recibo) — la clave natural verificada.
drop table if exists simafi_stg._m3_factura cascade;
create table simafi_stg._m3_factura as
select trim(t.cliente)              as cliente,
       t.recibo                     as recibo,
       min(t.fecha_docu)            as fecha_emision,
       max(t.plazo)                 as plazo,
       max(trim(coalesce(t.periodo,''))) as periodo,
       round(sum(t.debitos),2)      as total,
       count(*)                     as n_lineas,
       bool_or(trim(coalesce(t.tiene_med,''))='S') as con_medicion,
       max(trim(coalesce(t.ciclo,''))) as ciclo
from simafi_stg.transaccion_abonado t
where t.debitos > 0 and t.recibo is not null and t.recibo <> 0
group by trim(t.cliente), t.recibo;

create index _m3_factura_cli on simafi_stg._m3_factura(cliente);
create index _m3_factura_rec on simafi_stg._m3_factura(cliente, recibo);

select 'cabeceras' paso, count(*) facturas, count(distinct cliente) clientes,
       round(sum(total),2) importe, min(fecha_emision) desde, max(fecha_emision) hasta
from simafi_stg._m3_factura;

-- ¿cuántas facturas por ciclo? (para elegir el ciclo piloto y proyectar)
select m.ciclo, count(*) facturas, count(distinct f.cliente) clientes, round(sum(f.total),2) importe
from simafi_stg._m3_factura f
join simafi_stg.maestrosep m on trim(m.clave) = f.cliente
group by 1 order by 2 desc;
