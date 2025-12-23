# DIAGRAMA TÉCNICO: ARQUITECTURA CONTABILIDAD MULTIEMPRESA

---

## 📐 DIAGRAMA DE ENTIDADES Y RELACIONES (ERD)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           TIER 0: MULTITENANCY                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│  ┌─────────────────────┐                                                    │
│  │   cfg_company       │ ◄──── Root de todo (empresa multiempresa)          │
│  ├─────────────────────┤                                                    │
│  │ company_id (PK)     │                                                    │
│  │ code                │                                                    │
│  │ commercial_name     │                                                    │
│  │ legal_name          │                                                    │
│  │ tax_id              │                                                    │
│  │ currency_code (FK)  │                                                    │
│  └─────────────────────┘                                                    │
│           │ (1:N)                                                            │
│           └─────────────────────────────────────────────────────────┐        │
│                                                                      │        │
└──────────────────────────────────────────────────────────────────────┼────────┘

┌──────────────────────────────────────────────────────────────────────────────┐
│          TIER 1: CONFIGURACIÓN CONTABLE (CATÁLOGOS BÁSICOS)                  │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  ┌─────────────────────────┐    ┌─────────────────────────┐        │   │
│  │  │ con_plan_cuentas        │    │ con_centro_costo        │        │   │
│  │  ├─────────────────────────┤    ├─────────────────────────┤        │   │
│  │  │ account_id (PK)         │    │ cost_center_id (PK)     │        │   │
│  │  │ company_id (FK) ◄┐      │    │ company_id (FK) ◄┐      │        │   │
│  │  │ parent_account_id ◄──────────┤ (self-referencing)      │        │   │
│  │  │ code (UK)       ├───────────────────────────────────►  │        │   │
│  │  │ name            │      │    │ code (UK)               │        │   │
│  │  │ account_type    │      │    │ name                    │        │   │
│  │  │ level           │      │    │ description             │        │   │
│  │  │ allows_posting  │      │    │ status                  │        │   │
│  │  │ status          │      │    │ created_at/by           │        │   │
│  │  │ created_at/by   │      │    │ updated_at/by           │        │   │
│  │  └─────────────────────────┘    └─────────────────────────┘        │   │
│  │         ◄────────────────────────────────────────────────────────┐ │   │
│  │                                                                   │ │   │
│  │  ┌──────────────────────────┐     ┌──────────────────────────┐ │ │   │
│  │  │ con_periodo_contable     │     │ con_diario              │ │ │   │
│  │  ├──────────────────────────┤     ├──────────────────────────┤ │ │   │
│  │  │ period_id (PK)           │     │ journal_id (PK)         │ │ │   │
│  │  │ company_id (FK)          │     │ company_id (FK)         │ │ │   │
│  │  │ code (UK)                │     │ code (UK)               │ │ │   │
│  │  │ name                     │     │ name                    │ │ │   │
│  │  │ start_date               │     │ sequence_prefix         │ │ │   │
│  │  │ end_date                 │     │ last_sequence           │ │ │   │
│  │  │ status: OPEN/CLOSED/LOCK │     │ is_active               │ │ │   │
│  │  │ closed_at/by             │     │ allows_manual           │ │ │   │
│  │  └──────────────────────────┘     └──────────────────────────┘ │ │   │
│  │                                                                   │ │   │
│  │  ┌──────────────────────────────────────────────────────────┐   │ │   │
│  │  │ con_tipo_transaccion (NUEVA - CRÍTICA)                  │   │ │   │
│  │  ├──────────────────────────────────────────────────────────┤   │ │   │
│  │  │ type_id (PK)                                             │   │ │   │
│  │  │ company_id (FK) ◄─────────────────────────────────────────┼───┼───┘   │
│  │  │ code (UK): DIARIO, AJUSTE, CIERRE, APERTURA             │   │       │
│  │  │ name                                                     │   │       │
│  │  │ category                                                 │   │       │
│  │  │ is_automatic, allows_cost_center, allows_third_party    │   │       │
│  │  └──────────────────────────────────────────────────────────┘   │       │
│  │                                                                   │       │
│  └───────────────────────────────────────────────────────────────────┘       │
│                                                                               │
└──────────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────────┐
│          TIER 2: TRANSACCIONES CONTABLES (CORE - MÁS CRÍTICO)                │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │                 con_poliza (VOUCHER HEADER)                            │  │
│  │            ════════════════════════════════════                         │  │
│  ├────────────────────────────────────────────────────────────────────────┤  │
│  │ voucher_id (PK) ════════════════════════════════════════════════════┐  │  │
│  │ company_id (FK) ◄──┐                                                 │  │  │
│  │ period_id (FK) ◄───┼──────────► con_periodo_contable               │  │  │
│  │ journal_id (FK) ◄──┼──────────► con_diario                         │  │  │
│  │ type_id (FK) ◄─────┼──────────► con_tipo_transaccion               │  │  │
│  │ voucher_number (UK) │  ┌─────────────────────────────────────────────┼─┐│  │
│  │ voucher_date        │  │                                             │ ││  │
│  │ description         │  │                                             │ ││  │
│  │ document_ref        │  │   ┌─────────────────────────────────────┐ │ ││  │
│  │ total_debit         │  │   │ con_poliza_linea (VOUCHER LINES)   │ │ ││  │
│  │ total_credit        │  │   ├─────────────────────────────────────┤ │ ││  │
│  │ is_balanced: BOOL   │  │   │ line_id (PK)                        │ │ ││  │
│  │ status: DRAFT/POSTED│  │   │ voucher_id (FK) ◄─────────────────────┘ ││  │
│  │ posted_at/by        │  │   │ line_number                         │   ││  │
│  │ notes               │  │   │ account_id (FK) ──► con_plan_cuentas   ││  │
│  │ created_at/by       │  │   │ cost_center_id ────► con_centro_costo  ││  │
│  │ updated_at/by       │  │   │ third_party_id ────► con_tercero       ││  │
│  │ CONSTRAINT: balaced │  │   │ debit_amount                        │   ││  │
│  │ CHECK total_debit=  │  │   │ credit_amount                       │   ││  │
│  │       total_credit  │  │   │ currency_code                       │   ││  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                               │
│                                                                               │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │ con_apertura_saldo (OPENING BALANCES - Saldos de Apertura)            │ │
│  ├─────────────────────────────────────────────────────────────────────────┤ │
│  │ opening_id (PK)                                                         │ │
│  │ company_id (FK) ◄──┐                                                   │ │
│  │ period_id (FK) ◄───┼──────► con_periodo_contable                      │ │
│  │ account_id (FK) ◄──┼──────► con_plan_cuentas                          │ │
│  │ cost_center_id ◄───┼──────► con_centro_costo                          │ │
│  │ debit_amount                                                            │ │
│  │ credit_amount                                                           │ │
│  │ UNIQUE(company_id, period_id, account_id, cost_center_id)             │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
│                                                                               │
└──────────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────────┐
│          TIER 3: SALDOS Y REPORTES (ANALYTICS)                               │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│  ┌──────────────────────────────────────┐  ┌──────────────────────────────┐  │
│  │ con_saldo_cuenta                     │  │ con_balance_mensual          │  │
│  │ (Current Account Balances)           │  │ (Monthly Breakdown)          │  │
│  ├──────────────────────────────────────┤  ├──────────────────────────────┤  │
│  │ balance_id (PK)                      │  │ monthly_balance_id (PK)     │  │
│  │ company_id (FK)  ◄────────┐          │  │ company_id (FK)  ◄────────┐ │  │
│  │ period_id (FK)   ◄────────┼────┐     │  │ period_id (FK)   ◄────────┼─┼──┐│  │
│  │ account_id (FK)  ◄────────┼────┼─┐   │  │ account_id (FK)  ◄────────┼─┼──┼┐ │  │
│  │ cost_center_id   ◄────────┼────┼─┼─┐ │  │ cost_center_id   ◄────────┼─┼──┼┼─┐│  │
│  │ beginning_debit          │  │  │ │ │ │  │ month_number (1-13)        │  │ ││││  │
│  │ beginning_credit         │  │  │ │ │ │  │ debit_amount               │  │ ││││  │
│  │ period_debit             │  │  │ │ │ │  │ credit_amount              │  │ ││││  │
│  │ period_credit            │  │  │ │ │ │  │ transaction_count          │  │ ││││  │
│  │ ending_debit             │  │  │ │ │ │  │ UNIQUE(period, account,    │  │ ││││  │
│  │ ending_credit            │  │  │ │ │ │  │        month)              │  │ ││││  │
│  │ last_updated             │  │  │ │ │ │  └──────────────────────────────┘  │ │││  │
│  │ UNIQUE(period,account)   │  │  │ │ │ │                                   │ │││  │
│  └──────────────────────────────────────┘                                   │ │││  │
│         ▲                                                                   │ │││  │
│         │ se calcula desde                    ◄─────────────────────────────┼─┼┼┼──┐│  │
│         │                                                                   │ │││ ││  │
│         └──────────────────────┬───────────────────────────────────────────┘ │││ ││  │
│                                │                                              │││ ││  │
│                                │ automático al registrar póliza               │││ ││  │
│                                │                                              │││ ││  │
│                          ┌─────▼──────┐                                       │││ ││  │
│                          │ Servicio    │                                       │││ ││  │
│                          │ SaldosServ  │                                       │││ ││  │
│                          └─────┬──────┘                                       │││ ││  │
│                                │ suma líneas de:                              │││ ││  │
│                                │  - apertura_saldo                            │││ ││  │
│                                │  - poliza_linea (status=POSTED)              │││ ││  │
│                                └──────────────────────────────────────────────┘││ ││  │
│                                                                                ││ ││  │
│  ┌────────────────────────────────────────────────────────────────────────┐   ││ ││  │
│  │ con_libro_iva (IVA TAX REGISTER - Fiscal)                               │   ││ ││  │
│  ├────────────────────────────────────────────────────────────────────────┤   ││ ││  │
│  │ iva_register_id (PK)                                                    │   ││ ││  │
│  │ company_id (FK)  ◄────────────────────────────────────────────────────┐│   ││ ││  │
│  │ period_id (FK)   ◄──────────── con_periodo_contable                  ││   ││ ││  │
│  │ transaction_date                                                       ││   ││ ││  │
│  │ document_type, document_number                                         ││   ││ ││  │
│  │ third_party_id ────────────────► con_tercero                          ││   ││ ││  │
│  │ taxable_base, exempt_amount, tax_rate, tax_amount                     ││   ││ ││  │
│  │ iva_type: DEBIT_IVA / CREDIT_IVA                                      ││   ││ ││  │
│  │ status: RECORDED, REPORTED                                             ││   ││ ││  │
│  └────────────────────────────────────────────────────────────────────────┘   ││ ││  │
│                                                                                ││ ││  │
└────────────────────────────────────────────────────────────────────────────────┘│ ││  │
                                                                                   ││ ││  │
┌──────────────────────────────────────────────────────────────────────────────┐  ││ ││  │
│          TIER 4: TERCEROS (CLIENTES/PROVEEDORES)                            │  ││ ││  │
├──────────────────────────────────────────────────────────────────────────────┤  ││ ││  │
│                                                                              │  ││ ││  │
│  ┌─────────────────────────────────────────────────────────────────────┐   │  ││ ││  │
│  │ con_tercero (Third Parties)                                         │   │  ││ ││  │
│  ├─────────────────────────────────────────────────────────────────────┤   │  ││ ││  │
│  │ third_party_id (PK)                                                 │   │  ││ ││  │
│  │ company_id (FK) ◄──────────────────────────────────────────────────────┬──┘│ ││  │
│  │ code (UK)                                                           │   │  ││ ││  │
│  │ name                                                                │   │  ││ ││  │
│  │ category: CLIENTE, PROVEEDOR, EMPLEADO, OTRO                       │   │  ││ ││  │
│  │ is_supplier, is_customer                                            │   │  ││ ││  │
│  │ status, created_at/by, updated_at/by                               │   │  ││ ││  │
│  └─────────────────────────────────────────────────────────────────────┘   │  ││ ││  │
│       ◄────────────────────────────────────────────────────────────────────┘  ││ ││  │
│                                                                              │  ││ ││  │
└──────────────────────────────────────────────────────────────────────────────┘  ││ ││  │
                                                                                   ││ ││  │
┌──────────────────────────────────────────────────────────────────────────────┐  ││ ││  │
│          TIER 5: ACTIVOS FIJOS (FIXED ASSETS)                               │  ││ ││  │
├──────────────────────────────────────────────────────────────────────────────┤  ││ ││  │
│                                                                              │  ││ ││  │
│  ┌────────────────────────────┐     ┌──────────────────────────────┐       │  ││ ││  │
│  │ con_activo_tipo            │     │ con_activo_fijo              │       │  ││ ││  │
│  ├────────────────────────────┤     ├──────────────────────────────┤       │  ││ ││  │
│  │ type_id (PK)               │     │ asset_id (PK)               │       │  ││ ││  │
│  │ company_id (FK) ◄──────────┼─────► company_id (FK) ◄───────┐   │       │  ││ ││  │
│  │ code (UK)                  │     │ asset_type_id ◄─────────┘   │       │  ││ ││  │
│  │ name                       │     │ code (UK)                   │       │  ││ ││  │
│  └────────────────────────────┘     │ acquisition_date            │       │  ││ ││  │
│                                     │ acquisition_cost            │       │  ││ ││  │
│                                     │ salvage_value               │       │  ││ ││  │
│                                     │ useful_life_years           │       │  ││ ││  │
│                                     │ depreciation_method         │       │  ││ ││  │
│                                     │ accumulated_depreciation    │       │  ││ ││  │
│                                     │ asset_account_id ─────┐     │       │  ││ ││  │
│                                     │ depr_account_id ───┐  │     │       │  ││ ││  │
│                                     │ location            │  │     │       │  ││ ││  │
│                                     │ status              │  │     │       │  ││ ││  │
│                                     └────────┬───────────┬─┬─────────┘       │  ││ ││  │
│                                              │           │ │                │  ││ ││  │
│                                              ├───────────┼─┼────────────────┼──┘│ ││  │
│                                              │           │ │                │   │ ││  │
│          ┌───────────────────────────────────┴───────────┼─┼────────────────┼───┴─┘│  │
│          │                                   │           │ │                │      │  │
│          └─────────────► con_plan_cuentas ◄─┘           │ │                │      │  │
│                                                           │ │                │      │  │
│          ┌──────────────────────────────────────────────┼─┴────────────────┼──────┘  │
│          │                                              │                  │         │
│          │   ┌────────────────────────────────────────┬─────────────────┐  │         │
│          │   │ con_deprecacion (DEPRECIATION)         │                 │  │         │
│          │   ├─────────────────────────────────────────────────────────┤  │         │
│          │   │ depreciation_id (PK)                    │                 │  │         │
│          │   │ asset_id (FK) ◄──────────────────────────┘              │  │         │
│          │   │ period_id (FK) ◄────────► con_periodo_contable          │  │         │
│          │   │ month_number                            │                 │  │         │
│          │   │ depreciation_amount                     │                 │  │         │
│          │   │ accumulated_to_date                     │                 │  │         │
│          │   │ voucher_id ────────────► con_poliza                     │  │         │
│          │   │ UNIQUE(asset_id, period_id, month_num) │                 │  │         │
│          │   └─────────────────────────────────────────────────────────┘  │         │
│          │                                              │                  │         │
│          └───────────────────────────────────────────────┘                  │         │
│                                                                              │         │
└──────────────────────────────────────────────────────────────────────────────┘         │
                                                                                         │
┌────────────────────────────────────────────────────────────────────────────────────────┤
│          TIER 6: VALIDACIÓN Y CONTROLES DE INTEGRIDAD                                 │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                         │
│  REGLAS DE NEGOCIO:                                                                    │
│                                                                                         │
│  1. Póliza DRAFT                                                                       │
│     └─ Se puede crear, editar, eliminar                                               │
│     └─ No afecta saldos                                                               │
│                                                                                         │
│  2. Póliza POSTED                                                                      │
│     └─ total_debit MUST = total_credit (CHECK constraint)                              │
│     └─ Actualiza con_saldo_cuenta automáticamente                                      │
│     └─ No se puede editar (solo reversar)                                              │
│     └─ Registra en con_libro_iva si es operación fiscal                                │
│                                                                                         │
│  3. Período en estado diferente a OPEN                                                 │
│     └─ No se puede agregar pólizas                                                    │
│     └─ Se puede consultar histórico                                                   │
│                                                                                         │
│  4. Saldos de Apertura                                                                 │
│     └─ Se registran POR CUENTA por período                                             │
│     └─ Se incluyen en con_saldo_cuenta como beginning_debit/credit                     │
│                                                                                         │
│  5. Auditoría Completa                                                                 │
│     └─ created_at, created_by, updated_at, updated_by en TODAS las tablas              │
│     └─ posted_at, posted_by en con_poliza cuando status = POSTED                       │
│     └─ NUNCA borrar, solo marcar INACTIVE                                              │
│                                                                                         │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 🔄 FLUJO DE TRANSACCIÓN (STATE MACHINE)

```
┌─────────────────────────────────────────────────────────────────┐
│                    PÓLIZA LIFECYCLE                             │
└─────────────────────────────────────────────────────────────────┘

                          START
                            │
                            ▼
                  ┌─────────────────┐
                  │  CREAR PÓLIZA   │
                  │   (DRAFT)       │
                  └────────┬────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
        ▼                  ▼                  ▼
    ┌────────┐       ┌────────┐        ┌────────┐
    │EDITAR  │       │VALIDAR │        │ELIMINAR│
    │LÍNEAS  │       │BALANCE │        │(solo   │
    │        │       │        │        │DRAFT)  │
    └────────┘       └────┬───┘        └────────┘
                           │
                   ¿Balance OK?
                    /         \
                   /           \
                 SÍ              NO
                 │               │
                 ▼               ▼
          ┌──────────┐      ERRORES
          │ REGISTRAR│      Mostrar
          │(POSTED)  │
          └────┬─────┘
               │
               ▼
    ╔═════════════════════════════════╗
    ║ EFFECTS AL REGISTRAR:            ║
    ║ 1. total_debit = total_credit ✓  ║
    ║ 2. Actualizar con_saldo_cuenta   ║
    ║ 3. posted_at = NOW()             ║
    ║ 4. posted_by = USER              ║
    ║ 5. Registrar en con_libro_iva    ║
    ║    si es operación fiscal         ║
    ╚═════════════════════════════════╝
               │
               ▼
          ┌──────────┐
          │ REVERSAR │
          │(opcional)│
          └────┬─────┘
               │
               ▼
        ┌────────────┐
        │ REVERSED   │
        │(histórico) │
        └────────────┘
               │
               ▼
            FIN
```

---

## 📊 VISTA DE SALDOS

```
╔════════════════════════════════════════════════════════════════╗
║           CON_SALDO_CUENTA (Balance por Período)              ║
╠════════════════════════════════════════════════════════════════╣
║ Account: 1.1.2.01 (Clientes Nacionales)                       ║
║ Period: 202412                                                 ║
║                                                                ║
║  Saldo Inicial:                                               ║
║    Débito:    50,000.00    ◄─── De con_apertura_saldo        ║
║    Crédito:        0.00                                       ║
║                                                                ║
║  Movimientos del Período:  (POSTED pólizas solamente)          ║
║    Total Débito:   120,000.00                                 ║
║    Total Crédito:   80,000.00                                 ║
║                                                                ║
║  Saldo Final:                                                 ║
║    Débito:    90,000.00  ◄─── beginning + period              ║
║    Crédito:       80.00        saldos se rompen por           ║
║                                 naturaleza de cuenta          ║
║                                                                ║
║  Balance (Debit=positivo, Credit=negativo):                   ║
║    = (50,000 + 120,000) - 80,000 = 90,000 Debit              ║
╚════════════════════════════════════════════════════════════════╝
```

---

## 💾 INDICES RECOMENDADOS

```sql
-- Performance Indices

-- Búsquedas por empresa
CREATE INDEX ix_con_poliza_company ON con_poliza (company_id);
CREATE INDEX ix_con_saldo_cuenta_company ON con_saldo_cuenta (company_id);

-- Búsquedas por período
CREATE INDEX ix_con_poliza_period ON con_poliza (period_id);
CREATE INDEX ix_con_saldo_cuenta_period ON con_saldo_cuenta (period_id);

-- Búsquedas por cuenta
CREATE INDEX ix_con_saldo_cuenta_account ON con_saldo_cuenta (account_id);
CREATE INDEX ix_con_poliza_linea_account ON con_poliza_linea (account_id);

-- Búsquedas por fecha
CREATE INDEX ix_con_poliza_date ON con_poliza (voucher_date);
CREATE INDEX ix_con_libro_iva_date ON con_libro_iva (transaction_date);

-- Búsquedas por estado
CREATE INDEX ix_con_poliza_status ON con_poliza (status);
CREATE INDEX ix_con_periodo_status ON con_periodo_contable (status);

-- Unique constraints (también son índices)
UNIQUE(company_id, journal_id, voucher_number);
UNIQUE(company_id, period_id, account_id, cost_center_id);
```

---

## 📈 ESTIMACIÓN DE TAMAÑO (10 años, 1 empresa)

```
Asumiendo:
- 1 empresa
- 12 períodos/año
- 500 pólizas/mes = 6,000/año = 60,000 en 10 años
- 10 líneas promedio por póliza

Tabla                      | Rows      | Approx Size (MB)
──────────────────────────|───────────|─────────────────
con_poliza                | 60,000    | 5
con_poliza_linea          | 600,000   | 20
con_saldo_cuenta          | 36,000    | 2 (12 períodos x 3,000 cuentas)
con_balance_mensual       | 468,000   | 20
con_libro_iva             | 120,000   | 10
con_apertura_saldo        | 36,000    | 2
con_activo_fijo           | 500       | 0.1
con_deprecacion           | 60,000    | 2
──────────────────────────|───────────|─────────────────
TOTAL APROXIMADO          |           | ~60 MB (muy pequeño)
```

---

**Diagrama compilado**: 23 de Diciembre 2025  
**Precisión**: Fiel a especificación SQL detallada  
**Estado**: ✅ LISTO PARA DESARROLLO
