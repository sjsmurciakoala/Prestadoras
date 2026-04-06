# Guía completa: editar y crear reportes DevExpress desde la web

Esta guía está pensada para tu solución actual (`Prestadoras/apc` + `Prestadoras/apc.Client`) y para tu objetivo:

- **Diseñar/editar reportes desde navegador**.
- **Modificar reportes luego de publicar**.
- **Crear reportes nuevos sin recompilar toda la app**.

Actualización 2026-03-18:

- El trabajo actual de la branch no arrancó por `Report Viewer` clásico.
- Se priorizó el módulo `Informes` como consultas web con filtros, resultados en pantalla y catálogo dinámico.
- El avance implementado y el siguiente paso operativo quedaron documentados en `docs/INFORMES_WEB_ESTADO_AVANCE_2026-03-18.md`.

Actualizacion 2026-03-23:

- La base de reporteria web ya quedo implementada.
- Ya existe catalogo de reportes web, catalogo de datasets, versionado `DRAFT/PUBLISHED`, diseniador web, publicacion y preview de datasets.
- El estado actual, reglas operativas, limitaciones y scripts de soporte quedaron documentados en `docs/REPORTERIA_WEB_DATASETS_2026-03-23.md`.

Nota:

- Este documento sigue siendo util como guia conceptual y plan historico.
- Para operar o extender la implementacion actual, tomar como referencia principal `docs/REPORTERIA_WEB_DATASETS_2026-03-23.md`.

---

## 1) Respuesta corta a tu pregunta

Sí, **tu idea es correcta**: con DevExpress Reporting puedes:

1. Publicar la aplicación una sola vez.
2. Permitir que usuarios autorizados creen o editen reportes en la web.
3. Guardar los layouts (`.repx`) en base de datos o almacenamiento de archivos.
4. Mostrar esos reportes inmediatamente en el visor web.

> En la práctica, el reporte se vuelve “contenido administrable” (layout + parámetros + datasource), no código fijo.

---

## 2) Qué hace cada componente de DevExpress Reporting

- **Web Report Designer**: editor visual web para construir/editar reportes.
- **Document Viewer**: vista previa, paginación, exportación (PDF/Excel/etc.).
- **Report Storage**: capa para guardar/cargar layouts por URL/ID (archivo o DB).
- **Data Source Wizard**: asistente para conectar datasets (SQL, ObjectDataSource, etc.).

Flujo típico:

1. Usuario abre diseñador web.
2. Selecciona un reporte existente o “Nuevo”.
3. Edita bandas, campos, expresiones, formatos.
4. Guarda.
5. Otro usuario abre el viewer y consume la versión guardada.

---

## 3) Diagnóstico rápido de tu solución actual

Hoy tu app ya usa DevExpress en Blazor y PDF Viewer, pero **no** tiene paquetes/configuración de Reporting web todavía.

- En `apc.csproj` tienes `DevExpress.Blazor` y `DevExpress.Blazor.PdfViewer`.
- En `Program.cs` se registra `AddDevExpressServerSideBlazorPdfViewer()`.

Eso significa que estás cerca en stack tecnológico, pero te falta el bloque específico de reporting (designer/viewer + storage).

---

## 4) Arquitectura recomendada para tu caso

### Opción recomendada (producción):

- **Diseño/visualización en servidor (`Prestadoras/apc`)**.
- **Persistencia de layouts en DB** (por empresa/tenant y con versionado).
- **Permisos por rol**:
  - `Reportes.Disenar` (crear/editar/publicar)
  - `Reportes.Ver` (solo visualizar/exportar)

### ¿Por qué así?

- Evitas recompilar para cambios de layout.
- Centralizas seguridad y auditoría.
- Puedes versionar y revertir reportes.

---

## 5) Implementación paso a paso

## Paso 1: agregar paquetes NuGet de Reporting en `Prestadoras/apc`

Instala los paquetes de DevExpress Reporting para ASP.NET Core/Blazor (según la modalidad elegida en tu UI). Revisa versión compatible con tu rama 25.1.

Mínimo práctico:

- paquete base de reporting web para ASP.NET Core
- paquete de componentes Blazor de reporting (si usarás componentes Blazor)
- dependencias de export/rendering recomendadas por DevExpress

> Mantén las versiones alineadas con `25.1.*-*` para evitar incompatibilidades.

## Paso 2: registrar servicios de reporting en `Program.cs`

Además de tus registros actuales, agrega:

- `AddDevExpressControls()`
- configuración de servicios de reporting (`ConfigureReportingServices`)
- registro de tu implementación de `ReportStorageWebExtension`
- reglas de autorización para diseñador/viewer

Y en pipeline:

- `UseDevExpressControls()`
- mapeo de controladores/endpoints de reporting

## Paso 3: crear almacenamiento de reportes (DB recomendado)

Crea una tabla como:

- `report_definition`
  - `id`
  - `tenant_id`
  - `report_code` (único por tenant)
  - `display_name`
  - `layout_xml` (texto/XML del REPX)
  - `is_active`
  - `version`
  - `updated_by`
  - `updated_at`

Implementa una clase derivada de `ReportStorageWebExtension` para:

- `GetData(url)` → devuelve bytes del layout.
- `SetData(report, url)` → actualiza layout existente.
- `SetNewData(report, defaultUrl)` → crea layout nuevo.
- `IsValidUrl(url)` / `CanSetData(url)` → controles de seguridad.

## Paso 4: crear páginas de administración

En tu módulo web:

- **Lista de reportes** (filtros por empresa/módulo).
- **Botón “Diseñar”** abre diseñador.
- **Botón “Vista previa”** abre document viewer.
- **Botón “Duplicar versión”** para cambios controlados.

## Paso 5: seguridad y gobernanza (clave)

- Solo roles autorizados pueden abrir diseñador.
- Viewer puede ser más abierto (lectura).
- Audita quién cambió qué layout y cuándo.
- No expongas conexión SQL libre en el wizard a usuarios finales.
- Usa datasets/consultas preaprobadas para evitar fuga de datos.

## Paso 6: estrategia de publicación

- Publicas app (release) con motor de reporting ya integrado.
- Después, cambios de reportes se hacen por web y se guardan en DB.
- No necesitas nuevo despliegue para ajustar diseño (salvo cambios de lógica/código).

---

## 6) Qué sí puedes modificar después de publicar

Sí puedes cambiar sin redeploy:

- Layout visual (cabeceras, detalle, pie, logos).
- Formatos (fechas, moneda, alineación, estilos).
- Expresiones calculadas dentro del reporte.
- Parámetros y visibilidad de elementos.

Normalmente requiere despliegue de código:

- Nuevos endpoints backend.
- Nuevas fuentes de datos no configuradas.
- Reglas de negocio complejas en C# no expresables en diseño.
- Cambios estructurales de seguridad/permisos.

---

## 7) ¿Puedes crear reportes nuevos en producción?

Sí. Condiciones:

1. Tener el diseñador web habilitado.
2. Tener `ReportStorage` implementado (DB/archivo).
3. Permisos de creación.
4. Plantilla base opcional (muy recomendado) para uniformidad visual.

Buenas prácticas:

- Definir convención de códigos (`FAC_`, `COB_`, `CON_`...).
- Versionar (`v1`, `v2`) en vez de sobreescribir ciegamente.
- Flujo borrador → aprobado → publicado.

---

## 8) Riesgos frecuentes y cómo evitarlos

- **Riesgo:** usuarios rompen layout en vivo.  
  **Mitigación:** entorno borrador + publicar versión.

- **Riesgo:** consultas pesadas.  
  **Mitigación:** vistas SQL optimizadas y parámetros obligatorios.

- **Riesgo:** acceso a datos sensibles.  
  **Mitigación:** datasource controlado por backend y filtros por tenant.

- **Riesgo:** no trazabilidad.  
  **Mitigación:** auditoría de cambios + historial de versiones.

---

## 9) Plan de adopción en 3 fases (recomendado)

### Fase 1 (rápida)

- Integrar viewer web.
- Publicar reportes fijos iniciales.
- Validar exportaciones y performance.

### Fase 2

- Integrar diseñador web para admin.
- Implementar `ReportStorage` en DB.
- Habilitar edición de layouts existentes.

### Fase 3

- Versionado + workflow de aprobación.
- Catálogo de plantillas por módulo.
- Auditoría completa y métricas de uso.

---

## 10) Checklist técnico para tu proyecto

- [ ] Añadir paquetes de DevExpress Reporting al proyecto servidor.
- [ ] Registrar servicios y middleware de reporting en `Program.cs`.
- [ ] Implementar `ReportStorageWebExtension` con multitenancy.
- [ ] Crear tabla(s) de layouts/versiones.
- [ ] Definir políticas/roles para diseñar vs visualizar.
- [ ] Agregar pantallas de catálogo + diseñador + visor.
- [ ] Probar exportación PDF/Excel y filtros por tenant.
- [ ] Auditar cambios de layout.

---

## 11) Conclusión

Tu enfoque es totalmente válido: **sí puedes editar y crear reportes después de publicar**, siempre que prepares la app para que los layouts vivan en almacenamiento dinámico (DB/archivo) y no embebidos en código.

Si quieres, en el siguiente paso te puedo proponer un **blueprint técnico aterrizado a tu solución** (`apc` + `apc.Client`) con clases concretas, estructura de carpetas y orden exacto de implementación.
