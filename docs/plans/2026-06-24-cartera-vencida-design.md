# Diseño — Vista "Cartera vencida" (Cobranza)

Fecha: 2026-06-24
Rama: `Modulo_Caja_1.0`
Estado: **Aprobado** (Emilio, 2026-06-24). Pendiente: plan de implementación.

## 1. Objetivo

Nueva vista en **Facturación → Cobranza** que lista los clientes con **facturas
vencidas**, con análisis de antigüedad por tramos y acciones de cobranza masivas.

Filtros pedidos: fecha, cliente, clave y cantidad de días de vencimiento
(30 / 60 / 120 / más de 120 días).

## 2. Decisiones (cerradas con el usuario)

- **Granularidad:** por cliente (aging). Una fila por cliente con columnas de
  tramo (0–30 / 31–60 / 61–120 / +120) y total vencido.
- **Filtro de fecha:** *fecha de corte (as-of)*. Una sola fecha; los días vencidos
  se calculan a esa fecha. Default = hoy. Permite reconstruir la cartera a una
  fecha pasada.
- **Alcance:** consulta + **acciones masivas** (selección múltiple, exportar,
  registrar acción de cobranza en lote, generar cartas) reutilizando la
  infraestructura de "Clientes para cobros".

## 3. Fuente de datos y lógica de antigüedad

Fuente: tabla **`factura`**, alineada con la lógica canónica del reporte ya existente
`public.rep_analisis_antiguedad_cobros`.

> **Corrección (verificada contra datos 2026-06-24):** el diseño inicial anclaba la edad
> a `fechavence`, pero en la BD ese campo está **siempre NULL** (y `plazo`=0 en el ledger),
> así que no hay fecha de vencimiento almacenada. La antigüedad se modela como **edad desde
> la emisión**, igual que el reporte oficial. La deuda viva real está en `transaccion_abonado`
> (saldo corrido), pero el reporte canónico usa `factura.fechaemision`, y la vista se alinea
> a eso para ser consistente.

Una factura cuenta como **abierta a la fecha de corte `D`** cuando:

- `fechaemision IS NOT NULL AND fechaemision <= D` — ya existía a esa fecha
- `COALESCE(saldototal, 0) <> 0` — sigue con saldo (así se sabe lo pendiente; `fechapago`
  también está sin poblar)
- `COALESCE(estado, 'A') <> 'N'` — no anulada (`'N'` = anulada)

**Edad (días)** = `GREATEST(D − fechaemision, 0)`. Tramos: 0–30 (≤30), 31–60, 61–120, +120.

**Gotcha de implementación:** la versión de Dapper del repo **no soporta `DateOnly`** como
parámetro; la fecha de corte se pasa como `DateTime` y se castea en SQL (`CAST(@FechaCorte AS date)`,
reutilizada vía `CROSS JOIN`). `factura.numrecibo` es identidad (`GENERATED ALWAYS`).

Tramos (SUM condicional, agrupado por cliente). Umbrales como constantes para
ajustar fácil:

| Tramo   | Regla                       |
|---------|-----------------------------|
| 0–30    | `dias BETWEEN 1 AND 30`     |
| 31–60   | `dias BETWEEN 31 AND 60`    |
| 61–120  | `dias BETWEEN 61 AND 120`   |
| +120    | `dias > 120`                |

El filtro **Tramo** restringe (vía `HAVING`) a los clientes con saldo > 0 en el
tramo elegido.

### Caveats

1. **Conciliación:** el total vencido sale de `factura`, no del `saldo` corrido del
   ledger; puede no cuadrar exactamente con "Clientes para cobros" (mora/intereses,
   NC/ND). Esta vista reporta **antigüedad de facturas**, no saldo de ledger.
   Aceptado.
2. **`factura.estado`:** confirmar el dominio contra BD (cobrada = `'C'`; ver si hay
   estado de anulada) para excluir anuladas. Paso de verificación en el plan.
3. **Rendimiento:** índice sugerido `factura(company_id, fechavence, fechapago)`.
   Va como script en `Database/` y se replica en SRV (regla mirror).

## 4. Arquitectura (slice por capa)

DTO (`SIAD.Core`) → servicio (`SIAD.Services`) → endpoint (`apc`) → cliente HTTP
(`apc.Client`) → página Razor (`apc.Client`).

### 4.1 DTOs — `SIAD.Core/DTOs/Cobranza/`

- `CarteraVencidaFiltroDto`: `FechaCorte` (DateOnly?, default hoy), `Busqueda`
  (clave o nombre), `Tramo` (enum/int? nullable = todos), `CicloId?`.
- `CarteraVencidaClienteDto`: `Clave, Nombre, CicloId, Ruta, B0_30, B31_60,
  B61_120, BMas120, TotalVencido, FacturasVencidas, DiasMaxVencido, Bloqueado,
  NoCortable, AbogadoId`.

### 4.2 Servicio — `ICobranzaService` / `CobranzaService`

- `Task<IReadOnlyList<CarteraVencidaClienteDto>> ListarCarteraVencidaAsync(
  CarteraVencidaFiltroDto filtro, CancellationToken ct = default)`.
- Implementación con **Dapper SQL crudo** (mismo patrón que
  `ListarClientesCobroAsync`): `factura f` JOIN `cliente_maestro cm` por
  `f.clientecodigo = cm.maestro_cliente_clave AND f.company_id = cm.company_id`
  para nombre/ciclo/ruta/banderas; `GROUP BY` cliente; `SUM(CASE …)` por tramo;
  `WHERE f.company_id = @CompanyId`. Filtros opcionales (búsqueda, ciclo) por
  concatenación de SQL; tramo por `HAVING`. Tenant-safe (todas las tablas filtran
  `company_id`).
- Acciones masivas: **reutilizar sin cambios** `RegistrarAccionLoteAsync` y
  `GenerarCartasCobroAsync` (ya reciben lista de claves).

### 4.3 Endpoint — `CobranzaController`

- `GET api/Cobranza/cartera-vencida` con params `fechaCorte, busqueda, tramo,
  cicloId`. Hereda `[ModuleAuthorize(PermissionModules.Ventas,
  PermissionResources.Ventas.Cobranza)]` de la clase → **sin permiso nuevo**.

### 4.4 Cliente HTTP — `apc.Client/Services/Cobranza/ClientesCobroClient.cs`

- Agregar `ListarCarteraVencidaAsync(CarteraVencidaFiltroDto, ct)` (arma query
  string con `Uri.EscapeDataString`, usa `GetFromJsonAsyncWithAuthCheck`). Reusar
  `RegistrarAccionLoteAsync` / `GenerarCartasAsync` / `GetImprimirCartasUrl`
  existentes.

### 4.5 Página — `apc.Client/Pages/Facturacion/Cobranza/CarteraVencida.razor`

- Ruta `/facturacion/cobranza/cartera-vencida`, `[Authorize]`.
- Estructura calcada de `ClientesParaCobros.razor`:
  - Card de filtros: Fecha de corte (`DxDateEdit`), Búsqueda (`DxTextBox`),
    Tramo (`DxComboBox`: Todos / 0–30 / 31–60 / 61–120 / +120), Ciclo
    (`DxComboBox`), botón Buscar.
  - Barra de acciones masivas: Exportar selección (Xlsx), Registrar acción en lote
    (reutiliza popup), Generar cartas. Contadores seleccionados/total.
  - `DxGrid` con `SelectionMode=Multiple`, `KeyFieldName=Clave`, columnas
    Clave / Cliente / Ciclo / Ruta / 0–30 / 31–60 / 61–120 / +120 / Total vencido
    (formato `N2`, alineadas a la derecha) + columna Banderas (bloqueado /
    no cortable / abogado, igual que la vista existente). `TotalSummary` Sum por
    cada columna de tramo y total.
  - Resaltado opcional: +120 en rojo, 61–120 en ámbar.

### 4.6 Menú — `apc.Client/Layout/NavMenu.razor`

- Nuevo `DxMenuItem` "Cartera vencida" bajo el ítem "Cobranza"
  (NavigateUrl `/facturacion/cobranza/cartera-vencida`, icono `bi-calendar-x`).

## 5. Pruebas

- Test de integración en `SIAD.Tests` (envuelto en `BEGIN … ROLLBACK`):
  sembrar facturas con distintos `fechavence`/`fechapago`/`saldototal`; verificar
  sumas por tramo y comportamiento *as-of* (factura pagada después de `D` cuenta
  como vencida a `D`; factura pagada antes de `D` no cuenta).

## 6. Base de datos (regla mirror)

- Script `Database/2026-06-24_idx_factura_cartera_vencida.sql` con
  `CREATE INDEX IF NOT EXISTS … ON factura(company_id, fechavence, fechapago)`.
- Aplicar en mirror local `siad_v3_restore` y replicar en SRV `siad_v3` (VPN).

## 7. Fuera de alcance (YAGNI)

- Granularidad por factura / maestro-detalle (descartado: se eligió por cliente).
- Rangos de emisión/vencimiento (descartado: se eligió fecha de corte as-of).
- Cálculo de mora/intereses sobre el vencido (esta vista solo reporta antigüedad).
