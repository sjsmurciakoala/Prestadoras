---
applyTo: "apc.Client/**/*.razor,apc.Client/**/*.razor.cs,apc.Client/**/*.razor.css,apc.Client/Services/**/*.cs,apc/Components/**/*.razor,apc/Components/**/*.cs"
---

- Antes de cambiar componentes `Dx*`, PDF Viewer o controles DevExpress, consulta primero la documentacion oficial por MCP `dxdocs`.
- Manten el patron UI -> HTTP client -> API; no pongas acceso directo a BD ni reglas de negocio complejas en la pagina Razor.
- Para pantallas por empresa, usa `TenantState.EnsureCompanyAsync()` o el flujo tenant existente antes de cargar datos.
- Registra nuevos servicios HTTP en `apc.Client/CommonServices.cs`.
- Para errores/autenticacion, reutiliza `apc.Client/Services/HttpClientExtensions.cs`.
- Conserva la separacion existente entre `.razor`, `.razor.cs`, `.razor.css` y clases auxiliares de grid cuando el modulo ya la usa.
