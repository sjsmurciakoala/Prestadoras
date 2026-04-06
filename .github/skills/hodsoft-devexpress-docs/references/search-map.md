# DevExpress Search Map

## Versiones del repo

- `.NET 9`
- `DevExpress.Blazor 25.1.7`
- `DevExpress.AspNetCore.Reporting 25.1.7`
- `DevExpress.Reporting.Core 25.1.7`

## Namespaces y areas que aparecen en el codigo

- UI Blazor: `DevExpress.Blazor`
- PDF Viewer: `DevExpress.Blazor.PdfViewer`
- Reporting en Blazor: `DevExpress.Blazor.Reporting`
- Data access/reporting: `DevExpress.DataAccess.Sql`, `DevExpress.DataAccess.Web`, `DevExpress.DataAccess.Wizard.Services`
- Reportes: `DevExpress.XtraReports.UI`, `DevExpress.XtraReports.Web.Extensions`

## Componentes y servicios locales mas comunes

- `DxGrid`
- `DxFormLayout`
- `DxPopup`
- `DxComboBox`
- `DxDateEdit`
- `DxCheckBox`
- `DxButton`
- `DxLoadingPanel`
- `DxAlert`
- `DxMessageBox`
- `ReportStorageWebExtension`
- `ICustomQueryValidator`
- `SqlDataSource`
- `XtraReport`

## Archivos locales utiles para comparar implementaciones

- `apc/Program.cs`
- `apc.Client/CommonServices.cs`
- `apc.Client/Pages/Contabilidad/TransaccionesBancarias.razor`
- `apc.Client/Pages/Informes/ReporteDesigner.razor`
- `apc.Client/Pages/Informes/ReporteViewer.razor`
- `SIAD.Reports/Reporting/CompanyReportStorageWebExtension.cs`
- `SIAD.Reports/Reporting/ReportingCustomSqlValidation.cs`

## Regla practica

Si el cambio depende de comportamiento concreto de un componente o servicio DevExpress, consulta `dxdocs` primero y usa el codigo local solo para aterrizar el patron dentro del proyecto.
