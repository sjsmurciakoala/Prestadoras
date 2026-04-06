---
applyTo: "SIAD.Reports/**/*.cs,apc/Controllers/Informes/**/*.cs,apc.Client/Pages/Informes/**/*.razor,apc.Client/Services/Informes/**/*.cs"
---

- Consulta primero la documentacion oficial de DevExpress con `dxdocs` antes de modificar Reporting, Viewer, Designer o `SqlDataSource`.
- Conserva el ciclo catalogo -> dataset -> draft -> published y el alcance por `company_id`.
- La validacion de SQL custom debe seguir permitiendo solo `SELECT` o `WITH ... SELECT`.
- No serialices parametros de conexion dentro del layout persistido; conserva el flujo actual de limpieza de `ConnectionParameters`.
- Si agregas o cambias reportes/datasets, alinea backend, UI y scripts SQL del catalogo de reporteria.
