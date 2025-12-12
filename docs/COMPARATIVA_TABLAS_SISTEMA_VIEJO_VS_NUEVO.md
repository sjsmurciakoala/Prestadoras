# 📊 Comparativa: Sistema Viejo vs. Sistema Nuevo

**Fecha:** 10 de diciembre de 2025  
**Objetivo:** Evaluar si conviene adoptar estructuras del sistema viejo o mantener las actuales

---

## 1. TABLA `Company` / `cfg_company`

### Sistema VIEJO (SQL Server)
```sql
Company (ID_Entity como PK varchar(10))
├── ID_Entity (PK) ← Código manual, no auto-increment
├── Descrip
├── IDLabel (Identificador fiscal)
├── IDFiscal (RUC/CUIT)
├── ID_TypeEntity (FK → CompanyType)
├── EtySize (Tamaño empresa)
├── EtyCapital (Capital)
├── Contact, Address, Address2
├── Phone, Email, WEB
├── City, Country
├── dtDate, boStatus
├── DBPassword (¡SOSPECHOSO!)
├── MaskCode
├── EtyConsol (Consolidación)
├── ID_Master (Empresa padre)
├── dtMigrated
├── Reserved
├── Prefix
└── dtCCDate
```

**PROBLEMAS:**
- ❌ PK manual (string) → Dificulta operaciones CRUD
- ❌ `DBPassword` en la tabla → Violación de seguridad
- ❌ Campos obsoletos (`Reserved`, `dtMigrated`)
- ❌ Mezcla de responsabilidades (datos empresa + configuración)

### Sistema ACTUAL (PostgreSQL)
```sql
cfg_company
├── company_id (PK, SERIAL)
├── code (código único, índice único)
├── commercial_name
├── legal_name
├── tax_id
├── email, phone, address
├── country_code, currency_code, timezone
├── status
└── created_at, created_by, updated_at, updated_by
```

**VENTAJAS:**
✅ PK auto-increment (operaciones eficientes)  
✅ Separación de roles (solo datos empresa)  
✅ Auditoría moderna (created_by, updated_at)  
✅ Soporte multi-moneda y zona horaria  
✅ Seguridad: sin contraseñas en BD

### 📌 RECOMENDACIÓN
**MANTENER `cfg_company` ACTUAL** → Es más moderna y segura. Solo agregar campos si:
- `company_type_id` (FK → tipo de empresa) ← OPCIONAL
- `parent_company_id` (FK → consolidación) ← FUTURO

---

## 2. TABLA `C01Account` / `con_plan_cuenta`

### Sistema VIEJO (SQL Server)
```sql
C01Account (ID_Account como varchar(24))
├── ID_Account (PK) ← CÓDIGO, no ID numérico
├── ID_Parent ← Jerarquía
├── Descrip (nombre largo)
├── ID_AccountRef (referencia externa)
├── DescripShort (descripción corta)
├── siLevel (nivel)
├── boMovement (permite movimientos)
├── boBudget (presupuesto)
├── Activity, TypeAcct, TypeHAC
├── boCostCenter, boThird, boTaxBase
├── ID_Export, ID_Fit, ColumOffset
├── boBank, boCurrency, ID_Currency
├── Budget, LastBudget, BudPercent
├── boAmount, boStatus
└── dtDate
```

**PROBLEMAS:**
- ❌ PK = código (string 24 chars) → Ineficiente
- ❌ Campos de presupuesto mezclados → Debería estar en tabla aparte
- ❌ Demasiados booleanos (boMovement, boThird, etc.) → Poco legible

### Sistema ACTUAL (PostgreSQL)
```sql
con_plan_cuenta
├── account_id (PK, BIGINT)
├── company_id (FK → multi-empresa)
├── parent_account_id (FK → self, jerarquía)
├── code (código, índice único)
├── name, description
├── account_type (ACTIVO, PASIVO, etc.)
├── category, level
├── allows_posting (booleano claro)
├── currency_code, status
└── created_at, created_by, updated_at, updated_by
```

**VENTAJAS:**
✅ PK numérico (operaciones rápidas)  
✅ `company_id` → Multi-empresa nativo  
✅ Nombres claros en lugar de `bo*` flags  
✅ Auditoría moderna  
✅ Sin mezcla de responsabilidades  

### ⚠️ DIFERENCIAS IMPORTANTES

| Aspecto | Viejo | Actual | Acción |
|---------|-------|--------|--------|
| Presupuesto | En `C01Account` | Tablas separadas | ✅ MEJOR |
| Exportación | `ID_Export`, `ID_Fit` | No soporta | Futuro si necesario |
| Costos | `boCostCenter` (flag) | Tabla aparte `con_centro_costo` | ✅ MEJOR |
| Terceros | `boThird` (flag) | Tabla aparte si necesario | ✅ MEJOR |

### 📌 RECOMENDACIÓN
**MANTENER `con_plan_cuenta` ACTUAL + AGREGAR:**
```sql
-- Si se necesita presupuesto:
con_presupuesto_cuenta (account_id FK, monto, año)

-- Si se necesita exportación:
con_cuenta_exportacion (account_id FK, id_export_sistema, id_fit)
```

---

## 3. TABLA `C01Config` / `con_configuracion_sistema`

### Sistema VIEJO (SQL Server)
```sql
C01Config (siPeriod PK smallint - ¡SOLO UN REGISTRO!)
├── siPeriod (PK) ← Grave error: solo 1 registro para TODA la BD
├── ID_Entity (varchar(10)) ← debería ser FK, es string
├── EntityName (data redundante)
├── MaskChar, MaskCode, MaskCost (formatos)
├── CharCr, MaxAmount, Frequency
├── NextEntry, NextEntryTemp (correlativos)
├── NextCertify, NextDeprec, NextSheetDL, NextSheetDG, NextSheetBS, NextSheetPL
├── PrefixDep
├── ID_Currency
├── 8 campos de cuentas de ganancias/pérdidas:
│   ├── ID_AccumProfits, ID_AccumDeficit
│   ├── ID_FYProfits, ID_FYLoss
│   ├── ID_AccumProfitsX, ID_AccumDeficitX
│   ├── ID_FYProfitsX, ID_FYLossX
├── TitleProfLoss, TitleBalanceSheet
├── AssetDesc, LiabilityDesc, CapitalDesc
├── LibtyCapital, OrderDesc
└── DBVersion
```

**PROBLEMAS GRAVES:**
- ❌ `siPeriod` como PK smallint (0-32767) → Permite 1 solo registro para TODA la base
- ❌ `ID_Entity` string en lugar de FK → No referencia adecuada
- ❌ `EntityName` redundante (debería venir de join)
- ❌ Correlativos en tabla de config (debería estar en tabla aparte)
- ❌ Sin auditoría moderna
- ❌ 14 campos solo para títulos y descripciones

### Sistema ACTUAL (PostgreSQL)
```sql
con_configuracion_sistema
├── config_id (PK, BIGINT)
├── company_id (FK → cfg_company) ← Multi-empresa CORRECTO
├── Configuración principal:
│   ├── fecha_ini_ejer, fecha_fin_ejer
│   ├── meses_calc
│   ├── sep_codigo, fmt_ctas, fmt_centros
│   ├── sym_acreedor, mto_maximo
│   ├── frec_deprec, fec_ult_deprec
├── Cuentas de utilidad (como códigos, no IDs):
│   ├── cod_util_acum_hist, cod_util_ejer_hist
│   ├── cod_perd_acum_hist, cod_perd_ejer_hist
│   ├── cod_util_acum_inf, cod_util_ejer_inf
│   ├── cod_perd_acum_inf, cod_perd_ejer_inf
├── Opciones de presentación:
│   ├── mostrar_orden, mostrar_percontra
├── Títulos y descripciones:
│   ├── tit_est_result, tit_balance_gral
│   ├── descripcion_activo, descripcion_pasivo
│   ├── descripcion_capital, desc_pasiv_cap, desc_orden
└── created_at, created_by, updated_at, updated_by
```

**VENTAJAS:**
✅ `company_id` FK → Multi-empresa nativo (1 config POR empresa)  
✅ Sin datos redundantes  
✅ Auditoría moderna  
✅ Estructura clara y legible  
✅ Separación de responsabilidades (correlativos en tabla aparte)

### ⚠️ QUE FALTA EN ACTUAL

| Campo del Viejo | Propósito | Acción |
|-----------------|-----------|--------|
| NextEntry, NextEntryTemp | Correlativo de asientos | Crear tabla `con_correlativo` |
| NextCertify, NextDeprec, etc. | Correlativos generales | Crear tabla `con_correlativo` |
| PrefixDep | Prefijo de depreciación | Agregar a `con_correlativo` |
| boCashFlow, boCheckCost, etc. | Flags de validación | Agregar campos a `con_configuracion_sistema` |

### 📌 RECOMENDACIÓN
**MANTENER `con_configuracion_sistema` ACTUAL** + CREAR TABLA NUEVA:

```sql
CREATE TABLE con_correlativo (
    correlativo_id BIGINT PRIMARY KEY,
    company_id BIGINT FK,
    tipo_documento VARCHAR(50),  -- 'ASIENTO', 'CHEQUE', 'DEPRECIACION', etc.
    prefijo VARCHAR(10),
    siguiente_numero INT,
    año INT,
    estado VARCHAR(20),
    created_at, created_by, updated_at, updated_by
);
```

---

## 4. TABLA `C01AcctBalance` / `con_saldo_cuenta`

### Sistema VIEJO
```sql
C01AcctBalance
├── ID_Account, ID_Entity, siPeriod (PK)
├── DtStart, DtEnd (fechas)
├── Beginning (saldo inicial)
├── Debit, Credit (movimientos)
├── Ending (saldo final)
└── dtDate
```

### Sistema ACTUAL
```sql
con_saldo_cuenta
├── saldo_id (PK)
├── company_id (FK)
├── account_id (FK)
├── periodo_id (FK → con_periodo_contable)
├── saldo_inicial, saldo_deudor, saldo_acreedor
├── saldo_final
└── Auditoría
```

✅ **ACTUAL está BIEN** → Está normalizado

---

## 5. TABLA `C01ProfLoss` / Configuración de Resultados

### Sistema VIEJO
```sql
C01ProfLoss
├── ID_Account (PK)
├── ID_Type ('INGRESO', 'COSTO', 'GASTO', etc.)
├── boActual, boPresuppt (flags)
├── Format (formato para reportes)
```

### Sistema ACTUAL
NO EXISTE TABLA ESPECÍFICA → Se maneja en `con_configuracion_sistema` y `con_configuracion_linea_resultado`

**ANÁLISIS:**
- El sistema actual usa `con_configuracion_linea_resultado` (tipo + código_cuenta)
- Es más flexible que el viejo (permite múltiples líneas por tipo)

✅ **ACTUAL está BIEN**

---

## 6. TABLA `C01BSheet` / `con_configuracion_balance`

### Sistema VIEJO
```sql
C01BSheet
├── ID_Account (PK)
├── Description (línea del balance)
├── Order (posición)
├── Accounts (agregación de cuentas)
```

### Sistema ACTUAL
```sql
con_configuracion_balance
├── balance_id (PK)
├── company_id (FK)
├── renglon (descripción de línea)
├── orden (posición)
├── cuentas_a_incluir (JSON o string)
```

✅ **ACTUAL está BIEN**

---

## 📋 RESUMEN DE RECOMENDACIONES

| Tabla | Actual | Estado | Acción |
|-------|--------|--------|--------|
| `cfg_company` | Buena | ✅ Usar | Opcional: agregar `company_type_id` |
| `con_plan_cuenta` | Excelente | ✅ Usar | Mantener como está |
| `con_configuracion_sistema` | Buena | ✅ Usar | Crear tabla `con_correlativo` aparte |
| `con_saldo_cuenta` | Buena | ✅ Usar | Mantener como está |
| `con_configuracion_linea_resultado` | Buena | ✅ Usar | Mantener como está |
| `con_configuracion_balance` | Buena | ✅ Usar | Mantener como está |

### 🚀 PRÓXIMOS PASOS

**NO CAMBIAR nada ahora.** Las tablas actuales son:
1. ✅ Mejor diseño que el viejo sistema
2. ✅ Más seguras (sin passwords en BD)
3. ✅ Multi-empresa nativas
4. ✅ Auditoría moderna
5. ✅ Sin redundancias

**SOLO CREAR:**
```sql
con_correlativo (para gestionar correlativos de documentos)
```

---

## 📌 CONCLUSIÓN

**El sistema NUEVO es superior al viejo.**

Las tablas actuales siguen mejores prácticas:
- ✅ Normalización correcta
- ✅ Seguridad de datos
- ✅ Escalabilidad (multi-empresa)
- ✅ Auditoría moderna
- ✅ Sin datos redundantes

El viejo sistema tenía problemas graves:
- ❌ `C01Config` con PK smallint (¡solo 1 registro!)
- ❌ Contraseñas en tabla de datos
- ❌ Campos obsoletos
- ❌ Sin auditoría

**RECOMENDACIÓN FINAL: Continúa con la arquitectura actual.**

