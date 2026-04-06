# ✔️ INVENTARIO DE GAPS: QUÉ FALTA POR IMPLEMENTAR

**Propósito**: Lista exacta de qué falta para completar la estructura de Contabilidad  
**Fecha**: 23 de Diciembre 2025  
**Basado en**: Comparativa LEGADO vs Nueva Arquitectura

---

## 🎯 RESUMEN EJECUTIVO

**Total LEGADO**: 48 tablas  
**Implementado**: 4 tablas  
**Gap**: 44 tablas  
**Prioridad CRÍTICA**: 5 tablas (sin estas NO funciona contabilidad)  
**Prioridad ALTA**: 9 tablas (incompleto sin estas)  
**Prioridad MEDIA**: 20 tablas (futuro)  
**Prioridad BAJA**: 10 tablas (opcional)  

---

## 🔴 TIER 0: CRÍTICO ABSOLUTO (5 tablas)

Sin estas tablas, **NO hay contabilidad**. Son el núcleo transaccional.

### 1. `con_partida_hdr` (Encabezado de Pólizas)
- **Equivalente LEGADO**: `C01Entry`
- **Función**: Registro de pólizas contables
- **Campos principales**:
  - `voucher_id` (PK)
  - `company_id` (FK a cfg_company)
  - `period_id` (FK a con_periodo_contable)
  - `journal_id` (FK a con_diario)
  - `type_id` (FK a con_tipo_transaccion) ← NUEVA
  - `voucher_number` (código único por journal)
  - `voucher_date`
  - `description`
  - `total_debit`, `total_credit`
  - `is_balanced` (boolean)
  - `status` (DRAFT, POSTED, REVERSED)
  - `posted_at`, `posted_by`
- **Validaciones**:
  - Si status=POSTED: total_debit MUST = total_credit
  - No se puede editar si POSTED
  - Period MUST estar en `status_id = 0` (Abierto)
- **Auditoría**: created_at/by, updated_at/by
- **Índices**: company, period, journal, status, date
- **Criticidad**: 🔴🔴🔴 MÁXIMA

---

### 2. `con_partida_dtl` (Detalle de Pólizas)
- **Equivalente LEGADO**: `C01Trans`
- **Función**: Líneas de débito/crédito en pólizas
- **Campos principales**:
  - `line_id` (PK)
  - `voucher_id` (FK a con_partida_hdr) ON DELETE CASCADE
  - `line_number`
  - `account_id` (FK a con_plan_cuentas)
  - `cost_center_id` (FK a con_centro_costo, nullable)
  - `third_party_id` (FK a con_tercero, nullable)
  - `debit_amount`, `credit_amount`
  - `currency_code`, `exchange_rate`
  - `description`
  - `reference`
- **Validaciones**:
  - NO puede tener ambos debit_amount y credit_amount > 0
  - Exactamente una debe ser > 0 (naturaleza de asiento contable)
- **Índices**: voucher_id, account_id, cost_center_id
- **Criticidad**: 🔴🔴🔴 MÁXIMA

---

### 3. `con_apertura_saldo` (Saldos de Apertura)
- **Equivalente LEGADO**: `C01Opening`
- **Función**: Saldos iniciales de cuentas por período
- **Campos principales**:
  - `opening_id` (PK)
  - `company_id` (FK)
  - `period_id` (FK)
  - `account_id` (FK)
  - `cost_center_id` (FK, nullable)
  - `debit_amount`, `credit_amount`
  - `currency_code`, `exchange_rate`
  - `notes`
- **Validaciones**:
  - UNIQUE (company_id, period_id, account_id, cost_center_id)
  - Solo se registra una vez por período/cuenta
- **Índices**: company, period, account
- **Criticidad**: 🔴🔴🔴 MÁXIMA

---

### 4. `con_saldo_cuenta` (Saldos Actuales)
- **Equivalente LEGADO**: `C01AcctBalance` (parte de)
- **Función**: Saldos corrientes de cuentas por período
- **Campos principales**:
  - `balance_id` (PK)
  - `company_id` (FK)
  - `period_id` (FK)
  - `account_id` (FK)
  - `cost_center_id` (FK, nullable)
  - `beginning_debit` (del con_apertura_saldo)
  - `beginning_credit`
  - `period_debit` (suma de pólizas POSTED)
  - `period_credit`
  - `ending_debit` (beginning + period)
  - `ending_credit`
  - `last_updated`
- **Validaciones**:
  - UNIQUE (company_id, period_id, account_id, cost_center_id)
  - Se calcula automáticamente al registrar pólizas
- **Función**: Se ACTUALIZA (no inserta) cada vez que se registra póliza
- **Criticidad**: 🔴🔴🔴 MÁXIMA

---

### 5. `con_tipo_transaccion` (Clasificación de Pólizas)
- **Equivalente LEGADO**: `C01TransClass`
- **Función**: Tipos/categorías de pólizas
- **Campos principales**:
  - `type_id` (PK)
  - `company_id` (FK)
  - `code` (UK: DIARIO, AJUSTE, CIERRE, APERTURA)
  - `name`
  - `description`
  - `category`
  - `is_automatic` (se genera automáticamente)
  - `allows_cost_center` (permite ingresar centro costo)
  - `allows_third_party` (permite tercero)
  - `status`
- **Ejemplo de Data**:
  ```
  DIARIO    | Registro de operaciones normales | No automático, permite todo
  AJUSTE    | Ajustes de período               | No automático, permite CC
  CIERRE    | Cierre de período               | Automático, solo cuentas
  APERTURA  | Apertura de período             | Automático, solo apertura
  ```
- **Criticidad**: 🔴🔴🔴 MÁXIMA

---

## 🟠 TIER 1: MUY IMPORTANTE (9 tablas)

Incompleto sin estas. Contabilidad funciona pero sin información complementaria.

### 6. `con_balance_mensual` (Desglose Mensual de Saldos)
- **Equivalente LEGADO**: `C01AcctBalance` (parte de, con siMonth)
- **Función**: Saldos desglosados mes a mes
- **Campos principales**:
  - `monthly_balance_id` (PK)
  - `company_id, period_id, account_id, cost_center_id` (FKs)
  - `month_number` (1-12, 13 para acumulado)
  - `debit_amount, credit_amount`
  - `transaction_count`
- **Uso**: Reportes mensuales, análisis de tendencias
- **Criticidad**: 🟠🟠🟠 ALTA

---

### 7. `con_tercero` (Terceros Contables)
- **Equivalente LEGADO**: `C01Thirds`
- **Función**: Registro de clientes, proveedores, empleados, otras entidades
- **Campos principales**:
  - `third_party_id` (PK)
  - `company_id` (FK)
  - `code` (UK)
  - `name, description`
  - `tax_id`
  - `category` (CLIENTE, PROVEEDOR, EMPLEADO, OTRO)
  - `is_supplier, is_customer` (flags)
  - `status`
- **Nota**: ¿Reutilizar adm_cliente/adm_proveedor o crear independiente?
  - Recomendación: CREAR INDEPENDIENTE con relación 1:N a adm_cliente
- **Criticidad**: 🟠🟠🟠 ALTA

---

### 8. `con_tercero_cuenta` (CxC/CxP por Tercero)
- **Equivalente LEGADO**: `C01ThdAccount`
- **Función**: Deudas/Créditos de terceros
- **Campos principales**:
  - `third_account_id` (PK)
  - `third_party_id` (FK)
  - `company_id` (FK)
  - `account_type` (DEUDOR, ACREEDOR)
  - `account_id` (FK a con_plan_cuentas) - la cuenta contable
  - `balance_debit, balance_credit`
- **Uso**: Control de CxC/CxP
- **Criticidad**: 🟠🟠🟠 ALTA

---

### 9. `con_tercero_apertura_saldo` (Saldos de Terceros en Apertura)
- **Equivalente LEGADO**: `C01ThdOpening`
- **Función**: Saldos iniciales de terceros por período
- **Campos principales**:
  - `third_opening_id` (PK)
  - `third_party_id, company_id, period_id` (FKs)
  - `account_type` (DEUDOR, ACREEDOR)
  - `debit_amount, credit_amount`
- **Uso**: Base para CxC/CxP del período
- **Criticidad**: 🟠🟠🟠 ALTA

---

### 10. `con_libro_iva` (Registro Fiscal de IVA)
- **Equivalente LEGADO**: `C01LibroIVA`
- **Función**: Control de IVA débito (salida) y crédito (entrada) para fiscalización
- **Campos principales**:
  - `iva_register_id` (PK)
  - `company_id, period_id` (FKs)
  - `transaction_date`
  - `document_type, document_number`
  - `third_party_id` (FK a con_tercero)
  - `taxable_base, exempt_amount, tax_rate, tax_amount, total_amount`
  - `iva_type` (DEBIT_IVA, CREDIT_IVA)
  - `status` (RECORDED, REPORTED)
- **Criticidad**: 🟠🟠🟠 ALTA (obligatorio para fiscalización)

---

### 11. `con_activo_fijo` (Registro de Activos Fijos)
- **Equivalente LEGADO**: `C01FixedAssest`
- **Función**: Gestión de activos fijos
- **Campos principales**:
  - `asset_id` (PK)
  - `company_id, asset_type_id` (FKs)
  - `code, name, description`
  - `acquisition_date, in_service_date`
  - `acquisition_cost, salvage_value, useful_life_years`
  - `depreciation_method`
  - `accumulated_depreciation, current_value`
  - `asset_account_id, depreciation_account_id` (FKs a con_plan_cuentas)
  - `location, status`
- **Criticidad**: 🟠🟠🟠 ALTA

---

### 12. `con_activo_tipo` (Tipos de Activos Fijos)
- **Equivalente LEGADO**: `C01FixedType`
- **Función**: Catálogo de tipos de activos (Equipos, Muebles, Vehículos, etc.)
- **Campos principales**:
  - `type_id` (PK)
  - `company_id` (FK)
  - `code, name, description`
  - `default_depreciation_years`
  - `status`
- **Criticidad**: 🟠🟠🟠 ALTA

---

### 13. `con_deprecacion` (Cálculos de Depreciación)
- **Equivalente LEGADO**: Parcial en `C01FixedAssest` + `C01FixedAdd`
- **Función**: Registro de depreciaciones mensuales
- **Campos principales**:
  - `depreciation_id` (PK)
  - `asset_id, period_id` (FKs)
  - `month_number`
  - `depreciation_amount`
  - `accumulated_to_date`
  - `voucher_id` (FK a con_partida_hdr) - póliza de cierre generada
- **Validaciones**:
  - UNIQUE (asset_id, period_id, month_number)
- **Uso**: Generación automática de pólizas de cierre
- **Criticidad**: 🟠🟠🟠 ALTA

---

### 14. `con_detalle_tecnico` (Catálogo de Detalles)
- **Equivalente LEGADO**: `C01Detail`
- **Función**: Descripciones/detalles técnicos reutilizables
- **Campos principales**:
  - `detail_id` (PK)
  - `company_id` (FK)
  - `code` (UK)
  - `name, description`
  - `status`
- **Uso**: Reutilizable en análisis y reportes
- **Criticidad**: 🟠🟠 MEDIA-ALTA

---

## 🟡 TIER 2: IMPORTANTE (10 tablas)

Necesario para funcionalidad completa pero puede implementarse después.

### 15. `con_presupuesto_programa` (Programas Presupuestarios)
- **Equivalente LEGADO**: `C01BudProgram`
- **Función**: Agrupación de líneas presupuestarias
- **Criticidad**: 🟡🟡 MEDIA

---

### 16. `con_presupuesto_linea` (Líneas Presupuestarias)
- **Equivalente LEGADO**: `C01Budget`
- **Función**: Presupuestos por cuenta
- **Criticidad**: 🟡🟡 MEDIA

---

### 17. `con_traslado_presupuesto_cuenta` (Traslados Presupuestarios)
- **Equivalente LEGADO**: `C01BudTrfAcct`
- **Criticidad**: 🟡🟡 MEDIA

---

### 18. `con_traslado_presupuesto_programa`
- **Equivalente LEGADO**: `C01BudTrfPrg`
- **Criticidad**: 🟡🟡 MEDIA

---

### 19. `con_saldo_presupuesto` (Seguimiento Presupuestario)
- **Equivalente LEGADO**: `C01AcctBudget`
- **Criticidad**: 🟡🟡 MEDIA

---

### 20. `con_consolidacion_origen` (Orígenes de Consolidación)
- **Equivalente LEGADO**: `C01ConsolOrigin`
- **Criticidad**: 🟡🟡 MEDIA

---

### 21. `con_consolidacion_entrada` (Entradas Consolidadas)
- **Equivalente LEGADO**: `C01ConsolEntry`
- **Criticidad**: 🟡🟡 MEDIA

---

### 22. `con_consolidacion_transaccion` (Transacciones Consolidadas)
- **Equivalente LEGADO**: `C01ConsolTrans`
- **Criticidad**: 🟡🟡 MEDIA

---

### 23. `con_consolidacion_transaccion_tercero` (Consolidación por Tercero)
- **Equivalente LEGADO**: `C01ConsolTransThird`
- **Criticidad**: 🟡🟡 MEDIA

---

### 24. `con_configuracion_balance` (Configuración de Balance Sheet)
- **Equivalente LEGADO**: (C01BlSheet genera desde C01Account)
- **Función**: Mapeo: qué cuentas van al balance
- **Campos principales**:
  - `config_id` (PK)
  - `company_id, account_id` (FKs)
  - `balance_section` (ACTIVO_CORRIENTE, ACTIVO_NO_CORRIENTE, PASIVO_CORRIENTE, PATRIMONIO, etc.)
  - `line_number` (orden de aparición)
  - `is_total_line` (suma de grupo)
- **Criticidad**: 🟡🟡 MEDIA

---

## 🔵 TIER 3: FUTURO (10 tablas)

Útil pero puede implementarse mucho después.

### 25-34. Activos Fijos (Ubicaciones, Departamentos, Series, etc.)
- `con_activo_ubicacion` (Equivalente: `C01FixedLoc`)
- `con_activo_departamento` (Equivalente: `C01FixedDepart`)
- `con_activo_serie_numero` (Equivalente: `C01FixedSerial`)
- `con_activo_adicion` (Equivalente: `C01FixedAdd`)
- `con_transaccion_activo_fijo` (Equivalente: `C01TransFixed`)
- `con_transaccion_activo_fijo_linea` (Equivalente: `C01TransFixedItem`)
- Otros complementarios
- **Criticidad**: 🔵🔵 BAJA

---

## 📋 RESUMEN DE GAPS

```
TIER 0 (CRÍTICO - SIN ESTOS NO FUNCIONA):
├─ con_partida_hdr ✅ ESPECIFICADA
├─ con_partida_dtl ✅ ESPECIFICADA
├─ con_apertura_saldo ✅ ESPECIFICADA
├─ con_saldo_cuenta ✅ ESPECIFICADA
└─ con_tipo_transaccion ✅ ESPECIFICADA

TIER 1 (MUY IMPORTANTE):
├─ con_balance_mensual ✅ ESPECIFICADA
├─ con_tercero ✅ ESPECIFICADA
├─ con_tercero_cuenta ✅ ESPECIFICADA
├─ con_tercero_apertura_saldo ✅ ESPECIFICADA
├─ con_libro_iva ✅ ESPECIFICADA
├─ con_activo_fijo ✅ ESPECIFICADA
├─ con_activo_tipo ✅ ESPECIFICADA
├─ con_deprecacion ✅ ESPECIFICADA
└─ con_detalle_tecnico ✅ ESPECIFICADA

TIER 2 (IMPORTANTE - AFTER MVP):
├─ con_presupuesto_programa ⚠️ NO ESPECIFICADA
├─ con_presupuesto_linea ⚠️ NO ESPECIFICADA
├─ con_traslado_presupuesto_cuenta ⚠️ NO ESPECIFICADA
├─ con_traslado_presupuesto_programa ⚠️ NO ESPECIFICADA
├─ con_saldo_presupuesto ⚠️ NO ESPECIFICADA
├─ con_consolidacion_origen ⚠️ NO ESPECIFICADA
├─ con_consolidacion_entrada ⚠️ NO ESPECIFICADA
├─ con_consolidacion_transaccion ⚠️ NO ESPECIFICADA
├─ con_consolidacion_transaccion_tercero ⚠️ NO ESPECIFICADA
└─ con_configuracion_balance ⚠️ NO ESPECIFICADA

TIER 3 (FUTURO - OPTIONAL):
├─ con_activo_ubicacion ⚠️ NO ESPECIFICADA
├─ con_activo_departamento ⚠️ NO ESPECIFICADA
├─ con_activo_serie_numero ⚠️ NO ESPECIFICADA
├─ con_activo_adicion ⚠️ NO ESPECIFICADA
├─ con_transaccion_activo_fijo ⚠️ NO ESPECIFICADA
├─ con_transaccion_activo_fijo_linea ⚠️ NO ESPECIFICADA
└─ Otras... ⚠️ NO ESPECIFICADA
```

---

## 🛠️ QUÉ SE NECESITA POR COMPONENTE

### DTOs (SIAD.Core/DTOs/Contabilidad/)
```
✅ HECHO:
  - PlanCuentaDto
  - CentroCostoDto
  - PeriodoContableDto
  - DiarioDto

⚠️ FALTA:
  - TipoTransaccionDto
  - TipoTransaccionUpsertDto
  - PolizaDto
  - PolizaCrearDto
  - PolizaActualizarDto
  - PolizaLineaDto
  - AperturaSaldoDto
  - AperturaSaldoUpsertDto
  - SaldoCuentaDto
  - BalanceMensualDto
  - TerceroDto
  - TerceroUpsertDto
  - LibroIvaDto
  - ActivoFijoDto
  - DepreciacionDto
  - (+ 10 más para presupuestos)
```

### Servicios (SIAD.Services/Contabilidad/)
```
✅ HECHO:
  - IContabilidadCatalogosService
  - IPeriodoContableService

⚠️ FALTA:
  - IPolizaService (CRÍTICA)
  - IAperturaService (CRÍTICA)
  - ISaldosService (CRÍTICA)
  - ITipoTransaccionService
  - ITerceroService
  - ILibroIvaService
  - IActivoFijoService
  - IDepreciacionService
  - (+ 5 más para presupuestos)
```

### Controladores (apc/Controllers/Contabilidad/)
```
✅ HECHO:
  - ContabilidadEmpresasController
  - PeriodosContablesController

⚠️ FALTA:
  - PolizasController
  - AperturasSaldosController
  - SaldosController
  - TiposTransaccionController
  - TercerosController
  - LibroIvaController
  - ActivosFijosController
  - DepreciacionController
```

### HTTP Clients (apc.Client/Services/Contabilidad/)
```
✅ HECHO:
  - PlanCuentasClient
  - CentrosCostoClient
  - PeriodosContablesClient
  - DiariosClient

⚠️ FALTA:
  - PolizasClient
  - AperturasSaldosClient
  - SaldosClient
  - TiposTransaccionClient
  - TercerosClient
  - LibroIvaClient
  - ActivosFijosClient
```

### Componentes Blazor (apc.Client/Pages/Contabilidad/)
```
✅ HECHO:
  - (Revisar existentes)

⚠️ FALTA:
  - PolizasIndex.razor
  - PolizasEditar.razor
  - SaldosConsulta.razor
  - ActivosFijosIndex.razor
  - ActivosFijosEditar.razor
  - DepreciacionCalcular.razor
  - LibroIvaReporte.razor
  - TercerosIndex.razor
```

---

## 📊 MATRIZ DE ESFUERZO

| Componente | Tablas | DTOs | Servicios | Controllers | Clients | UI | Horas |
|-----------|--------|------|-----------|------------|---------|----|----|
| Pólizas | 2 | 5 | 1 | 1 | 1 | 2 | 16 |
| Saldos | 3 | 3 | 1 | 1 | 1 | 2 | 12 |
| Terceros | 3 | 3 | 1 | 1 | 1 | 2 | 12 |
| Activos Fijos | 2 | 2 | 1 | 1 | 1 | 2 | 12 |
| IVA | 1 | 2 | 1 | 1 | 1 | 1 | 8 |
| Presupuestos | 5 | 5 | 2 | 2 | 2 | 2 | 20 |
| Consolidaciones | 4 | 2 | 2 | 1 | 1 | 1 | 12 |
| **TOTAL CRÍTICA** | **5** | **6** | **2** | **2** | **2** | **4** | **32** |
| **TOTAL v1.0** | **14** | **14** | **6** | **6** | **6** | **10** | **68** |
| **TOTAL COMPLETA** | **44** | **30** | **15** | **15** | **15** | **25** | **150** |

---

## 🚀 RECOMENDACIÓN DE IMPLEMENTACIÓN

### MVP (Semana 1-2: 32 horas)
✅ Pólizas (crear, registrar, reversar)  
✅ Saldos (consultar, actualizar automático)  
✅ Apertura de saldos  
✅ Tipos de transacción  
❌ Terceros (Phase 2)  
❌ Activos Fijos (Phase 2)  
❌ IVA (Phase 2)  

### v1.0 (Semana 3-5: 36 horas más)
✅ Todo MVP  
✅ Terceros (CxC/CxP)  
✅ Activos Fijos + Depreciación  
✅ Libro IVA  
✅ Reportes Balance + P&L  
❌ Presupuestos (Phase 3)  

### v1.5 (Semana 6-7: 50 horas más)
✅ Todo v1.0  
✅ Presupuestos  
✅ Consolidaciones  
✅ Reportes avanzados  

---

**Total Estimado: 10-12 semanas (vs. 48 en LEGADO)**

---

**Documento compilado**: 23 Diciembre 2025  
**Estado**: ✅ LISTO PARA PRIORIZACIÓN DE TRABAJO


