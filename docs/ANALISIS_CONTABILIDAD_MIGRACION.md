# ANÁLISIS DETALLADO: MIGRACIÓN CONTABILIDAD LEGADO → NUEVA ARQUITECTURA MULTIEMPRESA

**Fecha**: 23 de Diciembre 2025  
**Estado**: ANÁLISIS INICIAL COMPLETADO  
**Objetivo**: Completar la estructura de Contabilidad migrando tablas del sistema LEGADO (SQL Server single-empresa) a la nueva arquitectura MULTIEMPRESA en Postgres

---

## 📊 RESUMEN EJECUTIVO

### Situación Actual (LEGADO - SQL Server Single Empresa)
- **48 tablas contables** (C01*) en namespace `[MD_CONTAB]`
- Estructura sin concepto de multiempresa (todas las operaciones assumían una sola compañía)
- Datos relacionados en texto (`varchar`) sin referencia a FK explícita en algunos casos
- Varios catálogos y configuraciones dispersas

### Situación Nueva (Actual - Postgres Multiempresa)
✅ **YA IMPLEMENTADO:**
- `con_plan_cuentas` (Chart of Accounts)
- `con_centro_costo` (Cost Centers)
- `con_periodo_contable` (Accounting Periods)
- `con_diario` (Journals)
- `con_plantilla_poliza` y `con_plantilla_poliza_linea` (Voucher Templates)
- `con_regla_integracion` (Integration Rules)

❌ **FALTA IMPLEMENTAR (CRÍTICO):**
- Pólizas reales (`con_poliza`, `con_poliza_linea`) - TRANSACCIONES CONTABLES
- Saldos de cuentas (`con_saldo_cuenta`, `con_balance_mensual`)
- Saldos de apertura (`con_apertura_saldo`)
- Movimientos terceros (`con_tercero`, `con_tercero_saldo`)
- Clase de transacciones (`con_tipo_transaccion`)
- Activos fijos (`con_activo_fijo`, `con_deprecacion`)
- Libro IVA (`con_libro_iva`)
- Políticas/pólizas de seguros (`con_poliza_seguros`)
- Detalles técnicos (`con_detalle_tecnico`)

---

## 🗂️ MAPEO TABLAS LEGADO → NUEVA ARQUITECTURA

### TIER 1: CATÁLOGOS BASE (Infraestructura)

| LEGADO | NUEVA ARQUITECTURA | ESTADO | NOTAS ADAPTACIÓN MULTIEMPRESA |
|--------|-------------------|--------|-------------------------------|
| `C01Account` | `con_plan_cuentas` | ✅ HECHO | Se agregó `company_id` + parent jerarquía |
| `C01Periods` | `con_periodo_contable` | ✅ HECHO | Se agregó `company_id` + estados OPEN/CLOSED/LOCKED |
| `C01TransClass` | `con_tipo_transaccion` | ❌ FALTA | Requiere tabla nueva (clasificación de pólizas) |
| `C01CostCenter` | `con_centro_costo` | ✅ HECHO | Se agregó `company_id` |
| `C01Detail` | `con_detalle_tecnico` | ❌ FALTA | Catálogo de detalles descriptivos |
| `C01Months` | (Innecesario) | 🔄 OPCIONAL | Se puede derivar de `con_periodo_contable` |

---

### TIER 2: TRANSACCIONES CONTABLES CORE (Lo más crítico)

| LEGADO | NUEVA ARQUITECTURA | ESTADO | DESCRIPCIÓN CRÍTICO |
|--------|-------------------|--------|-------------------|
| `C01Entry` | `con_poliza` (HEADER) | ❌ FALTA | Encabezado de póliza contable |
| `C01Trans` | `con_poliza_linea` (DETAIL) | ❌ FALTA | Línea de póliza (con débito/crédito) |
| `C01Opening` | `con_apertura_saldo` | ❌ FALTA | Saldos de apertura de período |
| `C01AcctBalance` | `con_saldo_cuenta` + `con_balance_mensual` | ❌ FALTA | Saldos mensuales por cuenta |

**CRITIICIDAD**: Estas tablas son el núcleo de la contabilidad. Sin ellas no hay registro de movimientos.

---

### TIER 3: TERCEROS (Clientes, Proveedores, Entidades)

| LEGADO | NUEVA ARQUITECTURA | ESTADO | NOTAS |
|--------|-------------------|--------|-------|
| `C01Thirds` | `con_tercero` (nueva) | ❌ FALTA | Registro de terceros contables (¿O reutilizar de ADM?) |
| `C01ThdAccount` | `con_tercero_cuenta` | ❌ FALTA | Cuentas por cobrar/pagar por tercero |
| `C01ThdOpening` | `con_tercero_apertura_saldo` | ❌ FALTA | Saldos de terceros en apertura |

**NOTA**: Posible reutilizar `adm_cliente` y `adm_proveedor` del módulo Administración con una tabla de mapeo contable.

---

### TIER 4: PRESUPUESTOS & CONTROL

| LEGADO | NUEVA ARQUITECTURA | ESTADO | NOTAS |
|--------|-------------------|--------|-------|
| `C01Budget` | `con_presupuesto_linea` | ❌ FALTA | Líneas presupuestarias por cuenta |
| `C01BudProgram` | `con_presupuesto_programa` | ❌ FALTA | Programas presupuestarios |
| `C01BudTrfAcct` | `con_traslado_presupuesto_ctum` | ❌ FALTA | Traslados entre cuentas |
| `C01BudTrfPrg` | `con_traslado_presupuesto_prg` | ❌ FALTA | Traslados entre programas |
| `C01AcctBudget` | `con_saldo_presupuesto` | ❌ FALTA | Seguimiento presupuestario |

---

### TIER 5: ACTIVOS FIJOS

| LEGADO | NUEVA ARQUITECTURA | ESTADO | NOTAS |
|--------|-------------------|--------|-------|
| `C01FixedType` | `con_activo_tipo` | ❌ FALTA | Tipos de activo fijo |
| `C01FixedAssest` | `con_activo_fijo` | ❌ FALTA | Registro de activos fijos |
| `C01FixedDepart` | `con_activo_departamento` | ❌ FALTA | Departamentos/ubicaciones |
| `C01FixedLoc` | `con_activo_ubicacion` | ❌ FALTA | Ubicaciones físicas |
| `C01FixedSerial` | `con_activo_serie_numero` | ❌ FALTA | Números de serie |
| `C01FixedAdd` | `con_activo_adicion` | ❌ FALTA | Adiciones a activos |
| (No existe) | `con_deprecacion` | ❌ FALTA | Cálculo y registro de depreciación |

---

### TIER 6: REPORTES FISCALES (IVA, Impuestos)

| LEGADO | NUEVA ARQUITECTURA | ESTADO | NOTAS |
|--------|-------------------|--------|-------|
| `C01LibroIVA` | `con_libro_iva` | ❌ FALTA | Registro de IVA (débito/crédito fiscal) |
| `C01ConsolTrans` | `con_consolidacion_transaccion` | ❌ FALTA | Consolidación de transacciones |
| `C01ConsolEntry` | `con_consolidacion_entrada` | ❌ FALTA | Entradas consolidadas |
| `C01ConsolOrigin` | `con_consolidacion_origen` | ❌ FALTA | Origen de consolidación |

---

### TIER 7: REPORTES FINANCIEROS (Estados contables)

| LEGADO | NUEVA ARQUITECTURA | ESTADO | NOTAS |
|--------|-------------------|--------|-------|
| `C01BlSheet` | (Generado/Reportes) | 🔄 VIEW | Balance Sheet (Estado de Situación) |
| `C01BlProfLoss` | (Generado/Reportes) | 🔄 VIEW | P&L Statement (Estado de Resultados) |
| (No existe) | `con_configuracion_balance` | ❌ FALTA | Configuración de qué cuentas van al balance |
| (No existe) | `con_configuracion_estado_resultado` | ❌ FALTA | Configuración de qué cuentas van a P&L |

---

### TIER 8: CONFIGURACIÓN & AUDITORÍA

| LEGADO | NUEVA ARQUITECTURA | ESTADO | NOTAS |
|--------|-------------------|--------|-------|
| `C01Config` | `con_configuracion_sistema` | ✅ HECHO | Parámetros de configuración |
| `C01SCPARAM` | `con_parametro_sistema` | 🔄 REVISAR | Parámetros del sistema (¿consolidar?) |
| `C01DBAudit` | (ASP.NET Identity + Audit) | 🔄 MANUAL | Auditoría manejada a nivel app |
| `C01RpFields` | (DevExpress Reports) | 🔄 CONFIG | Campos de reportes (en SIAD.Reports) |
| `C01RpFolders` | (DevExpress Reports) | 🔄 CONFIG | Carpetas de reportes |
| `C01DicFields` | (Metadata) | 🔄 OPCIONAL | Diccionario de campos (¿usar EF Core?) |
| `C01DicFolders` | (Metadata) | 🔄 OPCIONAL | Diccionario de carpetas |
| `C01DicJoins` | (Metadata) | 🔄 OPCIONAL | Diccionario de joins |

---

## 🎯 PRIORIZACIÓN: QUÉ IMPLEMENTAR PRIMERO

### 🔴 CRÍTICO (Sin esto no funciona contabilidad)
1. **`con_poliza` + `con_poliza_linea`** - Transacciones contables reales
2. **`con_apertura_saldo`** - Saldos iniciales por período
3. **`con_saldo_cuenta`** - Saldos actuales por cuenta y período
4. **`con_tipo_transaccion`** - Clasificación de pólizas (DIARIO, AJUSTE, CIERRE, etc.)

### 🟠 IMPORTANTE (Funciona sin esto, pero incompleto)
5. **`con_tercero` + `con_tercero_cuenta`** - Para CxC/CxP
6. **`con_libro_iva`** - Cumplimiento fiscal IVA
7. **`con_activo_fijo` + `con_deprecacion`** - Gestión de activos

### 🟡 NECESARIO DESPUÉS
8. Presupuestos (`con_presupuesto_*`)
9. Consolidaciones
10. Configuración de reportes financieros

---

## 📐 DISEÑO DE TABLAS FALTANTES - SPEC MULTIEMPRESA

### 1️⃣ `con_tipo_transaccion` (Classification)

```sql
CREATE TABLE IF NOT EXISTS public.con_tipo_transaccion
(
    type_id           bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code              varchar(20)    NOT NULL,
    name              varchar(100)   NOT NULL,
    description       varchar(300),
    category          varchar(30)    NOT NULL, -- DIARIO, AJUSTE, CIERRE, CONSOLIDACION, APERTURA
    is_automatic      boolean        NOT NULL DEFAULT false,
    allows_cost_center boolean       NOT NULL DEFAULT false,
    allows_third_party boolean       NOT NULL DEFAULT false,
    status            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code)
);
```

---

### 2️⃣ `con_poliza` (Voucher Header)

```sql
CREATE TABLE IF NOT EXISTS public.con_poliza
(
    voucher_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    period_id         bigint         NOT NULL REFERENCES public.con_periodo_contable(period_id) ON DELETE RESTRICT,
    journal_id        bigint         NOT NULL REFERENCES public.con_diario(journal_id) ON DELETE RESTRICT,
    type_id           bigint         NOT NULL REFERENCES public.con_tipo_transaccion(type_id) ON DELETE RESTRICT,
    voucher_number    varchar(20)    NOT NULL,
    voucher_date      date           NOT NULL,
    description       varchar(300)   NOT NULL,
    document_ref      varchar(30),   -- Referencia a documento origen (factura, recibo, etc.)
    total_debit       decimal(18,2)  NOT NULL DEFAULT 0,
    total_credit      decimal(18,2)  NOT NULL DEFAULT 0,
    status            varchar(20)    NOT NULL DEFAULT 'DRAFT', -- DRAFT, POSTED, REVERSED
    is_balanced       boolean        NOT NULL DEFAULT false,
    notes             text,
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    posted_at         timestamptz,
    posted_by         varchar(100),
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, journal_id, voucher_number),
    CHECK (total_debit >= 0 AND total_credit >= 0)
);

CREATE INDEX IF NOT EXISTS ix_con_poliza_company ON public.con_poliza (company_id);
CREATE INDEX IF NOT EXISTS ix_con_poliza_period ON public.con_poliza (period_id);
CREATE INDEX IF NOT EXISTS ix_con_poliza_journal ON public.con_poliza (journal_id);
CREATE INDEX IF NOT EXISTS ix_con_poliza_date ON public.con_poliza (voucher_date);
CREATE INDEX IF NOT EXISTS ix_con_poliza_status ON public.con_poliza (status);
```

---

### 3️⃣ `con_poliza_linea` (Voucher Detail)

```sql
CREATE TABLE IF NOT EXISTS public.con_poliza_linea
(
    line_id           bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    voucher_id        bigint         NOT NULL REFERENCES public.con_poliza(voucher_id) ON DELETE CASCADE,
    line_number       smallint       NOT NULL,
    account_id        bigint         NOT NULL REFERENCES public.con_plan_cuentas(account_id) ON DELETE RESTRICT,
    cost_center_id    bigint         REFERENCES public.con_centro_costo(cost_center_id) ON DELETE SET NULL,
    third_party_id    bigint,        -- FK a tercero (si aplica)
    debit_amount      decimal(18,2)  NOT NULL DEFAULT 0,
    credit_amount     decimal(18,2)  NOT NULL DEFAULT 0,
    currency_code     char(3)        REFERENCES public.cfg_currency(currency_code),
    exchange_rate     decimal(18,9)  DEFAULT 1.0,
    description       varchar(200),
    reference         varchar(30),
    is_tax_related    boolean        DEFAULT false,
    tax_base_amount   decimal(18,2),
    tax_amount        decimal(18,2),
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (voucher_id, line_number),
    CHECK (debit_amount >= 0 AND credit_amount >= 0 AND NOT (debit_amount > 0 AND credit_amount > 0))
);

CREATE INDEX IF NOT EXISTS ix_con_poliza_linea_account ON public.con_poliza_linea (account_id);
CREATE INDEX IF NOT EXISTS ix_con_poliza_linea_cost_center ON public.con_poliza_linea (cost_center_id);
```

---

### 4️⃣ `con_apertura_saldo` (Opening Balances)

```sql
CREATE TABLE IF NOT EXISTS public.con_apertura_saldo
(
    opening_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    period_id         bigint         NOT NULL REFERENCES public.con_periodo_contable(period_id) ON DELETE CASCADE,
    account_id        bigint         NOT NULL REFERENCES public.con_plan_cuentas(account_id) ON DELETE RESTRICT,
    cost_center_id    bigint         REFERENCES public.con_centro_costo(cost_center_id) ON DELETE SET NULL,
    debit_amount      decimal(18,2)  NOT NULL DEFAULT 0,
    credit_amount     decimal(18,2)  NOT NULL DEFAULT 0,
    currency_code     char(3)        REFERENCES public.cfg_currency(currency_code),
    exchange_rate     decimal(18,9)  DEFAULT 1.0,
    notes             varchar(300),
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, period_id, account_id, cost_center_id),
    CHECK (debit_amount >= 0 AND credit_amount >= 0)
);

CREATE INDEX IF NOT EXISTS ix_con_apertura_saldo_company ON public.con_apertura_saldo (company_id);
CREATE INDEX IF NOT EXISTS ix_con_apertura_saldo_period ON public.con_apertura_saldo (period_id);
CREATE INDEX IF NOT EXISTS ix_con_apertura_saldo_account ON public.con_apertura_saldo (account_id);
```

---

### 5️⃣ `con_saldo_cuenta` (Account Balances - Current)

```sql
CREATE TABLE IF NOT EXISTS public.con_saldo_cuenta
(
    balance_id        bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    period_id         bigint         NOT NULL REFERENCES public.con_periodo_contable(period_id) ON DELETE CASCADE,
    account_id        bigint         NOT NULL REFERENCES public.con_plan_cuentas(account_id) ON DELETE RESTRICT,
    cost_center_id    bigint         REFERENCES public.con_centro_costo(cost_center_id) ON DELETE SET NULL,
    beginning_debit   decimal(18,2)  NOT NULL DEFAULT 0,
    beginning_credit  decimal(18,2)  NOT NULL DEFAULT 0,
    period_debit      decimal(18,2)  NOT NULL DEFAULT 0,
    period_credit     decimal(18,2)  NOT NULL DEFAULT 0,
    ending_debit      decimal(18,2)  NOT NULL DEFAULT 0,
    ending_credit     decimal(18,2)  NOT NULL DEFAULT 0,
    last_updated      timestamptz    NOT NULL DEFAULT now(),
    UNIQUE (company_id, period_id, account_id, cost_center_id)
);

CREATE INDEX IF NOT EXISTS ix_con_saldo_cuenta_company ON public.con_saldo_cuenta (company_id);
CREATE INDEX IF NOT EXISTS ix_con_saldo_cuenta_period_account ON public.con_saldo_cuenta (period_id, account_id);
```

---

### 6️⃣ `con_balance_mensual` (Monthly Balances)

```sql
CREATE TABLE IF NOT EXISTS public.con_balance_mensual
(
    monthly_balance_id bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    period_id         bigint         NOT NULL REFERENCES public.con_periodo_contable(period_id) ON DELETE CASCADE,
    account_id        bigint         NOT NULL REFERENCES public.con_plan_cuentas(account_id) ON DELETE RESTRICT,
    cost_center_id    bigint         REFERENCES public.con_centro_costo(cost_center_id) ON DELETE SET NULL,
    month_number      smallint       NOT NULL CHECK (month_number >= 1 AND month_number <= 13), -- 13 para acumulado
    debit_amount      decimal(18,2)  NOT NULL DEFAULT 0,
    credit_amount     decimal(18,2)  NOT NULL DEFAULT 0,
    transaction_count int            NOT NULL DEFAULT 0,
    UNIQUE (company_id, period_id, account_id, cost_center_id, month_number)
);

CREATE INDEX IF NOT EXISTS ix_con_balance_mensual_period_month ON public.con_balance_mensual (period_id, month_number);
```

---

### 7️⃣ `con_tercero` (Third Parties - Accounting)

```sql
CREATE TABLE IF NOT EXISTS public.con_tercero
(
    third_party_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code              varchar(30)    NOT NULL,
    name              varchar(200)   NOT NULL,
    description       varchar(300),
    tax_id            varchar(30),
    category          varchar(30)    NOT NULL, -- CLIENTE, PROVEEDOR, EMPLEADO, OTRO
    is_supplier       boolean        NOT NULL DEFAULT false,
    is_customer       boolean        NOT NULL DEFAULT false,
    status            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_con_tercero_company ON public.con_tercero (company_id);
CREATE INDEX IF NOT EXISTS ix_con_tercero_category ON public.con_tercero (category);
```

---

### 8️⃣ `con_libro_iva` (IVA Register)

```sql
CREATE TABLE IF NOT EXISTS public.con_libro_iva
(
    iva_register_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    period_id         bigint         NOT NULL REFERENCES public.con_periodo_contable(period_id) ON DELETE RESTRICT,
    transaction_date  date           NOT NULL,
    document_type     varchar(30)    NOT NULL, -- FACTURA, NOTA_CREDITO, etc.
    document_number   varchar(30)    NOT NULL,
    third_party_id    bigint         REFERENCES public.con_tercero(third_party_id) ON DELETE SET NULL,
    taxable_base      decimal(18,2)  NOT NULL DEFAULT 0,
    exempt_amount     decimal(18,2)  NOT NULL DEFAULT 0,
    tax_rate          decimal(5,2)   NOT NULL,
    tax_amount        decimal(18,2)  NOT NULL DEFAULT 0,
    total_amount      decimal(18,2)  NOT NULL DEFAULT 0,
    iva_type          varchar(20)    NOT NULL, -- DEBIT_IVA (salida), CREDIT_IVA (entrada)
    status            varchar(20)    NOT NULL DEFAULT 'RECORDED',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user
);

CREATE INDEX IF NOT EXISTS ix_con_libro_iva_company ON public.con_libro_iva (company_id);
CREATE INDEX IF NOT EXISTS ix_con_libro_iva_period ON public.con_libro_iva (period_id);
CREATE INDEX IF NOT EXISTS ix_con_libro_iva_date ON public.con_libro_iva (transaction_date);
```

---

### 9️⃣ `con_activo_fijo` (Fixed Assets)

```sql
CREATE TABLE IF NOT EXISTS public.con_activo_fijo
(
    asset_id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    asset_type_id     bigint         NOT NULL REFERENCES public.con_activo_tipo(type_id) ON DELETE RESTRICT,
    code              varchar(30)    NOT NULL,
    name              varchar(200)   NOT NULL,
    description       varchar(500),
    acquisition_date  date           NOT NULL,
    in_service_date   date           NOT NULL,
    acquisition_cost  decimal(18,2)  NOT NULL,
    salvage_value     decimal(18,2)  NOT NULL DEFAULT 0,
    useful_life_years smallint       NOT NULL,
    depreciation_method varchar(30)   NOT NULL, -- STRAIGHT_LINE, DECLINING, etc.
    accumulated_depreciation decimal(18,2) DEFAULT 0,
    current_value     decimal(18,2),
    asset_account_id  bigint         REFERENCES public.con_plan_cuentas(account_id) ON DELETE SET NULL,
    depreciation_account_id bigint   REFERENCES public.con_plan_cuentas(account_id) ON DELETE SET NULL,
    location          varchar(200),
    status            varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        varchar(100)   NOT NULL DEFAULT current_user,
    updated_at        timestamptz,
    updated_by        varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_con_activo_fijo_company ON public.con_activo_fijo (company_id);
```

---

### 🔟 `con_deprecacion` (Depreciation Records)

```sql
CREATE TABLE IF NOT EXISTS public.con_deprecacion
(
    depreciation_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    asset_id          bigint         NOT NULL REFERENCES public.con_activo_fijo(asset_id) ON DELETE CASCADE,
    period_id         bigint         NOT NULL REFERENCES public.con_periodo_contable(period_id) ON DELETE CASCADE,
    month_number      smallint       NOT NULL,
    depreciation_amount decimal(18,2) NOT NULL,
    accumulated_to_date decimal(18,2) NOT NULL,
    voucher_id        bigint         REFERENCES public.con_poliza(voucher_id) ON DELETE SET NULL,
    created_at        timestamptz    NOT NULL DEFAULT now(),
    UNIQUE (asset_id, period_id, month_number)
);

CREATE INDEX IF NOT EXISTS ix_con_deprecacion_asset ON public.con_deprecacion (asset_id);
CREATE INDEX IF NOT EXISTS ix_con_deprecacion_period ON public.con_deprecacion (period_id);
```

---

## 🔗 RELACIONES DE INTEGRIDAD REFERENCIAL

### Diagrama de dependencias (orden de creación):

```
cfg_company
    ├── cfg_currency
    ├── con_plan_cuentas (parent_account_id)
    ├── con_centro_costo
    ├── con_periodo_contable
    ├── con_diario
    ├── con_tipo_transaccion
    ├── con_regla_integracion (debit_account_id, credit_account_id, cost_center_id)
    ├── con_tercero
    ├── con_poliza (journal_id, period_id, type_id)
    │   ├── con_poliza_linea (account_id, cost_center_id, third_party_id)
    │   └── con_libro_iva
    ├── con_apertura_saldo (account_id, cost_center_id)
    ├── con_saldo_cuenta (account_id, cost_center_id)
    ├── con_balance_mensual (account_id, cost_center_id)
    ├── con_activo_tipo
    │   └── con_activo_fijo (asset_type_id, asset_account_id, depreciation_account_id)
    │       └── con_deprecacion (asset_id, period_id, voucher_id)
    └── con_plantilla_poliza
        └── con_plantilla_poliza_linea (account_id, cost_center_id)
```

---

## 📋 ENTIDADES DE DOMINIO (DTOs) REQUERIDAS

### Crear en `/SIAD.Core/DTOs/Contabilidad/`:

1. ✅ `PlanCuentaDto`, `PlanCuentaUpsertDto` (YA EXISTE)
2. ✅ `CentroCostoDto`, `CentroCostoUpsertDto` (YA EXISTE)
3. ✅ `PeriodoContableDto`, `PeriodoContableUpsertDto` (YA EXISTE)
4. ✅ `DiarioDto`, `DiarioUpsertDto` (YA EXISTE)
5. ❌ `TipoTransaccionDto`, `TipoTransaccionUpsertDto` (FALTA)
6. ❌ `PolizaDto`, `PolizaCrearDto`, `PolizaActualizarDto` (FALTA)
7. ❌ `PolizaLineaDto` (FALTA)
8. ❌ `AperturaSaldoDto`, `AperturaSaldoUpsertDto` (FALTA)
9. ❌ `SaldoCuentaDto` (FALTA - solo lectura)
10. ❌ `BalanceMensualDto` (FALTA - solo lectura)
11. ❌ `TerceroDto`, `TerceroUpsertDto` (FALTA)
12. ❌ `LibroIvaDto`, `LibroIvaUpsertDto` (FALTA)
13. ❌ `ActivoFijoDto`, `ActivoFijoUpsertDto` (FALTA)
14. ❌ `DepreciacionDto` (FALTA - solo lectura)

---

## 🛠️ SERVICIOS DE DOMINIO REQUERIDOS

### Crear en `/SIAD.Services/Contabilidad/`:

**YA EXISTEN:**
- `IContabilidadCatalogosService` + `ContabilidadCatalogosService` - Plan cuentas, centros costo, diarios
- `IPeriodoContableService` + `PeriodoContableService` - Períodos contables

**FALTA - CRÍTICOS:**
1. `IPolizaService` + `PolizaService` - CRUD pólizas, validar balanceo, registrar
2. `IAperturaService` + `AperturaService` - Manejo saldos de apertura
3. `ISaldosService` + `SaldosService` - Cálculo/consulta de saldos
4. `ITipoTransaccionService` + `TipoTransaccionService` - Catálogo tipos transacción
5. `ITerceroService` + `TerceroService` - Gestión terceros contables
6. `ILibroIvaService` + `LibroIvaService` - Registro y reportes IVA
7. `IActivoFijoService` + `ActivoFijoService` - CRUD activos fijos
8. `IDepreciacionService` + `DepreciacionService` - Cálculo depreciación automática

---

## 🎮 CONTROLADORES API REQUERIDOS

### Crear en `/apc/Controllers/Contabilidad/`:

**YA EXISTEN:**
- `ContabilidadEmpresasController` - Configuración empresa
- `PeriodosContablesController` - Períodos

**FALTA:**
1. `PolizasController` - POST/PUT/GET/DELETE pólizas
2. `AperturasSaldosController` - POST/GET apertura
3. `SaldosController` - GET saldos (solo lectura)
4. `TiposTransaccionController` - CRUD tipos
5. `TercerosContablesController` - CRUD terceros
6. `LibroIvaController` - GET/reportes IVA
7. `ActivosFijosController` - CRUD activos
8. `DepreciacionController` - GET/cálculo depreciación

---

## 📱 CLIENTES HTTP (apc.Client)

### Crear en `/apc.Client/Services/Contabilidad/`:

**YA EXISTEN:**
- `PlanCuentasClient`
- `CentrosCostoClient`
- `PeriodosContablesClient`
- `DiariosClient`

**FALTA:**
1. `PolizasClient`
2. `AperturasSaldosClient`
3. `SaldosClient`
4. `TiposTransaccionClient`
5. `TercerosClient`
6. `LibroIvaClient`
7. `ActivosFijosClient`
8. `DepreciacionClient`

---

## 📊 MIGRACIONES EF CORE

### Plan de Migraciones:

```powershell
# En raíz del proyecto
dotnet ef migrations add InitContabilidadTransacciones -p SIAD.Data -s apc --output-dir Migrations/Contabilidad
dotnet ef migrations add ContabilidadTerceros -p SIAD.Data -s apc --output-dir Migrations/Contabilidad
dotnet ef migrations add ContabilidadActivosFijos -p SIAD.Data -s apc --output-dir Migrations/Contabilidad
dotnet ef migrations add ContabilidadReportesIva -p SIAD.Data -s apc --output-dir Migrations/Contabilidad
dotnet ef database update -p SIAD.Data -s apc
```

---

## 📝 SEED DATA RECOMENDADO

Crear en `Database/Seeds_v2/`:
- `05_seed_contabilidad_tipos_transaccion.sql`
- `06_seed_contabilidad_terceros_demo.sql`
- `07_seed_contabilidad_activos_demo.sql`

---

## ✅ CHECKLIST DE IMPLEMENTACIÓN

### FASE 1: TABLAS BASE (Semana 1-2)
- [ ] Crear migrations EF Core para todas las tablas nuevas
- [ ] Ejecutar migrations en Postgres
- [ ] Validar integridad referencial

### FASE 2: ENTIDADES & DTOs (Semana 2)
- [ ] Scaffold EF Core entities
- [ ] Crear todos los DTOs necesarios
- [ ] Crear AutoMapper profiles

### FASE 3: SERVICIOS CORE (Semana 3)
- [ ] Implementar `IPolizaService`
- [ ] Implementar `IAperturaService`
- [ ] Implementar `ISaldosService`
- [ ] Implementar `ITipoTransaccionService`

### FASE 4: CONTROLADORES & CLIENTS (Semana 3-4)
- [ ] Crear todos los controladores REST
- [ ] Crear todos los clients HTTP
- [ ] Registrar en DI (`ServiceRegistration.cs` y `CommonServices.cs`)

### FASE 5: UI BLAZOR (Semana 4-5)
- [ ] Componentes para crear/editar pólizas
- [ ] Componentes para consultar saldos
- [ ] Componentes para reportes IVA
- [ ] Componentes para gestión activos fijos

### FASE 6: TESTS & VALIDACIÓN (Semana 5-6)
- [ ] Unit tests servicios
- [ ] Tests de validación de pólizas balanceadas
- [ ] Tests cálculo saldos
- [ ] Seed data demo
- [ ] Testing en Postgres real

---

## 🚀 PRÓXIMOS PASOS INMEDIATOS

1. **CONFIRMACIÓN**: ¿Procedo con la implementación en este orden?
2. **PRIORIDADES**: ¿Enfocamos en Pólizas/Saldos primero o hay otro módulo más urgente?
3. **TERCEROS**: ¿Reutilizamos `adm_cliente`/`adm_proveedor` o creamos tabla `con_tercero` separada?
4. **AUDITORÍA**: ¿Queremos auditoría completa (quién/cuándo editó cada póliza) en la tabla o via middleware?

---

**Documento compilado por**: Análisis Automatizado de Migraciones  
**Último actualizado**: 23 de Diciembre 2025 - 10:15  
**Estado**: LISTO PARA IMPLEMENTACIÓN
