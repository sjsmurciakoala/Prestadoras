\pset pager off
\timing on
set work_mem='1GB';

\echo '=== V1) VOLUMEN: origen vs portal ==='
select 'clientes' concepto,
       (select count(distinct trim(clave)) from simafi_stg.maestrosep) origen,
       (select count(*) from public.cliente_maestro where company_id=2) portal
union all select 'movimientos del libro',
       (select count(*) from simafi_stg.transaccion_abonado where trim(coalesce(cliente,''))<>''),
       (select count(*) from public.transaccion_abonado where company_id=2)
union all select 'lineas de cargo',
       (select count(*) from simafi_stg.transaccion_abonado where debitos>0 and recibo is not null and recibo<>0),
       (select count(*) from public.factura_detalle where company_id=2)
union all select 'movimientos de credito',
       (select count(*) from simafi_stg.transaccion_abonado where creditos>0),
       (select count(*) from public.adm_pago where company_id=2);

\echo ''
\echo '=== V2) DINERO: origen vs portal ==='
select 'cargos' concepto,
       (select round(sum(debitos),2) from simafi_stg.transaccion_abonado where debitos>0 and recibo is not null and recibo<>0) origen,
       (select round(sum(montovalor),2) from public.factura_detalle where company_id=2) portal
union all select 'creditos',
       (select round(sum(creditos),2) from simafi_stg.transaccion_abonado where creditos>0),
       (select round(sum(monto_total),2) from public.adm_pago where company_id=2)
union all select 'aplicado a documentos',
       null,
       (select round(sum(monto_aplicado),2) from public.adm_pago_aplicacion where company_id=2);

\echo ''
\echo '=== V3) SALDO POR CLIENTE (criterio de aceptacion) ==='
WITH origen AS (
    SELECT trim(t.cliente) c, round(sum(t.debitos)-sum(t.creditos),2) saldo
    FROM simafi_stg.transaccion_abonado t WHERE trim(COALESCE(t.cliente,''))<>'' GROUP BY 1),
libro AS (
    SELECT p.cliente_clave c, round(sum(p.debitos)-sum(p.creditos),2) saldo
    FROM public.transaccion_abonado p WHERE p.company_id=2 GROUP BY 1),
docs AS (
    SELECT f.clientecodigo c, round(sum(d.montovalor_saldo),2) saldo
    FROM public.factura_detalle d JOIN public.factura f ON f.id=d.factura_id AND f.company_id=2
    WHERE d.company_id=2 GROUP BY 1)
SELECT count(*) clientes,
       count(*) FILTER (WHERE abs(o.saldo - COALESCE(lb.saldo,0)) < 0.005) cuadra_libro,
       count(*) FILTER (WHERE abs(GREATEST(o.saldo,0) - COALESCE(dc.saldo,0)) < 0.005) cuadra_documentos,
       round(sum(o.saldo),2) saldo_origen,
       round(sum(COALESCE(lb.saldo,0)),2) saldo_libro_portal,
       round(sum(COALESCE(dc.saldo,0)),2) saldo_documentos
FROM origen o LEFT JOIN libro lb ON lb.c=o.c LEFT JOIN docs dc ON dc.c=o.c;

\echo ''
\echo '=== V4) INVARIANTE: cada pago aplica exactamente su monto ==='
select count(*) pagos_con_aplicacion,
       count(*) filter (where abs(p.monto_total - a.aplicado) < 0.005) invariante_ok,
       count(*) filter (where abs(p.monto_total - a.aplicado) >= 0.005) invariante_rota
from public.adm_pago p
join (select pago_id, round(sum(monto_aplicado),2) aplicado
        from public.adm_pago_aplicacion where company_id=2 group by 1) a on a.pago_id=p.pago_id
where p.company_id=2;

\echo ''
\echo '=== V5) INTEGRIDAD: huerfanos ==='
select (select count(*) from public.factura_detalle d where d.company_id=2
         and not exists (select 1 from public.factura f where f.id=d.factura_id)) detalle_sin_factura,
       (select count(*) from public.factura f where f.company_id=2
         and not exists (select 1 from public.cliente_maestro c where c.company_id=2 and trim(c.maestro_cliente_clave)=f.clientecodigo)) factura_sin_cliente,
       (select count(*) from public.adm_pago_aplicacion a where a.company_id=2
         and not exists (select 1 from public.adm_pago p where p.pago_id=a.pago_id)) aplicacion_sin_pago;
