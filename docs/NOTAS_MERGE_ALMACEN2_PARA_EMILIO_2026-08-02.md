# Notas del merge de `Cambios_almacen2.0` — para Emilio (2026-08-02)

Tu rama `Cambios_almacen2.0` quedó **integrada completa en `main`** (PR #60, merge
`2b2c14a`). Entró todo tu trabajo: cheques (emisión manual y por transacción,
numeración por cuenta, bitácora, impresión), contactos de proveedor, presupuesto
multitenant + `valor_real`, almacén (kardex por bodega, guardas de ubicación,
artículo activo), catálogo de auditoría, el manual de Proveedores y la skill del
runbook. La suite completa corre en **407 verdes / 411** incluyendo tus tests.

Este documento explica **lo único que NO entró de tu rama, por qué, y qué te
afecta hacia adelante**. Léelo antes de seguir trabajando sobre `main`.

---

## 1. Qué no entró y por qué

Durante julio, en `main` se completó la **unificación de cobranza** (plan F1–F7):
todos los cobros del sistema pasan por un único motor. Eso significa que dos
archivos que tu rama modificaba ya no existen o cambiaron de rol:

| Archivo | Qué hiciste en tu rama | Qué pasó en main | Resolución del merge |
|---|---|---|---|
| `SIAD.Services/CaptacionPagos/CaptacionPagosService.cs` | Adaptación a tu firma nueva de `RegistrarMovimientoAsync` | **ELIMINADO** — lo reemplazó el motor único `CobroService` | Se mantiene eliminado |
| `SIAD.Services/Caja/AbonoService.cs` (método `RegistrarMovimientoBancarioCaptacionAsync`) | La misma adaptación | El método **ya no existe** — la banca de captación vive en `CobroService` | Quedó la versión de main |

Tu adaptación no se perdió: se aplicó **donde ahora vive ese código**, en
`SIAD.Services/Cobros/CobroService.cs` (y en 4 stubs de tests del motor).

## 2. El motor único de cobros — regla de oro

**Todo cobro entra por `CobroService` (`SIAD.Services/Cobros/`)**: facturas,
cuotas de convenio, notas de débito, recibos pendientes, efectivo o banco.

- **No crees flujos de cobro paralelos** ni resucites `CaptacionPagosService`.
  Si un cobro necesita algo nuevo (otro tipo de documento, otro canal), se
  agrega DENTRO de `CobroService`, que ya maneja idempotencia (advisory lock por
  referencia), derrame entre líneas, integración contable por config y kardex
  bancario.
- `AbonoService` sigue existiendo pero **solo para recibos/consultas de abono** —
  ya no registra movimientos bancarios.

## 3. Legacy: qué queda y qué no se puede tocar

- **`transaccion_abonado` es la ÚNICA tabla legacy que queda**, y está
  **CONGELADA** por el trigger `trg_transaccion_abonado_congelada`
  (`Database/2026-07-30_uc_f7_h4_freeze_legacy.sql`). Cualquier
  INSERT/UPDATE/DELETE tuyo va a reventar con error — es intencional. Es
  archivo histórico, ya ni siquiera se escribe espejo.
- Los documentos de cobro viven en **`adm_pago` + `adm_pago_aplicacion`**.
- El histórico unificado (viejo + nuevo) se lee por la vista
  **`vw_rep_movimiento_vigente`** — úsala en reportes/consultas en lugar de
  pegarle a `transaccion_abonado` directo.
- El saldo del cliente es **`sp_obtener_cliente_saldo(clave, company_id)`** —
  siempre el overload de 2 argumentos (el de 1 era cross-company y se retiró).
- `historialmes` y `clientesaldos` **ya no existen** (ni tabla ni código). El
  ciclo comercial vive en `adm_periodo_comercial(_ciclo)`.

## 4. Estados: numérico en la base, descripción al usuario

- **No crees columnas de estado string nuevas** ni compares contra strings
  mágicos — usa `SIAD.Core/Constants/EstadosNumericos.cs`.
- Las letras `A`/`B`/`C`/`N` de `factura.estado` son **códigos internos**
  (A=1 pendiente, C=2 pagada, N=3 anulada, B=4 abono parcial). **Nunca deben
  llegarle al usuario**: en pantalla siempre va la descripción legible.
  Referencia completa: `docs/ESTADOS_DOCUMENTOS_COMERCIALES.md`.

## 5. Tu firma nueva de `RegistrarMovimientoAsync`

Tu cambio de firma (ahora devuelve `ChequeId`/`NumeroCheque` y acepta
beneficiario/concepto de cheque) **fue adoptado** y `CobroService` ya la
consume. Hacia adelante: si vuelves a cambiar `IBanTransaccionesService`,
recuerda que los llamadores en main incluyen `CobroService` y los stubs de
`SIAD.Tests/Cobros/*` — compila la solución completa antes de subir.

## 6. Reglas de siempre (recordatorio)

- **Credenciales jamás al repo** — van en `appsettings.Local.json` (gitignored).
- **DDLs**: scripts timestamped en `Database/`; nada de EF migrations para el
  contexto SIAD; nada de seeds en C# para el catálogo de reportes.
- **Entidades nuevas del DbContext van en un partial** (`SiadDbContext.<Modulo>.cs`),
  no en el body scaffoldeado de `SiadDbContext.cs` — el scaffold las pisa.
  (Tus entidades de cheques/contactos quedaron en el body; se reacomodan en el
  próximo re-scaffold, pero no sigas ese patrón.)
- **Multitenancy**: toda tabla funcional con `company_id` + `ICompanyScopedEntity`;
  el tenant se resuelve por `ICurrentCompanyService`, nunca del request.
- **DevExpress**: consultar el MCP `dxdocs` antes de tocar cualquier API.
- **Deploy**: NADA se aplica al servidor 172.16.0.9 por cuenta propia — tus 10
  DDLs ya están aplicados en local y entran a 0.9 en la ventana única programada.

## 7. Estado de tus DDLs

Aplicados en las bases locales (`siad_v3_copia09` y `siad_v3_test`), 22/22 OK:
bitácora catálogo, cheques, prv_compromiso ×2, fix fn_pst_next_id, presupuesto
multitenant, contactos ×2, valor_real, alm_articulo_activo + vista de
presupuesto. **Pendientes de 0.9** hasta la ventana de deploy.
