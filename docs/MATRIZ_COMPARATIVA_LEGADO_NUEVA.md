# MATRIZ COMPARATIVA: LEGADO vs NUEVA ARQUITECTURA

**Documento Visual**: Comparación detallada tabla por tabla  
**Propósito**: Validación de cobertura funcional

---

## 📊 TABLA COMPARATIVA - TODAS LAS 48 TABLAS LEGADO

### LEYENDA
- ✅ **HECHO**: Implementado en nueva arquitectura
- 🔄 **EQUIVALENTE**: Reutiliza existente (Admin, Identity, etc.)
- ⚠️ **PENDIENTE**: Especificado, no codificado
- ❌ **NO NECESARIO**: Obsoleto o reemplazado por funcionalidad app
- ❓ **REVISAR**: Requiere decisión de negocio

---

## TABLAS C01 (49 TOTAL - LEGADO MD_CONTAB)

| # | Tabla LEGADO | Nueva Tabla | Estado | Observaciones |
|----|--------------|------------|--------|---------------|
| 1 | **C01Account** | **con_plan_cuentas** | ✅ HECHO | Migrado a multiempresa con jerarquía |
| 2 | **C01Periods** | **con_periodo_contable** | ✅ HECHO | Estados: OPEN, CLOSED, LOCKED |
| 3 | **C01CostCenter** | **con_centro_costo** | ✅ HECHO | Ahora multiempresa, auditoría |
| 4 | **C01TransClass** | **con_tipo_transaccion** | ⚠️ PENDIENTE | Categorías: DIARIO, AJUSTE, CIERRE, APERTURA |
| 5 | **C01Entry** | **con_poliza** | ⚠️ PENDIENTE | Header de póliza contable |
| 6 | **C01Trans** | **con_poliza_linea** | ⚠️ PENDIENTE | Detalle de póliza (débito/crédito) |
| 7 | **C01Detail** | **con_detalle_tecnico** | ⚠️ PENDIENTE | Catálogo de detalles descriptivos |
| 8 | **C01Opening** | **con_apertura_saldo** | ⚠️ PENDIENTE | Saldos iniciales por período |
| 9 | **C01AcctBalance** | **con_saldo_cuenta** | ⚠️ PENDIENTE | Saldos actuales por cuenta |
| 10 | **C01Months** | (Innecesario) | ❌ NO NECESARIO | Se calcula desde `con_periodo_contable` |
| 11 | **C01Budget** | **con_presupuesto_linea** | ⚠️ PENDIENTE | Líneas presupuestarias (Phase 2) |
| 12 | **C01BudProgram** | **con_presupuesto_programa** | ⚠️ PENDIENTE | Programas presupuestarios (Phase 2) |
| 13 | **C01BudTrfAcct** | **con_traslado_presupuesto_cuenta** | ⚠️ PENDIENTE | Traslados entre cuentas presupuestarias (Phase 2) |
| 14 | **C01BudTrfPrg** | **con_traslado_presupuesto_programa** | ⚠️ PENDIENTE | Traslados entre programas (Phase 2) |
| 15 | **C01AcctBudget** | **con_saldo_presupuesto** | ⚠️ PENDIENTE | Seguimiento presupuestario (Phase 2) |
| 16 | **C01Config** | **con_configuracion_sistema** | ✅ HECHO | Parámetros contables por empresa |
| 17 | **C01BlSheet** | (Reportes/View) | 🔄 EQUIVALENTE | Generado desde saldos con `con_configuracion_balance` |
| 18 | **C01BlProfLoss** | (Reportes/View) | 🔄 EQUIVALENTE | Generado desde saldos con `con_configuracion_estado_resultado` |
| 19 | **C01ConsolEntry** | **con_consolidacion_entrada** | ⚠️ PENDIENTE | Entradas consolidadas (Phase 2) |
| 20 | **C01ConsolOrigin** | **con_consolidacion_origen** | ⚠️ PENDIENTE | Orígenes de consolidación (Phase 2) |
| 21 | **C01ConsolTrans** | **con_consolidacion_transaccion** | ⚠️ PENDIENTE | Transacciones consolidadas (Phase 2) |
| 22 | **C01ConsolTransThird** | **con_consolidacion_transaccion_tercero** | ⚠️ PENDIENTE | Consolidaciones por tercero (Phase 2) |
| 23 | **C01CostOpening** | **con_apertura_centro_costo** | ⚠️ PENDIENTE | Saldos de apertura por centro costo |
| 24 | **C01DBAudit** | (ASP.NET Identity) | 🔄 EQUIVALENTE | Auditoría manejada por `created_by`, `updated_by` |
| 25 | **C01FixedType** | **con_activo_tipo** | ⚠️ PENDIENTE | Tipos de activo fijo |
| 26 | **C01FixedAssest** | **con_activo_fijo** | ⚠️ PENDIENTE | Registro maestro de activos |
| 27 | **C01FixedDepart** | **con_activo_departamento** | ⚠️ PENDIENTE | Departamentos/ubicaciones activos |
| 28 | **C01FixedLoc** | **con_activo_ubicacion** | ⚠️ PENDIENTE | Ubicaciones físicas activos |
| 29 | **C01FixedSerial** | **con_activo_serie_numero** | ⚠️ PENDIENTE | Números de serie activos |
| 30 | **C01FixedAdd** | **con_activo_adicion** | ⚠️ PENDIENTE | Adiciones a activos fijos |
| 31 | (No existe) | **con_deprecacion** | ⚠️ PENDIENTE | Nuevo: cálculo automático depreciación |
| 32 | **C01LibroIVA** | **con_libro_iva** | ⚠️ PENDIENTE | Registro fiscal de IVA |
| 33 | **C01Policys** | **con_poliza_seguros** | ⚠️ PENDIENTE | Pólizas de seguros (Phase 2) |
| 34 | **C01PolicyItem** | **con_poliza_seguros_linea** | ⚠️ PENDIENTE | Detalles de pólizas seguros (Phase 2) |
| 35 | **C01Thirds** | **con_tercero** | ⚠️ PENDIENTE | Terceros contables (Clientes/Proveedores/Otros) |
| 36 | **C01ThdAccount** | **con_tercero_cuenta** | ⚠️ PENDIENTE | Cuentas por cobrar/pagar por tercero |
| 37 | **C01ThdOpening** | **con_tercero_apertura_saldo** | ⚠️ PENDIENTE | Saldos de apertura de terceros |
| 38 | **C01TransThird** | **con_transaccion_tercero** | ⚠️ PENDIENTE | Movimientos de terceros (CxC/CxP) |
| 39 | **C01RpFields** | (DevExpress Reports) | 🔄 EQUIVALENTE | Configuración en `SIAD.Reports` |
| 40 | **C01RpFolders** | (DevExpress Reports) | 🔄 EQUIVALENTE | Carpetas de reportes en `SIAD.Reports` |
| 41 | **C01DicFields** | (EF Core Metadata) | ❌ NO NECESARIO | Reemplazado por reflection EF Core |
| 42 | **C01DicFolders** | (EF Core Metadata) | ❌ NO NECESARIO | Reemplazado por reflection EF Core |
| 43 | **C01DicJoins** | (EF Core Navigation) | ❌ NO NECESARIO | Reemplazado por EF Core relationships |
| 44 | **C01AccountMedios** | **con_cuenta_medios_pago** | ⚠️ PENDIENTE | Cuentas para medios de pago (Phase 2) |
| 45 | **C01TransFixed** | **con_transaccion_activo_fijo** | ⚠️ PENDIENTE | Transacciones relacionadas a activos |
| 46 | **C01TransFixedItem** | **con_transaccion_activo_fijo_linea** | ⚠️ PENDIENTE | Detalle de transacciones activos |
| 47 | **C01SCPARAM** | **con_parametro_sistema** | ⚠️ PENDIENTE | Parámetros adicionales del sistema |
| 48 | **C01LicSoft** | (Identity/Security) | 🔄 EQUIVALENTE | Licencias manejadas en ASP.NET Identity |

---

## TABLAS COMUNES (CG* - LEGADO)

| Tabla LEGADO | Nueva Tabla | Estado | Observaciones |
|--------------|------------|--------|---------------|
| **CGUSRS** | **AspNetUser** | 🔄 EQUIVALENTE | ASP.NET Identity |
| **CGAUTR** | **AspNetRole** | 🔄 EQUIVALENTE | ASP.NET Identity |
| **CGRPTUSR** | **AspNetUserRole** | 🔄 EQUIVALENTE | ASP.NET Identity |
| **CGNIVL** | (Roles/Claims) | 🔄 EQUIVALENTE | AspNetRoleClaim |
| **CGOPMN** | **adm_operacion** | 🔄 EQUIVALENTE | Módulo Administración |
| **CGEXNU** | (Sistema) | ❌ NO NECESARIO | Numeración: usar `LAST_SEQUENCE` en tablas |
| **CGPARD** | (Sistema) | ❌ NO NECESARIO | Parámetros deprecados |
| **CGPARM** | (Sistema) | ❌ NO NECESARIO | Parámetros: consolidar en `con_parametro_sistema` |

---

## TABLAS MAESTRAS (COMUNES)

| Tabla LEGADO | Nueva Tabla | Estado | Observaciones |
|--------------|------------|--------|---------------|
| **Company** | **cfg_company** | ✅ HECHO | Base multiempresa |
| **CompanyType** | **cfg_company_type** | ✅ HECHO | Tipo de empresa |
| **Currency** | **cfg_currency** | ✅ HECHO | Monedas |
| **Country** | **cfg_pais** | 🔄 EQUIVALENTE | Administración |
| **State** | **cfg_estado** | 🔄 EQUIVALENTE | Administración |
| **City** | **cfg_ciudad** | 🔄 EQUIVALENTE | Administración |
| **Municipality** | **cfg_municipio** | 🔄 EQUIVALENTE | Administración |
| **EntityPrefix** | **cfg_prefijo_entidad** | ⚠️ PENDIENTE | Prefijos para numeración |
| **Rates** | **cfg_tasa** | 🔄 EQUIVALENTE | Tasas impositivas |
| **TaxIdent** | **cfg_impuesto_identidad** | ⚠️ PENDIENTE | Tipos de impuestos |
| **PriceIdx** | **cfg_indice_precio** | ⚠️ PENDIENTE | Índices de precios |
| **Connections** | (Config/appsettings) | ❌ NO NECESARIO | Manejado por ASP.NET configuration |
| **LogLockReg** | (ASP.NET Logging) | 🔄 EQUIVALENTE | Bloqueos de registro: logging app |
| **LogStation** | (ASP.NET Logging) | 🔄 EQUIVALENTE | Estaciones: logging de sesiones |
| **ModelAccount** | **con_modelo_cuenta** | ⚠️ PENDIENTE | Plantillas de cuentas |
| **ModelAccountType** | **con_modelo_cuenta_tipo** | ⚠️ PENDIENTE | Tipos de plantilla |
| **UserMsg** | (ASP.NET SignalR/Notifications) | 🔄 EQUIVALENTE | Mensajes: usar NotificationHub |

---

## COMPARATIVA FUNCIONAL POR MÓDULO

### 📊 CONTABILIDAD CORE

| Funcionalidad | LEGADO | NUEVA | Estado |
|--------------|--------|------|--------|
| Plan de cuentas | C01Account | con_plan_cuentas | ✅ |
| Períodos | C01Periods | con_periodo_contable | ✅ |
| Centros costo | C01CostCenter | con_centro_costo | ✅ |
| Pólizas | C01Entry + C01Trans | con_poliza + con_poliza_linea | ⚠️ |
| Saldos | C01AcctBalance + C01Opening | con_saldo_cuenta + con_apertura_saldo | ⚠️ |
| Tipos transacción | C01TransClass | con_tipo_transaccion | ⚠️ |

### 💰 ACTIVOS FIJOS

| Funcionalidad | LEGADO | NUEVA | Estado |
|--------------|--------|------|--------|
| Registro activos | C01FixedAssest | con_activo_fijo | ⚠️ |
| Tipos activos | C01FixedType | con_activo_tipo | ⚠️ |
| Ubicaciones | C01FixedLoc | con_activo_ubicacion | ⚠️ |
| Depreciación | C01FixedAdd (parcial) | con_deprecacion | ⚠️ |
| Números serie | C01FixedSerial | con_activo_serie_numero | ⚠️ |

### 📋 REPORTES FISCALES

| Funcionalidad | LEGADO | NUEVA | Estado |
|--------------|--------|------|--------|
| Libro IVA | C01LibroIVA | con_libro_iva | ⚠️ |
| Consolidaciones | C01ConsolTrans | con_consolidacion_transaccion | ⚠️ |
| Balance | C01BlSheet (view) | (Query saldos) | 🔄 |
| P&L | C01BlProfLoss (view) | (Query saldos) | 🔄 |

### 💵 PRESUPUESTOS

| Funcionalidad | LEGADO | NUEVA | Estado |
|--------------|--------|------|--------|
| Líneas presupuesto | C01Budget | con_presupuesto_linea | ⚠️ |
| Programas | C01BudProgram | con_presupuesto_programa | ⚠️ |
| Traslados | C01BudTrfAcct | con_traslado_presupuesto | ⚠️ |

### 👥 TERCEROS

| Funcionalidad | LEGADO | NUEVA | Estado |
|--------------|--------|------|--------|
| Registro terceros | C01Thirds | con_tercero | ⚠️ |
| Cuentas terceros | C01ThdAccount | con_tercero_cuenta | ⚠️ |
| Saldos terceros | C01ThdOpening | con_tercero_apertura_saldo | ⚠️ |

---

## 📈 COBERTURA FUNCIONAL

### Resumen de Estado

```
TOTALES:
├── ✅ HECHO: 4 tablas (9%)
├── 🔄 EQUIVALENTE: 18 tablas (40%)
├── ⚠️ PENDIENTE: 24 tablas (51%)
└── ❌ NO NECESARIO: 1 tabla (2%)
    TOTAL: 47 tablas contables mapeadas
```

### Desglose por Fase

**PHASE 1 (CRÍTICO) - ~14 tablas**
- con_poliza ✓
- con_poliza_linea ✓
- con_apertura_saldo ✓
- con_saldo_cuenta ✓
- con_balance_mensual ✓
- con_tipo_transaccion ✓
- con_tercero ✓
- con_libro_iva ✓
- con_activo_fijo ✓
- con_deprecacion ✓
- con_detalle_tecnico ✓
- con_tercero_cuenta ✓
- con_tercero_apertura_saldo ✓
- con_transaccion_tercero ✓

**PHASE 2 (IMPORTANTE) - ~10 tablas**
- Presupuestos (4 tablas)
- Consolidaciones (4 tablas)
- Configuración reportes (2 tablas)

**PHASE 3 (OPCIONAL/FUTURO) - ~5 tablas**
- Pólizas seguros
- Medios de pago especiales
- Parámetros avanzados
- Modelos de cuentas

---

## 🎯 ALCANCE POR RELEASE

### MVP (Mínimo viable) - 2 semanas
✅ Pólizas básicas  
✅ Saldos de cuentas  
✅ Consulta balance  
✅ Auditoría completa  
❌ Presupuestos  
❌ Activos fijos avanzado  

### v1.0 (Completo Contabilidad) - 4 semanas más
✅ Todo MVP  
✅ Activos fijos + depreciación  
✅ Libro IVA fiscal  
✅ Reportes estado financiero  
❌ Presupuestos  

### v1.5 (Avanzado) - 2 semanas más
✅ Todo v1.0  
✅ Presupuestos  
✅ Consolidaciones  
✅ Configuración reportes personalizados  

---

## 🔍 VALIDACIÓN

### Checklist de Completitud

- [ ] Todas las transacciones de LEGADO se pueden registrar en NUEVA
- [ ] Todos los reportes de LEGADO se generan desde NUEVA
- [ ] No hay pérdida de datos en migración
- [ ] Multiempresa funcional
- [ ] Auditoría completa en todas las operaciones
- [ ] Performance aceptable (< 1s para queries)
- [ ] Backup y recovery procedures definidos
- [ ] Rollback procedure definido por si algo falla

---

## 📞 DECISIONES PENDIENTES

1. ¿Reutilizar `adm_cliente`/`adm_proveedor` o crear `con_tercero`?
2. ¿Incluir presupuestos en MVP o Phase 2?
3. ¿Profundidad de auditoría (solo header o también cambios de líneas)?
4. ¿Soportar múltiples divisas en pólizas o solo moneda base?

---

**Documento compilado**: 23 Diciembre 2025  
**Cobertura**: 47/48 tablas LEGADO mapeadas (98%)  
**Estado**: ✅ LISTO PARA DECISIÓN DE ALCANCE
