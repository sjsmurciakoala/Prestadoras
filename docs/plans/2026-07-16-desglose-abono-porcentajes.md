# Vigencia de abonos + porcentajes de desglose — Plan de implementación

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Que los abonos resten del saldo del cliente y se distribuyan en el desglose por servicio según porcentajes configurables en un mantenimiento nuevo.

**Architecture:** Una vista SQL (`vw_transaccion_abonado_vigente`) centraliza la regla de vigencia; `sp_obtener_cliente_saldo(bigint,varchar)` pasa a `SUM(debitos-creditos)` sobre esa vista (con respaldo previo del SP). El desglose clasifica los movimientos no-catálogo en SALDO_ANTERIOR / PAGOS / AJUSTES y un método puro en C# reparte los pagos por porcentaje. Mantenimiento nuevo (tabla + slice completo DTO→servicio→controller→client→página DxGrid).

**Tech Stack:** .NET 9, Blazor WASM + DevExpress 25.1.7, PostgreSQL (Dapper para SQL crudo), xUnit (SIAD.Tests, integración con ROLLBACK).

**Diseño aprobado:** `docs/plans/2026-07-16-desglose-abono-porcentajes-design.md`

**Reglas transversales:**
- Scripts SQL solo al mirror local `siad_v3_restore` (localhost). **Prod la aplica el usuario** — recordarlo en el resumen final.
- Antes de crear/aplicar scripts en `Database/`: skill `guardia-estructura-bd`.
- La regla de vigencia (formulación final; ver design doc): `COALESCE(estado,'') NOT IN ('N','R','P') AND NOT (estado='A' AND COALESCE(tipotransaccion,'') IN ('201','202'))` — por exclusión de lo muerto, para cubrir también el traslado `PLAN` con `'C'` de los planes de pago.
- La firma vieja `sp_obtener_cliente_saldo(varchar)` (1 arg) **no se toca**: está deprecated, sin callers desde el fix 2026-07-07, y `SaldoCrossCompanyTests.Overload_1arg_es_cross_company_documenta_el_bug` fija su comportamiento.
- No commitear salvo que el usuario lo pida.

---

### Task 1: Respaldo del SP actual

**Files:**
- Create: `Database/2026-07-16_backup_sp_obtener_cliente_saldo.sql`

Contenido = las dos definiciones actuales capturadas con `pg_get_functiondef` del mirror (verificadas 2026-07-16), con encabezado que explique que ejecutar el archivo restaura el comportamiento previo al fix de vigencia. Las definiciones actuales son:

- `sp_obtener_cliente_saldo(pcodigocliente varchar)` — último `ta.saldo` global con `estado='A'` (sin company, deprecated).
- `sp_obtener_cliente_saldo(p_company_id bigint, pcodigocliente varchar)` — último `ta.saldo` por empresa con `estado='A'`, `STABLE`.

**Verificación:** el archivo contiene ambas firmas exactamente como las devolvió `pg_get_functiondef` (ya capturadas en la sesión).

### Task 2: Script principal de BD (vista + SP + tabla)

**Files:**
- Create: `Database/2026-07-16_saldo_vigencia_y_desglose_abono.sql`

**Paso 1:** invocar `guardia-estructura-bd` (CREATE OR REPLACE de función = impacto; vista y tabla = aditivo).

**Paso 2:** escribir el script idempotente, en `BEGIN/COMMIT`:

1. `CREATE OR REPLACE VIEW public.vw_transaccion_abonado_vigente` con la regla de vigencia + `COMMENT`.
2. `CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(bigint, varchar)` → `SELECT COALESCE(SUM(COALESCE(debitos,0)-COALESCE(creditos,0)), 0) FROM vw_transaccion_abonado_vigente WHERE company_id/cliente_clave` (misma firma/retorno, `STABLE`). Ahora devuelve una fila con 0 para clientes sin movimientos (antes 0 filas; los callers hacen `?? 0` / `COALESCE`).
3. `CREATE TABLE IF NOT EXISTS public.adm_desglose_abono_porcentaje` (`desglose_abono_id identity PK`, `company_id bigint NOT NULL`, `item_codigo varchar(50) NOT NULL`, `porcentaje numeric(5,2) NOT NULL CHECK (porcentaje > 0 AND porcentaje <= 100)`, `usuario varchar(100)`, `fecha_modificacion timestamptz NOT NULL DEFAULT now()`, `UNIQUE (company_id, item_codigo)`).
4. Comentarios de auditoría al final (query comparativa saldo viejo vs nuevo por cliente, para correr en prod ANTES de aplicar).

**Paso 3:** aplicar al mirror: `psql -h localhost -U postgres -d siad_v3_restore -f <script>` (psql en `C:\Program Files\PostgreSQL\17\bin`).

**Paso 4:** verificar contra el caso conocido:
- `SELECT * FROM sp_obtener_cliente_saldo(2::bigint,'090040001')` → **331.11** (antes 662.22).
- `SELECT * FROM sp_obtener_cliente_saldo(2::bigint,'090041009')` → el pago reversado (`'A'+202`) ya no resta.

### Task 3: Tests de integración de vigencia (TDD sobre la BD)

**Files:**
- Create: `SIAD.Tests/SaldoVigenciaTests.cs`
- Modify: `SIAD.Tests/SaldoCrossCompanyTests.cs` (fixture `InsertarSaldoColisionanteAsync`)

**Paso 1 (antes de aplicar el script del Task 2, o contra una BD sin él, el test debe fallar; tras aplicarlo, pasa):** nuevo test con empresa sintética 9999 (patrón de `SaldoCrossCompanyTests`): insertar en `transaccion_abonado` (company 9999, clave sintética):
- 2 cargos `estado='A'`, `tipotransaccion='AGUA_POTABLE'`, `debitos=100`.
- 1 abono vigente `estado='C'`, `tipotransaccion='202'`, `creditos=50`.
- 1 abono reversado `estado='A'`, `tipotransaccion='202'`, `creditos=30`.
- 1 recibo pendiente `estado='P'`, `tipotransaccion='202'`, `creditos=20`.

Asserts: `sp_obtener_cliente_saldo(9999, clave) == 150.00` (200 − 50; ni el reversado ni el pendiente cuentan). Cliente sin movimientos → devuelve 0.

**Paso 2:** fixture de `SaldoCrossCompanyTests`: el INSERT colisionante agrega `debitos = @saldo` (además de `saldo = @saldo`) para que la firma nueva (SUM) y la vieja de 1 arg (last-row saldo) devuelvan lo mismo.

**Paso 3:** correr `$env:SIAD_TEST_DB='Host=localhost;...Database=siad_v3_restore...'; dotnet test --filter "FullyQualifiedName~SaldoVigenciaTests|FullyQualifiedName~SaldoCrossCompanyTests"` → PASS.

### Task 4: Distribuidor puro + unit tests

**Files:**
- Create: `SIAD.Services/Clientes/DesgloseAbonoDistribuidor.cs`
- Create: `SIAD.Tests/DesgloseAbonoDistribuidorTests.cs`

Método estático `Distribuir(items, pagos, ajustes, porcentajes)` → `IReadOnlyList<SaldoServicioDto>`:

- Distribuye `pagos` (neto, normalmente negativo) solo si `porcentajes.Values.Sum() == 100m` y `pagos != 0`.
- Solo entre ítems configurados **presentes** en el desglose del cliente, renormalizando pesos (si el cliente no tiene "Saldo anterior", su cuota se reparte proporcional entre el resto).
- Cuotas `decimal.Round(pagos * pct / pesoTotal, 2, AwayFromZero)`; el residuo de redondeo cae en el ítem de mayor % (desempate: menor orden).
- Lo no distribuido + `ajustes` (NC/ND) → fila final "Pagos y ajustes" (solo si ≠ 0).
- **Invariante: la suma de las filas == suma de entradas** (items + pagos + ajustes).

Tests (TDD: escribirlos primero, verlos fallar, implementar): reparto 60/40 exacto; residuo de redondeo (p.ej. −100 entre 3 ítems 33.33/33.33/33.34); suma ≠ 100 → fallback a fila "Pagos y ajustes"; ítem configurado ausente → renormaliza; `pagos == 0` con ajustes ≠ 0 → solo fila de ajustes; invariante de total en todos los casos.

### Task 5: `ClientesServices.GetEstadoCuentaAsync` — desglose con categorías y distribución

**Files:**
- Modify: `SIAD.Services/Clientes/ClientesServices.cs:556-644`

1. `ultimoPago`: incluir abonos V3 — filtro `(ILIKE '%PAGO%' OR (tipotransaccion IN ('201','202') AND estado = 'C'))` (hoy solo ILIKE '%PAGO%' y por eso no ve los abonos de caja).
2. `sqlDesglose`: `mov` lee de `public.vw_transaccion_abonado_vigente` (mismos filtros company/clave, sin filtro de estado propio); `servicios` agrega columna `codigo`; `otros` se agrupa por categoría: `SALDO_ANTERIOR` / `PAGOS` (tipotransaccion `IN ('201','202')` OR `tipo_servicio='E'` OR `ILIKE '%PAGO%'`) / `AJUSTES` (resto). El SELECT final devuelve `(categoria, codigo, servicio, saldo, orden)`.
3. C#: cargar porcentajes (`SELECT item_codigo, porcentaje FROM adm_desglose_abono_porcentaje WHERE company_id=@CompanyId`), armar `items` (servicios ordenados + fila `SALDO_ANTERIOR` si existe, orden 9000) y delegar en `DesgloseAbonoDistribuidor.Distribuir`.
4. Actualizar el comentario largo del desglose (explica la regla de vigencia y la distribución).

**Verificación:** `dotnet build HODSOFT_DEVEXPRESS.sln` + prueba manual posterior (Task 11).

### Task 6: DTOs + servicio del mantenimiento + DI

**Files:**
- Create: `SIAD.Core/DTOs/Tarifario/DesgloseAbonoConfigDtos.cs` (`DesgloseAbonoItemDto {ItemCodigo, ItemNombre, Orden, EsServicioCatalogo, Porcentaje}`, `DesgloseAbonoGuardarDto {Items: List<{ItemCodigo, Porcentaje}>}`)
- Create: `SIAD.Services/Tarifario/IDesgloseAbonoConfigService.cs`, `SIAD.Services/Tarifario/DesgloseAbonoConfigService.cs`
- Modify: `SIAD.Services/ServiceRegistration.cs` (junto a `ServicioTarifarioV3Service`, línea ~153)

`GetAsync`: servicios activos del catálogo (`adm_servicio`, `status_id=1`) `UNION ALL` fila `('SALDO_ANTERIOR', 'Saldo anterior', 9000)`, LEFT JOIN a la tabla de % (0 si no hay). Dapper + `ICurrentCompanyService` (patrón `ServicioTarifarioV3Service`).

`GuardarAsync(dto, usuario)`: validar en servicio — sin duplicados, porcentajes en [0,100], códigos válidos (contra la misma lista de `GetAsync`), y suma == 100.00m **o** todos 0 (desactivar). DELETE por company + INSERT de los > 0, en una transacción EF (`BeginTransactionAsync` + `GetDbTransaction()` para los comandos Dapper). Devuelve `ResponseModelDto`.

### Task 7: Controller + permisos

**Files:**
- Create: `apc/Controllers/DesgloseAbonoConfigController.cs`

Patrón exacto de `ServicioTarifarioV3Controller`: `[Route("api/tarifario/desglose-abonos")]`, clase con `[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]`; `GET` lista; `POST` con `[ModuleAuthorize(..., PermissionAction.Edit)]` y `usuario = User?.Identity?.Name ?? "system"`. No hace falta permiso nuevo en `PermissionNames` (mismo recurso que el maestro de servicios).

### Task 8: Cliente HTTP + registro

**Files:**
- Create: `apc.Client/Services/Tarifario/DesgloseAbonoConfigClient.cs`
- Modify: `apc.Client/CommonServices.cs` (junto a `ServicioTarifarioV3Client`, línea ~105)

Usar las extensiones auth-aware de `apc.Client/Services/HttpClientExtensions.cs` (`GetFromJsonAsyncWithAuthCheck`, `PostAsJsonAsyncWithAuthCheck` + `ObtenerMensajeErrorAsync`).

### Task 9: Página del mantenimiento + menú

**Files:**
- Create: `apc.Client/Pages/Tarifario/DesgloseAbonos.razor`
- Modify: `apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs` (hijos de `tarifario-v3`, tras "Maestro servicios")

Página `/tarifario/desglose-abonos` (mirar encabezado/`@attribute` de `MaestroServiciosV3.razor` para autorización): DxGrid **bajo el estándar de grids** (`.github/skills/hodsoft-blazor-devexpress-ui/references/grid-standard.md`; look global en `apc/wwwroot/css/siad-grid.css`) con columnas Ítem / Tipo (Servicio | Especial) / Porcentaje editable (template con `DxSpinEdit<decimal>` 0–100, 2 decimales, bind al item en memoria). Pie con suma total; botón Guardar habilitado solo si suma == 100 o todos 0; toasts de resultado; texto de ayuda que explique el efecto (distribución de abonos en el desglose del estado de cuenta). **Consultar dxdocs MCP antes de usar API DevExpress no vista en el repo.**

Menú: `new SidebarNavItem { Id = "tarv3-desglose-abonos", Text = "Distribución de abonos", NavigateUrl = "/tarifario/desglose-abonos", MatchPrefixes = ["/tarifario/desglose-abonos"], IconCssClass = "bi bi-percent" }`.

### Task 10: Consultas inline con el patrón viejo

**Files:**
- Modify: `SIAD.Services/Cobranza/CobranzaService.cs:1038-1053`
- Modify: `SIAD.Services/Cobranza/CorteMasivoService.cs:257-272`

Reemplazar los `LEFT JOIN LATERAL` de "último movimiento estado='A'" por `SELECT SUM(COALESCE(ta.debitos,0)-COALESCE(ta.creditos,0)) AS saldo FROM public.vw_transaccion_abonado_vigente ta WHERE ta.company_id = cm.company_id AND ta.cliente_clave = cm.maestro_cliente_clave`. En los laterales de `ultima_pago`, ampliar el filtro de pago: `(ta.tipotransaccion ILIKE '%PAGO%' OR (ta.tipotransaccion IN ('201','202') AND ta.estado = 'C'))`.

### Task 11: Build, suite completa y verificación manual

1. `dotnet build HODSOFT_DEVEXPRESS.sln` → 0 errores.
2. `$env:SIAD_TEST_DB='<mirror>'; dotnet test SIAD.Tests/SIAD.Tests.csproj` → suite completa. Si algún test calibrado a la semántica vieja de saldo falla, ajustar su fixture (documentar cuál y por qué), no revertir el SP.
3. Verificación funcional con el portal (`preview_start` con launch.json apuntando al mirror… solo si la config local lo permite; si no, verificar por SQL directo el desglose del cliente `090040001` con y sin configuración de % (INSERT temporal en `adm_desglose_abono_porcentaje` company 2, luego DELETE).

### Task 12: Cierre

- Actualizar `docs/plans/2026-07-16-desglose-abono-porcentajes-design.md` si hubo desvíos (p. ej. módulo Tarifario en lugar de Clientes).
- Actualizar memoria (`transaccion-abonado-estados-invertidos.md` → estado implementado; nueva nota del mantenimiento si aplica).
- Resumen final: recordar que **los dos scripts de `Database/` los aplica el usuario en prod**, con la query de auditoría previa.
