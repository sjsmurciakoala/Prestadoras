-- 1) Config contable para Captación
SELECT code, is_active,*
FROM public.cfg_document_type
WHERE company_id = 1 AND module = 'VENTAS' AND code IN ('REC','FAC');
/*resultado: 
code	is_active	document_type_id	company_id	module	code	name	description	requires_cai	is_active	created_at	created_by	updated_at	updated_by
FAC	True	1	1	VENTAS	FAC	Factura de servicios	Factura por servicios/consumo de agua	True	True	2026-03-04 16:47:29.889089-06	postgres	2026-03-05 13:03:43.495888-06	postgres
REC	True	2	1	VENTAS	REC	Recibo de cobro	Aplicacion de cobros a clientes	False	True	2026-03-04 16:47:29.889089-06	postgres	2026-03-05 13:03:43.495888-06	postgres
*/
SELECT module, document_type, name, is_active
FROM public.con_plantilla_partida_hdr
WHERE company_id = 1 AND module = 'VENTAS' AND document_type IN ('REC','FAC');
/*resultado:
module	document_type	name	is_active
VENTAS	FAC	E2E AUTO - VENTAS FAC	True
*/

-- 2) Manual: ¿hay saldos para ese cliente?
SELECT *
FROM fn_getclientesaldos_posteomanual('6090102789');
/*resultado:
r_ide	r_recibo	r_recibo_anterior	r_cliente	r_tiposervicio	r_monto	r_monto_distribuido	r_descripcion	r_estado
23	3075076	3075076	6090102789	SRV001	950.00	950.00	AGUA POTABLE	A
*/

-- 3) Misceláneos: ¿hay recibos tipo R para ese cliente?
SELECT numrecibo, clientecodigo, tipofactura, estado, saldototal, fechaemision
FROM factura
WHERE clientecodigo = '6090102789'
  AND tipofactura = 'R'
ORDER BY fechaemision DESC, numrecibo DESC;
--vacio