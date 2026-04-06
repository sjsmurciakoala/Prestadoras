# Estado de Avance: Informes Web

**Fecha**: 2026-03-18  
**Branch**: `feature/informes-reporteria-web`  
**Estado**: base funcional implementada, pendiente aplicación de tabla de catálogo en PostgreSQL

Actualizacion 2026-03-23:

- La parte de reporteria web administrable ya avanzo mas alla de este corte.
- Ya estan implementados catalogo de reportes web, layouts versionados, catalogo CRUD de datasets, preview de datasets, diseniador web y publicacion.
- La documentacion vigente del estado actual esta en `docs/REPORTERIA_WEB_DATASETS_2026-03-23.md`.

---

## 1. Decisión funcional tomada

El módulo `Informes` no arrancó como reportería clásica basada en preimpresión o `viewer` de reportes.

Se acordó iniciar con este enfoque:

1. El usuario entra a una vista web.
2. Aplica filtros.
3. Ve los datos directamente en pantalla.
4. Luego decide si exporta, imprime o ejecuta otra acción.

La primera consulta seleccionada para este flujo fue:

- **Partidas de Contabilidad**

---

## 2. Qué ya quedó implementado

### 2.1 Menú y navegación

Se creó la base del módulo `Informes` en el menú lateral y se agregó acceso al catálogo:

- Ruta índice: `/informes`
- Primera consulta: `/informes/partidas-contabilidad`

Archivo relevante:

- `apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs`

### 2.2 Catálogo dinámico de informes

Se definió una entidad para soportar informes configurables por empresa:

- Tabla lógica: `public.rep_catalogo_informe`
- Scope multiempresa
- Código, nombre, categoría, ruta, flags de exportar/imprimir, metadata

Archivos relevantes:

- `SIAD.Core/Entities/rep_catalogo_informe.cs`
- `SIAD.Data/SiadDbContext.Accounting.cs`

### 2.3 DTOs del módulo

Se agregaron DTOs para:

- catálogo de informes
- filtros de la consulta de partidas
- resultados y filas de la consulta

Archivos relevantes:

- `SIAD.Core/DTOs/Informes/InformeCatalogoDto.cs`
- `SIAD.Core/DTOs/Informes/PartidasInformeDto.cs`

### 2.4 Servicios backend

Se implementaron servicios para:

- listar catálogo dinámico
- sembrar catálogo inicial por empresa
- ejecutar consulta filtrada de partidas contables

Archivos relevantes:

- `SIAD.Reports/Informes/IInformesCatalogoService.cs`
- `SIAD.Reports/Informes/InformesCatalogoService.cs`
- `SIAD.Reports/Informes/IInformesConsultaService.cs`
- `SIAD.Reports/Informes/InformesConsultaService.cs`

### 2.5 API del módulo

Se agregaron endpoints backend:

- `GET /api/informes/catalogo`
- `GET /api/informes/consultas/partidas-contabilidad`

Archivo relevante:

- `apc/Controllers/Informes/InformesController.cs`

### 2.6 Cliente y páginas Blazor

Se implementó el cliente HTTP del módulo y las pantallas:

- catálogo de informes
- consulta web de partidas contables
- filtros
- grid de resultados
- popup con detalle de póliza

Archivos relevantes:

- `apc.Client/Services/Informes/InformesClient.cs`
- `apc.Client/Pages/Informes/InformesIndex.razor`
- `apc.Client/Pages/Informes/InformesIndex.razor.css`
- `apc.Client/Pages/Informes/PartidasContabilidad.razor`
- `apc.Client/Pages/Informes/PartidasContabilidad.razor.css`

### 2.7 Registro en DI

Se registraron servicios de `Informes` tanto en backend como en cliente.

Archivos relevantes:

- `SIAD.Services/ServiceRegistration.cs`
- `apc.Client/CommonServices.cs`

---

## 3. Migración y base de datos

Se preparó una migración mínima manual para la tabla del catálogo:

- `SIAD.Data/Migrations/Contabilidad/20260318050000_AddInformesCatalogo.cs`
- `SIAD.Data/Migrations/Contabilidad/20260318050000_AddInformesCatalogo.sql`

### Motivo

La generación automática con `dotnet ef` produjo cambios contaminados del snapshot general del contexto, por lo que se descartó esa salida y se dejó una migración mínima controlada para esta tabla puntual.

---

## 4. Estado técnico actual

### Ya validado

- El proyecto compila correctamente.
- La consulta de `Partidas de Contabilidad` responde y carga datos.
- El detalle de póliza abre correctamente.
- El módulo `Informes` ya existe en UI/API.

### Pendiente inmediato

Falta crear físicamente la tabla:

- `public.rep_catalogo_informe`

Mientras esa tabla no exista, el endpoint `/api/informes/catalogo` intentará consultarla y PostgreSQL devolverá:

- `42P01: no existe la relación public.rep_catalogo_informe`

Aunque el servicio tiene fallback para devolver el catálogo base, el paso correcto es crear la tabla para dejar el módulo completo y limpio.

---

## 5. Acción manual requerida

Ejecutar en PostgreSQL el script:

- `SIAD.Data/Migrations/Contabilidad/20260318050000_AddInformesCatalogo.sql`

Después:

1. reiniciar la aplicación
2. abrir `/informes`
3. abrir `/informes/partidas-contabilidad`

---

## 6. Lo que no se ha implementado todavía

Todavía no está hecho:

- administración visual del catálogo dinámico
- exportar resultados desde la consulta
- imprimir desde la consulta
- guardar filtros por usuario
- favoritos
- versionado funcional de informes
- diseñador web de reportes DevExpress

---

## 7. Siguiente paso recomendado

Una vez aplicada la tabla del catálogo, el siguiente paso recomendado es:

- **crear la pantalla de administración del catálogo dinámico de informes**

Orden sugerido:

1. mantenimiento de catálogo de informes
2. habilitar flags y acciones de exportar/imprimir
3. filtros guardados y preferencias
4. evaluar si algunas consultas deben evolucionar luego a reportería DevExpress formal

---

## 8. Resumen ejecutivo

El módulo `Informes` ya dejó de ser solo una idea o un plan.  
Ya existe una primera implementación usable con enfoque de consulta web: catálogo, filtros, resultados y detalle.

El único bloqueo operativo inmediato es aplicar la tabla `rep_catalogo_informe` en la base de datos para que el catálogo dinámico quede persistido.
