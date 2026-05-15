---
name: hodsoft-blazor-devexpress-ui
description: Construye y modifica UI Blazor WebAssembly tenant-aware para HODSOFT en `apc.Client`, usando componentes DevExpress y los patrones reales del proyecto. Use this when editing `.razor`, `.razor.cs`, `.razor.css`, client HTTP services, page navigation, layout components, or screens that depend on `DevExpress.Blazor`. Before changing any DevExpress API or component behavior, consult the official docs through `dxdocs`.
---

# HODSOFT Blazor DevExpress UI

Usa esta skill para trabajar sobre la capa UI sin romper el flujo real del proyecto.

## Workflow

1. Ubica el modulo en `apc.Client/Pages/<Modulo>/`.
2. Revisa si ya existe un cliente HTTP en `apc.Client/Services/<Modulo>/`.
3. Si el cambio toca un componente DevExpress, **call `devexpress_docs_search`** con el nombre exacto del control (e.g. `DxGrid`, `DxPopup`) y luego **call `devexpress_docs_get_content`** para leer el articulo relevante antes de implementar.
4. Manten la pagina como orquestadora de estado y llamadas HTTP, no de reglas de negocio profundas.
5. Si agregas un cliente HTTP nuevo, registralo en `apc.Client/CommonServices.cs`.

## Reglas de implementacion

- Para datos scoped por empresa, resuelve `TenantState.EnsureCompanyAsync()` antes de cargar informacion.
- Reutiliza `ReadFromJsonAsyncWithAuthCheck`, `PostAsJsonAsyncWithAuthCheck`, `PutAsJsonAsyncWithAuthCheck` y `ObtenerMensajeErrorAsync`.
- Conserva la separacion existente entre `.razor`, `.razor.cs` y `.razor.css` si el modulo ya la usa.
- Si una pantalla ya tiene una clase `*GridDataSource.cs`, no la colapses dentro de la pagina sin necesidad.
- Manten los componentes, captions, mensajes y patrones de popup/grid consistentes con el modulo existente.
- Si un cambio exige navegacion nueva, revisa tambien `apc.Client/Layout/*` y las definiciones de sidebar.

## Evita

- Acceder a base de datos directamente desde la UI.
- Duplicar DTOs que ya existan en `SIAD.Core/DTOs`.
- Inventar propiedades, eventos o plantillas de DevExpress sin revisar la fuente oficial.
- Saltarte el flujo de tenancy con `company_id`.

## Referencia rapida

Lee `references/project-map.md` para ver rutas, ejemplos y puntos de integracion habituales.
