# Entradas y salidas de almacén — estado real y traspaso de sesión

Fecha: 2026-08-03
Autor: sesión de Claude Code (PASO 0 del encargo "ENTRADAS Y SALIDAS DE ALMACÉN, equivalente a Centura `GA_IN.APT`").
Propósito: **este documento existe para que otra sesión, en otra cuenta, pueda retomar sin releer nada.**

> ✅ **Actualización 2026-08-03:** la solución **no compilaba** (2 errores `CS0117` por permisos no
> declarados). **Ya está arreglado y verificado** — ver §5.1. El resto del estado sigue vigente.

---

## 1. Qué se pidió y en qué punto está

El encargo pide migrar las entradas y salidas de almacén de Centura (`GA_IN.APT`) al portal SIAD,
con un proceso por roles (analista → DBA → desarrollador → QA) y **sin generar código hasta aprobar
las fases 1 y 2**.

Estado: **las fases de analista y DBA ya están hechas y escritas** (por sesiones anteriores, entre el
2026-07-31 y el 2026-08-01), y **alguien empezó a codificar la Fase 1 el 2026-08-03 por la mañana y
se interrumpió a media implementación**. No hay aprobación registrada de las fases 1 y 2 en ningún
documento; la implementación arrancó de todos modos.

| Rol | Entregable | Dónde | Estado |
|---|---|---|---|
| Analista funcional | Análisis del legacy: modelo, motor, catálogo de movimientos, 14 defectos verificados | [`docs/centura-flujos/README_entradas_salidas_almacen.md`](../centura-flujos/README_entradas_salidas_almacen.md) | ✅ Completo |
| DBA + arquitectura | Modelo de datos, DDL, servicios, permisos, UI, plan de pruebas, plan por fases | [`docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md`](2026-08-01-movimientos-almacen-catalogo-diseno.md) | ✅ Completo (propuesta) |
| DBA (costeo) | Costeo, concurrencia, invariantes, casos borde | [`docs/plans/2026-08-01-costeo-articulo-diseno.md`](2026-08-01-costeo-articulo-diseno.md) | ✅ Completo (propuesta) |
| Desarrollador | Fase 1 (catálogo) | Código (§4) | 🟡 **A medias — no compila** |
| Desarrollador | Fase 2 (documento entrada/salida) | — | ❌ No empezado |
| QA | Suite de pruebas del catálogo y del documento | — | ❌ No empezado (18 casos ya especificados en el diseño §7) |

---

## 2. Mapa del proyecto (PASO 0, punto 1)

### 2.1 Aclaración importante sobre las fuentes

El encargo dice *"código fuente en la carpeta APP ZIP con base de datos [MOTOR: SQL Server]"*. Preciso,
porque afecta a todo lo demás:

- **`SIAD_Centura/APP ZIP/`** es el **sistema legado de origen**: Centura/SQLWindows (`.APT`/`.APP`),
  DLLs Delphi, reportes Crystal/QRP. Su base es SQL Server (`MERENDON`). Es **solo lectura, es la
  referencia funcional**. `GA_IN.APT` (2,6 MB) es el módulo de inventario.
- **`Prestadoras/`** (directorio de trabajo) es el **destino**: portal SIAD, .NET 9, Blazor +
  DevExpress, **PostgreSQL**. Aquí se implementa.

El análisis del legacy ya está hecho y **no requiere volver a abrir `GA_IN.APT`**: el README de §1
cita archivo y línea de cada regla. Ojo con un detalle registrado ahí: el motor de kardex de Centura
no vive en `GA_IN.APT` sino en `Casajaar_Final/NEWAPP/GA_ES.APT:4245-4401` (clase
`clsKardex_Inventario.Grabar`), y `APP MERENDON/GA_IN.APT:1079-1230` tiene una **copia divergente**.

### 2.2 Arquitectura del destino

Nueve proyectos .NET 9 (detalle en [`CLAUDE.md`](../../CLAUDE.md)). El corte que importa aquí:

```
SIAD.Core      DTOs, entidades (scaffold de Postgres), constantes (permisos, estados)
SIAD.Data      SiadDbContext, partial por módulo → SiadDbContext.Almacen.cs
SIAD.Services  Servicios de dominio por módulo → SIAD.Services/Almacen/
apc            Host ASP.NET Core: controllers (delgados) + Identity + Reporting
apc.Client     Blazor WASM: Pages/Almacen/ + Services/Almacen/ (clientes HTTP)
SIAD.Tests     xUnit contra Postgres real; cada test en BEGIN … ROLLBACK
```

**El flujo de una funcionalidad, de punta a punta** (y el orden en que hay que tocar los archivos):

```
DTO en SIAD.Core/DTOs/Almacen/
  → interfaz + impl en SIAD.Services/Almacen/
    → registrar en SIAD.Services/ServiceRegistration.cs  (AddSiadServices)
      → controller en apc/Controllers/Almacen/          (validar, delegar; nada de negocio)
        → permiso en SIAD.Core/Constants/PermissionNames.cs + PermissionEndpointCatalog.cs
          → cliente HTTP en apc.Client/Services/Almacen/
            → registrar en apc.Client/CommonServices.cs
              → página en apc.Client/Pages/Almacen/
                → nodo de menú en apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs
```

**Los dos puntos de registro centralizados se olvidan con facilidad** — son `ServiceRegistration.cs`
(servidor) y `CommonServices.cs` (cliente, corre en **ambos** hosts, así que todo lo que se agregue
debe ser seguro en WASM y en server-interactive).

### 2.3 Convenciones que hay que respetar

| Tema | Regla |
|---|---|
| **Multi-tenant** | No negociable. Toda tabla funcional lleva `company_id`; la entidad implementa `ICompanyScopedEntity` y el filtro global de `SiadDbContext.Tenancy.cs` la acota sola. **Nunca** confiar en un `companyId` del body: se resuelve con `ICurrentCompanyService`. FK compuestas `(company_id, id)`, nunca `REFERENCES tabla(id)` a secas. |
| **Nombres** | Entidades y columnas en `snake_case` tal como están en Postgres (son scaffold). DTOs y servicios en `PascalCase`. Los DbSet se llaman `alm_tipo_movimientos` (plural pegado al nombre físico). |
| **Estados** | Numéricos (`EstadosNumericos.cs`). Prohibido introducir columnas de estado en texto o comparar contra literales. |
| **Base de datos** | **No hay EF migrations para el SIAD.** Todo cambio es un script fechado en `Database/` (`2026-08-01_alm_tipo_movimiento.sql`), idempotente, con bloque de verificación y de rollback. Las migraciones EF de `apc/Migrations/` son **solo de Identity**. |
| **Registro de scripts** | Todo script nuevo en `Database/` se registra en el runbook (`Database/2026-07-23_runbook_despliegue_srv.md`) vía la skill `runbook-despliegue-srv`. |
| **DDL** | Antes de cualquier `ALTER`/`DROP`/`TRUNCATE`/borrado masivo, pasar por la skill `guardia-estructura-bd`. |
| **UI** | Toda vista con `DxGrid` sigue el estándar de grid (referencia `ClientesList.razor`). El look compartido vive **una sola vez** en `apc/wwwroot/css/siad-grid.css`; el `.razor.css` de la página solo lleva lo suyo. |
| **DevExpress** | 25.2.4. Antes de cambiar cualquier API de un componente, consultar el MCP `dxdocs`. No inventar propiedades. |
| **HTTP en cliente** | Usar las extensiones de `apc.Client/Services/HttpClientExtensions.cs` (`GetFromJsonAsyncWithAuthCheck`, etc.), que convierten 401/redirect a `UnauthorizedAccessException`. |
| **Idioma** | Código, comentarios, documentación y mensajes de usuario en español. |

### 2.4 Modelo de datos de inventario **hoy** (Postgres, ya existente)

Todo en `SIAD.Data/SiadDbContext.Almacen.cs`.

**Maestros y catálogos**
`alm_articulo` (maestro; `activo` = soft delete) · `alm_tipo_articulo` (= los 9 grupos; dueño de las
5 cuentas contables y de `impuesto_tasa_id`) · `alm_grupo` · `alm_bodega` · `alm_unidad_medida` /
`alm_categoria_unidad` · `alm_articulo_proveedor` · `alm_config_inventario` · `cfg_compra_isv`.

**Stock y libro** — el núcleo:

| Tabla | Rol | Notas críticas |
|---|---|---|
| `alm_articulo_bodega` | **Fuente de verdad del stock** por par (artículo, bodega) | `existencia`, `costo_promedio`, `ultimo_costo`, más `existencia_comprometida` / `existencia_transito` **declaradas y sin escritor**. Es la fila que se bloquea con `FOR UPDATE`. |
| `alm_kardex` | **Libro inmutable** de movimientos | Protegido por trigger `trg_alm_kardex_inmutable` (SQLSTATE **K0001**): no admite `UPDATE` ni `DELETE`. Se corrige **solo por reversa**. Tiene `bodega_destino_id` declarada y **sin productor**. |
| `alm_articulo.existencia` | Rollup consolidado | Lo escribe `ArticuloRollupService`. |

**Documentos existentes**
`alm_orden_compra` + detalle + correlativo · `alm_compra_hdr` + `alm_compra` + correlativo ·
`alm_ajuste_inventario` (**cabecera plana de UNA sola línea**) · `alm_requisicion` y `alm_descargo`
(**tablas planas del histórico SIMAFI; sus servicios son SOLO CONSULTA**).

**El motor de posteo: `SIAD.Services/Almacen/InventarioPostingService.cs`**

Es el **único punto de escritura del kardex** y no debe duplicarse jamás (es el defecto D-12 del
legacy). Lo que ya resuelve bien, y que la Fase 2 debe reutilizar **sin tocar**:

- `SELECT … FOR UPDATE` sobre `alm_articulo_bodega` con el `company_id` **dentro del SQL crudo**.
- Idempotencia por `uuid` v5 determinista + índice único `(company_id, uuid)`.
- Una transacción, un solo `SaveChanges`.
- Guardas de existencia negativa y de costo positivo.

Su vocabulario cerrado (`TipoMovimientoInventario`, 8 valores):
`CargaInicialNueva` · `CargaInicialReconciliacion` · `AjustePositivo` · `AjusteNegativo` ·
`AjusteValor` · `Reversa` · `Compra` · `SalidaDescargo`.

Y el de `documento_tipo` (`TipoDocumentoInventario`, espejo del CHECK `ck_alm_kardex_documento_tipo`):
`COMPRA` · `REQUISICION` · `DESCARGO` · `TRASLADO` · `AJUSTE` · `CARGA_INICIAL` · `REVERSA`.

### 2.5 El hueco funcional, en una frase

**Hoy no existe ninguna salida operativa de almacén en el portal.** Los únicos servicios que postean
son `AjusteInventarioService`, `ArticuloUbicacionService`, `CargaInicialInventarioService` y
`RecepcionCompraService`. `DescargosService` y `RequisicionesService` son solo consulta, y
`SalidaDescargo` únicamente lo ejercitan los tests. La única forma de sacar mercadería es un
**ajuste negativo de una línea** desde la pestaña Ubicaciones de la ficha del artículo.

---

## 3. La decisión de arquitectura ya tomada (no reabrir sin motivo)

El usuario decidió el 2026-08-01 adoptar el **único gran acierto de Centura** —que el comportamiento
de un movimiento sea un **dato** (`INV_TIPOSTRANSACC`) y no código compilado— **sin heredar sus 14
defectos**, mediante dos capas separadas:

| Capa | Qué es | Dónde vive | Quién la cambia |
|---|---|---|---|
| **Clase** | Semántica que el motor sabe ejecutar: `ENTRADA` / `SALIDA` / `VALOR` | `ClaseAjusteInventario` (constante, ya existe) → mapea al enum del motor | Solo con código + tests |
| **Tipo** | Nombre de negocio: "Merma por vencimiento", "Donación", "Consumo interno" | `alm_tipo_movimiento` (tabla nueva) | **El usuario, desde una pantalla** |

Encima de eso, un **documento único multi-línea** (`alm_movimiento_hdr` / `alm_movimiento_dtl`) que
cubre entrada y salida con una sola pantalla y un solo servicio, se postea completo o nada, y se
anula **por reversa, nunca por UPDATE**.

Consecuencia deliberada: `DocumentoTipo` sigue siendo `AJUSTE` para el kardex. Agregar un tipo de
negocio nuevo **no toca la base del libro** ni amplía ningún CHECK.

**Costo aceptado:** `docs/centura-flujos/README_requisiciones_descargos.md` (1.576 líneas) queda
**supersedido en su arquitectura** (§6-12). Su análisis del legacy (§3-5) **sigue vigente**. El flujo
de dos actores (solicitante → jefe aprueba → bodeguero entrega) **no está cubierto** y se pospone.

---

## 4. Qué está hecho — verificado contra el código, no contra los documentos

### 4.1 Fase 1 (catálogo) — 🟡 a medias, hecha el 2026-08-03 entre 08:18 y 08:24

| Pieza | Archivo | Estado |
|---|---|---|
| Script SQL | `Database/2026-08-01_alm_tipo_movimiento.sql` | ✅ Completo: aditivo, idempotente, con verificación (V1-V6, incluidas 2 pruebas negativas) y rollback. Siembra 3 tipos (`SOBRANTE_CONTEO`/ENTRADA, `MERMA`/SALIDA, `CORRECCION_COSTO`/VALOR) por cada empresa con bodegas. Incluye la clave alterna `uq_alm_tipo_movimiento_tenant (company_id, id)` que la Fase 2 necesita para su FK compuesta. |
| Entidad | `SIAD.Core/Entities/alm_tipo_movimiento.cs` | ✅ Con `ICompanyScopedEntity` y XML docs |
| DTOs | `SIAD.Core/DTOs/Almacen/TipoMovimientoAlmacen{Dto,ListItemDto}.cs` | ✅ |
| Mapeo EF | `SIAD.Data/SiadDbContext.Almacen.cs:35, 89-96` | ✅ DbSet + índices |
| Servicio | `SIAD.Services/Almacen/{I,}TipoMovimientoService.cs` | ✅ CRUD + desactivar. Incluye la guarda de negocio clave: **no se puede cambiar la `clase` de un tipo con movimientos posteados** |
| DI | `SIAD.Services/ServiceRegistration.cs:133` | ✅ |
| Controller | `apc/Controllers/Almacen/TiposMovimientoController.cs` | ✅ 4 endpoints (rompía el build hasta el 2026-08-03, §5.1) |
| **Permisos** | `PermissionNames.cs` / `PermissionEndpointCatalog.cs` | ✅ Declarados el 2026-08-03 (§5.1) |
| Cliente HTTP | `apc.Client/Services/Almacen/TiposMovimientoClient.cs` | ✅ 2026-08-03 |
| Registro cliente | `apc.Client/CommonServices.cs` | ✅ 2026-08-03 |
| Pantallas | `TiposMovimientoList.razor` / `TipoMovimientoForm.razor` | ✅ 2026-08-03 (estándar de grid) |
| Menú | `SidebarNavigationDefinition.cs` | ✅ Almacén → Mantenimiento → Tipos de movimiento |
| Tests | `SIAD.Tests/Almacen/TipoMovimientoServiceTests.cs` | ✅ **11/11 en verde** contra el mirror (2026-08-03) |
| Runbook | `Database/2026-07-23_runbook_despliegue_srv.md` | ✅ Registrado como **paso 27** el 2026-08-03 |
| Aplicado al mirror | `siad_v3_restore` | ✅ Aplicado, con la **semilla real de Centura** (12 tipos) el 2026-08-03 |

Dos detalles del servicio que hay que conocer al continuar, ambos con `// Fase 2` en el código:
`GetAsync` devuelve **`EnUso = false` cableado**, y `TieneMovimientosPosteadosAsync` **retorna
`false` fijo**. Son correctos hoy (no existe `alm_movimiento_dtl`), pero **la guarda de la clase es
inoperante hasta que la Fase 2 los conecte**. Si se olvida, se podrá cambiar la clase de un tipo ya
usado y reinterpretar el histórico.

### 4.2 Fase 2 (documento) — ❌ no empezada

No existe nada: ni `alm_movimiento_hdr`/`_dtl`/`_correlativo`, ni entidades, ni
`IMovimientoAlmacenService`, ni controller, ni pantallas. El diseño está completo y detallado
(diseño §3.2-3.4 para el DDL, §4.2 para el servicio con el cuerpo de `CrearYPostearAsync` ya escrito).

### 4.3 Contexto: el módulo alrededor sí está muy avanzado

Órdenes de compra, recepción de compras, carga inicial de existencias, ISV de compras por tipo,
kardex por bodega y el motor de posteo con su suite (`InventarioPostingTests`, `RecepcionCompraTests`,
`CargaInicialTests`, `OrdenCompraTests`, `KardexPuntoCorteTests`, `PermisosInventarioTests`,
`UuidV5Tests`…) están implementados. **Nada de eso está commiteado**: son ~130 archivos entre
modificados y sin seguimiento en la rama `Cambios_almacen2.0`.

---

## 5. Pendientes, en orden de ejecución

### 5.1 ✅ RESUELTO (2026-08-03) — el build estaba roto

```
apc/Controllers/Almacen/TiposMovimientoController.cs(15,79): error CS0117:
  'PermissionResources.Inventario' no contiene una definición para 'TiposMovimiento'
apc/Controllers/Almacen/TiposMovimientoController.cs(72,83): error CS0117: (ídem)
```

Causa: el controller se escribió referenciando permisos que **nunca se declararon**.

Arreglado declarando el permiso completo, siguiendo el patrón de `CargaInicial` / `Ajustes`:

1. `SIAD.Core/Constants/PermissionNames.cs` — `PermissionResources.Inventario.TiposMovimiento`
   (`"tipos_movimiento"`); la clase anidada `PermissionNames.Inventario.TiposMovimiento` con
   `View/Create/Edit` (**sin `Delete`**: un tipo se desactiva, no se borra); su alta en la lista
   de permisos y **tres `PermissionPolicyDefinition` explícitas**.
2. `SIAD.Core/Constants/PermissionEndpointCatalog.cs` — los 4 endpoints, con
   `Resource: "tipos_movimiento__almacen_tipos_movimiento"`. `desactivar` es `POST` pero se
   cataloga como `Edit` (cambia el estado de un tipo existente, no crea uno).

**Verificado:** `dotnet build HODSOFT_DEVEXPRESS.sln` → *Compilación correcta, 0 errores*.

> **Precisión sobre cómo autoriza este sistema** (dato no obvio, verificado en
> `apc/Security/ModuleAuthorizeAttribute.cs:126-196`): `ModuleAuthorize` **no usa las políticas de
> ASP.NET**; es un `IAsyncAuthorizationFilter` que consulta los claims directamente
> (`user.HasClaim`), con la cadena de fallback endpoint → recurso base → módulo → legacy. Por eso
> olvidar una política **no** produce un 403 en la API. El array `Policies` sirve para
> `[Authorize(Policy = …)]` — es decir, para las **páginas Blazor**, donde una política inexistente
> sí falla. Las tres se declararon por eso, para las pantallas de §5.2.

`Movimientos` y `module.inventario.movimientos.autorizar_sensibles` **no se declararon todavía**:
son de la Fase 2 y sus endpoints aún no existen. Nombres exactos en el diseño §5.

### 5.2 ✅ Fase 1 completada (2026-08-03) — con un pendiente de verificación

Hecho: cliente HTTP + registro, las dos pantallas (estándar de grid, patrón
`TiposArticuloList.razor`), nodo de menú en Almacén → Mantenimiento, 11 pruebas de integración y el
registro del script como **paso 27** del runbook. **Build verificado: 0 errores.**

Detalle no obvio de la UI: el formulario **bloquea el combo de clase** cuando el tipo está en uso
(`EnUso`), reflejando la guarda del servicio en vez de dejar que el usuario la descubra con un error.
Hoy `EnUso` siempre es `false` (ver la trampa de §4.1), así que el bloqueo no se activa todavía.

**✅ Verificado contra el mirror el 2026-08-03:**

```
$env:SIAD_TEST_DB = 'Host=localhost;...;Database=siad_v3_restore;...'
dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~SIAD.Tests.Almacen"
```

- `TipoMovimientoServiceTests` → **11/11 en verde**.
- Regresión completa de Almacén → **203/203 en verde**. Importaba: al borrar los 3 tipos de la
  semilla genérica había que confirmar que ninguna prueba dependía de ellos. Ninguna.
- Mirror **limpio tras correr** (12 filas, sólo `company_id` 2, cero residuos `TM_*` ni empresas
  ficticias): el `BEGIN … ROLLBACK` de `IntegrationTestBase` hace su trabajo.

**🟡 Único pendiente de la fase:** prueba de humo en el navegador de `/almacen/tipos-movimiento`
(alta, edición, desactivación). No ejecutada.

### 5.3 Fase 2 — el documento de entrada/salida (el encargo propiamente dicho)

Diseño completo en §3.2-3.4 (DDL), §4.2 (servicio), §5 (API), §6 (UI), §7 (12 pruebas).
Al implementar, tres puntos que el diseño marca y son fáciles de perder:

- **Conectar `EnUso` y `TieneMovimientosPosteadosAsync`** contra `alm_movimiento_dtl` (§4.1).
- **Resolver los pares ordenados por id** antes del bucle de posteo, para que dos documentos
  concurrentes no hagan deadlock cruzado.
- **La unidad de posteo es la línea, no la cabecera**: cada línea con su `uuid` v5 determinista.

### 5.4 Fases posteriores (ya planificadas, fuera de la entrega actual)

- **Fase 3** UI · **Fase 4** deprecar `alm_ajuste_inventario` · **Fase 5 traslado entre bodegas**
  (con tránsito, dos pasos; requiere valores nuevos en el enum del motor y `bodega_destino_id`) ·
  **Fase 6** flujo de dos actores (requisición → aprobación → descargo).

---

## 6. Preguntas abiertas (PASO 0, punto 2) — ⚠️ PENDIENTE DE DEFINIR

Ninguna de éstas se puede suponer. Las cuatro primeras **bloquean** la Fase 2.

### Sobre el encargo actual

1. **⚠️ La Fase 1 se implementó sin aprobación explícita y quedó rota. ¿Se continúa por ese camino
   (arreglar y seguir) o se prefiere revisar el diseño antes de tocar más código?** El encargo dice
   "no generes código hasta que aprobemos las fases 1 y 2", y ya hay código a medias.
2. **⚠️ Requisitos no funcionales: el encargo los dejó como plantilla sin llenar.** Faltan:
   **volumen** (movimientos/día, número de SKUs, usuarios concurrentes) y si el módulo debe
   **soportar operación offline/reconexión (SÍ/NO)**. Lo segundo cambia la arquitectura de raíz.
3. **⚠️ ¿La primera entrega incluye el traslado entre bodegas?** Hoy está planificado como Fase 5
   (posterior). Es el **único documento del legacy que es puramente movimiento de almacén** y lo que
   más se parece al `GA_IN.APT` original. Si el usuario lo espera en esta entrega, cambia el alcance.
4. **⚠️ ¿Los movimientos requieren autorización real (dos personas) o basta un permiso?** Hoy el
   diseño resuelve `requiere_autorizacion` con un solo permiso adicional, sin segregación de
   funciones (decisión 4 del diseño).

### Heredadas de los diseños, aún sin respuesta

5. **⚠️ Fecha de corte del inventario (D4)** y **qué hacer con las 3 existencias negativas (D6)**:
   pares 0147 (−6), 5039 (−2, con `valor_unitario` **−317,5650**) y 0167 (−2). Sin la fecha no se
   ejecuta el corte; sin el saneo no se puede **cerrar**.
6. **⚠️ ¿Flete, otros gastos e importación entran al costo del inventario?** Hoy solo se capitaliza
   el ISV. Contablemente sí deberían; prorratearlos rompe la coincidencia renglón a renglón con la
   factura del proveedor.
7. **⚠️ ¿Qué cuenta contable estampa el asiento?** Hoy el motor copia `alm_articulo.cuenta_contable`,
   columna **ya declarada deprecada** (las cuentas se heredan del tipo de artículo). Como el kardex
   es inmutable, **cada asiento que se postee de aquí en adelante congela la cuenta equivocada para
   siempre**. Urgente: hay que decidirlo **antes del corte**.
8. **⚠️ ¿Se ejecuta el rollback de `alm_requisicion_hdr` / `alm_descargo_hdr` en el mirror?** El paso
   26 del runbook está aplicado ahí y su arquitectura quedó supersedida (decisión 1 del diseño).
9. **⚠️ ¿Cuántas filas tiene hoy `alm_ajuste_inventario`?** Determina si se congela como histórico o
   se migra. No consultado.
10. **⚠️ ¿Los 3 tipos de la semilla alcanzan** o el usuario ya tiene en mente otros (donación,
    consumo interno, merma por vencimiento) que convenga sembrar desde el script?

### Del legacy, sin resolver por falta de acceso a SQL Server

11. **⚠️ `AREA_AFECTADA`** (`'C'`, `'P'`, `'D'`): su significado literal **no consta en el fuente**.
    Se infiere Clientes / Proveedores / movimientos internos. Requiere leer `INV_TIPOSTRANSACC`.
12. **⚠️ Contenido real del catálogo del legacy**: los 17 tipos documentados son los que el fuente
    postea; la tabla puede tener más, y el `CAMBIA_COSTO` de cada uno solo se sabe leyéndola.
13. **⚠️ `INV_TRANSACC_AXL`**: su propósito se infiere del join, sin DDL no se confirma.
14. **⚠️ `DEM`** (materiales de orden de trabajo) está **comentado** en el fuente: no se sabe si se
    desactivó o nunca entró en producción.

---

## 7. Estado de despliegue (contexto para no romper nada)

- **Nada de esto está commiteado.** Rama `Cambios_almacen2.0`, ~130 archivos modificados/nuevos.
- **Trabajo solo en local.** El usuario decide cuándo commitear, subir o publicar.
- **No conectarse a ninguna base de datos por iniciativa propia.** Los scripts de `Database/` los
  aplica el usuario.
- Topología: prod `siad_v3` @ 172.16.0.9 · mirror `siad_v3_restore` @ localhost · desarrollo
  `siad_v3_desarrollo` @ 3.208.232.209.
- **26 pasos pendientes de aplicar en el SRV** (`Database/2026-07-30_pendientes_srv.md`), más un
  "grupo B" **sin confirmar** (6 scripts base del kardex + `cfg_impuestos`) que es **prerrequisito
  duro** y **deja de ser re-ejecutable** una vez activo el trigger de inmutabilidad.
- El **paso 24** (mudanza del stock de `PRIN` a `01`) va **sí o sí antes del corte**.
- El corte de inventario **no es SQL**: se opera desde `/almacen/carga-inicial` con el binario
  desplegado. Guion en `docs/plans/2026-07-31-fase8-ejecucion-corte-inventario.md`.

---

## 8. Resumen en 5 líneas

1. El análisis del legacy y el diseño (analista + DBA) están **completos y escritos**; la decisión de
   arquitectura —catálogo configurable en dos capas + documento multi-línea— **ya está tomada**.
2. La Fase 1 (catálogo) está **completa en código** (§5.1 y §5.2): build arreglado, cliente, dos
   pantallas, menú, 11 pruebas y el script registrado como paso 27. Falta **ejecutar** las pruebas
   contra una base con el paso 27 aplicado — hoy salen `Skipped`.
3. La Fase 2 (el documento de entradas y salidas, que es el encargo) **no está empezada**, pero su
   diseño está listo hasta el nivel de DDL y cuerpo de método.
4. **Lo siguiente es**: verificar la Fase 1 contra base real → Fase 2 (§5.3).
5. **Antes de seguir hay 14 preguntas abiertas** (§6); cuatro bloquean la Fase 2 y una (la cuenta
   contable del asiento) es urgente porque el kardex es inmutable.
