# 🔍 Debug: Error PostgreSQL en PosteoManual.razor

## Problema Reportado
- Usuario hace clic en "Registrar" en PosteoManual
- GET `saldos-manual` devuelve datos ✅
- POST `posteo-manual` retorna **400 Bad Request**
- Error: `Npgsql.PostgresException` (8 excepciones)

## Rastreo del Flujo

### 1️⃣ Request: GET /api/captacionpagos/saldos-manual/CLI-DEMO-001
✅ **FUNCIONA**
- Endpoint: `CaptacionPagosController.GetSaldosManual(clienteClave)`
- Servicio: `CaptacionPagosService.ObtenerSaldosPosteoManualAsync(clienteClave)`
- BD: Ejecuta `fn_getclientesaldos_posteomanual(@ClienteClave)`
- Retorna: `List<SaldoPosteoManualDto>` con 10+ filas

### 2️⃣ Función BD: fn_getclientesaldos_posteomanual
**Ubicación**: `Database/ddl_v3/captacion_pagos_stored_procedures.sql:114-162`

**Propósito**: Retorna facturas pendientes del cliente con distribución de servicios

**Estructura**:
```sql
RETURNS TABLE (
    recibo_actual INT,
    recibo_anterior INT,
    valor DECIMAL(18,2),
    distribucion_agua DECIMAL(18,2),
    distribucion_alcantarillado DECIMAL(18,2),
    distribucion_otros DECIMAL(18,2),
    detalle_id BIGINT  ← ¡CRÍTICO!
)
```

**Columna Problemática**: `detalle_id`
```sql
-- Línea 153
COALESCE(ds.detalle_id, 0) AS detalle_id
```

**Problema**:
- Si **NO HAY** filas en `factura_detalle` para la factura → retorna `0`
- Si **HAY** filas en `factura_detalle` → retorna el `MIN(id)` de esas filas
- El código C# rechaza si `DetalleId <= 0` (PosteoManual.razor:209-210)

### 3️⃣ Data Model
```
factura (tabla maestra)
├── id (BIGINT) ← PK
├── numfactura (VARCHAR)
├── clientecodigo (VARCHAR)
├── estado (CHAR) = 'A' (Abierto)
├── saldototal (DECIMAL)
└── [otros campos]

factura_detalle (lineas de factura)
├── id (BIGINT) ← PK
├── factura_id (BIGINT) ← FK
├── linea (INT)
├── tiposervicio (VARCHAR) = 'AGUA', 'ALCANTARILLADO', etc
├── montovalor (DECIMAL)
├── montovalor_saldo (DECIMAL) ← Saldo actual
└── [otros campos]
```

### 4️⃣ Query de la Función
```sql
WITH facturas_pendientes AS (
    SELECT f.numrecibo, f.id, f.saldototal, ...
    WHERE f.clientecodigo = @Cliente
      AND f.estado = 'A'
      AND f.saldototal > 0
),
distribucion_servicios AS (
    SELECT fd.factura_id,
           MIN(fd.id) AS detalle_id,  ← Primer detalle de la factura
           SUM(...) AS agua,
           SUM(...) AS alcantarillado,
           ...
    FROM factura_detalle fd
    GROUP BY fd.factura_id
)
SELECT ..., COALESCE(ds.detalle_id, 0) AS detalle_id
FROM facturas_pendientes fp
LEFT JOIN distribucion_servicios ds ON ds.factura_id = fp.factura_id
```

**Lógica**:
- Si `factura` existe pero `factura_detalle` NO → `detalle_id = 0` ❌
- Si `factura` y `factura_detalle` existen → `detalle_id = MIN(id)` ✅

### 5️⃣ Validación en PosteoManual.razor
```csharp
// PosteoManual.razor:209-210
if (seleccionado.DetalleId is null || seleccionado.DetalleId <= 0)
{
    ShowToast("No se encontró el detalle a acreditar.", ToastRenderStyle.Danger);
    return;
}
```

**Problema**: Si no hay detalles, `DetalleId` es `0` → Rechazo ❌

### 6️⃣ POST /api/captacionpagos/posteo-manual
📨 **Payload**:
```json
{
  "clienteClave": "CLI-DEMO-001",
  "numRecibo": 2025001,
  "banco": "EFE",
  "valor": 100.00,
  "distribucion": [
    {
      "id": 0,  ← ¡AQUÍ ESTÁ EL PROBLEMA!
      "valorDistribuido": 100.00
    }
  ],
  "usuario": "system"
}
```

### 7️⃣ Backend: CaptacionPagosService.RegistrarPagoManualAsync
**Ubicación**: `SIAD.Services/CaptacionPagos/CaptacionPagosService.cs:346-438`

**Flujo**:
1. Valida DTO ✅
2. Abre conexión BD
3. **Para cada distribución**:
   ```csharp
   await connection.ExecuteAsync(
       sqlActualizar,  // SP: sp_actualizar_detalle_posteomanual
       new {
           DetalleId = dist.Id,  ← 0 (¡INVÁLIDO!)
           MontoAcreditado = 100.00,
           MontoDebitado = 0
       }
   );
   ```
4. Ejecuta SP: `sp_actualizar_detalle_posteomanual(0, 100.00, 0)`

### 8️⃣ SP: sp_actualizar_detalle_posteomanual
**Ubicación**: `Database/ddl_v3/captacion_pagos_stored_procedures.sql:167-181`

```sql
CREATE OR REPLACE FUNCTION sp_actualizar_detalle_posteomanual(
    p_detalle_id BIGINT,          ← 0 (INVÁLIDO)
    p_monto_acreditado DECIMAL,
    p_monto_debitado DECIMAL
) RETURNS VOID AS $$
BEGIN
    UPDATE factura_detalle
       SET montovalor_saldo = montovalor_saldo - p_monto_acreditado + p_monto_debitado
     WHERE id = p_detalle_id;  ← WHERE id = 0 (NO ENCUENTRA NADA)
    
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No se encontró el detalle % para actualizar', p_detalle_id;
        ↓
        POSTGRESQL EXCEPTION ❌
    END IF;
END;
```

**Error**:
```
Npgsql.PostgresException: 
  "No se encontró el detalle 0 para actualizar"
  
Severity: ERROR
Sql State: P0001 (PL/pgSQL raise exception)
```

---

## 🎯 Causa Raíz

| Punto Crítico | Estado | Problemas |
|---|---|---|
| `factura_maestro` tabla | ✅ Existe | Facturas de prueba creadas |
| `factura_detalle` tabla | ❓ Desconocido | **¿TIENE DATOS?** |
| `fn_getclientesaldos_posteomanual` | 🔴 **RETORNA DETALLE_ID=0** | Si no hay `factura_detalle` |
| Validación C# PosteoManual | 🔴 **RECHAZA SI ID<=0** | Bloquea registro anticipadamente |
| SP `sp_actualizar_detalle_posteomanual` | 🔴 **FALLA CON ID=0** | Excepción PostgreSQL |

---

## ✅ Solución: 2 Enfoques

### Opción 1: Fijar los Datos de Prueba (Recomendado para Testing)
**Crear `factura_detalle` con datos válidos**:
```sql
-- Script: Database/ddl_v3/fix_factura_detalle_prueba.sql

-- Para cada factura de prueba CLI-DEMO-001, crear detalles
INSERT INTO factura_detalle 
  (factura_id, linea, tiposervicio, montovalor, montovalor_saldo)
VALUES
  (1, 1, 'AGUA', 50.00, 50.00),
  (1, 2, 'ALCANTARILLADO', 30.00, 30.00),
  (1, 3, 'OTROS', 20.00, 20.00),
  ... (más para otras facturas)
```

**Resultado**:
- `fn_getclientesaldos_posteomanual` retorna `detalle_id` = valor real ✅
- POST succeeds ✅

### Opción 2: Ajustar la SP para Crear Detalles Implícitos
**Modificar `sp_actualizar_detalle_posteomanual`**:
```sql
-- Si no existe detalle, crear uno con el monto total
CREATE OR REPLACE FUNCTION sp_actualizar_detalle_posteomanual(
    p_detalle_id BIGINT,
    p_monto_acreditado DECIMAL,
    p_monto_debitado DECIMAL
) RETURNS VOID AS $$
BEGIN
    -- Si detalle_id es 0 o nulo, buscar/crear uno
    IF p_detalle_id <= 0 THEN
        RAISE EXCEPTION 'detalle_id debe ser > 0';
    END IF;
    
    UPDATE factura_detalle
       SET montovalor_saldo = ...
     WHERE id = p_detalle_id;
    
    IF NOT FOUND THEN
        RAISE EXCEPTION '...';
    END IF;
END;
```

### Opción 3: Crear Detalles Automáticamente en Posteo
**En `sp_registrar_posteo_manual`, si no hay detalle, crearlo**:
```sql
-- En lugar de fallar, crear factura_detalle ausente
IF p_detalle_id = 0 THEN
    INSERT INTO factura_detalle (factura_id, montovalor_saldo, ...)
    RETURNING id INTO p_detalle_id;
END IF;
```

---

## 🚀 Plan de Acción Inmediato

### Paso 1: Verificar Datos Existentes
```sql
-- Ejecutar en psql:
SELECT COUNT(*) FROM factura WHERE clientecodigo = 'CLI-DEMO-001' AND estado = 'A';
SELECT COUNT(*) FROM factura_detalle;
SELECT * FROM fn_getclientesaldos_posteomanual('CLI-DEMO-001');
```

### Paso 2: Poblar factura_detalle si falta
```sql
-- Si la query anterior retorna detalle_id = 0, ejecutar seed:
\i Database/ddl_v3/test_data_posteo_manual.sql
```

### Paso 3: Reintentar POST
- Usuario regresa a UI
- Hace clic "Cargar saldos" → GET limpio
- Detalles ahora tienen IDs válidos
- Click "Registrar" → POST succeeds ✅

---

## 📝 Documentación de Cambios

**Archivos Afectados**:
1. [Database/ddl_v3/captacion_pagos_stored_procedures.sql](Database/ddl_v3/captacion_pagos_stored_procedures.sql) - SPs
2. [SIAD.Services/CaptacionPagos/CaptacionPagosService.cs](SIAD.Services/CaptacionPagos/CaptacionPagosService.cs) - Servicio backend
3. [apc.Client/Pages/Facturacion/CaptacionPagos/PosteoManual.razor](apc.Client/Pages/Facturacion/CaptacionPagos/PosteoManual.razor) - Component Blazor

**Última Actualización**: 2026-01-15  
**Status**: 🔴 Bloqueado - Falta `factura_detalle` con datos válidos
