# Normativa: vistas con grid (DxGrid)

Toda vista del portal que muestre una lista en `DxGrid` sigue este estándar. La
referencia canónica es el maestro de clientes:
[`apc.Client/Pages/Clientes/ClientesList.razor`](../../../../apc.Client/Pages/Clientes/ClientesList.razor).

El módulo **Almacén** está migrado completo y sirve de ejemplo secundario.

---

## 1. Estructura de la página

```razor
<div class="page-container">

    <div class="header-section">
        <div>
            <h2 class="page-title">Directorio de Clientes</h2>
            <p class="page-subtitle">Gestión centralizada de cartera y expedientes</p>
        </div>
        <DxButton Text="Nuevo Cliente" IconCssClass="bi bi-plus-lg"
                  RenderStyle="ButtonRenderStyle.Primary" CssClass="btn-modern" Click="Nuevo" />
    </div>

    <div class="modern-card position-relative">
        <DxLoadingPanel Visible="isLoading" IsContentBlocked="true" Text="Cargando..." />
        <div class="grid-wrapper">
            <DxGrid ... />
        </div>
    </div>

</div>
```

Las vistas que muestran KPIs los ponen entre el header y el card del grid, como
`modern-card summary-card` (ver `Almacen/RequisicionesList.razor`).

## 2. El grid

```razor
<DxGrid @ref="grid" Data="@datos" KeyFieldName="Id" PageSize="15"
        PageSizeSelectorVisible="true"
        PageSizeSelectorItems="new int[] { 5, 10, 15, 25, 50, 100 }"
        PagerPosition="GridPagerPosition.Bottom"
        ColumnResizeMode="GridColumnResizeMode.ColumnsContainer"
        ShowGroupPanel="false" CssClass="grid-solicitudes"
        LayoutAutoSaving="OnLayoutAutoSaving"
        LayoutAutoLoading="OnLayoutAutoLoading">

    <ToolbarTemplate>
        <div class="d-flex flex-wrap gap-2 align-items-center w-100">
            @* Filtros y acciones propios de la pantalla, a la izquierda. *@

            <DxButton Text="Columnas" IconCssClass="bi bi-layout-three-columns"
                      RenderStyle="ButtonRenderStyle.Secondary" Click="MostrarColumnChooser" CssClass="me-1" />

            <div class="ms-auto text-muted small">
                Total: <span class="fw-bold">@total.ToString("N0")</span>
            </div>
        </div>
    </ToolbarTemplate>

    <Columns>@* ... *@</Columns>

    <EmptyDataAreaTemplate>
        <div class="p-4 text-center text-muted">No hay registros para mostrar.</div>
    </EmptyDataAreaTemplate>
</DxGrid>
```

Obligatorio en todo grid:

| Elemento | Regla |
|---|---|
| `PageSize` | `15`. Excepción: master-detail (Presupuesto) usa `5`. |
| Selector de página | `PageSizeSelectorVisible` + items `{5,10,15,25,50,100}` |
| Pager | `PagerPosition="GridPagerPosition.Bottom"` |
| Redimensionado | `ColumnResizeMode="GridColumnResizeMode.ColumnsContainer"` |
| Apariencia | `CssClass="grid-solicitudes"` |
| Column chooser | Botón "Columnas" en el `ToolbarTemplate` → `grid.ShowColumnChooser()` |
| Contador | A la derecha con `ms-auto`, formato `N0` |
| Vacío | `EmptyDataAreaTemplate` con mensaje propio de la pantalla |
| Persistencia | `LayoutAutoSaving` / `LayoutAutoLoading` |

`ShowAllRows="true"` es **incompatible** con la paginación: DevExpress ignora
`PageSize` y oculta el pager. No usarlo (ver
[Paging in Blazor Grid](https://docs.devexpress.com/Blazor/404474)).

## 3. Persistencia de layout

Guarda ancho/orden/visibilidad de columnas y ordenamiento en `localStorage`. Usa el
helper [`apc.Client/Services/GridLayoutStorage.cs`](../../../../apc.Client/Services/GridLayoutStorage.cs):

```razor
@using apc.Client.Services
@inject IJSRuntime JS

@code {
    private DxGrid? grid;

    private const string GridLayoutStorageKey = "<modulo>-<entidad>-grid-layout";

    private Task OnLayoutAutoSaving(GridPersistentLayoutEventArgs e)
        => GridLayoutStorage.SaveAsync(JS, GridLayoutStorageKey, e);

    private Task OnLayoutAutoLoading(GridPersistentLayoutEventArgs e)
        => GridLayoutStorage.LoadAsync(JS, GridLayoutStorageKey, e);

    private void MostrarColumnChooser() => grid?.ShowColumnChooser();
}
```

La clave es única por grid y en kebab-case: `almacen-articulos-grid-layout`,
`clientes-list-grid-layout`.

**Columnas condicionales:** si el grid declara columnas con `@if` (p. ej. una columna
"Estado" que sólo existe en edición), la colección de columnas cambia entre modos y
DevExpress descarta el layout guardado al no coincidir. Usa una clave por modo:

```csharp
private string GridLayoutStorageKey => EnMemoria
    ? "almacen-articulo-proveedores-alta-grid-layout"
    : "almacen-articulo-proveedores-grid-layout";
```

## 4. CSS — no copiar, heredar

Las clases del estándar (`page-container`, `header-section`, `page-title`,
`page-subtitle`, `modern-card`, `summary-card`, `btn-modern`, `btn-icon` + variantes,
`badge-status`, `filters-*`, `grid-wrapper`, `grid-solicitudes`) están definidas **una
sola vez** en [`apc/wwwroot/css/siad-grid.css`](../../../../apc/wwwroot/css/siad-grid.css),
cargado globalmente desde `apc/Components/App.razor`.

- **No** copies ese bloque a un `.razor.css`. Una vista nueva no necesita archivo CSS
  para verse bien: hereda el estándar.
- El `.razor.css` de una página es sólo para lo que le es **propio** (celdas compuestas,
  KPIs específicos, colores de una insignia de dominio).
- Si necesitas pisar el estándar en una página concreta, hazlo desde su `.razor.css`: el
  CSS aislado de Blazor añade `[b-xxxxx]` al selector y gana por especificidad. Por eso
  `siad-grid.css` usa `!important` sólo contra los estilos internos de DevExpress
  (`.dxg-*`, `.dxbs-*`), nunca en las clases propias.
- Dentro de un `.razor.css`, los descendientes de un componente DevExpress necesitan
  `::deep`. En `siad-grid.css` (global) no, porque no hay aislamiento.

`grid-solicitudes` es un nombre heredado de la pantalla Solicitudes, donde nació el
estilo. Se conserva porque lo usan todos los grids del portal.

## 5. Excepciones aceptadas

- **Etiqueta del contador.** El estándar es `Total: N`. Si las filas no son la entidad
  del título, usa la etiqueta precisa: `Almacen/RequisicionesList.razor` muestra
  `Líneas: N` porque cada fila es una línea y varias comparten número de requisición —
  "Total" haría creer que hay N requisiciones.
- **Grids embebidos** (tabs, paneles dentro de una ficha) llevan el estándar completo,
  pero su `ToolbarTemplate` sólo trae "Columnas" y el contador: los filtros y el botón de
  alta viven en el encabezado del panel. Ver `Almacen/ArticuloProveedoresTab.razor`.
- **Master-detail** (`Presupuesto/PresupuestoConfiguracionesList`): el grid de detalle no
  lleva el estándar y el maestro usa `PageSize="5"`.

## 6. Fuera de alcance

Por decisión del usuario, **no** se aplica esta normativa a los módulos **Contabilidad**
y **Facturación**, ni a `Tarifario/CaiOffline`, `Mantenimientos/AjustesTarifarios`,
`Rutas/RutasList` y `Ciclos/CiclosList`. Al tocar esas pantallas por otros motivos, no
las migres.

## 7. Checklist

- [ ] `@using apc.Client.Services` + `@inject IJSRuntime JS`
- [ ] Estructura `page-container` → `header-section` → `modern-card` → `grid-wrapper`
- [ ] `@ref="grid"` y campo `private DxGrid? grid;`
- [ ] `PageSize`, selector de página, pager abajo, `ColumnResizeMode`
- [ ] `CssClass="grid-solicitudes"`
- [ ] `LayoutAutoSaving` / `LayoutAutoLoading` + clave única
- [ ] Botón "Columnas" y contador en el `ToolbarTemplate`
- [ ] `EmptyDataAreaTemplate`
- [ ] Sin `.razor.css` duplicando el estándar
- [ ] `dotnet build apc/apc.csproj` → 0 errores
