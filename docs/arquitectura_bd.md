# Arquitectura de Datos v2

## 1. Objetivo
Contar con un modelo de datos consistente, normalizado y extensible que permita operar los seis módulos funcionales definidos (Administración, Ventas, Compras, Bancos, Contabilidad y Configuración) garantizando la integración contable de todas las operaciones. Esta etapa se limita a diagnosticar el estado actual y fijar los lineamientos que usaremos en las siguientes fases.

## 2. Alcance de esta etapa
- Inventario del esquema existente (`Database/`, `SIAD.Core/Entities` y seeds vigentes).
- Identificación de problemas de nomenclatura, tipos y relaciones.
- Definición de estándares de modelado (nombres, claves, catálogos, auditoría).
- Mapeo preliminar de entidades por módulo considerando la trazabilidad contable.
- Plan de trabajo para la fase de rediseño (Etapa 2).

## 3. Inventario actual
| Fuente | Contenido | Observaciones |
| --- | --- | --- |
| `SIAD.Core/Entities/*` | Clases scaffoldeadas desde la BD actual. | Nombres en minúscula, ausencia de anotaciones, sin FK expresas. |
| `Database/TablasPostgrets/*.sql` | Scripts de tablas legacy (no todos actualizados). | Algunos archivos (p. ej. `orden_trabajo.sql`) no corresponden al esquema real. |
| `Database/Seeds/*.sql` | Seeds por módulo (clientes, captación, misceláneos, notas/cobranza, órdenes demo). | Útiles para pruebas, pero mezclan estilos y carecen de estándares de naming. |
| `docs/modulo_*.md` | Estado funcional por módulo. | Indican que casi todos los flujos ya migraron, pero no documentan el DDL definitivo. |

## 4. Hallazgos principales
1. **Inconsistencia de nombres:** tablas y columnas usan minúsculas sin separadores (`orden_trabajo`, `maestro_cliente_clave`), otras mezclan camelCase o abreviaturas poco claras (`tipo_d`, `ordent_mate`).  
2. **Falta de claves foráneas explícitas:** muchas relaciones existen sólo a nivel lógico (p. ej. `orden_trabajo.maestro_cliente_clave` → `cliente_maestro`). Esto dificulta la integridad y el diseño de migraciones.  
3. **Tipos y tamaños heterogéneos:** campos `varchar` con longitudes arbitrarias, date/time mezclados (`DateOnly`, `DateTime`, `char(1)` para estados).  
4. **Ausencia de auditoría estandarizada:** algunos registros tienen `usuariocreacion/fechacreacion`, otros no. No hay políticas uniformes para `updated_at`, `deleted_at`, etc.  
5. **Contabilidad desconectada:** existen tablas contables (`cnt_*`) pero no se documenta qué eventos de ventas/compras/bancos las alimentan; tampoco hay un plan de cuentas unificado.  
6. **Seeds sin normalización:** los scripts insertan datos de prueba útiles, pero no siguen un patrón (nombres en duro, sin catálogos) y no garantizan idempotencia total.

## 5. Estándares propuestos
### 5.1. Nomenclatura general
- **Tablas:** `modulo_entidad` en snake_case (ej. `ventas_factura`, `config_empresa`).  
- **Columnas:** `snake_case`, con prefijos sólo cuando agregan contexto (ej. `cliente_id`).  
- **PK:** `id` (serial/identity) o `entidad_id` cuando haya varias PK en la misma tabla.  
- **FK:** `fk_<tabla>_<columna>` y declaradas explícitamente en el DDL.  
- **Índices:** `ix_<tabla>_<columna>`.  

### 5.2. Campos obligatorios
- `created_at`, `created_by`, `updated_at`, `updated_by` para auditoría.  
- `status` como `varchar(20)` o enumeración referenciada cuando aplique.  
- `company_id` (o equivalente) para soportar múltiples empresas si se requiere.  

### 5.3. Catálogos y configuraciones
- Catálogos maestros (impuestos, tipos de documento, series) residirán en el módulo **Configuración** y se referenciarán mediante FK desde los demás módulos.  
- Documentar cada catálogo en `docs/configuracion_catalogos.md` (pendiente en Etapa 2).  

### 5.4. Integración contable
- Toda tabla que represente un movimiento financiero debe incluir campos de referencia contable (`ledger_entry_id`, `numero_poliza`, etc.) o disparar registros en tablas contables mediante stored procedures/servicios.  
- Definir plantillas de pólizas por operación (p. ej. factura de venta → cargo a clientes, abono a ingresos). Estas plantillas se almacenarán en catálogos para permitir parametrización.  

### 5.5. Tipos de datos
- Fechas: `date` para datos sin hora, `timestamptz` para eventos con hora/zona.  
- Estados: preferir FK a catálogos (`config_estado`) en lugar de `char(1)` sueltos.  
- Monto/moneda: `numeric(18,2)` con `currency_code` opcional si se soportan varias monedas.  

## 6. Módulos y entidades núcleo
| Módulo | Entidades principales | Integraciones | Comentarios |
| --- | --- | --- | --- |
| Administración | Empresa, usuarios, roles, permisos, parametrizaciones generales. | Configuración alimenta a todos. | Base para seguridad y multitenancy. |
| Configuración | Catálogos (impuestos, monedas, series, tipos de documento, centros de costo). | Ligado a Contabilidad y a cada módulo operativo. | Será el “maestro” de datos. |
| Ventas | Clientes, cotizaciones, órdenes de servicio, facturas, notas crédito/débito, cobranza. | Bancos (cobros), Contabilidad (pólizas). | Ya existen tablas (`factura`, `transaccion_abonado`), se normalizarán. |
| Compras | Proveedores, requisiciones, órdenes de compra, facturas proveedor, pagos. | Bancos (egresos), Contabilidad. | Hoy hay tablas `prv_*`, necesitan revisión. |
| Bancos | Cuentas bancarias, movimientos, conciliaciones. | Ventas/Compras (pagos), Contabilidad. | Tablas `bnc_*` requieren normalización de estados. |
| Contabilidad | Catálogo de cuentas, pólizas, asientos, centros de costo. | Todos los módulos. | Debe centralizar plan de cuentas y plantillas de asientos. |

## 7. Dependencias contables
- **Ventas:** cada factura, nota y cobro debe generar asientos (cliente vs ingreso, cuentas por cobrar, IVA).  
- **Compras:** órdenes y facturas de proveedor → cuentas por pagar, inventario/gasto, IVA crédito.  
- **Bancos:** depósitos y retiros → cuentas bancarias vs cuentas puente.  
- **Órdenes de servicio/corte:** si generan cargos o gastos deben reflejarse en contabilidad.  
- **Configuración:** define cuentas default por operación (ej. cuenta de ingresos por servicio, cuenta de mora).  

## 8. Roadmap (fases siguientes)
1. **Etapa 2 – Rediseño por módulo:**  
   - Exportar DDL real (pg_dump por tabla) y actualizar `Database/ddl_v2/`.  
   - Aplicar estándares anteriores tabla por tabla (nombres, PK/FK, auditoría).  
   - Documentar cada cambio y preparar scripts de migración.  
2. **Etapa 3 – Seeds y pruebas SQL:**  
   - Crear datasets consistentes por módulo (ventas, compras, bancos, contabilidad).  
   - Validar integraciones contables ejecutando transacciones completas en SQL.  
3. **Etapa 4 – Ajuste de servicios/API/UI:**  
   - Actualizar `SiadDbContext`, DTOs y servicios para usar la nueva estructura.  
   - Probar end-to-end con los seeds definitivos.  
4. **Etapa 5 – Documentación final:**  
   - Diagramas ER, manual de naming, plantillas de pólizas, guías de integración.  

## 9. Próximos pasos inmediatos
- Crear `Database/ddl_v2/` con exportes reales y comenzar por los catálogos críticos (empresa, plan de cuentas, impuestos).  
- Definir la plantilla de póliza por tipo de operación (tabla `contabilidad_template_poliza`).  
- Elaborar un registro de “referencias contables” para cada módulo (qué tabla/campo apunta a una póliza).  
- Alinear con el equipo funcional para validar los seis módulos y confirmar si se requiere multiempresa/multimoneda desde el inicio.

---

### Avances Etapa 2
- Se creó `Database/ddl_v2/00_readme.md` con las reglas de versionado.  
- Se añadió `01_configuracion_base.sql` con las tablas normalizadas para empresa, sucursales, monedas, impuestos y series de documentos (incluye auditoría, estados e índices).  
- Próximo bloque: catálogos contables (plan de cuentas, plantillas de póliza) y parametrización fiscal.
