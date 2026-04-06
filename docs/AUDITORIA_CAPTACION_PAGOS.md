# 🔍 AUDITORÍA - Captación de Pagos: Legado vs Migración Blazor

**Fecha**: 16 de enero de 2026  
**Objetivo**: Validar que TODAS las funcionalidades del módulo "Captación de Pagos" del sistema legado (ASP.NET Core 3) fueron migradas correctamente al nuevo sistema Blazor WASM + .NET 9.

---

## 📋 RESUMEN EJECUTIVO

| **Componente** | **Legado** | **Migrado** | **Estado** | **% Completado** |
|---|---|---|---|---|
| **Tab 1: Posteo Lectoras** | ✅ Completo | ⚠️ **PARCIAL** | **GAP CRÍTICO** | **40%** |
| **Tab 2: Posteo Manual** | ✅ Completo | ❌ **NO MIGRADO** | **FALTA** | **0%** |
| **Tab 3: Posteo Misceláneos** | ✅ Completo | ⚠️ **PARCIAL** | **GAP MEDIO** | **60%** |
| **Búsqueda autocompletado** | ✅ Completo | ❌ **NO MIGRADO** | **FALTA** | **0%** |
| **Stored Procedures** | ✅ Completo | ❌ **NO MIGRADO** | **FALTA** | **0%** |

### ⚠️ **CONCLUSIÓN**: El módulo está **INCOMPLETO** - Faltan componentes críticos del flujo original.

---

## 🔴 GAPS CRÍTICOS IDENTIFICADOS

### 1. **Tab 1: Posteo Lectoras (Lector Óptico)** - 40% Completado

#### ✅ **Lo que SÍ está migrado**:
- ✅ Estructura básica de tablas `pagos_hdr` y `pagos_dtl`
- ✅ Endpoint básico para registrar pago simple
- ✅ Endpoint para reversar pago (parcial)
- ✅ Obtener header y detalle de pago por factura

#### ❌ **Lo que FALTA (CRÍTICO)**:

| **Funcionalidad Legado** | **Estado Actual** | **Componente Faltante** |
|---|---|---|
| **SearchCodigo(coincidencia)** - Autocompletado de factura/recibo | ❌ NO EXISTE | Endpoint `/api/captacionpagos/search/{term}` |
| **GetCaptacionPagosHeader(numfactura)** - Trae datos cliente + dirección + recibo + verifica si existe captación previa | ⚠️ PARCIAL | Método `ExisteRegistroPago()` no implementado |
| **GetCaptacionPagosDetails(numfactura)** - Devuelve líneas de `Factura + FacturaDetalle`, incluye saldo anterior si aplica | ⚠️ PARCIAL | No incluye saldo anterior del cliente |
| **RegistrarPagoLectora(CaptaciondePagosDTO)** - Tipo transacción 201, partida 002, descripción, créditos=∑montos, saldo actualizado, ciclo/ruta/secuencia del cliente | ⚠️ SIMPLIFICADO | No registra tipo transacción, partida, ni ciclo/ruta/secuencia |
| **ReversarPagoLectora(numfactura, clienteclave)** - Usa `GetCaptacionPagoId` y detalle; revierte saldos con SP, borra `TransaccionAbonado`, actualiza factura a estado 'A' | ⚠️ SIMPLIFICADO | No usa stored procedure `sp_actualizar_detalle_posteolectora`, no actualiza `TransaccionAbonado` |
| **JavaScript buscarcod (enter)** - Consume endpoint y muestra lista; al seleccionar, llama `loadheaderdata` y `loadDataTableOrdenes` | ❌ NO EXISTE | UI completa de búsqueda con autocompletado |
| **Combo Clientes** - `_unitOfWork.captaciondepagos.GetCodCliente()` → SelectList en ViewBag | ❌ NO EXISTE | Endpoint para listar clientes activos |
| **Combo Bancos** - `_unitOfWork.captaciondepagos.GetCodBanco()` → SelectList en ViewBag | ❌ NO EXISTE | Endpoint para listar bancos/recolectoras |
| **Periodo actual** - `_unitOfWork.general.GetPeriodoActual()` → label periodo | ❌ NO EXISTE | Endpoint para obtener período activo actual |

#### 📋 **Repositorio faltante**:
```csharp
// LEGADO: ICaptaciondePagosRepository.cs
public interface ICaptaciondePagosRepository
{
    List<Cliente> GetCodCliente();                        // ❌ NO MIGRADO
    List<Recolectora> GetCodBanco();                      // ❌ NO MIGRADO
    IEnumerable<dynamic> GetMatchingInvoices(string term); // ❌ NO MIGRADO (SP get_matching_invoices)
    CaptacionHeaderDto GetCaptacionPagosHeader(string numfactura); // ⚠️ PARCIAL
    List<CaptacionDetailDto> GetCaptacionPagosDetails(string numfactura); // ⚠️ PARCIAL
    bool ExisteRegistroPago(string numfactura);           // ❌ NO MIGRADO
    void RegistrarPagoLectora(CaptaciondePagosDTO dto);   // ⚠️ SIMPLIFICADO
    void ReversarPagoLectora(string numfactura, string clienteclave); // ⚠️ SIMPLIFICADO
}
```

---

### 2. **Tab 2: Posteo Manual** - 0% Completado ❌

**ESTADO**: **COMPLETAMENTE AUSENTE** en el código migrado.

#### ❌ **Funcionalidades NO migradas**:

| **Funcionalidad Legado** | **Estado Actual** | **Descripción** |
|---|---|---|
| **GetSaldosDtlPosteoManual(codigoCliente)** | ❌ NO EXISTE | Retorna DataTable con saldos pendientes usando función `fn_getclientesaldos_posteomanual` |
| **Validación JS ValidarTotalDistribuido** | ❌ NO EXISTE | Total distribuido debe igualar valor total |
| **RegistrarPagoManual(codigoCliente, numreciboanterior, numrecibo, banco, valor, distribucion JSON)** | ❌ NO EXISTE | Por cada línea ejecuta `sp_actualizar_detalle_posteomanual(@p_ide, @p_monto_acreditado=valordistribuido, debitado=0)`. Luego `sp_registrar_posteo_manual` para insertar `TransaccionAbonado` (tipo 201, partida 002) |
| **ReversarPagoManual(codigoCliente, numrecibo, distribucion)** | ❌ NO EXISTE | Por línea: `sp_actualizar_detalle_posteomanual(@p_ide, @p_valor=0)`. Luego `sp_reversar_posteo_manual` (borra/registra reversión de transacción abonado) |
| **DataTable dtposteoManual** | ❌ NO EXISTE | Grid con saldos distribuidos (recibo actual/anterior, valor, distribución) |
| **Labels de totales** | ❌ NO EXISTE | `lbltotalreciboManual`, `lbltotalDistreciboManual` |

#### 📁 **Archivos faltantes**:
```
❌ apc/Controllers/CaptacionPagosController.cs
   └─ [POST] /api/captacionpagos/posteo-manual          (NO EXISTE)
   └─ [POST] /api/captacionpagos/posteo-manual/reverso  (NO EXISTE)
   └─ [GET]  /api/captacionpagos/saldos-manual/{clienteClave} (NO EXISTE)

❌ SIAD.Services/CaptacionPagos/CaptacionPagosService.cs
   └─ Task<IReadOnlyList<SaldoPosteoManualDto>> ObtenerSaldosPosteoManualAsync(string clienteClave, CancellationToken ct)
   └─ Task<ResponseModelDto> RegistrarPagoManualAsync(PagoManualCrearDto dto, CancellationToken ct)
   └─ Task<ResponseModelDto> ReversarPagoManualAsync(ReversoManualRequestDto dto, CancellationToken ct)

❌ SIAD.Core/DTOs/CaptacionPagos/PosteoManualDtos.cs (ARCHIVO COMPLETO NO EXISTE)
   └─ SaldoPosteoManualDto
   └─ PagoManualCrearDto
   └─ PagoManualDistribucionDto
   └─ ReversoManualRequestDto

❌ apc.Client/Pages/Facturacion/CaptacionPagos/PosteoManual.razor (ARCHIVO COMPLETO NO EXISTE)
```

#### 🗄️ **Stored Procedures faltantes**:
```sql
-- LEGADO: Usados por Posteo Manual
❌ fn_getclientesaldos_posteomanual(@p_codigo_cliente)  -- Función para saldos pendientes
❌ sp_actualizar_detalle_posteomanual(@p_ide, @p_monto_acreditado, @p_monto_debitado)
❌ sp_registrar_posteo_manual(@p_codigo_cliente, @p_numrecibo, @p_banco, @p_valor, @p_tipo, @p_partida, @p_usuario)
❌ sp_reversar_posteo_manual(@p_codigo_cliente, @p_numrecibo, @p_usuario)
```

---

### 3. **Tab 3: Posteo Misceláneos** - 60% Completado ⚠️

#### ✅ **Lo que SÍ está migrado**:
- ✅ Endpoint para listar recibos misceláneos por cliente: `GET /api/captacionpagos/miscelaneos?clienteClave={clave}`
- ✅ DTO `ReciboMiscelaneoDto` con propiedades: Recibo, Cliente, Fecha, Total, Estado

#### ❌ **Lo que FALTA (MEDIO)**:

| **Funcionalidad Legado** | **Estado Actual** | **Componente Faltante** |
|---|---|---|
| **RecibosMiscelaneos()** - Retorna partial `_captacionMiscelaneos` (modal de selección) | ❌ NO EXISTE | Modal/componente Blazor para seleccionar recibos misceláneos |
| **GetReciboDtl(recibo)** - Trae detalle del recibo seleccionado | ❌ NO EXISTE | Endpoint `/api/captacionpagos/miscelaneos/{recibo}/detalle` |
| **RegistrarPagoMiscelaneo(recibo, banco)** - Actualiza factura a estado C, setea recolectora/fecha pago, crea `TransaccionAbonado` (tipo 201) con saldo recalculado | ❌ NO EXISTE | Endpoint POST con lógica completa |
| **ReversarPagoMiscelaneo(recibo)** - Devuelve factura a estado A (recolectora null, fechapago null) | ❌ NO EXISTE | Endpoint POST para reverso específico de misceláneos |
| **DataTable dtrecibodtl** - Grid con detalles del recibo | ❌ NO EXISTE | Grid de detalle en UI |
| **Labels de totales** - `lbltotalreciboMis` | ❌ NO EXISTE | Labels para mostrar total del recibo |

#### 📁 **Archivos que necesitan ampliación**:
```
⚠️ apc/Controllers/CaptacionPagosController.cs
   └─ [GET]  /api/captacionpagos/miscelaneos/{recibo}/detalle  (FALTA)
   └─ [POST] /api/captacionpagos/miscelaneos/registrar         (FALTA)
   └─ [POST] /api/captacionpagos/miscelaneos/reverso           (FALTA)

⚠️ SIAD.Services/CaptacionPagos/CaptacionPagosService.cs
   └─ Task<IReadOnlyList<ReciboMiscelaneoDetalleDto>> ObtenerDetalleReciboMiscelaneoAsync(long recibo, CancellationToken ct)
   └─ Task<ResponseModelDto> RegistrarPagoMiscelaneoAsync(PagoMiscelaneoCrearDto dto, CancellationToken ct)
   └─ Task<ResponseModelDto> ReversarPagoMiscelaneoAsync(ReversoMiscelaneoRequestDto dto, CancellationToken ct)

❌ SIAD.Core/DTOs/CaptacionPagos/CaptacionPagosDtos.cs (AMPLIAR)
   └─ ReciboMiscelaneoDetalleDto (NO EXISTE)
   └─ PagoMiscelaneoCrearDto (NO EXISTE)
   └─ ReversoMiscelaneoRequestDto (NO EXISTE)

❌ apc.Client/Pages/Facturacion/CaptacionPagos/PosteoMiscelaneos.razor (ARCHIVO NO EXISTE)
```

---

### 4. **Stored Procedures y Funciones de Base de Datos** - 0% Migrado ❌

El sistema legado usa **MÚLTIPLES stored procedures críticos** que NO fueron migrados:

#### 🗄️ **Stored Procedures del Legado (NO EXISTEN en el nuevo sistema)**:

```sql
-- POSTEO LECTORAS
❌ sp_actualizar_detalle_posteolectora(
      @p_factura_id INT,
      @p_linea INT,
      @p_monto_acreditado DECIMAL(18,2),
      @p_monto_debitado DECIMAL(18,2)
   ) -- Actualiza saldos en factura_detalle

❌ sp_actualizar_factura_pago(
      @p_numfactura VARCHAR(50),
      @p_cliente_clave VARCHAR(50),
      @p_estado CHAR(1),
      @p_banco VARCHAR(100),
      @p_usuario VARCHAR(100)
   ) -- Actualiza estado de factura y registra pago

❌ sp_obtener_cliente_saldo(
      @p_cliente_clave VARCHAR(50)
   ) -- Retorna saldo actual del cliente
   RETURNS TABLE (cliente_clave, saldo_actual, saldo_anterior, ...)

-- POSTEO MANUAL
❌ fn_getclientesaldos_posteomanual(
      @p_codigo_cliente VARCHAR(50)
   ) -- Función que retorna saldos pendientes para posteo manual
   RETURNS TABLE (recibo_actual, recibo_anterior, valor, distribucion_agua, distribucion_alcantarillado, ...)

❌ sp_actualizar_detalle_posteomanual(
      @p_ide BIGINT,
      @p_monto_acreditado DECIMAL(18,2),
      @p_monto_debitado DECIMAL(18,2)
   ) -- Actualiza detalle de pago manual

❌ sp_registrar_posteo_manual(
      @p_codigo_cliente VARCHAR(50),
      @p_numrecibo INT,
      @p_banco VARCHAR(100),
      @p_valor DECIMAL(18,2),
      @p_tipo_transaccion VARCHAR(10),
      @p_partida VARCHAR(10),
      @p_usuario VARCHAR(100)
   ) -- Inserta TransaccionAbonado con tipo 201, partida 002

❌ sp_reversar_posteo_manual(
      @p_codigo_cliente VARCHAR(50),
      @p_numrecibo INT,
      @p_usuario VARCHAR(100)
   ) -- Borra/registra reversión de transacción abonado

-- BÚSQUEDA Y AUTOCOMPLETADO
❌ get_matching_invoices(
      @p_term VARCHAR(100)
   ) -- Retorna facturas/recibos que coinciden con el término de búsqueda
   RETURNS TABLE (numfactura, cliente, fecha, total, estado, ...)
```

#### ⚠️ **IMPACTO CRÍTICO**: 
- El código actual usa **EF Core LINQ** para todo
- Los stored procedures del legado contienen **lógica de negocio crítica** que se perdió en la migración
- **Riesgo de inconsistencias** en los saldos y transacciones contables

---

### 5. **Interfaz de Usuario (Razor/JavaScript)** - 30% Migrado

#### ✅ **Lo que SÍ está migrado**:
- ✅ Página básica `/facturacion/captacion/caja` con arqueo diario
- ✅ Formulario simple de registro rápido de pago
- ✅ Grid de arqueos con filtros por caja y fecha
- ✅ Página `/facturacion/captacion/reverso` con búsqueda y reverso básico

#### ❌ **Lo que FALTA (CRÍTICO)**:

| **Componente Legado** | **Estado Actual** | **Descripción** |
|---|---|---|
| **Vista con Tabs (Posteo Lectoras, Manual, Misceláneos)** | ❌ NO EXISTE | La vista actual es monolítica sin tabs |
| **Autocompletado de factura/recibo con ENTER** | ❌ NO EXISTE | Búsqueda en tiempo real al presionar ENTER, muestra lista de coincidencias |
| **DataTable dtcaptacion (lector)** | ❌ NO EXISTE | Grid con detalle de factura seleccionada |
| **DataTable dtposteoManual (manual)** | ❌ NO EXISTE | Grid con saldos distribuidos |
| **DataTable dtrecibodtl (misceláneos)** | ❌ NO EXISTE | Grid con detalle del recibo misceláneo |
| **Validaciones JS del legado** | ❌ NO EXISTE | - Validar campos obligatorios (banco/fecha/factura)<br>- Validar distribución = valor total (manual)<br>- Validar selección de conceptos |
| **Labels dinámicos de totales** | ❌ NO EXISTE | - `lbltotalrecibo` (lector)<br>- `lbltotalreciboManual` (manual)<br>- `lbltotalDistreciboManual` (manual)<br>- `lbltotalreciboMis` (misceláneos) |
| **Botones habilitar/deshabilitar según estado** | ⚠️ PARCIAL | Botón habilita/deshabilita según `existeCaptacion` o estado del recibo/saldo |
| **Modal de selección de recibos misceláneos** | ❌ NO EXISTE | Partial `_captacionMiscelaneos` con búsqueda y selección |
| **Combo de clientes (GetCodCliente)** | ❌ NO EXISTE | Combo desplegable con clientes activos |
| **Combo de bancos (GetCodBanco)** | ❌ NO EXISTE | Combo desplegable con bancos/recolectoras |
| **Label de período actual** | ❌ NO EXISTE | Indicador del período contable activo |

#### 📁 **Archivos de UI faltantes**:
```
❌ apc.Client/Pages/Facturacion/CaptacionPagos/PosteoLectoras.razor
❌ apc.Client/Pages/Facturacion/CaptacionPagos/PosteoManual.razor
❌ apc.Client/Pages/Facturacion/CaptacionPagos/PosteoMiscelaneos.razor
❌ apc.Client/Pages/Facturacion/CaptacionPagos/Components/BuscadorFacturas.razor
❌ apc.Client/Pages/Facturacion/CaptacionPagos/Components/DetalleFactura.razor
❌ apc.Client/Pages/Facturacion/CaptacionPagos/Components/SaldosManual.razor
❌ apc.Client/Pages/Facturacion/CaptacionPagos/Components/SelectorMiscelaneos.razor
```

---

## 📊 MATRIZ DE COMPARACIÓN DETALLADA

### **Posteo Lectoras (Lector Óptico)**

| **Funcionalidad** | **Legado** | **Migrado** | **Gap** |
|---|---|---|---|
| Búsqueda por factura/recibo (autocompletado) | ✅ Completo con enter | ❌ NO | **CRÍTICO** |
| Mostrar header de pago (cliente, dirección, recibo) | ✅ Completo | ⚠️ Parcial | **MEDIO** |
| Verificar si existe captación previa | ✅ ExisteRegistroPago() | ❌ NO | **CRÍTICO** |
| Mostrar detalle de tarifa (grid) | ✅ DataTable | ❌ NO | **ALTO** |
| Sumar total de factura | ✅ JS | ❌ NO | **MEDIO** |
| Registrar pago con tipo transacción 201 | ✅ Completo | ⚠️ Simplificado | **CRÍTICO** |
| Registrar partida 002 | ✅ Completo | ❌ NO | **CRÍTICO** |
| Actualizar saldo con SP | ✅ `sp_actualizar_detalle_posteolectora` | ❌ NO | **CRÍTICO** |
| Crear TransaccionAbonado | ✅ Completo | ❌ NO | **CRÍTICO** |
| Actualizar factura a estado C | ✅ Completo | ⚠️ Parcial | **MEDIO** |
| Reversar pago con SP | ✅ `sp_actualizar_detalle_posteolectora` (debitar) | ❌ NO | **CRÍTICO** |
| Borrar TransaccionAbonado en reverso | ✅ Completo | ❌ NO | **CRÍTICO** |
| Actualizar factura a estado A en reverso | ✅ Completo | ⚠️ Parcial | **MEDIO** |
| Combo de clientes | ✅ GetCodCliente() | ❌ NO | **MEDIO** |
| Combo de bancos | ✅ GetCodBanco() | ❌ NO | **MEDIO** |
| Label de período actual | ✅ GetPeriodoActual() | ❌ NO | **BAJO** |

### **Posteo Manual**

| **Funcionalidad** | **Legado** | **Migrado** | **Gap** |
|---|---|---|---|
| Selección de cliente | ✅ Completo | ❌ NO | **CRÍTICO** |
| Obtener saldos pendientes | ✅ `fn_getclientesaldos_posteomanual` | ❌ NO | **CRÍTICO** |
| Grid de saldos distribuidos | ✅ DataTable | ❌ NO | **CRÍTICO** |
| Validación total distribuido = valor | ✅ JS | ❌ NO | **CRÍTICO** |
| Registrar pago manual con distribución | ✅ `sp_actualizar_detalle_posteomanual` | ❌ NO | **CRÍTICO** |
| Crear TransaccionAbonado (tipo 201, partida 002) | ✅ `sp_registrar_posteo_manual` | ❌ NO | **CRÍTICO** |
| Reversar pago manual | ✅ `sp_reversar_posteo_manual` | ❌ NO | **CRÍTICO** |
| Labels de totales | ✅ `lbltotalreciboManual`, `lbltotalDistreciboManual` | ❌ NO | **BAJO** |

### **Posteo Misceláneos**

| **Funcionalidad** | **Legado** | **Migrado** | **Gap** |
|---|---|---|---|
| Listar recibos misceláneos por cliente | ✅ GetRecibosMiscelaneos() | ✅ GET /api/captacionpagos/miscelaneos | ✅ OK |
| Modal de selección | ✅ Partial `_captacionMiscelaneos` | ❌ NO | **MEDIO** |
| Obtener detalle del recibo | ✅ GetReciboDtl(recibo) | ❌ NO | **MEDIO** |
| Grid de detalle | ✅ DataTable | ❌ NO | **MEDIO** |
| Registrar pago misceláneo | ✅ RegistrarPagoMiscelaneo() | ❌ NO | **CRÍTICO** |
| Actualizar factura a estado C | ✅ Completo | ❌ NO | **CRÍTICO** |
| Setear recolectora/fecha pago | ✅ Completo | ❌ NO | **CRÍTICO** |
| Crear TransaccionAbonado (tipo 201) | ✅ Completo | ❌ NO | **CRÍTICO** |
| Reversar pago misceláneo | ✅ ReversarPagoMiscelaneo() | ❌ NO | **CRÍTICO** |
| Devolver factura a estado A | ✅ Completo | ❌ NO | **CRÍTICO** |
| Label de total | ✅ `lbltotalreciboMis` | ❌ NO | **BAJO** |

---

## 🎯 PLAN DE ACCIÓN - IMPLEMENTACIÓN COMPLETA

### **Fase 1: Stored Procedures (PRIORIDAD MÁXIMA)** ⚠️

**Objetivo**: Crear todos los stored procedures faltantes en la base de datos.

#### **Archivo a crear**: `Database/ddl_v3/captacion_pagos_stored_procedures.sql`

```sql
-- ================================================
-- Stored Procedures para Captación de Pagos
-- ================================================

-- 1. POSTEO LECTORAS
CREATE OR REPLACE FUNCTION sp_actualizar_detalle_posteolectora(
    p_factura_id BIGINT,
    p_linea INT,
    p_monto_acreditado DECIMAL(18,2),
    p_monto_debitado DECIMAL(18,2)
) RETURNS VOID AS $$
BEGIN
    -- Lógica de actualización de saldos en factura_detalle
    UPDATE factura_detalle
       SET montovalor_saldo = montovalor_saldo - p_monto_acreditado + p_monto_debitado
     WHERE factura_id = p_factura_id
       AND linea = p_linea;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_actualizar_factura_pago(
    p_numfactura VARCHAR(50),
    p_cliente_clave VARCHAR(50),
    p_estado CHAR(1),
    p_banco VARCHAR(100),
    p_usuario VARCHAR(100)
) RETURNS VOID AS $$
BEGIN
    -- Lógica de actualización de estado de factura
    UPDATE factura
       SET estado = p_estado,
           recolectora = p_banco,
           fechapago = CASE WHEN p_estado = 'C' THEN NOW() ELSE NULL END,
           usuario = p_usuario
     WHERE numfactura = p_numfactura
       AND clientecodigo = p_cliente_clave;
END;
$$ LANGUAGE plpgsql;

-- 2. POSTEO MANUAL
CREATE OR REPLACE FUNCTION fn_getclientesaldos_posteomanual(
    p_codigo_cliente VARCHAR(50)
) RETURNS TABLE (
    recibo_actual INT,
    recibo_anterior INT,
    valor DECIMAL(18,2),
    distribucion_agua DECIMAL(18,2),
    distribucion_alcantarillado DECIMAL(18,2),
    distribucion_otros DECIMAL(18,2)
) AS $$
BEGIN
    -- Lógica de consulta de saldos pendientes
    RETURN QUERY
    SELECT 
        f.numrecibo AS recibo_actual,
        LAG(f.numrecibo) OVER (ORDER BY f.numrecibo DESC) AS recibo_anterior,
        f.saldototal AS valor,
        -- ... distribución por servicio
    FROM factura f
    WHERE f.clientecodigo = p_codigo_cliente
      AND f.estado = 'A'
    ORDER BY f.numrecibo DESC;
END;
$$ LANGUAGE plpgsql;

-- ... (resto de stored procedures)
```

**Tareas**:
1. ✅ Crear archivo `captacion_pagos_stored_procedures.sql`
2. ✅ Implementar los 7 stored procedures faltantes
3. ✅ Documentar parámetros y lógica de negocio
4. ✅ Ejecutar en base de datos de desarrollo
5. ✅ Validar con datos de prueba

---

### **Fase 2: DTOs y Contratos (Backend)** 📋

**Objetivo**: Ampliar DTOs existentes y crear los faltantes.

#### **Archivo a modificar**: `SIAD.Core/DTOs/CaptacionPagos/CaptacionPagosDtos.cs`

**Agregar**:
```csharp
// POSTEO MANUAL
public class SaldoPosteoManualDto
{
    public int ReciboActual { get; set; }
    public int? ReciboAnterior { get; set; }
    public decimal Valor { get; set; }
    public decimal DistribucionAgua { get; set; }
    public decimal DistribucionAlcantarillado { get; set; }
    public decimal DistribucionOtros { get; set; }
}

public class PagoManualCrearDto
{
    [Required]
    public string ClienteClave { get; set; } = string.Empty;
    
    public int? NumReciboAnterior { get; set; }
    
    [Required]
    public int NumRecibo { get; set; }
    
    [Required]
    public string Banco { get; set; } = string.Empty;
    
    [Range(0.01, double.MaxValue)]
    public decimal Valor { get; set; }
    
    [Required]
    public List<PagoManualDistribucionDto> Distribucion { get; set; } = new();
    
    public string Usuario { get; set; } = string.Empty;
}

public class PagoManualDistribucionDto
{
    public long Id { get; set; }
    public decimal ValorDistribuido { get; set; }
}

public class ReversoManualRequestDto
{
    [Required]
    public string ClienteClave { get; set; } = string.Empty;
    
    [Required]
    public int NumRecibo { get; set; }
    
    [Required]
    public List<PagoManualDistribucionDto> Distribucion { get; set; } = new();
    
    public string Usuario { get; set; } = string.Empty;
}

// POSTEO MISCELÁNEOS
public class ReciboMiscelaneoDetalleDto
{
    public int Linea { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string TipoServicio { get; set; } = string.Empty;
    public decimal MontoValor { get; set; }
    public decimal MontoValorSaldo { get; set; }
}

public class PagoMiscelaneoCrearDto
{
    [Required]
    public long Recibo { get; set; }
    
    [Required]
    public string Banco { get; set; } = string.Empty;
    
    public string Usuario { get; set; } = string.Empty;
}

public class ReversoMiscelaneoRequestDto
{
    [Required]
    public long Recibo { get; set; }
    
    public string Usuario { get; set; } = string.Empty;
}

// AUTOCOMPLETADO
public class BusquedaFacturaDto
{
    public string NumFactura { get; set; } = string.Empty;
    public string ClienteClave { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
}

// COMBOS
public class BancoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public class PeriodoActualDto
{
    public string Periodo { get; set; } = string.Empty;
    public string Anio { get; set; } = string.Empty;
    public string Mes { get; set; } = string.Empty;
}
```

---

### **Fase 3: Servicios de Dominio (Backend)** 🔧

**Objetivo**: Implementar todos los métodos faltantes en el servicio.

#### **Archivo a modificar**: `SIAD.Services/CaptacionPagos/ICaptacionPagosService.cs`

**Agregar**:
```csharp
public interface ICaptacionPagosService
{
    // EXISTENTE
    Task<IReadOnlyList<CajaDto>> ListarCatalogoCajasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ArqueoDto>> ListarArqueosAsync(CaptacionArqueoFilterDto? filtro, CancellationToken ct = default);
    Task<CaptacionHeaderDto?> ObtenerDetallePagoAsync(string numFactura, CancellationToken ct = default);
    Task<CaptacionPagoResponseDto?> ObtenerPagoAsync(string numFactura, CancellationToken ct = default);
    Task<IReadOnlyList<CaptacionDetailDto>> ObtenerDetallePagoLineasAsync(string numFactura, CancellationToken ct = default);
    Task<ResponseModelDto> RegistrarPagoAsync(PagoCrearDto dto, CancellationToken ct = default);
    Task<ResponseModelDto> ReversarPagoAsync(ReversoRequestDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ReciboMiscelaneoDto>> ListarPagosMiscelaneosAsync(string? clienteClave, CancellationToken ct = default);
    
    // NUEVOS - AUTOCOMPLETADO
    Task<IReadOnlyList<BusquedaFacturaDto>> BuscarFacturasAsync(string term, CancellationToken ct = default);
    Task<bool> ExisteRegistroPagoAsync(string numFactura, CancellationToken ct = default);
    
    // NUEVOS - POSTEO MANUAL
    Task<IReadOnlyList<SaldoPosteoManualDto>> ObtenerSaldosPosteoManualAsync(string clienteClave, CancellationToken ct = default);
    Task<ResponseModelDto> RegistrarPagoManualAsync(PagoManualCrearDto dto, CancellationToken ct = default);
    Task<ResponseModelDto> ReversarPagoManualAsync(ReversoManualRequestDto dto, CancellationToken ct = default);
    
    // NUEVOS - POSTEO MISCELÁNEOS
    Task<IReadOnlyList<ReciboMiscelaneoDetalleDto>> ObtenerDetalleReciboMiscelaneoAsync(long recibo, CancellationToken ct = default);
    Task<ResponseModelDto> RegistrarPagoMiscelaneoAsync(PagoMiscelaneoCrearDto dto, CancellationToken ct = default);
    Task<ResponseModelDto> ReversarPagoMiscelaneoAsync(ReversoMiscelaneoRequestDto dto, CancellationToken ct = default);
    
    // NUEVOS - COMBOS
    Task<IReadOnlyList<BancoDto>> ListarBancosAsync(CancellationToken ct = default);
    Task<PeriodoActualDto> ObtenerPeriodoActualAsync(CancellationToken ct = default);
}
```

#### **Archivo a modificar**: `SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`

**Implementar todos los métodos** usando:
- EF Core para consultas básicas
- `FromSqlRaw` para llamar a los stored procedures creados en Fase 1
- Transacciones explícitas para operaciones complejas

---

### **Fase 4: API Controllers (Backend)** 🌐

**Objetivo**: Exponer todos los endpoints faltantes.

#### **Archivo a modificar**: `apc/Controllers/CaptacionPagosController.cs`

**Agregar**:
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CaptacionPagosController : ControllerBase
{
    // EXISTENTE
    [HttpGet("cajas")] ...
    [HttpGet("arqueos")] ...
    [HttpGet("miscelaneos")] ...
    [HttpGet("{numFactura}")] ...
    [HttpPost] ...
    [HttpPost("reverso")] ...
    
    // NUEVOS - AUTOCOMPLETADO
    [HttpGet("search/{term}")]
    public async Task<IActionResult> SearchFacturas(string term, CancellationToken ct)
    {
        var resultados = await _service.BuscarFacturasAsync(term, ct);
        return Ok(resultados);
    }
    
    [HttpGet("{numFactura}/existe")]
    public async Task<IActionResult> ExisteRegistroPago(string numFactura, CancellationToken ct)
    {
        var existe = await _service.ExisteRegistroPagoAsync(numFactura, ct);
        return Ok(new { Existe = existe });
    }
    
    // NUEVOS - POSTEO MANUAL
    [HttpGet("saldos-manual/{clienteClave}")]
    public async Task<IActionResult> GetSaldosPosteoManual(string clienteClave, CancellationToken ct)
    {
        var saldos = await _service.ObtenerSaldosPosteoManualAsync(clienteClave, ct);
        return Ok(saldos);
    }
    
    [HttpPost("posteo-manual")]
    public async Task<IActionResult> RegistrarPagoManual([FromBody] PagoManualCrearDto dto, CancellationToken ct)
    {
        var respuesta = await _service.RegistrarPagoManualAsync(dto, ct);
        return respuesta.Success ? Ok(respuesta) : BadRequest(respuesta);
    }
    
    [HttpPost("posteo-manual/reverso")]
    public async Task<IActionResult> ReversarPagoManual([FromBody] ReversoManualRequestDto dto, CancellationToken ct)
    {
        var respuesta = await _service.ReversarPagoManualAsync(dto, ct);
        return respuesta.Success ? Ok(respuesta) : BadRequest(respuesta);
    }
    
    // NUEVOS - POSTEO MISCELÁNEOS
    [HttpGet("miscelaneos/{recibo}/detalle")]
    public async Task<IActionResult> GetDetalleReciboMiscelaneo(long recibo, CancellationToken ct)
    {
        var detalle = await _service.ObtenerDetalleReciboMiscelaneoAsync(recibo, ct);
        return Ok(detalle);
    }
    
    [HttpPost("miscelaneos/registrar")]
    public async Task<IActionResult> RegistrarPagoMiscelaneo([FromBody] PagoMiscelaneoCrearDto dto, CancellationToken ct)
    {
        var respuesta = await _service.RegistrarPagoMiscelaneoAsync(dto, ct);
        return respuesta.Success ? Ok(respuesta) : BadRequest(respuesta);
    }
    
    [HttpPost("miscelaneos/reverso")]
    public async Task<IActionResult> ReversarPagoMiscelaneo([FromBody] ReversoMiscelaneoRequestDto dto, CancellationToken ct)
    {
        var respuesta = await _service.ReversarPagoMiscelaneoAsync(dto, ct);
        return respuesta.Success ? Ok(respuesta) : BadRequest(respuesta);
    }
    
    // NUEVOS - COMBOS
    [HttpGet("bancos")]
    public async Task<IActionResult> GetBancos(CancellationToken ct)
    {
        var bancos = await _service.ListarBancosAsync(ct);
        return Ok(bancos);
    }
    
    [HttpGet("periodo-actual")]
    public async Task<IActionResult> GetPeriodoActual(CancellationToken ct)
    {
        var periodo = await _service.ObtenerPeriodoActualAsync(ct);
        return Ok(periodo);
    }
}
```

---

### **Fase 5: HTTP Client (Frontend)** 📡

**Objetivo**: Crear client con todos los métodos necesarios.

#### **Archivo a modificar**: `apc.Client/Services/CaptacionPagos/CaptacionPagosClient.cs`

**Ampliar** con:
```csharp
public sealed class CaptacionPagosClient
{
    private readonly HttpClient _http;
    
    public CaptacionPagosClient(HttpClient http) => _http = http;
    
    // EXISTENTE
    public async Task<IReadOnlyList<CajaDto>> GetCajasAsync(CancellationToken ct = default) { ... }
    public async Task<IReadOnlyList<ArqueoDto>> GetArqueosAsync(CaptacionArqueoFilterDto filtro, CancellationToken ct = default) { ... }
    // ...
    
    // NUEVOS - AUTOCOMPLETADO
    public async Task<IReadOnlyList<BusquedaFacturaDto>> SearchFacturasAsync(string term, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/captacionpagos/search/{Uri.EscapeDataString(term)}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<BusquedaFacturaDto>>(cancellationToken: ct)
            ?? new List<BusquedaFacturaDto>();
    }
    
    public async Task<bool> ExisteRegistroPagoAsync(string numFactura, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/captacionpagos/{Uri.EscapeDataString(numFactura)}/existe", ct);
        response.EnsureSuccessStatusCode();
        var resultado = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken: ct);
        return resultado?.Existe ?? false;
    }
    
    // NUEVOS - POSTEO MANUAL
    public async Task<IReadOnlyList<SaldoPosteoManualDto>> GetSaldosPosteoManualAsync(string clienteClave, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/captacionpagos/saldos-manual/{Uri.EscapeDataString(clienteClave)}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SaldoPosteoManualDto>>(cancellationToken: ct)
            ?? new List<SaldoPosteoManualDto>();
    }
    
    public async Task<ResponseModelDto> RegistrarPagoManualAsync(PagoManualCrearDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/posteo-manual", dto, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct)
            ?? ResponseModelDto.Fail("No se pudo registrar el pago manual.");
    }
    
    public async Task<ResponseModelDto> ReversarPagoManualAsync(ReversoManualRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/posteo-manual/reverso", dto, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct)
            ?? ResponseModelDto.Fail("No se pudo reversar el pago manual.");
    }
    
    // NUEVOS - POSTEO MISCELÁNEOS
    public async Task<IReadOnlyList<ReciboMiscelaneoDetalleDto>> GetDetalleReciboMiscelaneoAsync(long recibo, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/captacionpagos/miscelaneos/{recibo}/detalle", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ReciboMiscelaneoDetalleDto>>(cancellationToken: ct)
            ?? new List<ReciboMiscelaneoDetalleDto>();
    }
    
    public async Task<ResponseModelDto> RegistrarPagoMiscelaneoAsync(PagoMiscelaneoCrearDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/miscelaneos/registrar", dto, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct)
            ?? ResponseModelDto.Fail("No se pudo registrar el pago misceláneo.");
    }
    
    public async Task<ResponseModelDto> ReversarPagoMiscelaneoAsync(ReversoMiscelaneoRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/miscelaneos/reverso", dto, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct)
            ?? ResponseModelDto.Fail("No se pudo reversar el pago misceláneo.");
    }
    
    // NUEVOS - COMBOS
    public async Task<IReadOnlyList<BancoDto>> GetBancosAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/captacionpagos/bancos", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<BancoDto>>(cancellationToken: ct)
            ?? new List<BancoDto>();
    }
    
    public async Task<PeriodoActualDto> GetPeriodoActualAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/captacionpagos/periodo-actual", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PeriodoActualDto>(cancellationToken: ct)
            ?? new PeriodoActualDto { Periodo = "N/A", Anio = "0000", Mes = "00" };
    }
}
```

---

### **Fase 6: Interfaz de Usuario (Frontend)** 🎨

**Objetivo**: Crear las 3 páginas principales con tabs.

#### **Archivos a crear**:

1. **`apc.Client/Pages/Facturacion/CaptacionPagos/Index.razor`** (página principal con tabs)
2. **`apc.Client/Pages/Facturacion/CaptacionPagos/PosteoLectoras.razor`**
3. **`apc.Client/Pages/Facturacion/CaptacionPagos/PosteoManual.razor`**
4. **`apc.Client/Pages/Facturacion/CaptacionPagos/PosteoMiscelaneos.razor`**
5. **`apc.Client/Pages/Facturacion/CaptacionPagos/Components/BuscadorFacturas.razor`**
6. **`apc.Client/Pages/Facturacion/CaptacionPagos/Components/DetalleFactura.razor`**
7. **`apc.Client/Pages/Facturacion/CaptacionPagos/Components/SaldosManual.razor`**
8. **`apc.Client/Pages/Facturacion/CaptacionPagos/Components/SelectorMiscelaneos.razor`**

**Estructura de `Index.razor`**:
```razor
@page "/facturacion/captacion"
@inject CaptacionPagosClient CaptacionClient
@inject IToastNotificationService ToastService

<div class="page-container">
    <h2 class="page-title">Captación de Pagos</h2>
    <p class="page-subtitle">Registro y control de pagos de clientes</p>
    
    @if (periodoActual is not null)
    {
        <div class="alert alert-info">
            <strong>📅 Período Actual:</strong> @periodoActual.Mes/@periodoActual.Anio
        </div>
    }
    
    <DxTabs @bind-ActiveTabIndex="@activeTabIndex">
        <DxTabPage Text="Posteo Lectoras">
            <ContentTemplate>
                @if (activeTabIndex == 0)
                {
                    <PosteoLectoras />
                }
            </ContentTemplate>
        </DxTabPage>
        
        <DxTabPage Text="Posteo Manual">
            <ContentTemplate>
                @if (activeTabIndex == 1)
                {
                    <PosteoManual />
                }
            </ContentTemplate>
        </DxTabPage>
        
        <DxTabPage Text="Posteo Misceláneos">
            <ContentTemplate>
                @if (activeTabIndex == 2)
                {
                    <PosteoMiscelaneos />
                }
            </ContentTemplate>
        </DxTabPage>
    </DxTabs>
</div>

@code {
    private int activeTabIndex = 0;
    private PeriodoActualDto? periodoActual;
    
    protected override async Task OnInitializedAsync()
    {
        periodoActual = await CaptacionClient.GetPeriodoActualAsync();
    }
}
```

---

### **Fase 7: Testing y Validación** ✅

**Objetivo**: Validar TODAS las funcionalidades contra el sistema legado.

#### **Checklist de pruebas**:

**Posteo Lectoras**:
- [ ] Buscar factura/recibo con autocompletado (enter)
- [ ] Mostrar header de pago con datos del cliente
- [ ] Verificar si existe captación previa
- [ ] Mostrar detalle de tarifa en grid
- [ ] Sumar total de factura automáticamente
- [ ] Registrar pago con tipo transacción 201
- [ ] Registrar partida 002
- [ ] Actualizar saldo con stored procedure
- [ ] Crear TransaccionAbonado
- [ ] Actualizar factura a estado C
- [ ] Reversar pago con stored procedure
- [ ] Borrar TransaccionAbonado en reverso
- [ ] Actualizar factura a estado A en reverso
- [ ] Cargar combo de clientes
- [ ] Cargar combo de bancos
- [ ] Mostrar período actual

**Posteo Manual**:
- [ ] Seleccionar cliente
- [ ] Obtener saldos pendientes con stored procedure
- [ ] Mostrar grid de saldos distribuidos
- [ ] Validar total distribuido = valor
- [ ] Registrar pago manual con distribución
- [ ] Crear TransaccionAbonado (tipo 201, partida 002)
- [ ] Reversar pago manual con stored procedure
- [ ] Mostrar labels de totales

**Posteo Misceláneos**:
- [ ] Listar recibos misceláneos por cliente
- [ ] Mostrar modal de selección
- [ ] Obtener detalle del recibo
- [ ] Mostrar grid de detalle
- [ ] Registrar pago misceláneo
- [ ] Actualizar factura a estado C
- [ ] Setear recolectora/fecha pago
- [ ] Crear TransaccionAbonado (tipo 201)
- [ ] Reversar pago misceláneo
- [ ] Devolver factura a estado A
- [ ] Mostrar label de total

---

## 📈 ESTIMACIÓN DE ESFUERZO

| **Fase** | **Descripción** | **Horas Estimadas** | **Complejidad** |
|---|---|---|---|
| **Fase 1** | Stored Procedures | **16 horas** | 🔴 ALTA |
| **Fase 2** | DTOs y Contratos | **4 horas** | 🟡 MEDIA |
| **Fase 3** | Servicios de Dominio | **24 horas** | 🔴 ALTA |
| **Fase 4** | API Controllers | **8 horas** | 🟡 MEDIA |
| **Fase 5** | HTTP Client | **6 horas** | 🟢 BAJA |
| **Fase 6** | Interfaz de Usuario | **32 horas** | 🔴 ALTA |
| **Fase 7** | Testing y Validación | **16 horas** | 🟡 MEDIA |
| **TOTAL** | **106 horas** | **~3 semanas** | 🔴 **ALTA** |

---

## 🚨 RIESGOS IDENTIFICADOS

1. **Stored Procedures críticos perdidos**: La lógica de negocio que estaba en los SPs del legado se perdió. **DEBE** ser recreada exactamente igual.

2. **Inconsistencias de saldos**: Si se usa el código actual sin los SPs, los saldos de clientes pueden quedar incorrectos.

3. **TransaccionAbonado faltante**: El código actual NO registra movimientos contables en `transaccion_abonado`, lo cual es crítico para auditoría.

4. **Tipo transacción 201 y partida 002**: Estos códigos contables NO se están registrando, lo que puede causar problemas en reportes financieros.

5. **Ciclo/Ruta/Secuencia del cliente**: Esta información NO se está capturando en el pago, lo cual puede afectar reportes operativos.

---

## 📌 RECOMENDACIONES FINALES

1. **PRIORIDAD MÁXIMA**: Crear los stored procedures ANTES de continuar con el código. Son la base de todo el módulo.

2. **Revisar el código legado línea por línea**: Asegurarse de que TODA la lógica fue migrada, no solo la estructura.

3. **Validar con usuario final**: El sistema actual NO cumple con el flujo operativo del sistema legado. Debe ser revisado por quien lo usa.

4. **Documentar diferencias**: Si se decide NO migrar algo, debe quedar documentado explícitamente por qué.

5. **Rollback plan**: Tener un plan para revertir cambios si los saldos quedan inconsistentes.

---

## ✅ CRITERIOS DE ACEPTACIÓN

El módulo se considerará **COMPLETAMENTE MIGRADO** cuando:

1. ✅ Todos los stored procedures estén creados y probados
2. ✅ Los 3 tabs (Posteo Lectoras, Manual, Misceláneos) funcionen igual que en el legado
3. ✅ El autocompletado de factura/recibo funcione con ENTER
4. ✅ Todos los registros de pago generen `TransaccionAbonado` con tipo 201
5. ✅ Todos los reversos actualicen correctamente los saldos
6. ✅ Los combos de clientes, bancos y período actual funcionen
7. ✅ Las validaciones JS del legado estén implementadas
8. ✅ Los grids muestren los mismos datos que en el legado
9. ✅ Los labels de totales se calculen automáticamente
10. ✅ Un usuario pueda usar el sistema sin notar diferencias con el legado

---

**Última actualización**: 16 de enero de 2026  
**Autor**: GitHub Copilot (Claude Sonnet 4.5)  
**Estado**: ⚠️ **GAPS CRÍTICOS IDENTIFICADOS** - Requiere acción inmediata
