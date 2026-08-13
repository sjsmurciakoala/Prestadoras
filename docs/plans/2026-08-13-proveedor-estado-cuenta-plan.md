# Estado de cuenta del proveedor — plan de implementación

**Fecha:** 2026-08-13
**Rama:** `feat/almacen-integracion-contable`
**Estado:** diseñado, SIN implementar
**Prototipo:** https://claude.ai/code/artifact/d1920346-7eed-453d-9f41-7d025093ffad

---

## 1. Qué se pide

Desde el maestro de proveedores (`/proveedores`) debe poder verse el **estado de cuenta** del
proveedor: qué se le debe, por cuáles documentos, desde cuándo, y el historial de cargos y pagos.

---

## 2. Hallazgos de la investigación (verificados en el código)

### 2.1 No existe nada de esto para proveedores

No hay ninguna vista, función ni SP de saldo/antigüedad/estado de cuenta de proveedor en
`Database/`. Todo lo que existe con ese nombre es de **clientes**
(`sp_obtener_cliente_saldo`, `rep_saldo_clientes_antiguedad`, `Rpt_Dev_EstadoCuenta.cs`,
[ClienteEstadoCuentaTab.razor](../../apc.Client/Pages/Clientes/Components/ClienteEstadoCuentaTab.razor)).
Ese slice de clientes es el **molde** a seguir, no una pieza reutilizable.

### 2.2 Las columnas de saldo del maestro están muertas

`prv_proveedores` arrastra `saldo_actual`, `saldo_anterior`, `saldo_act_dolares`,
`compras_acum` de SIMAFI. [ProveedoresService.cs:74](../../SIAD.Services/Proveedores/ProveedoresService.cs:74)
las inserta en `NULL` y ningún servicio las actualiza. **No usarlas.**

Lo mismo con `prv_kardex` (el kardex de proveedor del legacy) y `ops_compromiso`: el propio
script [2026-07-10_prv_company_id_y_constraints.sql:36](../../Database/2026-07-10_prv_company_id_y_constraints.sql:36)
las declara *"vacías y sin uso"*, y no tienen `company_id`. Quedan fuera.

### 2.3 La deuda viva está en dos módulos separados

| # | Tabla | Rol | Proveedor | Fecha | Saldo | Estado |
|---|---|---|---|---|---|---|
| 1 | `prv_compromiso_hdr` | cargo (+) | `cod_proveedor` | `fecha` | **derivado** | `status_transacc`, `anulado` |
| 2 | `prv_compromiso_abono` | abono (−) | vía `numero_orden` | `fecha` | — | `estado` `'V'`/`'A'` |
| 3 | `alm_compra_cxp` | cargo (+) | `cod_proveedor` | `fecha`, `fecha_vencimiento` | **materializado** | `estado_id` 1/2/3/9 |
| 4 | `alm_compra_cxp_abono` | abono (−) | vía `cxp_id` | `fecha` | — | `estado` `'V'`/`'A'` |

Las dos ramas usan **`cod_proveedor` (varchar) sin FK** — `prv_proveedores` es keyless.

### 2.4 Cuidados que cambian el SQL

- **`company_id` no es homogéneo**: `prv_proveedores.company_id` es `int4`;
  `prv_compromiso_hdr` y `alm_compra_cxp` son `BIGINT`. Hace falta cast explícito.
- **Compat legacy del compromiso** ([OrdenesPagoDirectoService.cs:289](../../SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs:289)):
  un compromiso con `status_transacc = true` y **cero filas de abono** se considera **saldado**
  (saldo 0). Son los ~228 compromisos migrados de SIMAFI, L 6.8M
  ([docs/ANALISIS_DEUDAS_PROVEEDORES_SIMAFI_2026-07-21.md](../ANALISIS_DEUDAS_PROVEEDORES_SIMAFI_2026-07-21.md)).
  **Si el SQL no replica esta regla, el estado de cuenta inventa L 6.8M de deuda.**
- **El abono de compromiso es BRUTO**: con retención, al banco sale el neto pero el saldo baja
  por el bruto ([OrdenesPagoDirectoService.cs:1047](../../SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs:1047)).
  El estado de cuenta usa el **bruto**; la retención es informativa.
- **Prepagadas no generan CxP** ([RecepcionCompraService.cs:376](../../SIAD.Services/Almacen/RecepcionCompraService.cs:376)) — correcto, no deben aparecer.
- **Anulados fuera**: `anulado = true` (compromiso) y `estado_id = 9` (CxP).

### 2.5 Alcance contable — decirlo en pantalla

El saldo operativo **no cuadra con el mayor**. La CxP histórica del proveedor
(~L 101M al HABER en `prv_proveedores.cuenta_contable`) la mantiene SIMAFI y no tiene
documentos operativos en el portal. La pantalla lleva una nota de alcance explícita; no se
intenta reconciliar aquí.

---

## 3. Regla de negocio

```
saldo(proveedor) = Σ cargos vigentes − Σ abonos vigentes
```

**Cargos vigentes**
- `alm_compra_cxp` con `estado_id <> 9` → aporta `monto`; el saldo por documento ya viene
  materializado en `saldo`.
- `prv_compromiso_hdr` con `anulado = false` **y** que no sea legacy-saldado
  (no es el caso `status_transacc = true` con 0 abonos) → aporta `monto`.

**Abonos vigentes**
- `alm_compra_cxp_abono` con `estado = 'V'`.
- `prv_compromiso_abono` con `estado = 'V'` (monto bruto).

**Vencimiento**
- CxP: `fecha_vencimiento` (real, del término de pago).
- Compromiso: **no tiene columna de vencimiento** → se usa `fecha`. Ver D2.

**Antigüedad**: Corriente / 1–30 / 31–60 / 61–90 / +90 días desde el vencimiento, a la fecha de corte.

---

## 4. Decisiones

Tomadas (no re-litigar al ejecutar):

- **D-A. Se unifican compras y compromisos** en un solo estado de cuenta. Es lo que el usuario
  entiende por "lo que le debemos al proveedor". El origen se distingue con un chip por fila.
- **D-B. Página propia** `/proveedores/{codigo}/estado-cuenta` (permite enlace directo, filtros y
  PDF), embebida además como pestaña en `ProveedorDetail.razor`. No solo un popup.
- **D-C. La retención no es un movimiento**: se muestra como referencia dentro de la línea del
  pago, nunca como cargo ni abono propio.
- **D-D. Solo lempiras** en la primera entrega. `alm_compra_hdr` tiene `moneda`/`tasa_cambio`
  pero hoy todo el flujo es local.

Abiertas — **no bloquean la implementación**, se asume lo indicado y se confirma después:

- **D1. ¿El grid del maestro lleva columna "Saldo"?** Cuesta una vista agregada sobre los ~605
  proveedores en cada carga. *Asunción: no en la primera entrega* (Fase 4 opcional).
- **D2. Vencimiento del compromiso.** No existe la columna. *Asunción: se usa `fecha` y la
  antigüedad del compromiso se cuenta desde ahí.* Si el contador quiere plazo real, hay que
  agregar `fecha_vencimiento` a `prv_compromiso_hdr` (script aditivo aparte).
- **D3. ¿La deuda migrada de SIMAFI debe verse?** *Asunción: no* (regla 2.4). Es coherente con
  lo que ya muestran las pantallas de compromisos.

---

## 5. Arquitectura del slice

Sigue el patrón del proyecto: función Postgres → servicio con Dapper → controller → cliente → página.
**Sin LINQ** en el código nuevo ([hodsoft-sin-linq](../../.github/skills/hodsoft-sin-linq/SKILL.md));
`ProveedoresService` y `RetencionRegistroService` están contaminados de LINQ y **no** sirven de plantilla.

### 5.1 Base de datos — script nuevo

`Database/2026-08-1X_prv_estado_cuenta.sql` (aditivo, idempotente, solo funciones de lectura).

Tres funciones, todas con `company_id` como primer parámetro:

```sql
-- Resumen: saldo, vencido, por vencer, conteo de documentos, último pago.
CREATE OR REPLACE FUNCTION public.fn_prv_estado_cuenta_resumen(
    p_company_id BIGINT, p_cod_proveedor VARCHAR, p_corte DATE)
RETURNS TABLE (saldo_total NUMERIC, saldo_vencido NUMERIC, saldo_por_vencer NUMERIC,
               documentos_pendientes INT, ultimo_pago_monto NUMERIC, ultimo_pago_fecha DATE,
               antiguedad_corriente NUMERIC, antiguedad_30 NUMERIC, antiguedad_60 NUMERIC,
               antiguedad_90 NUMERIC, antiguedad_mas90 NUMERIC)

-- Documentos con saldo (o todos, según p_solo_pendientes).
CREATE OR REPLACE FUNCTION public.fn_prv_estado_cuenta_documentos(
    p_company_id BIGINT, p_cod_proveedor VARCHAR, p_corte DATE, p_solo_pendientes BOOLEAN)
RETURNS TABLE (origen SMALLINT, documento_id BIGINT, numero_documento VARCHAR, fecha DATE,
               fecha_vencimiento DATE, concepto VARCHAR, monto NUMERIC, abonado NUMERIC,
               saldo NUMERIC, dias_vencido INT, estado_id SMALLINT)

-- Libro de movimientos con saldo corrido (el corrido lo calcula la función, no el filtro).
CREATE OR REPLACE FUNCTION public.fn_prv_estado_cuenta_movimientos(
    p_company_id BIGINT, p_cod_proveedor VARCHAR, p_desde DATE, p_hasta DATE)
RETURNS TABLE (fecha DATE, origen SMALLINT, tipo SMALLINT, numero_documento VARCHAR,
               referencia VARCHAR, cargo NUMERIC, abono NUMERIC, saldo_corrido NUMERIC)
```

Notas de implementación del SQL:

- El cuerpo es un `UNION ALL` de las cuatro fuentes (§2.3) sobre un CTE `documentos_vigentes`
  que ya excluye anulados y legacy-saldados.
- El saldo corrido se calcula con `SUM(...) OVER (ORDER BY fecha, origen, documento_id)`
  sobre **toda** la historia; el rango de fechas filtra al final, igual que hace el estado de
  cuenta de cliente (el corrido de una fila es el acumulado real, no el del rango).
- `origen` y `tipo` son **códigos numéricos**, no letras
  ([EstadosNumericos.cs](../../SIAD.Core/Constants/EstadosNumericos.cs)): `origen` 1 = compra,
  2 = compromiso; `tipo` 1 = cargo, 2 = abono. La descripción la pone el C#.
- Cast obligatorio en el join al maestro: `p.company_id::BIGINT = p_company_id` (§2.4).
- Registrar el script en [Database/2026-07-30_pendientes_srv.md](../../Database/2026-07-30_pendientes_srv.md)
  con la skill `runbook-despliegue-srv`. Al ser solo `CREATE OR REPLACE FUNCTION`, es
  reversible y no toca datos → pasa la `guardia-estructura-bd` como aditivo.

### 5.2 DTOs — `SIAD.Core/DTOs/Proveedores/ProveedorEstadoCuentaDtos.cs`

`ProveedorEstadoCuentaResumenDto`, `ProveedorEstadoCuentaDocumentoDto`,
`ProveedorEstadoCuentaMovimientoDto`, `ProveedorEstadoCuentaDto` (identidad + resumen).
Cada uno con la **descripción legible** además del código (regla de CLAUDE.md: se muestra la
etiqueta, nunca el código interno).

### 5.3 Servicio — `SIAD.Services/Proveedores/ProveedorEstadoCuentaService.cs`

Servicio **nuevo** (no ampliar `ProveedoresService`). Interfaz `IProveedorEstadoCuentaService`,
registrado en [ServiceRegistration.cs](../../SIAD.Services/ServiceRegistration.cs).

```csharp
Task<ProveedorEstadoCuentaDto?> GetResumenAsync(string codigo, DateOnly? corte, CancellationToken ct = default);
Task<IReadOnlyList<ProveedorEstadoCuentaDocumentoDto>> GetDocumentosAsync(string codigo, DateOnly? corte, bool soloPendientes, CancellationToken ct = default);
Task<IReadOnlyList<ProveedorEstadoCuentaMovimientoDto>> GetMovimientosAsync(string codigo, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default);
Task<ProveedorEstadoCuentaImpresionDto?> GetDatosImpresionAsync(string codigo, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default);
```

Patrón obligatorio (Dapper sobre la conexión del contexto, `company_id` explícito porque Dapper
**no** pasa por el filtro global de tenancy) — molde en
[ClientesServices.cs:596](../../SIAD.Services/Clientes/ClientesServices.cs:596):

```csharp
var companyId = _currentCompanyService.GetCompanyId();
if (companyId <= 0) throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");

var connection = _context.Database.GetDbConnection();
if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);

const string sql = "SELECT ... FROM public.fn_prv_estado_cuenta_documentos(@CompanyId, @Codigo, @Corte, @SoloPendientes)";
var filas = await connection.QueryAsync<ProveedorEstadoCuentaDocumentoDto>(
    new CommandDefinition(sql, new { CompanyId = companyId, Codigo = codigo, ... }, cancellationToken: ct));
```

### 5.4 Controller — `apc/Controllers/Proveedores/ProveedorEstadoCuentaController.cs`

Molde: [RetencionRegistroController.cs:18](../../apc/Controllers/Proveedores/RetencionRegistroController.cs:18).

```csharp
[ApiController]
[Route("api/proveedores/{codigo}/estado-cuenta")]
[ModuleAuthorize(PermissionModules.Proveedores, PermissionResources.Proveedores.EstadoCuenta)]
public sealed class ProveedorEstadoCuentaController : ControllerBase
```

`GET` (resumen) · `GET documentos` · `GET movimientos` · `GET pdf`.

### 5.5 Cliente — `apc.Client/Services/Proveedores/ProveedorEstadoCuentaClient.cs`

Molde moderno: [RetencionRegistroClient.cs:10](../../apc.Client/Services/Retenciones/RetencionRegistroClient.cs:10)
(`BaseUrl` const, query con `List<string>`, `GetFromJsonAsyncWithAuthCheck`, y
`GetEstadoCuentaPdfUrl(codigo, desde, hasta)` para abrir el PDF en pestaña nueva).
Registrar en [CommonServices.cs](../../apc.Client/CommonServices.cs).

### 5.6 UI

**Componente** `apc.Client/Pages/Proveedores/Components/ProveedorEstadoCuentaTab.razor`
(parámetro `Codigo`), consumido por dos anfitriones:

1. **Página** `ProveedorEstadoCuenta.razor` — `@page "/proveedores/{codigo}/estado-cuenta"`,
   con encabezado, botón Regresar (patrón `originUrl` de
   [ProveedoresList.razor:318](../../apc.Client/Pages/Proveedores/ProveedoresList.razor:318)) y PDF.
2. **Pestaña** en [ProveedorDetail.razor:68](../../apc.Client/Pages/Proveedores/ProveedorDetail.razor:68),
   que ya tiene un `DxTabs` con una sola pestaña — se agrega "Estado de cuenta".

**Entrada desde el maestro**: botón de fila nuevo en
[ProveedoresList.razor:146](../../apc.Client/Pages/Proveedores/ProveedoresList.razor:146),
icono `bi bi-journal-text`, clase `btn-icon btn-estado`, título "Estado de cuenta".

**Contenido** (ver prototipo): tarjeta de identidad · 4 KPIs (saldo, vencido, vence en 7 días,
último pago) · barra de antigüedad de 5 tramos · pestañas *Documentos pendientes* / *Movimientos*
· nota de alcance contable.

Los dos grids siguen el
[estándar de grid](../../.github/skills/hodsoft-blazor-devexpress-ui/references/grid-standard.md):
`DxGrid` con `CssClass="grid-solicitudes"` dentro de `.grid-wrapper`, `ToolbarTemplate` con
filtros a la izquierda, botón Columnas y contador `Total` a la derecha, `DisplayFormat` en las
columnas de monto (**no** `CellDisplayTemplate`, que el export a Excel ignora), franja de
severidad por vencimiento y `badge-status` para el estado.

### 5.7 Permisos y menú

En [PermissionNames.cs](../../SIAD.Core/Constants/PermissionNames.cs), calcando el patrón de
`Retenciones`:

- `PermissionResources.Proveedores.EstadoCuenta = "estado_cuenta"`
- `PermissionNames.Proveedores.EstadoCuenta.View = "module.proveedores.estado_cuenta.view"`
- Política que acepta también los permisos de módulo:
  `[EstadoCuenta.View, Proveedores.View, Legacy.Proveedores]` (línea 619 es el molde exacto).

Menú: **no** se agrega item propio en
[SidebarNavigationDefinition.cs](../../apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs) —
la pantalla es por proveedor y se entra desde el maestro o el detalle.

---

## 6. Fases

| Fase | Alcance | Entregable |
|---|---|---|
| **F0** | Script SQL con las 3 funciones + registro en el runbook SRV | `Database/2026-08-1X_prv_estado_cuenta.sql` |
| **F1** | DTOs + servicio + controller + cliente + permisos | Backend consultable |
| **F2** | Componente + página + pestaña en el detalle + botón en el maestro | Pantalla usable |
| **F3** | PDF del estado de cuenta | `Rpt_Dev_EstadoCuenta_Proveedor.cs` |
| **F4** *(opcional, D1)* | Columna "Saldo" en el maestro + antigüedad global de todos los proveedores | Vista agregada |

F0 y F1 son un solo bloque de trabajo (la función se prueba desde los tests). F2 no arranca
hasta que F1 devuelva datos correctos contra el mirror.

---

## 7. Pruebas

`SIAD.Tests/Proveedores/ProveedorEstadoCuentaTests.cs` (xUnit + Dapper contra el mirror,
dentro de `BEGIN ... ROLLBACK` como el resto de la suite):

1. Factura de compra al crédito sin pagos → aparece con saldo = total.
2. Factura con abono parcial → `abonado` y `saldo` correctos; estado Parcial.
3. Factura anulada (`estado_id = 9`) → **no** aparece ni suma.
4. Abono anulado (`estado = 'A'`) → no resta.
5. Compromiso con abonos → saldo = monto − Σ abonos vigentes.
6. **Compromiso legacy** (`status_transacc = true`, cero abonos) → **no** aparece como deuda.
   *(Este es el test que protege contra los L 6.8M fantasma.)*
7. Abono de compromiso con retención → el saldo baja por el **bruto**, no por el neto.
8. Antigüedad: documento vencido hace 45 días cae en el tramo 31–60.
9. Movimientos: el saldo corrido de la última fila == saldo del resumen.
10. **Tenancy**: un proveedor con el mismo código en otra empresa no contamina el resultado.

---

## 8. Riesgos

| Riesgo | Mitigación |
|---|---|
| Deuda fantasma de SIMAFI (L 6.8M) por ignorar la compat legacy | Test 6, regla explícita en el CTE |
| El saldo no cuadra con el mayor y alguien lo reporta como bug | Nota de alcance visible en la pantalla y en el PDF |
| Fuga de tenant (Dapper no aplica el filtro global) | `company_id` como primer parámetro de las 3 funciones + test 10 |
| `company_id` int4 vs bigint en el join al maestro | Cast explícito, verificado en §2.4 |
| Rendimiento con proveedores de mucho movimiento | Índices ya existentes: `(company_id, cod_proveedor)` en `alm_compra_cxp`; revisar si `prv_compromiso_hdr` necesita uno equivalente |

---

## 9. Fuera de alcance

- Reconciliar el saldo operativo con la cuenta contable del proveedor.
- Revivir `prv_kardex` / `ops_compromiso`.
- Multi-moneda (D-D).
- Cualquier cambio en cómo nacen o se pagan los documentos: esta iniciativa **solo lee**.
