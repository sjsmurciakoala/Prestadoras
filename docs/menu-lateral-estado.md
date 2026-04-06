# Estado actual del menu lateral (DevExpress)

Este documento describe como esta implementado hoy el menu lateral, su comportamiento y donde esta el codigo relevante.

## Estructura actual

Componentes clave:
- Layout: `apc.Client/Layout/MainLayout.razor`
- Drawer (componente contenedor): `apc.Client/Layout/Drawer.razor`
- Menu lateral: `apc.Client/Layout/NavMenu.razor`
- Estilos del menu: `apc.Client/Layout/NavMenu.razor.css`
- Estilos del layout principal: `apc.Client/Layout/MainLayout.razor.css`
- Estilos del drawer: `apc.Client/Layout/Drawer.razor.css`

## Comportamiento actual

### Desktop (>= 768px)
- El sidebar se renderiza con `DxDrawer` en modo `Shrink` y con `MiniModeEnabled`.
- Cuando el drawer esta abierto, se ve el menu completo (icono + texto).
- Cuando el drawer esta colapsado, se ve solo iconos (mini panel), y los textos se ocultan por CSS.
- El boton de colapsar/expandir (chevron/list) vive en el header del drawer.

### Mobile (< 768px)
- Se usa un drawer externo en modo `Overlap`.
- El boton hamburguesa aparece en el top row (solo movil) para abrir/cerrar.
- El overlay/shading se controla via CSS en `Drawer.razor.css`.

### Submenus
- En modo expandido: los submenus se muestran como acordeon (popup statica, posicion `static`).
- En modo colapsado: los submenus aparecen como flyout/popup (no se ocultan). Se ajustan estilos para que el popup sea una tarjeta blanca con sombra.

### Tooltips en colapsado
- Los tooltips se aplican con `Attributes` (title + aria-label) solo cuando `IsCollapsed` es true.
- Se aplican a items principales y a hijos.

### Estado activo
- `DxMenu` no maneja seleccion activa automaticamente, por eso se calcula una clase activa por ruta.
- Se agregan clases:
  - `menu-item-active` para item hoja.
  - `menu-group-active` para grupos (cuando la ruta actual empieza con un prefijo del grupo).
- Se escucha `NavigationManager.LocationChanged` para refrescar el estado en navegacion.

## Codigo actual relevante

### Layout principal (drawer + toggle)
- `apc.Client/Layout/MainLayout.razor` define el `Drawer`, el top row mobile y los toggles.
- `apc.Client/Layout/MainLayout.razor.css` controla el layout y oculta el top row en desktop.

### Drawer (desktop + mobile)
- `apc.Client/Layout/Drawer.razor` anida dos `DxDrawer`:
  - Drawer externo (mobile, Overlap)
  - Drawer interno (desktop, Shrink + MiniModeEnabled)
- `apc.Client/Layout/Drawer.razor.css` define el panel, header, body, shading y reglas responsive.

### Menu lateral
- `apc.Client/Layout/NavMenu.razor`
  - Usa `DxMenu` vertical con secciones.
  - Agrega `CssClass` dinamico por ruta (activo) y `Attributes` para tooltips en colapsado.
  - Logica de rutas: `ActiveClass`, `GroupClass`, `NormalizePath`.

### Estilos del menu
- `apc.Client/Layout/NavMenu.razor.css`
  - Define apariencia general del menu.
  - Define estado colapsado (oculta textos, centra iconos).
  - Define estado activo (`menu-item-active`, `menu-group-active`).
  - Define popup de submenus en colapsado (flyout con tarjeta blanca).

## Notas y limitaciones actuales

- Los submenus en colapsado se abren con click (comportamiento default de DevExpress). Si quieres hover, hay que ajustar.
- No hay persistencia del estado colapsado en localStorage.
- No se usa `DxLayoutBreakpoint` para cambiar automaticamente de `Shrink` a `Overlap` segun el tamano (se hace por CSS y drawer externo/interno).
- Accesibilidad avanzada (aria-expanded en submenus) no esta implementada mas alla de `aria-label` en colapsado.

## Ubicacion de los iconos

- Los iconos son clases Bootstrap Icons (`bi ...`) configuradas en los `DxMenuItem` dentro de `apc.Client/Layout/NavMenu.razor`.
- Ejemplo:
  - `IconCssClass="bi bi-house"` para Home
  - `IconCssClass="bi bi-people"` para Clientes

Si quieres, puedo agregar secciones mas detalladas o diagramas de flujo.
