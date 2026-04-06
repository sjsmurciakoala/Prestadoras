# UI Project Map

## Rutas principales

- `apc.Client/Pages/<Modulo>/`: pantallas y formularios.
- `apc.Client/Services/<Modulo>/`: clientes HTTP por modulo.
- `apc.Client/Layout/`: sidebar, menu, layout y definiciones de navegacion.
- `apc.Client/CommonServices.cs`: registro DI de clientes y servicios de UI.
- `apc.Client/Services/HttpClientExtensions.cs`: helpers de autenticacion y errores.

## Patrones para copiar

- Cliente HTTP con manejo auth/error:
  - `apc.Client/Services/Bancos/BanTransaccionesClient.cs`
  - `apc.Client/Services/Informes/InformesClient.cs`
- Pagina DevExpress con filtros, grid y popups:
  - `apc.Client/Pages/Contabilidad/TransaccionesBancarias.razor`
- Estado tenant-aware:
  - `apc.Client/Services/Tenant/TenantState.cs`

## Checklist rapido para una pantalla nueva

1. Crear o extender DTOs compartidos si hacen falta.
2. Crear o extender cliente HTTP en `apc.Client/Services/<Modulo>/`.
3. Registrar el cliente en `CommonServices.cs`.
4. Crear pagina Razor y archivo CSS si el modulo lo requiere.
5. Resolver empresa actual antes de cargar datos tenant-aware.
6. Usar componentes DevExpress compatibles con el patron del modulo.

## Regla de DevExpress

Si la solucion depende de un detalle concreto de `DxGrid`, `DxPopup`, editores, viewer o reporting controls, consulta primero la documentacion oficial con `dxdocs`.
