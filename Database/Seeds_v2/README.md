# Seeds v2

Orden recomendado para poblar la base `siad_v2` una vez ejecutados los DDL `Database/ddl_v2/*.sql`.

1. **01_cfg_configuracion_seed.sql**  
   - Monedas (HNL base + USD de referencia).  
   - Empresa demo, sucursal matriz.  
   - Impuestos/retenciones base.  
   - Tipos y series de documentos para Ventas, Compras y Bancos.

2. **02_con_contabilidad_seed.sql**  
   - Plan de cuentas normalizado, centros de costo, diarios y períodos del año en curso.  
   - Plantillas de póliza para factura/recibo de ventas y factura/pago de compras.  
   - Reglas de integración automática ligadas a los tipos de documento configurados.

3. **03_adm_security_seed.sql**  
   - Roles y claims en ASP.NET Identity (schema identity) para superadmin/operativo/operador.  
   - Usuarios demo (sysadmin / Admin123$, opcom / Oper123$) con asignacion de roles.

4. **04_adm_maestros_seed.sql**  
   - Zonas, rutas, clientes, proveedores, servicios, productos, depósitos, instrumentos, operaciones.  
   - Listas de precio, lotes de facturación, ofertas, retenciones y configuraciones de reporte.

5. **05_ven_operaciones_seed.sql**  
   - Dos facturas demo (residencial/empresarial), líneas e impuestos calculados.  
   - Nota de crédito parcial y recibo con aplicación contra facturas.

6. **06_com_operaciones_seed.sql**  
   - Orden de compra demo, factura asociada y pago aplicado con detalle.  
   - Usa proveedores/materiales cargados en los maestros.

7. **07_ban_movimientos_seed.sql**  
   - Cuentas bancarias demo, movimientos ligados a cobros/pagos y actualización de referencias en ventas/compras.

8. **08_inv_movimientos_seed.sql**  
   - Categoría/producto/almacén base, existencias iniciales y movimientos de ingreso/egreso enlazados a compras/ventas.

9. **09_adm_transacciones_seed.sql**  
   - Resúmenes y movimientos administrativos CxC/CxP, cálculo de interés y ajustes fiscales, bitácora operativa.

10. **10_qa_operaciones_avanzadas.sql**  
    - Escenarios QA de retenciones, nota de débito y pagos especiales en ventas/compras para pruebas.

Cada script seguirá el mismo patrón: bloques `DO $$` idempotentes con `INSERT ... ON CONFLICT` para permitir múltiples ejecuciones sin duplicados. Ejecuta los seeds en el orden descrito para cumplir dependencias (ej. Ventas requiere maestros y plantillas contables previamente cargados).
