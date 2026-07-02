# Diseño — Checkbox para generar partida en Nuevo compromiso de proveedor

Fecha: 2026-06-30
Estado: aprobado (pendiente de plan de implementación)

## Problema / objetivo

Hoy, al crear un nuevo compromiso de proveedor (`CreateAsync`), la partida contable
de creación (GEN) se genera **siempre**, automáticamente, dentro de la misma
transacción. Se requiere que al momento de **Nuevo compromiso de proveedor** un
checkbox decida si se genera o no la partida.

## Decisiones tomadas

- **Rol del checkbox:** decide si se genera la partida.
  - Marcado → compromiso + partida GEN (comportamiento actual).
  - Desmarcado → compromiso **sin** partida GEN, para generarla después.
- **Estado inicial:** marcado por defecto (preserva el comportamiento actual).
- **Generar después:** ambas vías —
  1. botón manual en la pantalla de edición del compromiso, y
  2. automáticamente al **procesar**, si la GEN aún no existe.
- **Sin cuenta de gasto al diferir:** cuando el checkbox está desmarcado se permite
  crear el compromiso aunque las líneas no tengan cuenta de gasto; las cuentas se
  asignan luego, antes de generar la partida.

## Contexto técnico relevante

- Existen dos partidas por compromiso, ligadas por el documento `OPD-{numeroOrden}`:
  - **GEN** — generada al crear (`CreateAsync` → `BuildCreatePartidaLineasAsync` →
    `RegisterPartidaContableAsync`, sufijo `-GEN`).
  - **PRC** — generada al procesar (`MarkAsProcessedAsync`, sufijo `-PRC`).
- `LoadPartidaContableAsync` detecta **cualquier** partida `OPD-{n}` (GEN, PRC o ANU),
  por lo que para "¿ya existe la GEN?" hace falta un check dirigido por
  `poliza_number = OPD-{n}-GEN`.
- La afectación presupuestaria (`ApplyCompromisoPresupuestoAsync`) es independiente
  de la partida y debe seguir ejecutándose siempre al crear.

## Diseño por capa

### DTO
- `OrdenPagoDirectoUpsertDto` (`SIAD.Core/DTOs/Presupuesto/OrdenesPagoDirectoDtos.cs`):
  agregar `public bool GenerarPartida { get; set; } = true;`
  (default `true` para no romper llamadores existentes).

### Servicio (`SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs`)
- **`CreateAsync`**: envolver el bloque de generación de la GEN
  (`BuildCreatePartidaLineasAsync` + `RegisterPartidaContableAsync`) en
  `if (dto.GenerarPartida)`. El resto (presupuesto, INSERT hdr/dtl) sin cambios.
- **`GenerarPartidaCreacionAsync(numeroOrden)`** (nuevo): carga el compromiso,
  valida que no esté procesada y que la GEN **no exista**, arma las líneas con la
  misma regla y registra la GEN. Devuelve `OrdenPagoDirectoOperacionResultadoDto`.
- **`MarkAsProcessedAsync`**: dentro de su `dbTransaction`, antes de registrar la
  PRC, si la GEN no existe la genera con la misma regla.
- **Helper `GenPartidaExistsAsync(numeroOrden)`**: existencia dirigida por
  `poliza_number = OPD-{n}-GEN`.
- Reflejar la nueva firma en `IOrdenesPagoDirectoService`.

### Controller (`apc/Controllers/Presupuesto/OrdenesPagoDirectoController.cs`)
- Nuevo `POST {numeroOrden:int}/generar-partida` que delega en
  `GenerarPartidaCreacionAsync`, con el mismo manejo de excepciones
  (`ArgumentException` → 400, `InvalidOperationException` → 409).

### Cliente (`apc.Client/Services/Presupuesto/OrdenesPagoDirectoClient.cs`)
- Nuevo `GenerarPartidaAsync(numeroOrden)` (POST al nuevo endpoint).
- `CrearAsync` ya envía el DTO completo; el flag viaja sin cambios.

### UI (`apc.Client/Pages/Proveedores/CompromisoProveedorEdit.razor`)
- **Checkbox** `DxCheckBox` enlazado a `model.GenerarPartida`, visible solo en
  creación (`!IsEdit`), en la tarjeta de "Vista previa de partida contable".
- **Validación** (`ValidarDetalle`): las reglas de partida
  (`PuedeGenerarPartidaContable()` y "elegir cuenta de servicio o de gasto") solo se
  exigen al crear **si el checkbox está marcado**. Desmarcado → se permite crear sin
  cuentas.
- **Botón "Generar partida contable"**: visible cuando
  `IsEdit && !ordenProcesada && !TienePartidaContableRegistrada`; habilitado cuando
  la vista previa cuadra (`PuedeGenerarPartidaContable()`). Llama al cliente, muestra
  toast (o `ErrorNotice` ante fallo) y recarga el detalle.

## Pruebas (SIAD.Tests)

- Crear con `GenerarPartida=false` → no existe partida GEN.
- Crear con `GenerarPartida=true` → existe GEN (comportamiento actual).
- `generar-partida` sobre compromiso sin GEN → la crea; segunda llamada → rechazada
  (ya existe).
- Procesar compromiso sin GEN → genera GEN + PRC.

## Fuera de alcance

- Cambios al formato/contenido contable de las partidas GEN/PRC.
- Anulación (ANU) y su relación con la GEN diferida (se mantiene el flujo actual).
