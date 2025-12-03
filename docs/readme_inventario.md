# Inventario del sistema legado `koala-outsourcing-apc_siadweb`

Este documento resume el estado funcional y tecnico de la solucion legacy para apoyar la migracion al stack Blazor/DevExpress descrito en `readme.md` y `trabajo.md`. Toda la informacion fue verificada en el repositorio a octubre de 2025.

## Soluciones presentes en el repositorio
- `ASPNET_Core_3/ASPNET_Core_3.csproj`: aplicacion web MVC (net8.0) que concentra negocio, reporting RDLC y autenticacion.
- `DXApplicationAPC/DXApplicationAPC.sln`: solucion objetivo (Blazor Server + librerias `SIAD.*`) en construccion, con reporting DevExpress ya configurado.
- `Reporting/Reporting.csproj`: utilidad de consola netcoreapp3.1 usada unicamente como stub (Hello World).
- Artefactos operativos relevantes: `ASPNET_Core_3/apc.xlsx`, `ASPNET_Core_3/bdnes.sql`, `ASPNET_Core_3/cambios_rolando_APC.sql`, `ASPNET_Core_3/INSERTS/*.sql`, `ASPNET_Core_3/FUNCTIONS/*.sql`.

## Arquitectura actual (`ASPNET_Core_3`)

### Configuracion y dependencias
- `Program.cs` registra `ApplicationDbContext` (Identity) y `bdnesContext` (negocio) con PostgreSQL, AutoMapper, cookies de autenticacion y `UnitOfWork`.
- Paquetes NuGet clave (`ASPNET_Core_3.csproj`): ASP.NET Core Identity/UI, EF Core + Npgsql/EFCore.NamingConventions, Dapper, AutoMapper, AspNetCore.Reporting, ReportViewerCore.NETCore, SendGrid, DocumentFormat.OpenXml.
- `appsettings.json` expone `ConnectionStrings:DefaultConnection` (PostgreSQL) y credenciales SendGrid; requiere externalizarse durante la migracion.
- `Helpers/AutoMapperProfile.cs` mapea entidades y DTO para clientes, cobranza, servicios, proveedores y catalogos.

### Contextos y entidades
- `Data/ApplicationDbContext.cs` hereda de `IdentityDbContext` con convencion snake_case y tablas renombradas; `ApplicationRolePolicies.cs` define claims por rol; `ApplicationUser.cs` extiende identidad base.
- `EntityModels/bdnesContext.cs` publica mas de 60 `DbSet<T>` para clientes, servicios, facturacion, bancos, contabilidad, proveedores y bitacoras; se apoya en configuraciones `OnModelCreating` con precision decimal y mapeos de schema `public`.
- `Migrations/20210714145256_Initial-Migration.cs` y `ApplicationDbContextModelSnapshot.cs` contienen la migracion inicial de identidad.

### Repositorios y servicios compartidos
- `Repositories/Repository.cs` e `IRepository.cs` implementan CRUD generico; `UnitOfWork.cs` agrega mas de 20 repositorios especializados expuestos por `IUnitOfWork.cs`.
- Repositorios especializados (`Repositories/Repositorios/*`) encapsulan logica EF + Dapper para cada dominio (clientes, cobranza, bancos, presupuesto, ordenes, proveedores, servicios, etc.).
- `Helpers/Queries/*.cs` centralizan SQL parametrizado para reportes y operaciones criticas (clientes, bancos, ordenes, proveedores, aplicar transacciones).
- Servicios auxiliares: `Utilities/EmailSender.cs` (SendGrid), `Services/GeneratePassword.cs`, `Utilities/ReflectionPopulator.cs` (mapear `NpgsqlDataReader` a objetos), `Utilities/RoleName.cs` y `Utilities/RoleClaimName.cs`.

### Reporting y recursos estaticos
- RDLC activos en `ASPNET_Core_3/wwwroot/Reportes`: `rptaccioncobranza`, `rptcatalogo`, `RptCatalogoContable`, `RptCortesPorCliente`, `RptPagare`, `RptPlanPago`, `RptReciboPrima`.
- Dataset tipado `ReporteriaDataSet/Reporteria.xsd` mas `Reporteria.Designer.cs` alimenta reportes; `CobranzaController` y `ClientesController` generan PDF con `LocalReport`.
- `wwwroot/lib` contiene dependencias front-end (Bootstrap, jQuery UI, Toastr, Chartist, Moment, SweetAlert, etc.) y assets de plantillas.
- Vistas Razor segmentadas por area (`Areas/*/Views` mas `Views/Dashboards`, `Views/Home`); `Areas/Identity` mantiene el scaffolding de Identity UI.

## Inventario funcional por modulo (ASPNET_Core_3)

### Seguridad y parametrizacion
- `Areas/Admin` (`AccountController`, `UsersController`, `RolsUserController`, `HomeController`): login/logout, perfiles, reseteo de contrasena, gestion de roles/policies y administracion de usuarios (incluye cuentas moviles `UsuariosAppMiOrden`).
- `Areas/Identity` (Razor Pages por defecto): registro/login/confirmaciones autogeneradas por Identify scaffold.
- `Areas/AbogadosArea/AbogadosController`: catalogo de abogados con listado, alta/edicion, desactivacion e integracion a cuentas contables.
- `Areas/CiclosArea/CiclosController`: mantenimiento de ciclos de facturacion (CRUD, desactivacion) con combos para clientes.
- `Areas/ConceptoArea/ConceptoController`: catalogo de conceptos contables/miscelaneos, formulario parcial `_ConceptoForm` y guardado via repositorio.
- `Areas/ServiciosArea/ServiciosController`: catalogo maestro de servicios (agua, alcantarillado, miscelaneos) con activacion/desactivacion y tarifas base.

### Clientes y servicio al usuario
- `Areas/ClientesArea/ClientesController`: ciclo completo de clientes (busqueda, detalle, configuracion tarifaria, creacion/actualizacion, estado de cuenta, historicos de consumo/pago, repositorios EF+Dapper, generacion RDLC `RptCortesPorCliente` y `rptaccioncobranza`).
- `Areas/SolicitudServicioArea/SolicitudServicioController`: gestion de solicitudes de servicio (listar, formularios parciales, creacion/actualizacion y alta rapida de cliente).
- `Areas/MedidorArea/MedidorController`: catalogo de medidores (CRUD, desactivacion) y asociacion a clientes.
- `Areas/AuxiliarLecturaArea/AuxiliarLecturaController`: soporte para lecturas masivas (busquedas, datos por periodo, valores por defecto para cuadrillas).
- `Areas/OrdenesArea/OrdenesController`: ordenes de corte/trabajo (agua y alcantarillado), visualizacion/geolocalizacion (`ObtenerCoordenadas`), asignacion de cuadrillas y creacion de ordenes.
- `Areas/RutasArea/RutasController`: mantenimiento de rutas de lectura/distribucion, formularios y guardado de rutas operativas.

### Facturacion y recaudo
- `Areas/CaptaciondePagosArea/CaptaciondePagosController`: recepcion de pagos (lector optico, manual, miscelaneos), reversos, cabeceras/detalles, consulta de recibos y saldos con reglas por cliente.
- `Areas/FacturacionMiscelaneosArea/FacturacionMiscelaneosController`: emision de recibos miscelaneos, busqueda por codigo, catalogo de categorias y guardado de conceptos adicionales.
- `Areas/NotasCreditoDebitoArea/NotasCreditoDebitoController`: registro de notas de credito/debito con validacion de tasas configuradas por cliente.
- `Areas/CobranzaArea/CobranzaController`: planes de pago, bloqueo de cobranza, calculo de cuotas, conversion numero-a-letras, generacion de reportes `RptPlanPago`, `RptPagare`, `RptReciboPrima`.

### Finanzas y contabilidad
- `Areas/BANCOSAREA` (`BancosController`, `TransaccionesController`): catalogos de bancos/cuentas, tipos de transaccion, asignacion de transacciones a usuarios, mantenimiento con vistas parciales.
- `Areas/OrdenesPagoArea/OrdenPagoController`: compromisos y ordenes de pago, seleccion de codigos presupuestarios, detalle de compromisos e integracion con repositorio de presupuesto.
- `Areas/PresupuestosArea/PresupuestoController`: modulos de presupuesto (carga, edicion, traspasos, proyectos) con vistas `ListaPresupuesto`, `TraspasosIndex` y calculos de estados.
- `Areas/ContabilidadArea/CatalagoController`: catalogo contable, configuracion de estados financieros/balance general, mantenimiento de partidas y centros de costo, generacion PDF del catalogo (`GenerarCatalogoPdf`).
- `Areas/AplicarTransaccionArea/AplicarTransaccionController`: aplicacion de transacciones contables con seleccion de cuentas, correlativos y guardado via repositorio especializado/Dapper.
- `Areas/AreaProveedores/ProveedoresController`: gestion integral de proveedores (autogeneracion de codigo secuencial, categorias, cuentas contables, cambios de estado, formularios modales).

## Capa de datos y repositorios destacados
- `Repositories/Repositorios/Cliente/ClienteRepository.cs`: consultas avanzadas (estado de cuenta, tarifas, historial, reportes) combinando EF Core y Dapper (`Helpers/Queries/ClientesQuery.cs`).
- `Repositories/Repositorios/Cobranza/CobranzaRepository.cs`: calculo y guardado de planes de pago, reportes PDF, conversion numerica y bloqueo de clientes.
- `Repositories/Repositorios/Bancos/BancosRepository.cs` y `TransaccionesRepository.cs`: administracion de catalogos bancarios, cuentas contables y asignaciones por usuario.
- `Repositories/Repositorios/Presupuesto/*`: operaciones sobre cabeceras/detalles presupuestarios, movimientos y traspasos respaldados por SQL directo.
- `Repositories/Repositorios/Ordenes/OrdenesRepository.cs`: creacion/asignacion de ordenes de corte/servicio, integracion con cuadrillas, ubicaciones GPS y seguimiento.
- `Helpers/Queries/OrdenesQuery.cs`, `OrdenesdePagoQuery.cs`, `BancosQuery.cs`, `AplicarTransaccionesQuery.cs`: encapsulan consultas SQL para operaciones criticas de negocio.

## Reporting y scripts de soporte
- RDLC listados arriba se apoyan en `ReporteriaDataSet` y metodos de repositorios para alimentar `LocalReport`.
- Scripts SQL (`INSERTS`, `FUNCTIONS`, `bdnes.sql`, `cambios_rolando_APC.sql`) respaldan carga de datos iniciales, funciones Postgres y cambios operativos; deben evaluarse para migracion a EF Core migrations o procesos ETL.
- Archivo `apc.xlsx` incluye catalogos y plantillas operativas usados por usuarios finales (validar necesidad al migrar UI).

## Estado de la solucion objetivo (`DXApplicationAPC`)
- Estructura multicapa con proyectos `SIAD.Core`, `SIAD.Data`, `SIAD.Services`, `SIAD.Reports` referenciados desde `DXApplicationAPC/DXApplicationAPC.csproj`.
- `DXApplicationAPC/Program.cs` configura DevExpress Reporting (`AddDevExpressServerSideBlazorReportViewer`, `AddDevExpressBlazorReporting`) y registra `SiadDbContext` (PostgreSQL) mas `ReportDbContext` (SQLite) para plantillas DevExpress.
- Servicios personalizados (`Services/CustomReportStorageWebExtension.cs`, `CustomJsonDataConnectionProviderFactory.cs`, `CustomSqlDataConnectionProviderFactory.cs`) habilitan almacenamiento y conexiones de datos para el disenador.
- `Pages/AdminTest.razor` consume `SIAD.Services` para validar lectura de clientes via `/admintest`; el menu (`Shared/NavMenu.razor`) expone ejemplos de DocumentViewer y ReportDesigner.
- Documentacion viva en `readme.md` (plan de trabajo de seis meses) y `trabajo.md` (bitacora Sprint 0); este inventario se alinea con dichas guias para decidir prioridades de migracion.

## Consideraciones para la migracion
- Externalizar secretos (connection strings, SendGrid) usando User Secrets o Azure Key Vault antes de moverlos a la nueva solucion.
- Mapear entidades: `SIAD.Data` ya contiene modelos iniciales (clientes, categorias, barrios, ciclos); completar el traspaso aprovechando `bdnesContext` como referencia.
- Priorizar migracion de modulos segun plan: iniciar con catalogo/seguridad (Admin, Servicios, Ciclos), continuar con clientes/solicitudes, y seguir con facturacion y finanzas.
- Reconvertir reportes RDLC a DevExpress Reports (`SIAD.Reports`) aprovechando `CustomReportStorageWebExtension`; catalogar reportes criticos (`RptPlanPago`, `RptPagare`, `RptCatalogoContable`, `RptCortesPorCliente`).
- Revisar dependencias Dapper/SQL directo y definir equivalentes en EF Core o servicios DevExpress (consultas de clientes, ordenes, bancos).
- Mantener este inventario actualizado conforme se desmonten modulos del legado y se integren a la solucion Blazor.
