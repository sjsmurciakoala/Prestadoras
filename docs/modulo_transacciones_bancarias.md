# Implementación de Transacciones Bancarias

## Descripción General
Se ha implementado un módulo completo para gestionar transacciones bancarias en el sistema SIAD. Este módulo permite registrar, consultar y seguir el movimiento de dinero en cuentas bancarias con un enfoque en la integridad de datos y la auditoria.

## Estructura de la Implementación

### 1. **DTOs (Data Transfer Objects)** - Capa de Dominio
Ubicación: `SIAD.Core/DTOs/Bancos/`

#### `BanTransaccionCreateDto.cs`
- **Propósito**: Recibir datos para crear una nueva transacción bancaria
- **Propiedades clave**:
  - `BancoCuentaId`: ID de la cuenta bancaria
  - `IdTipoTransaccion`: Código del tipo de movimiento (DEP, RET, TRF, etc.)
  - `FechaMovimiento`: Fecha del movimiento
  - `Descripcion`: Descripción de la transacción
  - `Referencia`: Referencia opcional (número de comprobante, etc.)
  - `Monto`: Monto de la transacción (siempre positivo, el tipo de transacción define si es entrada o salida)
- **Validaciones**: Anotaciones de data annotation para validación en cliente y servidor

#### `BanTransaccionListDto.cs`
- **Propósito**: Mostrar datos de transacciones en grillas y listados
- **Propiedades adicionales**:
  - `BanKardexId`: ID del registro en el kardex
  - `CuentaNombre`, `NumeroCuenta`: Información de la cuenta
  - `SaldoResultante`: Saldo después de la transacción
  - `CreadoPor`, `CreadoEn`: Información de auditoría

#### `BanTransaccionFilterDto.cs`
- **Propósito**: Filtros para búsquedas avanzadas
- Permite filtrar por cuenta, rango de fechas y tipo de transacción

### 2. **Servicio de Dominio** - Capa de Lógica de Negocio
Ubicación: `SIAD.Services/Bancos/`

#### `IBanTransaccionesService.cs` (Interfaz)
Define el contrato de operaciones:
```csharp
public interface IBanTransaccionesService
{
    Task<IReadOnlyList<BanTransaccionListDto>> GetTransaccionesAsync(...);
    Task<BanTransaccionListDto?> GetTransaccionByIdAsync(...);
    Task<(long BanKardexId, decimal SaldoResultante)> RegistrarMovimientoAsync(...);
}
```

#### `BanTransaccionesService.cs` (Implementación)
**Métodos principales**:

1. **GetTransaccionesAsync**
   - Recupera transacciones con filtros opcionales
   - Ordenamiento: descendente por fecha y ID
   - Incluye información de la cuenta asociada
   - Retorna `IReadOnlyList<BanTransaccionListDto>`

2. **GetTransaccionByIdAsync**
   - Obtiene una transacción específica
   - Validación de compañía y ID
   - Retorna null si no existe

3. **RegistrarMovimientoAsync**
   - **Lógica principal**:
     1. Valida existencia de la cuenta bancaria
     2. Obtiene el último saldo del kardex o saldo inicial de la cuenta
     3. Calcula nuevo saldo: `saldoAnterior + monto`
     4. Crea registro en `ban_kardex`
     5. Actualiza saldo actual en `ban_cuenta`
     6. Guarda cambios en BD
   - **Manejo de errores**: Lanza excepciones específicas
   - **Retorna**: Tupla con (BanKardexId, SaldoResultante)

**Características de seguridad**:
- Validación de argumentos nulos
- Filtrado por company_id (multi-tenencia)
- Auditoria automática (created_at, created_by, updated_at, updated_by)

### 3. **Controlador REST** - Capa de Presentación (Backend)
Ubicación: `apc/Controllers/Bancos/BanTransaccionesController.cs`

**Endpoints**:

```
GET /api/bancos/transacciones
Query params: companyId, bancoCuentaId?, fechaDesde?, fechaHasta?
Retorna: List<BanTransaccionListDto>

GET /api/bancos/transacciones/{banKardexId}
Query params: companyId
Retorna: BanTransaccionListDto | 404

POST /api/bancos/transacciones
Body: BanTransaccionCreateDto
Query params: companyId
Retorna: (long BanKardexId, decimal SaldoResultante)
```

**Seguridad**:
- Autorización: Policy = `AuthorizationPolicies.Bancos`
- Validación de ModelState
- Extracción de usuario desde claims: `User.FindFirst(ClaimTypes.NameIdentifier)`
- Manejo comprehensive de excepciones con Problem Details (RFC 7807)

**Respuestas HTTP**:
- `200 OK`: Éxito
- `400 Bad Request`: Validación fallida
- `404 Not Found`: Recurso no encontrado
- `500 Internal Server Error`: Error del servidor

### 4. **Cliente HTTP** - Capa de Cliente (WASM)
Ubicación: `apc.Client/Services/Bancos/BanTransaccionesClient.cs`

```csharp
public sealed class BanTransaccionesClient
{
    public async Task<IReadOnlyList<BanTransaccionListDto>> GetTransaccionesAsync(...);
    public async Task<BanTransaccionListDto?> GetByIdAsync(...);
    public async Task<(long, decimal)> RegistrarMovimientoAsync(BanTransaccionCreateDto);
}
```

**Características**:
- Métodos async con CancellationToken opcional
- Extensión `ReadFromJsonAsyncWithAuthCheck<T>` para manejo de autenticación
- Manejo de errores HTTP con extracción de mensajes
- Validación de argumentos antes de hacer requests

**Registro en DI**:
```csharp
// apc.Client/CommonServices.cs
services.AddScoped<BanTransaccionesClient>();
```

### 5. **Interfaz de Usuario (Blazor)** - Capa de Presentación (WASM)
Ubicación: `apc.Client/Pages/Contabilidad/TransaccionesBancarias.razor`

#### **Componente Principal: TransaccionesBancarias.razor**

**Estructura**:
1. **Header**: Título y descripción
2. **Alertas**: Sistema de notificaciones (éxito/error)
3. **Toolbar**: Botones de acción (Nueva Transacción, Refrescar)
4. **Filtros**: 
   - ComboBox de cuentas bancarias
   - DateEdit para rango de fechas
   - Aplicación de filtros en tiempo real
5. **Grilla (Grid)**:
   - Muestra transacciones con paginación (15 registros por página)
   - Columnas: Cuenta, Número, Fecha, Tipo, Descripción, Monto, Saldo, Fecha Registro, Creado Por
   - Selección de fila única
   - Comando para ver detalles

**Funcionalidades**:
- **OnInitializedAsync**: Carga cuentas bancarias y transacciones
- **RefreshAsync**: Recarga datos desde servidor
- **AbrirFormularioAsync**: Muestra modal para nueva transacción
- **GuardarTransaccionAsync**: Envía datos al servidor y actualiza grilla
- **VerDetallesAsync**: Abre modal de detalles de transacción
- **AplicarFiltros**: Filtra en memoria (lado cliente)

**Estados**:
- `isLoading`: Indica si se están cargando datos
- `transacciones`: Lista completa de transacciones
- `transaccionesFiltradas`: Lista filtrada mostrada en grilla

#### **Modal: TransaccionBancariaModal.razor**

**Propósito**: Formulario para crear nuevas transacciones

**Campos**:
1. **Cuenta Bancaria**: ComboBox con cuentas disponibles
   - Muestra nombre, número y moneda
   - Validación requerida
2. **Tipo de Transacción**: TextInput (3 caracteres max)
   - Ejemplos: DEP (Depósito), RET (Retiro), TRF (Transferencia)
3. **Fecha**: DatePicker
   - Valor por defecto: hoy
4. **Descripción**: TextArea (500 caracteres max)
5. **Referencia**: TextInput opcional (100 caracteres max)
6. **Monto**: NumberInput (mínimo 0.01)

**Flujo de Guardado**:
1. Validación de formulario (DataAnnotationsValidator)
2. Conversión de DateTime a DateOnly
3. Invocación de callback `OnTransaccionGuardada`
4. Cierre automático del modal en éxito
5. Visualización de mensajes de error

#### **Modal: DetalleTransaccionBancariaModal.razor**

**Propósito**: Mostrar información completa de una transacción

**Información mostrada** (en modo lectura):
- ID de transacción y estado
- Información de cuenta (nombre, número)
- Fecha de movimiento y tipo
- Descripción y referencia
- Monto (coloreado: verde para entrada, rojo para salida)
- Saldo resultante
- Información de auditoría (creado por/en, actualizado por/en)

## Flujo de Datos

```
Usuario (WASM)
    ↓
TransaccionBancariaModal (captura datos)
    ↓
BanTransaccionesClient (HTTP POST)
    ↓
BanTransaccionesController (validación, autorización)
    ↓
IBanTransaccionesService.RegistrarMovimientoAsync()
    ├─ Valida cuenta bancaria
    ├─ Obtiene último saldo
    ├─ Crea registro ban_kardex
    ├─ Actualiza ban_cuenta.saldo_actual
    └─ SaveChangesAsync()
    ↓
(200 OK con BanKardexId, SaldoResultante)
    ↓
TransaccionesBancarias.razor (actualiza grilla)
```

## Base de Datos

### Entidades Utilizadas

#### `ban_kardex`
- **Propósito**: Registro de todos los movimientos de cada cuenta bancaria
- **Campos principales**:
  - `ban_kardex_id`: PK (auto-incremental)
  - `company_id`: FK (multi-tenencia)
  - `banco_cuenta_id`: FK a cuenta bancaria
  - `fecha_movimiento`: Fecha del movimiento
  - `fecha_registro`: Timestamp de registro
  - `tipo_movimiento`: Código de tipo (DEP, RET, etc.)
  - `descripcion`, `referencia`: Información adicional
  - `monto_debito`, `monto_credito`: Montos (siempre positivos)
  - `saldo`: Saldo resultante después del movimiento
  - Campos de auditoría: `created_at`, `created_by`, `updated_at`, `updated_by`

#### `ban_cuenta`
- **Propósito**: Información de cuentas bancarias
- **Campos actualizados**:
  - `saldo_actual`: Se actualiza con cada movimiento
  - `updated_at`, `updated_by`: Auditoría

### Índices
```sql
CREATE INDEX ix_ban_kardex_cuenta_fecha 
  ON ban_kardex(company_id, banco_cuenta_id, fecha_movimiento)
```

## Seguridad y Validaciones

### 1. **Autorización**
- Policy: `AuthorizationPolicies.Bancos`
- Solo usuarios con permiso pueden acceder

### 2. **Validación de Datos**
- **Lado Cliente**: DataAnnotationsValidator en formularios
- **Lado Servidor**: 
  - ModelState.IsValid
  - Validación de argumentos (null, ranges)
  - Validación de negocio (cuenta existe, monto válido)

### 3. **Auditoria**
- Todos los registros incluyen:
  - `created_at`, `created_by`: Generado al crear
  - `updated_at`, `updated_by`: Generado al actualizar
- El usuario se extrae de `User.Identity.Name` o claims

### 4. **Multi-tenencia**
- Todos los queries filtran por `company_id`
- Los usuarios solo ven datos de su compañía

### 5. **Manejo de Errores**
- Try-catch en todos los endpoints
- Problem Details responses (RFC 7807)
- Mensajes descriptivos en español
- Logging implícito (logs de excepción)

## Ejemplo de Uso

### Crear una Transacción (Cliente)

```csharp
var dto = new BanTransaccionCreateDto
{
    BancoCuentaId = 1,
    IdTipoTransaccion = "DEP",
    FechaMovimiento = DateOnly.Today,
    Descripcion = "Depósito cliente XYZ",
    Referencia = "CHQ-12345",
    Monto = 5000.00m
};

var resultado = await transaccionesClient.RegistrarMovimientoAsync(dto);
Console.WriteLine($"Transacción: {resultado.BanKardexId}, Nuevo Saldo: {resultado.SaldoResultante}");
```

### Consultar Transacciones (Cliente)

```csharp
var transacciones = await transaccionesClient.GetTransaccionesAsync(
    companyId: 1,
    bancoCuentaId: 5,
    fechaDesde: new DateOnly(2025, 01, 01),
    fechaHasta: new DateOnly(2025, 12, 31)
);

foreach (var t in transacciones)
{
    Console.WriteLine($"{t.FechaMovimiento} - {t.Descripcion}: {t.Monto:N2} → Saldo: {t.SaldoResultante:N2}");
}
```

## Procedimiento Almacenado Existente

El proyecto ya tiene el procedimiento `public.sp_ban_kardex_registrar_movimiento` que realiza la misma lógica. La implementación actual usa EF Core directamente para mantener consistencia con la arquitectura del proyecto.

Si en el futuro se requiere usar el SP, se puede crear una variante del servicio que lo invoque via `context.Database.ExecuteSqlRawAsync()`.

## Próximas Mejoras (Backlog)

1. **Conciliación Bancaria**: 
   - Marcar transacciones como conciliadas
   - Reportes de diferencias

2. **Importación Masiva**:
   - Cargar múltiples transacciones via CSV/Excel

3. **Reglas de Negocio**:
   - Restricciones de moneda por cuenta
   - Límites de transacción
   - Aprobaciones para montos grandes

4. **Reportería Avanzada**:
   - Reporte de movimientos diarios
   - Análisis de flujo de efectivo
   - Proyecciones de saldo

5. **Integración con Contabilidad**:
   - Crear pólizas automáticas desde movimientos
   - Sincronización de saldos

## Conclusión

El módulo de Transacciones Bancarias sigue los patrones arquitéctonicos del proyecto SIAD, implementando una solución robusta, segura y escalable para la gestión de movimientos bancarios con énfasis en la integridad de datos y auditoría completa.
