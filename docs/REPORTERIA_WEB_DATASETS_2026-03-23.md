# Reporteria Web y Datasets Administrables

**Fecha**: 2026-03-23  
**Estado**: implementado y operativo en desarrollo  
**Alcance**: catalogo de reportes web, versionado `DRAFT/PUBLISHED`, catalogo CRUD de datasets, preview de datasets, diseno/publicacion desde navegador y soporte para `STORED_PROCEDURE`, `VIEW` y `SQL`.

**Nota 2026-03-25**: la ruta legacy `OBJECT` fue retirada del runtime. El dataset semilla `bancos-transacciones` fue migrado a PostgreSQL como `public.rep_bancos_transacciones`, y la reporteria nueva queda DB-first sobre catalogo + layouts persistidos.

---

## 1. Objetivo

La meta del modulo es separar dos responsabilidades:

- el **layout** del reporte, que debe poder editarse/publicarse sin redeploy;
- la **fuente de datos**, que debe poder registrarse y reutilizarse con control operacional y de seguridad.

Esto permite:

1. crear o ajustar layouts desde web;
2. publicar nuevas versiones sin recompilar la aplicacion;
3. crear nuevos datasets para reportes especiales sin tener que tocar C# en todos los casos;
4. mantener la reporteria nueva sobre PostgreSQL y catalogo DB, sin depender de `ObjectDataSource`.

---

## 2. Estado actual implementado

Quedo implementado el flujo completo de reporteria web administrable:

- catalogo de reportes web en `/informes/reportes`;
- diseniador web en `/informes/reportes/{codigo}/designer`;
- visor web en `/informes/reportes/{codigo}/viewer`;
- catalogo de datasets en `/informes/reportes/datasets`;
- persistencia de layouts versionados en `public.rep_reporte_layout`;
- catalogo de datasets en `public.rep_catalogo_dataset`;
- catalogo de parametros por dataset en `public.rep_dataset_parametro`;
- creacion, edicion, eliminacion y preview de datasets;
- creacion y publicacion de reportes web;
- deteccion de desfase entre dataset actual y layout base del diseniador;
- regeneracion controlada de `DRAFT` desde el dataset actual sin tocar la publicacion vigente;
- regla de seguridad para `SQL` solo para `Admin` y `SuperAdministrador`.

Tambien quedo sembrado el dataset base:

- `bancos-transacciones` como `OBJECT`

y el reporte semilla:

- `bancos-transacciones` como `REPORT`

---

## 3. Arquitectura actual

### 3.1 Catalogo de reportes

`public.rep_catalogo_informe` sigue siendo el catalogo principal del modulo `Informes`.

Para reporteria web se usan filas con:

- `tipo_origen = 'REPORT'`
- `ruta = /informes/reportes/{codigo}/viewer`
- `consulta_clave = codigo del dataset`

La relacion reporte-dataset es indirecta:

- `rep_catalogo_informe.consulta_clave`
- apunta a
- `rep_catalogo_dataset.codigo`

### 3.2 Catalogo de datasets

`public.rep_catalogo_dataset` define la fuente de datos reutilizable y `public.rep_dataset_parametro` define sus parametros.

Tipos soportados hoy:

- `OBJECT`
- `STORED_PROCEDURE`
- `VIEW`
- `SQL`

### 3.3 Plantillas y layouts persistidos

El flujo en tiempo de ejecucion es este:

1. el usuario abre un reporte en visor o diseniador;
2. `CompanyReportStorageWebExtension` resuelve el reporte por codigo;
3. si existe `layout_xml` persistido en `rep_reporte_layout`, ese XML gana;
4. si no existe layout persistido, `ReportTemplateFactory` genera una plantilla base usando el dataset actual;
5. al guardar desde el diseniador, se crea o actualiza el `DRAFT`;
6. al publicar, el `DRAFT` pasa a `PUBLISHED` y la publicacion anterior pasa a `ARCHIVED`.

Este punto es critico:

- **editar un dataset no reescribe layouts ya guardados**;
- solo afecta plantillas nuevas o reportes sin layout persistido.

Para cerrar ese hueco ahora existe una salida operativa:

- el detalle del reporte detecta si el dataset cambio despues del layout que abre el diseniador;
- si hay desfase, la UI avisa con fechas UTC exactas;
- el usuario puede ejecutar `Regenerar borrador`;
- esa accion crea o sobreescribe solo el `DRAFT` usando la plantilla actual del dataset;
- la version `PUBLISHED` no se toca.

### 3.4 Integracion DevExpress

Desde el ajuste de capas del `2026-03-25`:

- la infraestructura y servicios de reporteria viven en `Prestadoras/SIAD.Reports`;
- `Prestadoras/apc/Program.cs` conserva solo la composicion ASP.NET/DevExpress propia del host.

La aplicacion servidor registra:

- `AddDevExpressBlazorReporting()`
- `ConfigureReportingServices(...)`
- `ReportStorageWebExtension -> CompanyReportStorageWebExtension`
- `ReportTemplateFactory`

El `ObjectDataSource` expuesto al wizard sigue siendo solo:

- `BancosTransaccionesReportDataSource`

Los datasets `STORED_PROCEDURE`, `VIEW` y `SQL` no dependen del wizard para nacer: la plantilla se crea ya enlazada a un `SqlDataSource` y se ejecuta `RebuildResultSchema()` para poblar el `Field List`.

---

## 4. Scripts y base de datos

### 4.1 Layouts versionados

Script:

- [2026-03-21_add_rep_reporte_layout.sql](../Database/2026-03-21_add_rep_reporte_layout.sql)

Objetos creados:

- `public.rep_reporte_layout`
- indice unico de draft por reporte
- indice unico de publicado por reporte

Estados soportados:

- `DRAFT`
- `PUBLISHED`
- `ARCHIVED`

### 4.2 Catalogo de datasets

Script:

- [2026-03-21_add_rep_catalogo_dataset.sql](../Database/2026-03-21_add_rep_catalogo_dataset.sql)

Objetos creados:

- `public.rep_catalogo_dataset`
- `public.rep_dataset_parametro`

Restricciones relevantes:

- `tipo_origen IN ('OBJECT', 'STORED_PROCEDURE', 'VIEW', 'SQL')`
- `tipo_dato IN ('TEXT', 'INT64', 'DECIMAL', 'DATE', 'DATETIME', 'BOOLEAN')`
- `fuente_valor IN ('REPORT', 'CURRENT_COMPANY', 'FIXED')`
- nombre de parametro unico por dataset
- orden unico por dataset

El mismo script:

- siembra `bancos-transacciones` como dataset `OBJECT`;
- actualiza `rep_catalogo_informe.consulta_clave` del reporte `bancos-transacciones`.

### 4.3 Script de remediacion para layout persistido roto

Script:

- [2026-03-21_reset_layout_reporte_20260321165750.sql](../Database/2026-03-21_reset_layout_reporte_20260321165750.sql)

Uso:

- borrar layouts persistidos de un reporte especifico para forzar regeneracion desde la plantilla actual;
- cambiar `company_id` antes de ejecutarlo.

---

## 5. Endpoints y rutas

### 5.1 Reportes

API:

- `GET /api/informes/reportes`
- `GET /api/informes/reportes/{codigo}`
- `POST /api/informes/reportes`
- `POST /api/informes/reportes/{codigo}/publicar`
- `POST /api/informes/reportes/{codigo}/regenerar-borrador`

UI:

- `/informes/reportes`
- `/informes/reportes/{codigo}/designer`
- `/informes/reportes/{codigo}/viewer`

### 5.2 Datasets

API:

- `GET /api/informes/reportes/datasets`
- `GET /api/informes/reportes/datasets/{codigo}`
- `POST /api/informes/reportes/datasets`
- `PUT /api/informes/reportes/datasets/{codigo}`
- `DELETE /api/informes/reportes/datasets/{codigo}`
- `POST /api/informes/reportes/datasets/{codigo}/probar`

UI:

- `/informes/reportes/datasets`

---

## 6. Tipos de dataset y uso recomendado

### 6.1 `OBJECT`

Uso:

- logica de negocio compleja;
- integracion fuerte con servicios del dominio;
- casos donde no conviene exponer SQL o SP.

Ejemplo actual:

- `bancos-transacciones`

Restricciones actuales:

- no se puede editar ni eliminar desde la UI;
- no tiene preview implementado desde el catalogo;
- sigue dependiendo de codigo C#.

### 6.2 `STORED_PROCEDURE`

Uso recomendado:

- primera opcion para reportes especiales multiempresa;
- datasets nuevos sin redeploy de la app;
- contratos de datos controlados por BD.

Detalle importante de la implementacion actual:

- el preview en PostgreSQL espera una funcion que retorne filas (`RETURNS TABLE` o `SETOF`);
- si se usa una rutina que no devuelve filas, el preview va a fallar.

### 6.3 `VIEW`

Uso recomendado:

- consultas simples;
- catalogos o datasets de lectura estables;
- escenarios donde no se necesita logica procedural.

Limitacion actual:

- el preview funciona;
- pero los parametros definidos en el dataset no se inyectan hoy dentro del `SELECT * FROM vista`.

### 6.4 `SQL`

Uso recomendado:

- solo para usuarios tecnicos admin;
- solo cuando `VIEW` o `STORED_PROCEDURE` no resuelven el caso con costo razonable.

Reglas actuales:

- solo `Admin` y `SuperAdministrador`;
- debe iniciar con `SELECT` o `WITH`;
- se bloquean `;`, `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `CREATE`, `GRANT`, `REVOKE`, `CALL`, `DO`, `COPY`;
- solo una sentencia;
- solo lectura.

Orden recomendado de uso:

1. `STORED_PROCEDURE`
2. `VIEW`
3. `OBJECT`
4. `SQL` solo cuando lo apruebe operacion tecnica

---

## 7. Seguridad y permisos

### 7.1 Permisos funcionales

El modulo usa permisos de `Reporteria` para ver, crear, editar y eliminar.

En particular:

- ver datasets/reportes requiere permisos de lectura del modulo;
- crear datasets/reportes requiere permiso `Create`;
- editar requiere `Edit`;
- eliminar dataset requiere `Delete`.

### 7.2 Restriccion adicional para SQL

Ademas de los permisos del modulo, `SQL` tiene una regla adicional:

- solo `Admin` o `SuperAdministrador` pueden registrar o probar datasets SQL.

Motivo:

- DevExpress documenta que el uso de `Custom SQL Query` en web necesita validacion adicional y principios de minimo privilegio en BD;
- por eso la solucion no deja SQL libre para usuarios finales.

### 7.3 Recomendacion operativa para produccion

Aunque el control funcional ya existe, para produccion sigue siendo recomendable:

- usar una conexion de reporteria `readonly`;
- poner timeout de ejecucion;
- auditar quien crea/edita/ejecuta datasets SQL;
- limitar esquemas o vistas permitidas si se habilita SQL en ambientes productivos.

---

## 8. Flujo operativo recomendado

### 8.1 Crear un dataset

1. entrar a `/informes/reportes/datasets`;
2. crear dataset con tipo `STORED_PROCEDURE`, `VIEW` o `SQL` si el usuario es admin;
3. definir parametros:
   - `REPORT` para parametros visibles/operables desde reporte;
   - `CURRENT_COMPANY` para aislamiento por empresa;
   - `FIXED` para valores internos fijos;
4. ejecutar preview;
5. corregir hasta que devuelva columnas y filas validas.

### 8.2 Crear un reporte

1. entrar a `/informes/reportes`;
2. crear el reporte;
3. asignar `DatasetCodigo`;
4. abrir el diseniador;
5. guardar para crear el `DRAFT`;
6. publicar cuando quede validado.

### 8.3 Editar un reporte existente

1. abrir `/informes/reportes/{codigo}/designer`;
2. si no hay borrador:
   - si existe publicado, el diseniador abre la ultima version publicada;
   - si no existe publicado, abre una plantilla base;
3. guardar para crear o actualizar el `DRAFT`;
4. publicar para mover el draft a `PUBLISHED`.

### 8.4 Cambiar un dataset ya usado

1. editar el dataset;
2. probarlo de nuevo;
3. revisar si los reportes que ya tienen `layout_xml` guardado necesitan regeneracion o re-guardado;
4. si el dataset cambio despues del layout base, usar `Regenerar borrador` desde el diseniador;
5. si un layout viejo quedo incompatible, resetear el layout persistido y volver a abrir el diseniador.

---

## 9. Validaciones y comportamiento actual

### 9.1 Validaciones de datasets

El servicio valida:

- codigo de dataset con letras, numeros, guion y guion bajo;
- nombre obligatorio;
- tipo de dataset valido;
- `origen_clave` valido para `STORED_PROCEDURE` y `VIEW`;
- nombre de parametro valido;
- parametros sin nombres duplicados;
- parametros sin orden duplicado;
- tipo de dato valido;
- fuente de valor valida;
- `FIXED` debe traer `valor_default`.

### 9.2 Preview

El preview devuelve:

- hasta `50` filas;
- nombres de columnas;
- filas serializadas como diccionario `columna -> valor`.

Comportamiento por tipo:

- `OBJECT`: no soportado actualmente;
- `STORED_PROCEDURE`: ejecuta la funcion/rutina y limita a 50;
- `VIEW`: `SELECT * FROM vista LIMIT 50`;
- `SQL`: `SELECT * FROM (<sql>) dataset_preview LIMIT 50`.

### 9.3 Eliminacion

Un dataset no puede eliminarse si esta asignado a reportes existentes.

La eliminacion se bloquea indicando cuantos reportes lo estan usando.

---

## 10. Limitaciones conocidas

1. Editar un dataset no actualiza automaticamente layouts ya persistidos.
2. Ahora existe regeneracion controlada del `DRAFT`, pero no rehidratacion automatica del XML persistido.
3. El preview de `OBJECT` no esta implementado.
4. El preview de `STORED_PROCEDURE` en PostgreSQL asume una funcion que retorna filas, no un procedure sin resultset.
5. Los parametros de datasets `VIEW` no se inyectan todavia en la consulta base.
6. Persisten casos de layouts viejos que disparan errores de deserializacion de DevExpress; cuando eso ocurre, hay que resetear el layout XML persistido.

---

## 11. Archivos clave de la implementacion

Backend:

- `Prestadoras/apc/Program.cs`
- `Prestadoras/apc/Controllers/Informes/ReportesDisenoController.cs`
- `Prestadoras/apc/Controllers/Informes/ReportesDatasetsController.cs`
- `Prestadoras/SIAD.Reports/Reporting/CompanyReportStorageWebExtension.cs`
- `Prestadoras/SIAD.Reports/Reporting/ReportTemplateFactory.cs`
- `Prestadoras/SIAD.Reports/Reporting/CompanyObjectDataSourceWizardTypeProvider.cs`
- `Prestadoras/SIAD.Reports/Informes/ReportesDisenoService.cs`
- `Prestadoras/SIAD.Reports/Informes/ReportesDatasetService.cs`

Cliente:

- `Prestadoras/apc.Client/Pages/Informes/ReporteDesigner.razor`
- `Prestadoras/apc.Client/Pages/Informes/ReportesDatasets.razor`
- `Prestadoras/apc.Client/Services/Informes/InformesClient.cs`

DTOs:

- `Prestadoras/SIAD.Core/DTOs/Informes/ReporteDisenoDto.cs`
- `Prestadoras/SIAD.Core/DTOs/Informes/ReporteDatasetDto.cs`

Base de datos:

- `Prestadoras/Database/2026-03-21_add_rep_reporte_layout.sql`
- `Prestadoras/Database/2026-03-21_add_rep_catalogo_dataset.sql`
- `Prestadoras/Database/2026-03-21_reset_layout_reporte_20260321165750.sql`

---

## 12. Fuentes DevExpress consultadas

Estas referencias se usaron para alinear la implementacion con el modelo soportado por DevExpress:

- Bind a Report to a Stored Procedure: https://docs.devexpress.com/XtraReports/403320
- Use Query Parameters: https://docs.devexpress.com/XtraReports/17387
- Custom SQL Query in the Report Designer for Web: https://docs.devexpress.com/XtraReports/115116

Puntos aplicados desde esas referencias:

- uso de `SqlDataSource` con `StoredProcQuery` / `CustomSqlQuery`;
- mapeo de parametros de consulta desde parametros del reporte;
- `RebuildResultSchema()` para poblar metadatos del dataset;
- endurecimiento de seguridad para `SQL` en web en lugar de permitir SQL libre a cualquier usuario.
