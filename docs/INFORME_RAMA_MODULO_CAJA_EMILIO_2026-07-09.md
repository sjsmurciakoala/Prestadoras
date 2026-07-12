# Informe: análisis de la rama `Modulo_Caja_1.0` (Emilio Garay) vs nuestro main

**Fecha:** 2026-07-09 · **Alcance:** solo análisis (sin integrar), por decisión de usuario.
**Fuente:** repo padre `koala-outsourcing/HODSOFT_DEVEXPRESS`, rama `origin/Modulo_Caja_1.0` (tip `d2f82e35`, 2026-07-09), base de divergencia `7d9fa291` (2026-05-20).
**Destino de comparación:** este repo (`sjsmurciakoala/Prestadoras`), main `637ce77`.

---

## 1. Hallazgo maestro

**El grueso del trabajo de Emilio (mayo–junio) YA ESTÁ en nuestro main.** Los commits de consolidación `65f6a29` ("db: scripts SQL junio 2026") y `8a75607` ("feat: consolidación junio 2026 — cobranza, caja/abonos, presupuesto, proveedores, reportes") del 2-jul absorbieron su árbol, y las fases F1–F8 se construyeron *encima* de ese código. Verificación archivo por archivo (normalizando CRLF/BOM) de los ~600 archivos reales de su rama: **266 byte-idénticos a nuestro main**, 144 difieren (mayoría: nuestro refactor de alertas y nuestras fases posteriores — es decir, nosotros vamos adelante), 192 no existen en el nuestro (su trabajo de julio).

**El delta real pendiente es su trabajo del 3 al 9 de julio**: módulo Almacén/Activo Fijo, migración SIMAFI de presupuesto y bancos, bitácora de maestros (WIP), solicitud→cliente, y un puñado de fixes puntuales.

Dato transversal verificado: ambos árboles están en **DevExpress 25.2.4 / .NET 9** (el CLAUDE.md que dice 25.1.7 está desactualizado). No hay conflicto de versión. Sí hay una diferencia de estilo: sus páginas nuevas usan `DxAlert`; nuestro main lo eliminó por completo (patrón `SiadAlert`/`DxToast`/div bootstrap) — toda página que se tome de su rama pasa por esa adaptación mecánica.

---

## 2. Qué NO integrar (ya está, o sería regresión)

| Área | Motivo |
|---|---|
| Caja sesión (`sesion_caja`, CajaService, GestionCaja), Cobranza completa (cortes masivos, llamadas, notas, cartas, acciones+PDF, cartera vencida, banderas), 22 scripts SQL de caja/cobranza, tests | **Ya integrado byte-idéntico** en main |
| Reportería completa (17 endpoints, ~14 reportes `rep_*`, plantillas, catálogo tenant-scoped, runtime) | **Ya integrado** (65f6a29). Donde difiere, nuestro main es más nuevo (Ref. SIMAFI, encabezado por empresa, fix publicación layouts) |
| Su `AbonoService` / `CaptacionPagosService` / `CobranzaController` / `CuentasAbonos.razor` | **Regresión pre-F4/F7/F8**: muta plantillas de partida en caliente, lee `historialmes` (retirado), **no conoce el marker `WSBANCO:`** (permitiría reversar pagos del canal bancario dejando `ban_ws_pago` APLICADO → replay del banco sin cobro), contracuenta legacy, llama una sobrecarga de `sp_con_revertir_poliza` inexistente |
| Seed `2026-06-10_seed_document_type_y_reglas_abonos.sql` | Obsoleto por F4 (adivina cuentas con `LIKE '%caja%'`, solo primera empresa) |
| `NavMenu.razor`, `PermissionNames`, `PermissionEndpointCatalog`, `SIAD.Tests.csproj`, `PartidasContabilidad.razor`, páginas Informes/Contabilidad | Nuestro main es superconjunto estricto o más nuevo |
| `UsuariosAppList.razor` (él lo modifica) | **Nosotros lo borramos** (reemplazado por `LectoresCredencialList` + `adm_lector_credencial`). Un merge ciego lo resucitaría |
| Numeración mensual de partidas (commit `70a82cbe`) | **Compatible, no choca**: nuestra `fn_con_siguiente_poliza` (F3) se diseñó sobre su cambio; mismo advisory lock y convención `{company}-{AAAA-MM}-{NNNNNN}` |

### 🔴 NO tomar bajo ningún concepto
- **`apc/appsettings.json` / `appsettings.Development.json`**: credenciales en claro (`Host=172.16.0.9;Username=postgres;Password=root`; `Koala@2021` comentado). Nuestro repo usa placeholder + `appsettings.Local.json`. **Además: esas credenciales están hoy en el remoto del repo padre — considerar rotarlas.**
- **`DiagnosticoController.cs` / `UserRepairHelper` / `VerificadorUsuarios`**: endpoints sin `[Authorize]` (`confirmar-todos-usuarios`, `reparar-usuario/{email}`), solo protegidos por `IsDevelopment()` en el cuerpo.
- **Migración EF `20260707173000_AddBitacoraMaestros.cs`**: contra la convención (el SIAD funcional no usa EF migrations); basta el DDL espejo.

---

## 3. Delta real a integrar (recomendado)

### 3.1 Módulo Almacén + Activo Fijo — INTEGRAR con adaptación (esfuerzo M)
Módulo nuevo completo y **bien hecho**: 8 scripts DDL timestamped e idempotentes (`alm_articulo`, `alm_kardex`, `alm_compra/requisicion/descargo`, `alm_unidad_medida` con conversión, catálogos tipo/línea/grupo, bodega→estantería→estante, puente `alm_articulo_bodega` con rollup de existencia, `af_activo_fijo` + depreciación), 16 entidades con `ICompanyScopedEntity`, partial limpio `SiadDbContext.Almacen.cs` (índices tenant-safe), 13 servicios, 11 controllers con `[ModuleAuthorize(Inventario)]`, ~24 páginas, 5 tests patrón BEGIN…ROLLBACK. Es catálogo + consulta de histórico migrado (compras/requisiciones/kardex de solo lectura; el posteo transaccional es fase futura). Migrado de MySQL SIMAFI (44,579 filas de depreciación).

Adaptaciones: (a) invocar `ConfigureAlmacenModel` desde nuestro `OnModelCreatingPartial` (él lo encadena desde *su* `SiadDbContext.Cobranza.cs`); (b) `DxAlert` → patrón nuestro en las ~24 páginas; (c) fusión aditiva en `ServiceRegistration`, `CommonServices`, `SidebarNavigationDefinition` (grupo "Almacén"); (d) re-scaffold del contexto después de aplicar los DDLs.

### 3.2 Migración SIMAFI presupuesto — INTEGRAR TAL CUAL (S)
Pipeline 00–03 (`stg_simafi_*` → `pst_config_presupuesto_hdr/dtl`, un presupuesto `PRE-<año>` por año) + `tools/MigradorPresupuesto` + script que conserva solo `PRE-2025`. Idempotente, aditiva, ya aplicada en 0.9. Depende de `stg_simafi_cuenta_map` (existe por nuestra migración contable). Pendiente operativo: ~53% de cuentas sin mapa → completar `cuenta_map` y re-correr el 02.

### 3.3 Migración SIMAFI bancos + fixes de Bancos — INTEGRAR COORDINADO (S/M)
- Scripts `2026-07-09_bancos_simafi_00..04` + `tools/MigradorBancos`: complementarios a nuestra migración comercial (misma BD destino, company_id=2).
- **Fix real**: `2026-07-09_fix_sp_ban_kardex_detalle_tipo_transaccion.sql` (el SP devolvía id numérico en vez del código `DEP`; join de partida nunca casaba).
- **Estado de cuenta bancario a Excel**: `GetEstadoCuentaAsync` + `EstadoCuentaDto` + endpoint ClosedXML + botón en `TransaccionesBancarias.razor` (verificar dependencia ClosedXML; adaptar DxAlert).
- **`PolizaId`/`TienePartidaContable`** en detalle de transacción bancaria (el kardex migrado no tiene partidas — muestra el vínculo solo si existe).
- Base = nuestro `BanTransaccionesService` (tiene el fallback F4 `ResolverCuentaAsync` que él no tiene); lo suyo se fusiona encima.

### 3.4 Fix de bug NUESTRO detectado por su rama — TOMAR SÍ O SÍ (S)
`OrdenesPagoDirectoService`: nuestro main conserva referencias al nombre viejo `con_poliza`/`con_poliza_linea` en fallbacks (líneas ~1547, 1664, 1696, 3112, 3271, 3314, 3322) que nosotros renombramos a `con_partida_hdr/_dtl` en el DDL 20260220 — **bug latente en main**; su rama lo corrige. Revisar también `con_poliza_id` en el UPDATE de `prv_compromiso_hdr` (~línea 1416).

### 3.5 Parches menores — INTEGRAR (S)
- **Bloqueo de clientes con contraseña + rol**: su `CobranzaController.BloquearDesbloquear` valida `UserManager.CheckPasswordAsync` + rol `Cobranzas`. **Cierra un TODO de seguridad real en nuestro main** (la UI pide contraseña pero el servidor no valida). Requiere añadir `RoleNames.Cobranzas` y seed del rol.
- **`GridLayoutStorage.cs`**: persistencia de layout de DxGrid en localStorage + column chooser (cableado en `Cobranza.razor`).
- **Solicitud → cliente**: `ClientesServices`, `ClienteCreateDto`, `ClienteCreatePage/EditPage`, `SolicitudesIndex`, `ClienteDesdeSolicitudTests`.
- **Tildes/mojibake**: nuestro árbol tiene `MISCEL�NEOS`, `catÃ¡logo`, `v�lida` en `CaptacionPagosController`, `PolizaService`, `CuentasBancosService`, `BanTransaccionesService`; su versión trae las cadenas correctas (+ hook `check-tildes.ps1`).
- **`site.css`**: +114 líneas (estilos popup acción cobranza), aditivo.
- `HttpClientExtensions`/`CobranzaClient`: mejoras triviales (CancellationToken, mensajes).

---

## 4. Decisiones de usuario pendientes

| # | Tema | Opciones |
|---|---|---|
| 1 | **Bitácora de maestros (WIP de Emilio)** | Interceptor en `SiadDbContext.Tenancy.cs` (nuestro archivo más crítico) que audita **toda** entidad en cada SaveChanges (doble I/O — auditaría facturación masiva) y `bitacora_maestro` sin `ICompanyScopedEntity` (legible cross-tenant si el controller no filtra). Recomendación: integrarlo como fase aparte, acotando `ShouldAuditEntry` a maestros/catálogos y haciendo la tabla tenant-scoped. O diferirlo. |
| 2 | **Regla de fecha en presupuesto** | Él: `FechaInicia ≥ 1-ene del año en curso` (permite capturar el presupuesto anual); nosotros: `≥ hoy`. Recomendación: la suya. |
| 3 | **`RequireConfirmedAccount` false** (Program.cs) | Relaja Identity globalmente. Decidir si se adopta. |
| 4 | **Wipe bancos+presupuesto** (`2026-07-09_bancos_presupuesto_wipe.sql`) | Ya ejecutado por él en 0.9. Como script histórico ok, pero: 🔴 **borra `ban_ws_credencial` y `ban_ws_pago` (F8)** → re-ejecutarlo exige re-seed de credenciales del WS o el banco deja de autenticar, y pierde la bitácora de idempotencia; y en BDs con F6 exige rebuild/verificador de `con_saldo_cuenta` después. Integrarlo solo dentro del runbook. |
| 5 | **Numeración `OPD-*` de compromisos** | No choca con el correlativo mensual (el MAX exige sufijo numérico), pero son dos numeraciones conviviendo. Candidato a unificar con `fn_con_siguiente_poliza` a futuro. |

---

## 5. Riesgos operativos anotados

1. **Repo padre**: NO hacer pull ni commit en GitHub Desktop sobre `HODSOFT_DEVEXPRESS` (7,508 archivos sucios en el working tree compartido). El fetch ya está hecho; el padre solo se usa como fuente de lectura (`git show origin/Modulo_Caja_1.0:...`).
2. **Credenciales en el remoto del padre**: `postgres/root@172.16.0.9` y `Koala@2021` están commiteadas en la rama de Emilio. Rotar cuando sea viable.
3. **DDL perdido del repo** (preexistente, no culpa de Emilio): la creación de `rep_catalogo_informes`/`rep_catalogo_datasets`/`rep_reporte_layouts` (`2026-03-21_add_rep_*.sql`) ya no existe en ningún árbol — rescatar del historial para instalaciones limpias.
4. **0.9 compartida**: sus migraciones de bancos/presupuesto y las nuestras (comercial 2 ciclos) escriben en la misma BD de pruebas `company_id=2`. Coordinar orden en el runbook.
5. **CLAUDE.md desactualizado**: dice DevExpress 25.1.7; la solución real está en 25.2.4.

---

## 6. Plan de integración propuesto (cuando se apruebe)

Rama `integracion/emilio-julio` desde main. Orden:

1. **Fixes seguros** (S): `con_poliza`→`con_partida` en OrdenesPagoDirecto, fix `sp_ban_kardex_detalle`, tildes, GridLayoutStorage, site.css, bloqueo clientes con contraseña (+`RoleNames.Cobranzas`).
2. **Almacén + Activo Fijo** (M): DDLs 07-01→07-09 (sin wipe) → entidades/partials → re-scaffold → servicios/controllers/clients → páginas con adaptación de alertas → DI/sidebar → tests.
3. **Bancos julio** (S): estado de cuenta Excel + PolizaId (sobre nuestro BanTransaccionesService).
4. **Solicitud→cliente + presupuesto UI** (S) con la regla de fecha decidida.
5. **Migraciones SIMAFI bancos/presupuesto** (S): scripts al repo; ejecución solo vía runbook (con re-seed `ban_ws_credencial`).
6. **Bitácora de maestros** (M): fase aparte si se aprueba, con interceptor acotado y tabla tenant-scoped.
7. Compilar + `SIAD_TEST_DB` tests completos + PR(s) a `sjsmurciakoala/Prestadoras`.

Esfuerzo total estimado: **1–2 días de trabajo enfocado** (el 80% es Almacén, mecánico).
