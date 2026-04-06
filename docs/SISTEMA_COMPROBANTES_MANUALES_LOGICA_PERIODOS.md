# Sistema de Comprobantes Manuales: Lógica, Períodos & Arquitectura

**Documento de Referencia Técnica**  
**Fecha:** 15 enero 2026  
**Módulo:** Contabilidad - Pólizas (Comprobantes Manuales)

---

## 📋 ÍNDICE

1. [Introducción & Conceptos](#introducción--conceptos)
2. [¿Cómo identificar el período contable actual?](#cómo-identificar-el-período-contable-actual)
3. [Arquitectura del Sistema de Comprobantes](#arquitectura-del-sistema-de-comprobantes)
4. [Flujo Completo de Comprobante Manual](#flujo-completo-de-comprobante-manual)
5. [Tablas & Relaciones](#tablas--relaciones)
6. [Lógica de Negocio por Operación](#lógica-de-negocio-por-operación)
7. [Mejoras Recomendadas para UI/UX](#mejoras-recomendadas-para-uiux)
8. [Ejemplos de Código & SQL](#ejemplos-de-código--sql)

---

## Introducción & Conceptos

### ¿Qué es un Comprobante Manual (Póliza)?

Una **póliza** es un documento contable que registra una transacción económica manualmente en el sistema. Está compuesto por:

- **Encabezado (con_partida_hdr)**: Información general del comprobante
  - Tipo de transacción (ingreso, egreso, traslado, etc.)
  - Fecha del comprobante
  - Período contable asociado
  - Diario contable (journal)
  - Usuario que lo creó y lo postuló

- **Líneas (con_partida_dtl)**: Detalles de débito/crédito
  - Cuenta contable
  - Centro de costo (opcional)
  - Tercero (opcional)
  - Monto de débito/crédito
  - Moneda
  - Descripción

### Estados de una Póliza

```
┌─────────────────────────────────────────────────────┐
│  DRAFT (0)                                          │
│  ├─ Puede editarse                                  │
│  ├─ Puede borrarse                                  │
│  └─ No afecta balances                              │
│                                                       │
│  → POSTED (1)  [Acción: Registrar/Postear]          │
│  ├─ NO puede editarse                               │
│  ├─ NO puede borrarse                               │
│  ├─ Afecta saldos contables                         │
│  └─ Se registra quién y cuándo se postuló           │
│                                                       │
│  → VOID (2)  [Acción: Anular]                       │
│  ├─ Reversión de asiento (se crea póliza inversa)   │
│  ├─ Genera auditoría completa                       │
│  └─ Original mantiene status VOID                   │
└─────────────────────────────────────────────────────┘
```

---

## ¿Cómo identificar el Período Contable Actual?

### 1️⃣ **Desde la Base de Datos**

```sql
-- Obtener el período ABIERTO actual
SELECT 
    period_id,
    code,
    name,
    start_date,
    end_date,
    status,
    created_by,
    created_at
FROM public.con_periodo_contable
WHERE company_id = :company_id
  AND status_id = 0
ORDER BY start_date DESC
LIMIT 1;
```

**Estructura de la tabla:**

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `period_id` | bigint PK | ID único del período |
| `company_id` | bigint FK | Empresa (multitenancy) |
| `code` | varchar(30) | Código formato: "YYYY-MM" |
| `name` | varchar(100) | Nombre formato: "Enero 2026" o similar |
| `start_date` | date | Inicio del período (1º del mes) |
| `end_date` | date | Fin del período (último día del mes) |
| `status_id` | smallint | 0 Abierto / 1 Precierre / 2 Cerrado |
| `status` | varchar(20) | ABIERTO / PRECIERRE / CERRADO (espejo temporal) |
| `created_by` | varchar(100) | Usuario que creó el período |
| `created_at` | timestamp tz | Timestamp UTC |
| `updated_by` | varchar(100) | Última actualización |
| `updated_at` | timestamp tz | Última fecha de actualización |
| `closed_by` | varchar(100) | Usuario que cerró el período |
| `closed_at` | timestamp tz | Cuándo se cerró |

### 2️⃣ **Desde el Backend (.NET)**

```csharp
// Interface IPeriodoContableService
public interface IPeriodoContableService
{
    // Obtener período activo/abierto para una empresa
    Task<PeriodoContableDto?> ObtenerPeriodoActivoAsync(long companyId, CancellationToken ct = default);
    
    // Crear período inicial si no existe
    Task<PeriodoContableDto> ObtenerOCrearPeriodoInicialAsync(
        long companyId, 
        DateTime? fechaInicio = null,
        CancellationToken ct = default);
    
    // Verificar si existe período abierto
    Task<bool> ExistePeriodoAbiertoAsync(long companyId, CancellationToken ct = default);
}

// Implementación en PeriodoContableService.cs
public async Task<PeriodoContableDto?> ObtenerPeriodoActivoAsync(long companyId, CancellationToken ct = default)
{
    var periodo = await _context.con_periodo_contables
        .Where(p => p.company_id == companyId && p.status_id == 0)
        .OrderByDescending(p => p.start_date)
        .FirstOrDefaultAsync(ct);
    
    return periodo == null ? null : MapearDto(periodo);
}
```

### 3️⃣ **Desde el Cliente Blazor**

```csharp
// En apc.Client/Services/Contabilidad/PeriodosClient.cs
public sealed class PeriodosClient
{
    private readonly HttpClient _http;
    
    public async Task<PeriodoContableDto?> ObtenerPeriodoActivoAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/contabilidad/catalogos/periodo-activo", ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al obtener período: {response.StatusCode}");
        
        return await response.Content.ReadFromJsonAsync<PeriodoContableDto>(cancellationToken: ct);
    }
}
```

### 4️⃣ **Información del Período en DTO**

```csharp
public record PeriodoContableDto(
    long PeriodId,
    long CompanyId,
    string Code,              // "2026-01"
    string Name,              // "Enero 2026"
    DateTime StartDate,       // 2026-01-01
    DateTime EndDate,         // 2026-01-31
    string Status,            // espejo textual: "ABIERTO" / "PRECIERRE" / "CERRADO"
    byte Month,               // 1-12 (número del mes)
    short Year,               // 2026
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ClosedAt,
    string? ClosedBy
);
```

---

## Arquitectura del Sistema de Comprobantes

### Stack Tecnológico

```
┌─────────────────────────────────────────────────────────────────┐
│  CLIENTE: Blazor WebAssembly (apc.Client)                       │
│  ├─ Pages/Contabilidad/Polizas.razor                            │
│  ├─ Components: Formularios DevExpress                          │
│  └─ Services: PolizasClient, PeriodosClient, CatalogosClient    │
└────────────┬────────────────────────────────────────────────────┘
             │ HTTP/REST (JSON)
             ↓
┌─────────────────────────────────────────────────────────────────┐
│  API: ASP.NET Core 9 (apc)                                      │
│  ├─ Controllers/Contabilidad/PolizasController.cs               │
│  ├─ IPolizaService (interfaz de negocio)                        │
│  └─ ICurrentCompanyService (multitenancia)                      │
└────────────┬────────────────────────────────────────────────────┘
             │ Entity Framework Core + Npgsql
             ↓
┌─────────────────────────────────────────────────────────────────┐
│  SERVICIOS: SIAD.Services (Lógica de Negocio)                   │
│  ├─ PolizaService.cs                                            │
│  ├─ ContabilidadCatalogosService.cs                             │
│  ├─ PeriodoContableService.cs                                   │
│  └─ SaldosService.cs (cálculo de saldos en tiempo real)         │
└────────────┬────────────────────────────────────────────────────┘
             │ DbContext
             ↓
┌─────────────────────────────────────────────────────────────────┐
│  DATOS: PostgreSQL (via SIAD.Data/SiadDbContext)                │
│  ├─ con_partida_hdr (encabezados de pólizas)                         │
│  ├─ con_partida_dtl (líneas de pólizas)                        │
│  ├─ con_plan_cuentas (catálogo de cuentas)                      │
│  ├─ con_centro_costo (centros de costo)                         │
│  ├─ con_tipo_transaccion (tipos de comprobantes)                │
│  ├─ con_periodo_contable (períodos contables)                   │
│  ├─ con_balance_mensual (saldos mensuales calculados)           │
│  └─ con_diario (diarios contables)                              │
└─────────────────────────────────────────────────────────────────┘
```

### Responsabilidades por Capa

| Capa | Responsabilidad |
|------|-----------------|
| **Blazor (Cliente)** | Capturar datos del usuario, validar formato, renderizar UI |
| **Controller (API)** | Validar ModelState, extraer contexto (usuario, empresa), orquestar servicios, devolver respuestas HTTP |
| **Service (Negocio)** | Validaciones de negocio, persistencia, cálculos de saldos, audit trail |
| **DbContext (EF)** | Mapeo de entidades, relaciones, migraciones |
| **PostgreSQL (DB)** | Almacenamiento persistente, integridad referencial, índices |

---

## Flujo Completo de Comprobante Manual

### 🎬 ESCENA 1: Carga Inicial (OnInitializedAsync)

```
┌─────────────────────────────────────┐
│ Usuario abre: /contabilidad/polizas │
└────────────┬────────────────────────┘
             ↓
   [Blazor OnInitializedAsync]
             ↓
  ┌──────────────────────────┐
  │ 1. ObtenerCatalogos()    │
  └──────┬───────────────────┘
         ├─→ GET /api/contabilidad/catalogos/tipos-transaccion
         │    → TiposTransaccion[] (cargar en memoria)
         │
         ├─→ GET /api/contabilidad/catalogos/plan-cuentas
         │    → PlanCuentas[] (cargar en memoria)
         │
         └─→ GET /api/contabilidad/catalogos/centros-costo
              → CentrosCosto[] (cargar en memoria)
             ↓
  ┌──────────────────────────┐
  │ 2. ObtenerPeriodo()      │
  └──────┬───────────────────┘
         └─→ GET /api/contabilidad/catalogos/periodo-activo
              Respuesta: { PeriodId=1, Code="2026-01", ... }
              Guardar en Form.PeriodId ← MUY IMPORTANTE
             ↓
  ✅ Formulario listo para capturar datos
```

### 🎬 ESCENA 2: Captura de Encabezado

```
Usuario completa:
├─ Tipo de comprobante: "1001" (Ingreso)
├─ Fecha: "2026-01-15"
├─ Documento: "FAC-2026-001"
├─ Módulo: "FACTURACION"
└─ Descripción: "Venta de servicios"

↓

Form.PeriodId ya contiene el ID obtenido en ESCENA 1
```

### 🎬 ESCENA 3: Captura de Líneas

```
Usuario agrega líneas:

Línea 1:
├─ Cuenta: "1010" (Bancos)
├─ Débito: 1000.00
├─ Crédito: 0.00
├─ Centro Costo: 01 (Ventas)
└─ Descripción: "Depósito en banco"

Línea 2:
├─ Cuenta: "4100" (Ingresos)
├─ Débito: 0.00
├─ Crédito: 1000.00
└─ Centro Costo: 01 (Ventas)

↓ VALIDACIONES AUTOMÁTICAS:
├─ ✅ Suma débitos == suma créditos (1000 = 1000)
├─ ✅ Todas las líneas tienen cuenta válida
├─ ✅ Centro de costo existe
└─ ✅ Período sigue abierto
```

### 🎬 ESCENA 4: Guardar Comprobante (DRAFT)

```
[Usuario presiona: "Guardar"]

↓

[Validar Cliente]
├─ Form.TypeId != null
├─ Form.PolizaDate != null
├─ Form.Lineas.Count > 0
├─ Form.Lineas.Sum(d) == Form.Lineas.Sum(c)
└─ Period aún abierto

↓ [Si pasa, enviar al servidor]

POST /api/contabilidad/polizas
{
    "typeId": 1001,
    "periodId": 1,                    ← PERÍODO CRÍTICO
    "journalId": null,
    "polizaDate": "2026-01-15T00:00:00Z",  ← UTC
    "module": "FACTURACION",
    "documentType": "FAC",
    "description": "Venta de servicios",
    "lineas": [
        { "accountId": 1010, "debitAmount": 1000, "creditAmount": 0, ... },
        { "accountId": 4100, "debitAmount": 0, "creditAmount": 1000, ... }
    ]
}

↓ [En PolizasController.Crear]

1. ICurrentCompanyService.GetCompanyId() → 1 (empresa actual del usuario)
2. User?.Identity?.Name → "jgarcia@empresa.com" (usuario actual)
3. var polizaDate = ConvertToUtc(req.PolizaDate) ← UTC CONVERSION
4. Llamar: _polizas.CrearAsync(
     companyId=1,
     typeId=1001,
     periodId=1,                ← PERÍODO VALIDADO
     journalId=null,
     polizaDate=... (UTC),
     module="FACTURACION",
     documentType="FAC",
     description="...",
     lineas=[...],
     userId="jgarcia@empresa.com",  ← AUDITORÍA
     ct=cancellationToken
   )

↓ [En PolizaService.CrearAsync]

1. Validaciones de Negocio:
   ├─ ¿El período existe y está abierto (`status_id = 0`)?
   │  SELECT * FROM con_periodo_contable 
   │  WHERE period_id = 1 AND company_id = 1 AND status_id = 0
   │
   ├─ ¿El tipo de transacción existe?
   │  SELECT * FROM con_tipo_transaccion 
   │  WHERE type_id = 1001 AND company_id = 1
   │
   └─ ¿Todas las cuentas existen y permiten posteo?
      SELECT * FROM con_plan_cuentas 
      WHERE account_id IN (1010, 4100) AND company_id = 1

2. Crear encabezado (con_partida_hdr):
   INSERT INTO con_partida_hdr (
       company_id=1,
       period_id=1,
       poliza_number=100001,          ← SECUENCIAL AUTO
       poliza_date='2026-01-15'::date,
       type_id=1001,
       module='FACTURACION',
       document_type='FAC',
       description='Venta de servicios',
       status=0,                       ← DRAFT
       created_by='jgarcia@empresa.com',
       created_at=NOW()::UTC,
       total_debit=1000.00,
       total_credit=1000.00
   ) RETURNING poliza_id → 5001

3. Crear líneas (con_partida_dtl):
   INSERT INTO con_partida_dtl (
       poliza_id=5001,
       line_number=1,
       account_id=1010,
       debit_amount=1000.00,
       credit_amount=0.00,
       ...
   )
   INSERT INTO con_partida_dtl (
       poliza_id=5001,
       line_number=2,
       account_id=4100,
       debit_amount=0.00,
       credit_amount=1000.00,
       ...
   )

4. Devolver: { id = 5001 }

↓ [Respuesta al cliente]

HTTP 201 Created
Location: /api/contabilidad/polizas/5001
{
    "id": 5001
}

↓ [Blazor muestra éxito]

✅ SuccessMessage = "Comprobante guardado como DRAFT"
   Grid se actualiza mostrando: #5001, DRAFT, ...
```

### 🎬 ESCENA 5: Registrar/Postear (DRAFT → POSTED)

```
[Usuario abre comprobante #5001]
[Revisa líneas: OK]
[Presiona botón: "Registrar"]

↓

PUT /api/contabilidad/polizas/5001/registrar

↓ [En PolizasController.Registrar]

1. CompanyId = 1 (usuario actual)
2. UserId = "jgarcia@empresa.com"
3. Llamar: _polizas.RegistrarAsync(1, 5001, "jgarcia@empresa.com", ct)

↓ [En PolizaService.RegistrarAsync]

1. Obtener póliza:
   SELECT * FROM con_partida_hdr WHERE poliza_id = 5001 AND company_id = 1
   → poliza (status=0, DRAFT)

2. Validar que esté en DRAFT:
   if (poliza.status != 0) throw InvalidOperationException(...)

3. Validar período abierto:
   SELECT * FROM con_periodo_contable 
   WHERE period_id = 1 AND status_id = 0
   → si no existe o está en precierre/cerrado, error

4. Actualizar póliza:
   UPDATE con_partida_hdr SET
       status = 1,                  ← POSTED
       posted_by = 'jgarcia...',
       posted_at = NOW()::UTC,
       updated_by = 'jgarcia...',
       updated_at = NOW()::UTC
   WHERE poliza_id = 5001

5. Recalcular saldos mensuales (con_balance_mensual):
   Para cada línea de la póliza:
   {
       monthly_balance_id := GET_OR_CREATE(
           company_id=1,
           period_id=1,
           month_number=1,        ← enero
           account_id=1010,
           cost_center_id=1
       )
       
       UPDATE con_balance_mensual SET
           debit_amount += 1000.00,
           transaction_count += 1,
           updated_at = NOW()
       WHERE monthly_balance_id = ...
   }

6. Generar entrada de auditoría:
   (Capturada automáticamente en created_by/updated_by/posted_by)

↓ [Respuesta]

HTTP 204 No Content

↓ [Blazor muestra éxito]

✅ SuccessMessage = "Comprobante registrado exitosamente"
   Botón "Registrar" desaparece
   Botón "Anular" se habilita
   Grid muestra status = "POSTED"
```

### 🎬 ESCENA 6: Anular (POSTED → VOID)

```
[Usuario abre comprobante #5001 (POSTED)]
[Presiona botón: "Anular"]
[Confirma acción]

↓

DELETE /api/contabilidad/polizas/5001/anular

↓ [En PolizasController.AnularAsync]

1. CompanyId = 1
2. UserId = "jgarcia@empresa.com"
3. Llamar: _polizas.AnularAsync(1, 5001, "jgarcia@empresa.com", ct)

↓ [En PolizaService.AnularAsync]

1. Obtener póliza original:
   SELECT * FROM con_partida_hdr WHERE poliza_id = 5001
   → poliza (status=1, POSTED)

2. Validar que NO esté VOID:
   if (poliza.status == 2) throw InvalidOperationException("Ya fue anulada")

3. Crear póliza inversa (reversal):
   INSERT INTO con_partida_hdr (
       company_id=1,
       period_id=1,
       poliza_number=100002,          ← NUEVO NÚMERO
       poliza_date='2026-01-15'::date,
       type_id=9999,                  ← TIPO REVERSAL
       description='REVERSAL: Venta de servicios #5001',
       status=1,                      ← POSTED INMEDIATAMENTE
       created_by='jgarcia...',
       created_at=NOW(),
       posted_by='jgarcia...',
       posted_at=NOW()
   ) RETURNING poliza_id → 5002

4. Crear líneas inversas (débitos/créditos invertidos):
   Para cada línea en póliza #5001:
   {
       INSERT INTO con_partida_dtl (
           poliza_id=5002,
           line_number=X,
           account_id=1010,
           debit_amount=0.00,         ← INVERTIDO (era 1000, ahora 0)
           credit_amount=1000.00,     ← INVERTIDO (era 0, ahora 1000)
           ...
       )
   }

5. Marcar original como VOID:
   UPDATE con_partida_hdr SET
       status = 2,                   ← VOID
       updated_by = 'jgarcia...',
       updated_at = NOW()
   WHERE poliza_id = 5001

6. Recalcular saldos (revertir transacción original + agregar inversa):
   Para póliza #5001 líneas:
       monthly_balance RESTA el importe
   Para póliza #5002 líneas:
       monthly_balance SUMA el importe inverso

7. Auditoría:
   - Póliza #5001: void_reason, void_by, void_at registrados
   - Póliza #5002: created_by, posted_by registrados (reversal)

↓ [Respuesta]

HTTP 200 OK
{
    "reversalPolizaId": 5002
}

↓ [Blazor muestra éxito]

✅ SuccessMessage = "Comprobante anulado. Reversal: #5002"
   Grid muestra:
   - #5001: VOID
   - #5002: POSTED (nueva póliza inversa)
```

---

## Tablas & Relaciones

### con_partida_hdr (Encabezados)

```sql
CREATE TABLE public.con_partida_hdr (
    poliza_id                BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id               BIGINT NOT NULL REFERENCES cfg_company(company_id),
    period_id                BIGINT NOT NULL REFERENCES con_periodo_contable(period_id),
    type_id                  BIGINT NOT NULL REFERENCES con_tipo_transaccion(type_id),
    journal_id               BIGINT REFERENCES con_diario(journal_id),
    poliza_number            BIGINT NOT NULL,           -- Número secuencial
    poliza_date              DATE NOT NULL,             -- Fecha del comprobante
    module                   VARCHAR(50),               -- FACTURACION, ORDENES, etc.
    document_type            VARCHAR(50),               -- FAC, OT, etc.
    document_number          VARCHAR(100),              -- FAC-2026-001, etc.
    document_id              BIGINT,
    description              TEXT,
    template_id              BIGINT,
    source_reference         VARCHAR(100),
    sequence_number          BIGINT,
    status                   SMALLINT NOT NULL,        -- 0=DRAFT, 1=POSTED, 2=VOID
    total_debit              NUMERIC(18,2) DEFAULT 0,
    total_credit             NUMERIC(18,2) DEFAULT 0,
    created_by               VARCHAR(100) NOT NULL,     -- AUDITORÍA: quién
    created_at               TIMESTAMP WITH TIME ZONE NOT NULL,  -- AUDITORÍA: cuándo
    updated_by               VARCHAR(100),              -- AUDITORÍA: quién actualizó
    updated_at               TIMESTAMP WITH TIME ZONE,  -- AUDITORÍA: cuándo actualizó
    posted_by                BIGINT,                    -- AUDITORÍA: quién postuló (user_id o nombre)
    posted_at                TIMESTAMP WITH TIME ZONE,  -- AUDITORÍA: cuándo se postuló
    
    UNIQUE (company_id, period_id, poliza_number)
);
```

### con_partida_dtl (Líneas de Pólizas)

```sql
CREATE TABLE public.con_partida_dtl (
    poliza_line_id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    poliza_id               BIGINT NOT NULL REFERENCES con_partida_hdr(poliza_id) ON DELETE CASCADE,
    company_id              BIGINT NOT NULL REFERENCES cfg_company(company_id),
    line_number             SMALLINT NOT NULL,         -- 1, 2, 3...
    account_id              BIGINT NOT NULL REFERENCES con_plan_cuentas(account_id),
    cost_center_id          BIGINT REFERENCES con_centro_costo(cost_center_id),
    third_party_id          BIGINT,                    -- Referencia a tercero (cliente/proveedor)
    debit_amount            NUMERIC(18,2) DEFAULT 0,
    credit_amount           NUMERIC(18,2) DEFAULT 0,
    currency_code           VARCHAR(3) DEFAULT 'DOP',  -- Código moneda
    exchange_rate           NUMERIC(10,4) DEFAULT 1.00,
    description             TEXT,
    source_document         VARCHAR(100),
    
    UNIQUE (poliza_id, line_number),
    CONSTRAINT ck_debit_or_credit CHECK (debit_amount > 0 XOR credit_amount > 0)
);
```

### con_periodo_contable (Períodos)

```sql
CREATE TABLE public.con_periodo_contable (
    period_id               BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id              BIGINT NOT NULL REFERENCES cfg_company(company_id),
    code                    VARCHAR(30) NOT NULL,      -- "2026-01"
    name                    VARCHAR(100),              -- "Enero 2026"
    start_date              DATE NOT NULL,
    end_date                DATE NOT NULL,
    status                  VARCHAR(20),               -- ABIERTO, PRECIERRE, CERRADO (espejo)
    created_by              VARCHAR(100),
    created_at              TIMESTAMP WITH TIME ZONE,
    updated_by              VARCHAR(100),
    updated_at              TIMESTAMP WITH TIME ZONE,
    closed_by               VARCHAR(100),
    closed_at               TIMESTAMP WITH TIME ZONE,
    
    UNIQUE (company_id, code),
    CONSTRAINT ck_dates CHECK (end_date >= start_date)
);
```

### con_tipo_transaccion (Tipos de Comprobantes)

```sql
CREATE TABLE public.con_tipo_transaccion (
    type_id                 BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id              BIGINT NOT NULL REFERENCES cfg_company(company_id),
    code                    VARCHAR(50) NOT NULL,      -- "1001", "1002"
    name                    VARCHAR(100),              -- "Ingreso", "Egreso"
    description             TEXT,
    category                VARCHAR(50),               -- INGRESO, EGRESO, TRASLADO
    type_trans              VARCHAR(20),               -- Tipo de transacción
    type_oper               VARCHAR(20),               -- Tipo de operación
    frequency               VARCHAR(20),               -- MANUAL, AUTOMATICA
    max_entries             INT,
    allows_cost_center      BOOLEAN DEFAULT TRUE,
    allows_third_party      BOOLEAN DEFAULT TRUE,
    allows_cash_flow        BOOLEAN DEFAULT FALSE,
    allows_account_limit    BOOLEAN DEFAULT FALSE,
    is_default              BOOLEAN DEFAULT FALSE,
    is_automatic            BOOLEAN DEFAULT FALSE,
    status                  VARCHAR(20) DEFAULT 'ACTIVE',
    created_by              VARCHAR(100),
    created_at              TIMESTAMP WITH TIME ZONE,
    updated_by              VARCHAR(100),
    updated_at              TIMESTAMP WITH TIME ZONE,
    
    UNIQUE (company_id, code)
);
```

### con_balance_mensual (Saldos Calculados)

```sql
CREATE TABLE public.con_balance_mensual (
    monthly_balance_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id              BIGINT NOT NULL REFERENCES cfg_company(company_id),
    period_id               BIGINT NOT NULL REFERENCES con_periodo_contable(period_id),
    account_id              BIGINT NOT NULL REFERENCES con_plan_cuentas(account_id),
    cost_center_id          BIGINT REFERENCES con_centro_costo(cost_center_id),
    month_number            SMALLINT NOT NULL,         -- 1-12 (o 13 para acumulado)
    debit_amount            NUMERIC(18,2) DEFAULT 0,
    credit_amount           NUMERIC(18,2) DEFAULT 0,
    transaction_count       INT DEFAULT 0,
    created_at              TIMESTAMP WITH TIME ZONE,
    updated_at              TIMESTAMP WITH TIME ZONE,
    
    UNIQUE (company_id, period_id, account_id, cost_center_id, month_number),
    CONSTRAINT ck_month CHECK (month_number >= 1 AND month_number <= 13)
);
```

---

## Lógica de Negocio por Operación

### ✅ CREAR (DRAFT)

```csharp
public async Task<long> CrearAsync(
    long companyId,
    long typeId,
    long periodId,
    long? journalId,
    DateTime polizaDate,
    string module,
    string documentType,
    string description,
    List<PolizaLineaCrearDto> lineas,
    string userId,
    CancellationToken ct = default
)
{
    // 1. VALIDAR PERÍODO ABIERTO
    var periodo = await _context.con_periodo_contables
        .FirstOrDefaultAsync(p => p.period_id == periodId && p.company_id == companyId, ct);
    
    if (periodo?.status_id != 0)
        throw new InvalidOperationException($"Período {periodId} no está abierto");

    // 2. VALIDAR TIPO DE TRANSACCIÓN
    var tipo = await _context.con_tipo_transaccions
        .FirstOrDefaultAsync(t => t.type_id == typeId && t.company_id == companyId, ct);
    
    if (tipo == null)
        throw new InvalidOperationException($"Tipo de transacción {typeId} no existe");

    // 3. VALIDAR CUENTAS Y CALCULAR TOTALES
    decimal totalDebit = 0, totalCredit = 0;
    foreach (var linea in lineas)
    {
        var cuenta = await _context.con_plan_cuentass
            .FirstOrDefaultAsync(c => c.account_id == linea.AccountId && c.company_id == companyId, ct);
        
        if (cuenta == null)
            throw new InvalidOperationException($"Cuenta {linea.AccountId} no existe");
        
        if (!cuenta.allows_posting)
            throw new InvalidOperationException($"Cuenta {linea.AccountId} no permite posteo");
        
        totalDebit += linea.DebitAmount;
        totalCredit += linea.CreditAmount;
    }

    // 4. VALIDAR BALANCE DÉBITO = CRÉDITO
    if (Math.Abs(totalDebit - totalCredit) > 0.01m)
        throw new InvalidOperationException(
            $"Desbalance: Débito={totalDebit}, Crédito={totalCredit}");

    // 5. OBTENER NÚMERO SECUENCIAL
    var maxNumber = await _context.con_partida_hdrs
        .Where(p => p.company_id == companyId && p.period_id == periodId)
        .MaxAsync(p => (long?)p.poliza_number, ct) ?? 0;
    
    var polizaNumber = maxNumber + 1;

    // 6. CREAR ENCABEZADO
    var poliza = new con_partida_hdr
    {
        company_id = companyId,
        period_id = periodId,
        type_id = typeId,
        journal_id = journalId,
        poliza_number = polizaNumber,
        poliza_date = polizaDate.Date,
        module = module,
        document_type = documentType,
        description = description,
        status = 0,  // DRAFT
        total_debit = totalDebit,
        total_credit = totalCredit,
        created_by = userId,
        created_at = DateTime.UtcNow
    };
    
    await _context.con_partida_hdrs.AddAsync(poliza, ct);
    await _context.SaveChangesAsync(ct);

    // 7. CREAR LÍNEAS
    short lineNumber = 1;
    foreach (var lineaDto in lineas)
    {
        var linea = new con_partida_dtl
        {
            poliza_id = poliza.poliza_id,
            company_id = companyId,
            line_number = lineNumber++,
            account_id = lineaDto.AccountId,
            cost_center_id = lineaDto.CostCenterId,
            third_party_id = lineaDto.ThirdPartyId,
            debit_amount = lineaDto.DebitAmount,
            credit_amount = lineaDto.CreditAmount,
            currency_code = lineaDto.CurrencyCode,
            exchange_rate = lineaDto.ExchangeRate,
            description = lineaDto.Description,
            source_document = lineaDto.SourceDocument
        };
        
        await _context.con_partida_dtls.AddAsync(linea, ct);
    }
    
    await _context.SaveChangesAsync(ct);
    
    return poliza.poliza_id;
}
```

### ✅ REGISTRAR (DRAFT → POSTED)

```csharp
public async Task RegistrarAsync(
    long companyId,
    long polizaId,
    string userId,
    CancellationToken ct = default
)
{
    // 1. OBTENER PÓLIZA
    var poliza = await _context.con_partida_hdrs
        .FirstOrDefaultAsync(p => p.poliza_id == polizaId && p.company_id == companyId, ct);
    
    if (poliza == null)
        throw new InvalidOperationException("Póliza no encontrada");
    
    if (poliza.status != 0)
        throw new InvalidOperationException("Solo se pueden registrar pólizas en DRAFT");

    // 2. VALIDAR PERÍODO ABIERTO
    var periodo = await _context.con_periodo_contables
        .FirstOrDefaultAsync(p => p.period_id == poliza.period_id && p.status_id == 0, ct);
    
    if (periodo == null)
        throw new InvalidOperationException("Período no está abierto");

    // 3. ACTUALIZAR PÓLIZA A POSTED
    poliza.status = 1;  // POSTED
    poliza.posted_by = /* userId ID desde user context */ long.Parse(userId.GetUserIdFromClaims());
    poliza.posted_at = DateTime.UtcNow;
    poliza.updated_by = userId;
    poliza.updated_at = DateTime.UtcNow;
    
    _context.con_partida_hdrs.Update(poliza);
    await _context.SaveChangesAsync(ct);

    // 4. RECALCULAR SALDOS MENSUALES
    var lineas = await _context.con_partida_dtls
        .Where(l => l.poliza_id == polizaId)
        .ToListAsync(ct);
    
    var monthNumber = (short)poliza.poliza_date.Month;
    
    foreach (var linea in lineas)
    {
        var balance = await _context.con_balance_mensuales
            .FirstOrDefaultAsync(b =>
                b.company_id == companyId &&
                b.period_id == poliza.period_id &&
                b.account_id == linea.account_id &&
                b.cost_center_id == linea.cost_center_id &&
                b.month_number == monthNumber,
            ct);
        
        if (balance == null)
        {
            balance = new con_balance_mensual
            {
                company_id = companyId,
                period_id = poliza.period_id,
                account_id = linea.account_id,
                cost_center_id = linea.cost_center_id,
                month_number = monthNumber,
                debit_amount = linea.debit_amount,
                credit_amount = linea.credit_amount,
                transaction_count = 1,
                created_at = DateTime.UtcNow
            };
            
            await _context.con_balance_mensuales.AddAsync(balance, ct);
        }
        else
        {
            balance.debit_amount += linea.debit_amount;
            balance.credit_amount += linea.credit_amount;
            balance.transaction_count += 1;
            balance.updated_at = DateTime.UtcNow;
            
            _context.con_balance_mensuales.Update(balance);
        }
    }
    
    await _context.SaveChangesAsync(ct);
}
```

---

## Mejoras Recomendadas para UI/UX

### 🎨 PROBLEMA ACTUAL

La UI actual es básica (inputs HTML simples + grillas). Necesita:

1. **Flujo visual deficiente**: No se ve claramente el período actual, estado de la póliza, progreso
2. **Sin validación en tiempo real**: Errores solo aparecen al enviar
3. **Pobre integración de catálogos**: Combos desorganizados
4. **Tabla de líneas poco funcional**: Difícil agregar/editar líneas
5. **Sin resumen/preview**: Usuario no ve totales hasta guardar

### ✨ MEJORAS PROPUESTAS

#### 1. **Header con Período Prominente**

```razor
<div class="card border-primary mb-3">
    <div class="card-header bg-primary text-white">
        <h5>Comprobante Manual - Período Activo</h5>
    </div>
    <div class="card-body">
        <div class="row">
            <div class="col-md-3">
                <strong>Período:</strong><br>
                <span class="badge bg-success fs-5">@PeriodoActivo.Code</span>
                <small class="d-block text-muted mt-2">
                    @PeriodoActivo.StartDate.ToDateString() - @PeriodoActivo.EndDate.ToDateString()
                </small>
            </div>
            <div class="col-md-3">
                <strong>Estado:</strong><br>
                <span class="badge bg-info">@Form.Status</span>
            </div>
            <div class="col-md-3">
                <strong>Número:</strong><br>
                <span class="fw-bold">@Form.PolizaNumber</span>
            </div>
            <div class="col-md-3">
                <strong>Usuario:</strong><br>
                <small>@CurrentUser.Name</small>
            </div>
        </div>
    </div>
</div>
```

#### 2. **Formulario Encabezado Mejorado**

```razor
<div class="card mb-3">
    <div class="card-header">
        <h5>Datos del Comprobante</h5>
    </div>
    <div class="card-body">
        <div class="row g-3">
            <!-- Tipo de Comprobante con descripción -->
            <div class="col-md-4">
                <label class="form-label">Tipo de Comprobante *</label>
                <DxComboBox Data="@TiposTransaccion"
                            @bind-Value="Form.TypeId"
                            ValueFieldName="TypeId"
                            TextFieldName="@((Func<TipoTransaccionDto, string>)(t => $"{t.Code} - {t.Name}"))"
                            Placeholder="Seleccione tipo..."
                            CssClass="form-control"
                            @onchange="OnTypeChanged">
                </DxComboBox>
                @if (SelectedTipo != null)
                {
                    <small class="d-block text-muted mt-1">
                        Categoría: @SelectedTipo.Category | Frecuencia: @SelectedTipo.Frequency
                    </small>
                }
            </div>
            
            <!-- Fecha con validación -->
            <div class="col-md-2">
                <label class="form-label">Fecha *</label>
                <DxDateEdit @bind-Date="Form.PolizaDate"
                            Min="@PeriodoActivo.StartDate"
                            Max="@PeriodoActivo.EndDate"
                            CssClass="w-100"
                            @onchange="OnFechaChanged">
                </DxDateEdit>
                <small class="d-block mt-1">
                    Debe estar en el período
                </small>
            </div>
            
            <!-- Documento y Módulo -->
            <div class="col-md-3">
                <label class="form-label">Documento / Módulo</label>
                <div class="d-flex gap-2">
                    <DxComboBox Data="@DocumentTypes"
                                @bind-Value="Form.DocumentType"
                                Placeholder="FAC, OT..."
                                CssClass="flex-grow-1">
                    </DxComboBox>
                    <input class="form-control" placeholder="Módulo" @bind="Form.Module" />
                </div>
            </div>
            
            <!-- Descripción -->
            <div class="col-md-3">
                <label class="form-label">Descripción</label>
                <textarea class="form-control" placeholder="Concepto" @bind="Form.Description" rows="1"></textarea>
            </div>
        </div>
    </div>
</div>
```

#### 3. **Tabla de Líneas Editable**

```razor
<div class="card mb-3">
    <div class="card-header d-flex justify-content-between align-items-center">
        <h5>Líneas de la Póliza</h5>
        <button class="btn btn-sm btn-success" @onclick="AgregarLinea">
            <i class="fas fa-plus"></i> Agregar Línea
        </button>
    </div>
    <div class="card-body" style="overflow-x: auto;">
        <DxGrid Data="@Form.Lineas"
                @bind-SelectedDataItems="SelectedLineas"
                EditMode="GridEditMode.EditRow"
                AllowSelectRowByClick="true"
                CssClass="w-100">
            <DxGridCommandColumn Width="100px">
                <CellDisplayTemplate Context="context">
                    @{
                        var linea = (PolizaLineaCrearDto)context.DataItem;
                    }
                    <button class="btn btn-sm btn-warning" @onclick="() => EditarLinea(linea)">
                        ✎
                    </button>
                    <button class="btn btn-sm btn-danger" @onclick="() => EliminarLinea(linea)">
                        ✕
                    </button>
                </CellDisplayTemplate>
            </DxGridCommandColumn>
            
            <DxGridDataColumn FieldName="@nameof(PolizaLineaCrearDto.LineNumber)" Caption="#" Width="50px" />
            
            <DxGridDataColumn FieldName="@nameof(PolizaLineaCrearDto.AccountId)" Caption="Cuenta" Width="150px">
                <EditCellTemplate Context="editContext">
                    <DxComboBox Data="@PlanCuentas"
                                @bind-Value="editContext.CellValue"
                                ValueFieldName="AccountId"
                                TextFieldName="@((Func<PlanCuentasDto, string>)(c => $"{c.Code} - {c.Name}"))"
                                @oninput="(ChangeEventArgs args) => editContext.CellValue = args?.Value">
                    </DxComboBox>
                </EditCellTemplate>
            </DxGridDataColumn>
            
            <DxGridDataColumn FieldName="@nameof(PolizaLineaCrearDto.Description)" Caption="Descripción" />
            
            <DxGridDataColumn FieldName="@nameof(PolizaLineaCrearDto.DebitAmount)" Caption="Débito" DisplayFormat="N2" Width="120px">
                <EditCellTemplate Context="editContext">
                    <DxSpinEdit @bind-Value="editContext.CellValue" 
                                DisplayFormat="N2"
                                @oninput="(ChangeEventArgs args) => OnLineaChanged()">
                    </DxSpinEdit>
                </EditCellTemplate>
            </DxGridDataColumn>
            
            <DxGridDataColumn FieldName="@nameof(PolizaLineaCrearDto.CreditAmount)" Caption="Crédito" DisplayFormat="N2" Width="120px">
                <EditCellTemplate Context="editContext">
                    <DxSpinEdit @bind-Value="editContext.CellValue" 
                                DisplayFormat="N2"
                                @oninput="(ChangeEventArgs args) => OnLineaChanged()">
                    </DxSpinEdit>
                </EditCellTemplate>
            </DxGridDataColumn>
            
            <DxGridDataColumn FieldName="@nameof(PolizaLineaCrearDto.CostCenterId)" Caption="Centro Costo" Width="150px">
                <EditCellTemplate Context="editContext">
                    <DxComboBox Data="@CentrosCosto"
                                @bind-Value="editContext.CellValue"
                                ValueFieldName="CostCenterId"
                                TextFieldName="@((Func<CentroCostoDto, string>)(c => $"{c.Code} - {c.Name}"))"
                                AllowCustomValues="true"
                                @oninput="(ChangeEventArgs args) => editContext.CellValue = args?.Value">
                    </DxComboBox>
                </EditCellTemplate>
            </DxGridDataColumn>
        </DxGrid>
    </div>
</div>
```

#### 4. **Resumen Dinámico**

```razor
<div class="card border-info mb-3">
    <div class="card-header bg-info text-white">
        <h5>Resumen</h5>
    </div>
    <div class="card-body">
        <div class="row text-center">
            <div class="col-md-3">
                <h3 class="text-success">@Form.Lineas.Sum(l => l.DebitAmount).ToString("N2")</h3>
                <small>Total Débito</small>
            </div>
            <div class="col-md-3">
                <h3 class="text-danger">@Form.Lineas.Sum(l => l.CreditAmount).ToString("N2")</h3>
                <small>Total Crédito</small>
            </div>
            <div class="col-md-3">
                <h3 class="@GetBalanceClass()">@Math.Abs(Form.Lineas.Sum(l => l.DebitAmount) - Form.Lineas.Sum(l => l.CreditAmount)).ToString("N2")</h3>
                <small>Diferencia</small>
            </div>
            <div class="col-md-3">
                <h3>@Form.Lineas.Count</h3>
                <small>Líneas</small>
            </div>
        </div>
        @if (!IsBalanceOk())
        {
            <div class="alert alert-warning mt-3 mb-0">
                ⚠️ El comprobante está desbalanceado (Débito ≠ Crédito)
            </div>
        }
    </div>
</div>
```

#### 5. **Acciones Mejoradas**

```razor
<div class="d-flex gap-2">
    <button class="btn btn-primary" @onclick="GuardarAsync" disabled="@IsSaving || !IsValid()">
        <i class="fas fa-save"></i> Guardar como DRAFT
    </button>
    @if (Form.Status == "DRAFT")
    {
        <button class="btn btn-success" @onclick="RegistrarAsync" disabled="@IsSaving || !IsValid()">
            <i class="fas fa-check"></i> Registrar
        </button>
    }
    @if (Form.Status == "POSTED")
    {
        <button class="btn btn-danger" @onclick="AnularAsync" disabled="@IsSaving">
            <i class="fas fa-times"></i> Anular
        </button>
    }
    <button class="btn btn-secondary" @onclick="LimpiarFormulario">
        <i class="fas fa-redo"></i> Limpiar
    </button>
    <a href="/contabilidad/polizas-lista" class="btn btn-outline-secondary">
        <i class="fas fa-list"></i> Ver Historial
    </a>
</div>
```

---

## Ejemplos de Código & SQL

### Obtener Período Activo (Backend)

```csharp
[HttpGet("periodo-activo")]
public async Task<IActionResult> ObtenerPeriodoActivo(CancellationToken ct)
{
    var companyId = _currentCompany.GetCompanyId();
    var periodo = await _periodosService.ObtenerPeriodoActivoAsync(companyId, ct);
    
    if (periodo == null)
        return NotFound(new ProblemDetails
        {
            Title = "Período no encontrado",
            Detail = "No hay período contable abierto para esta empresa"
        });
    
    return Ok(periodo);
}
```

### Validar Período al Crear Póliza

```sql
-- Verificar que el período está abierto ANTES de insertar
SELECT EXISTS (
    SELECT 1
    FROM public.con_periodo_contable
    WHERE company_id = :company_id
      AND period_id = :period_id
      AND status_id = 0
      AND start_date <= :poliza_date::date
      AND end_date >= :poliza_date::date
) AS is_valid;
```

### Recalcular Saldos Mensuales

```sql
-- Después de registrar póliza, actualizar o crear balance mensual
INSERT INTO public.con_balance_mensual (
    company_id, period_id, account_id, cost_center_id,
    month_number, debit_amount, credit_amount, transaction_count, created_at
)
SELECT 
    :company_id,
    :period_id,
    l.account_id,
    l.cost_center_id,
    EXTRACT(MONTH FROM p.poliza_date)::smallint,
    SUM(CASE WHEN l.debit_amount > 0 THEN l.debit_amount ELSE 0 END),
    SUM(CASE WHEN l.credit_amount > 0 THEN l.credit_amount ELSE 0 END),
    COUNT(*)::int,
    NOW()::timestamp with time zone
FROM public.con_partida_hdr p
JOIN public.con_partida_dtl l ON p.poliza_id = l.poliza_id
WHERE p.company_id = :company_id
  AND p.period_id = :period_id
  AND p.status = 1  -- POSTED
  AND p.poliza_date >= (
      SELECT start_date FROM public.con_periodo_contable 
      WHERE period_id = :period_id
  )
GROUP BY l.account_id, l.cost_center_id
ON CONFLICT (company_id, period_id, account_id, cost_center_id, month_number)
DO UPDATE SET
    debit_amount = EXCLUDED.debit_amount,
    credit_amount = EXCLUDED.credit_amount,
    transaction_count = EXCLUDED.transaction_count,
    updated_at = NOW();
```

### Auditoría: Historial de Cambios por Usuario

```sql
-- Ver todas las pólizas que registró un usuario en un período
SELECT 
    p.poliza_id,
    p.poliza_number,
    p.poliza_date,
    p.status,
    p.created_by,
    p.created_at,
    p.posted_by,
    p.posted_at,
    COUNT(l.poliza_line_id) as line_count,
    p.total_debit,
    p.total_credit
FROM public.con_partida_hdr p
LEFT JOIN public.con_partida_dtl l ON p.poliza_id = l.poliza_id
WHERE p.company_id = :company_id
  AND p.period_id = :period_id
  AND p.posted_by = :user_id
GROUP BY p.poliza_id, p.poliza_number, p.poliza_date, p.status, 
         p.created_by, p.created_at, p.posted_by, p.posted_at, 
         p.total_debit, p.total_credit
ORDER BY p.created_at DESC;
```

---

## 📊 RESUMEN EJECUTIVO

| Aspecto | Estado | Recomendación |
|---------|--------|---------------|
| **Período Actual** | ✅ Implementado | Obtener automáticamente en OnInitialized, usar `IPeriodoContableService` |
| **Validación Período** | ✅ Implementado | Verificar que fecha está dentro de rango antes de guardar |
| **Flujo DRAFT→POSTED** | ✅ Implementado | Botones progresivos según estado |
| **Auditoría (WHO/WHEN)** | ✅ Implementado | created_by/at, posted_by/at, updated_by/at |
| **UI/UX** | ⚠️ Mejorable | Implementar grid editable, resumen en tiempo real |
| **Validación en Tiempo Real** | ❌ Pendiente | Agregar validación onchange en líneas (débito=crédito) |
| **Reversal (Anular)** | ✅ Implementado | Crear póliza inversa automáticamente |
| **Balances Mensuales** | ✅ Implementado | Se actualizan al registrar |

---

**Documento Completo**  
Archivos relacionados:
- [AUDITORIA_CONTABILIDAD_SISTEMA.md](AUDITORIA_CONTABILIDAD_SISTEMA.md)
- [CONSULTAS_AUDITORIA_CONTABILIDAD.md](CONSULTAS_AUDITORIA_CONTABILIDAD.md)
- [RESUMEN_REVISION_AUDITORIA.md](RESUMEN_REVISION_AUDITORIA.md)
- [apc/Controllers/Contabilidad/PolizasController.cs](../apc/Controllers/Contabilidad/PolizasController.cs)
- [SIAD.Services/Contabilidad/PolizaService.cs](../SIAD.Services/Contabilidad/PolizaService.cs)



