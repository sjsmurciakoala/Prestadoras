# Plan de Trabajo – Módulo de Contabilidad (con\_*)  

## 1. Objetivo  
Implementar completamente la capa contable del proyecto SIAD-Blazor para que cada transacción operativa genere, valide y consulte pólizas contables; además, proveer libros oficiales (diario, mayor, balances) y capacidades de control de períodos/cierres.

## 2. Alcance Funcional  
### 2.1 Catálogos y parámetros  
- Administración del **Plan de Cuentas** (`con_plan_cuentas`) con niveles ilimitados y atributos (tipo, categoría, cuenta padre).  
- **Centros de costo**: alta, edición y asignación a líneas de póliza/movimientos de inventario y gastos.  
- **Diarios contables** (generales, ventas, compras, bancos) con control de secuencias.  
- Catalogo de bancos (`ban_banco`): gestion de bancos y sucursales.  
- **Períodos contables**: apertura, bloqueo, cierre con registro de usuario/fecha.  
- **Plantillas y reglas de integración** (`con_plantilla_partida_hdr`, `con_regla_integracion`): definición por módulo/documento, fórmulas (`{total}`, `{iva}`, `{retencion_isr}`) y cost centers.  
- Parámetros globales (cuentas por defecto, diarios por módulo, tolerancias de cuadratura).

### 2.2 Procesos contables  
1. **Generación automática de pólizas**  
   - Triggers/servicios que, al confirmar documentos de Ventas, Compras, Bancos, Inventarios y Administración, crean `con_partida_hdr` + `con_partida_dtl`.  
   - Reutilizan las fórmulas de plantillas y las reglas de integración; admiten complementos (ej. retenciones, notas).  
   - Manejo de vouchers reversibles (VOID) y re-procesamiento idempotente.  

2. **Pólizas manuales**  
   - UI/API para capturar asientos manuales por diario especificando cuentas, centros de costo, descripciones.  
   - Validaciones: periodos abiertos, cuadratura débito=crédito, bloqueo por usuario/rol.  

3. **Conciliación y cierres**  
   - Asociación entre `ban_conciliacion` y `con_partida_hdr` para marcar movimientos conciliados.  
   - Proceso de cierre mensual: bloquea períodos, genera asiento de cierre y traspaso a resultados acumulados.  

4. **Libros contables y reportes**  
   - Libro Diario y Mayor (filtros por periodo, diario, cuenta).  
   - Balances de comprobación y estados básicos (Balance General, Estado de Resultados).  
   - Exportaciones a Excel/PDF y soporte para secuencias oficiales (CAI/RTN si aplica).  

## 3. Integraciones por módulo  
| Módulo                | Documentos origen                         | Plantilla/Regla clave                        | Resultado contable                               |
|-----------------------|-------------------------------------------|----------------------------------------------|--------------------------------------------------|
| Ventas                | Facturas, Notas crédito/débito, Cobros    | `Factura servicios`, `Nota crédito`, `Cobro` | CxC vs Ingresos/IVA, ajustes y aplicación pagos  |
| Compras               | Órdenes (opcional), Facturas, Pagos       | `Factura proveedor`, `Pago proveedor`        | CxP vs Gastos/IVA, retenciones ISR/ISV           |
| Bancos                | Ingresos/Egresos/Transferencias           | Diario Bancos                                | Movimientos de caja/bancos y conciliación        |
| Inventarios           | INGRESO/EGRESO/Ajuste/Traslado            | Plantillas por tipo                          | Inventario vs Costo/Gasto/Ingreso                |
| Administración        | CxC/CxP administrativos, intereses, ajustes | Plantillas específicas                       | Regularizaciones fiscales y log de operaciones   |

## 4. Plan de Trabajo (fases)
### Fase 1 – Fundamentos técnicos (1-2 sprints)  
1. Revisar/completar seeds `02_con_contabilidad_seed.sql` con ejemplos de pólizas reales y más cuentas auxiliares.  
2. Construir servicios API para:  
   - Catálogo de cuentas (listar, crear, actualizar).  
   - Centros de costo, diarios, períodos.  
   - Plantillas/reglas (CRUD + pruebas de fórmulas).  
3. UI DevExpress para administración de estos catálogos.  
4. Automatizar validaciones:  
   - Debe existir periodo abierto.  
   - Cuentas no permiten movimientos si `allows_posting=false`.  

### Fase 2 – Integraciones automáticas (2-3 sprints)  
1. Diseñar servicio `AccountingIntegrationService` con métodos por módulo (`PostSalesDocument`, `PostPurchaseDocument`, etc.).  
2. Implementar en Ventas/Compras/Bancos los hooks post-estado que invocan al servicio y registran `con_partida_hdr`.  
3. Manejo de reintentos/idempotencia: se marca el documento origen con `con_partida_hdr_id` y no se vuelve a generar si ya existe.  
4. Pruebas unitarias/integración simulando facturas con retenciones, notas y pagos parciales.  

### Fase 3 – Pólizas manuales y reportes (2 sprints)  
1. API/UI para captura manual (plantilla vacía, validaciones).  
2. Implementar consultas:  
   - Libro Diario (`con_partida_hdr`+`con_partida_dtl`).  
   - Libro Mayor (saldos acumulados por cuenta).  
   - Balance de comprobación (período seleccionado).  
3. Exportación a Excel/PDF y filtros (por diario, cuenta, centro de costo).  

### Fase 4 – Cierres y conciliación (1-2 sprints)  
1. Proceso de cierre mensual: bloquea período, genera asientos de cierre y apertura.  
2. Conciliación bancaria UI (match entre `ban_movimiento` y saldos).  
3. Bitácora y auditoría: registrar usuario/fecha en cada acción contable.  

### Fase 5 – QA y escenarios adicionales  
1. Ejecutar seeds QA (retenciones/notas/pagos especiales) y derivar pólizas para cada uno.  
2. Casos multi-divisa y revaluaciones (si aplica posteriormente).  
3. Documentación técnica: diagramas de flujo, guía de operación y plan de pruebas.  

## 5. Dependencias y riesgos  
- Necesario que todos los módulos operativos usen los códigos exactos (clientes/proveedores, servicios) definidos en los seeds.  
- Cambios en plan de cuentas o plantillas deben versionarse; se recomienda migraciones controladas.  
- Requiere coordinación con el backend actual para exponer endpoints de `con_*`.  
- Auditoría/seguridad: las operaciones contables deben quedar registradas y protegidas por roles/claims de ASP.NET Identity (`identity.AspNetRoles`, `AspNetRoleClaims`).  

## 6. Resultado esperado  
Al finalizar el plan, SIAD-Blazor contará con un módulo contable robusto que:  
- Permite administrar catálogos y períodos con seguridad.  
- Genera automáticamente los asientos de cada documento operativo.  
- Ofrece libros oficiales y cierres mensuales.  
- Sirve de base para reportería fiscal/financiera y auditorías externas.
## 7. Progreso inicial
- Se incorporaron las entidades `con_*` en `SIAD.Core` y se configuró `SiadDbContext` para mapear plan de cuentas, centros de costo, diarios, periodos y pólizas definidos en el DDL v2.
- Se agregó `IContabilidadCatalogosService` en `SIAD.Services` con operaciones básicas para cuentas, centros, diarios y periodos (alta/edición/listado), desbloqueando el Sprint 1 de la Fase 1.

## 8. Avances al 2026-01-14
- Catálogos listos en UI/API: plan de cuentas, centros de costo, tipos de transacción y reglas por rangos (cuentas/centros/terceros). Reglas validadas contra catálogos y línea correlativa automática.
- DDL y mapeo de `con_partida_hdr` y `con_partida_dtl` completos (header/detalle de comprobantes).
- Aperturas (`con_apertura_saldo`, `con_apertura_centro_costo`) y saldos (`con_saldo_cuenta`, `con_balance_mensual`) ya modelados.

## 9. Pendientes inmediatos (para comprobantes manuales)
1) Agregar a `con_partida_hdr` el `type_id` (FK a `con_tipo_transaccion`), campos de control de posteo (`posted_at/by`, `status` Draft/Posted/Void) y totales de control opcionales (`total_debit/credit`).
2) Agregar `third_party_id` a `con_partida_dtl` para soportar terceros cuando el tipo lo requiera.
3) Servicio/API de pólizas manuales:
   - CRUD header + líneas, validación de cuadratura y período abierto.
   - Aplicar reglas del tipo (rangos y flags allows_cost_center/third/cash_flow).
   - Generar correlativo por diario/tipo y bloquear edición al postear.
4) Rutina de “posteo” que actualice `con_saldo_cuenta`/`con_balance_mensual` y controle estado de período.
5) UI DevExpress para captura de pólizas manuales con autocompletes de cuenta/centro/tercero.



