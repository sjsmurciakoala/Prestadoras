# Plan de trabajo apc



## 1. Visión general

El objetivo es migrar el sistema SIAD (actualmente en ASPNET_Core_3) hacia una solución Blazor WebAssembly hospedada con componentes DevExpress y reportería nativa (pc). Este documento resume cómo se organizará la solución, la estrategia de migración y la secuencia de etapas para ejecutar el plan.



## 2. Organización de la solución



### 2.1 Capas y proyectos actuales

- pc.Client: front-end Blazor WebAssembly (UI general + componentes JS de DevExpress Reporting).

- pc.Server: host ASP.NET Core que sirve la app WASM, expone API, integra DevExpress Reporting y manejará servicios de dominio.

- pc.Shared: contratos básicos compartidos entre cliente y servidor.

- SIAD.Core: librería para entidades de dominio, DTOs y contratos.

- SIAD.Data: acceso a datos, DbContext y configuraciones EF Core.

- SIAD.Services: servicios de dominio y orquestadores.

- SIAD.Reports: repositorio para reportes DevExpress (.repx) y lógica auxiliar.



### 2.2 Lineamientos de código

- Estándar C# 12 / .NET 9 con análisis de código habilitado.

- Convención PascalCase para clases y camelCase para variables; evitar abreviaturas.

- DTOs generados desde SIAD.Core con perfiles AutoMapper; servicios expondrán métodos asíncronos.

- Uso de Options para cadenas de conexión y llaves sensibles (configuración externa).



### 2.3 Flujo de trabajo

- Branch principal main, rama de integración dev, ramas por feature (

eature/<modulo>).

- Pull requests con revisiones cruzadas y validación automática (build + pruebas unitarias).

- Versionado semántico en despliegues mensuales (

YYYY.MM).

- Documentación actualizada al cierre de cada iteración (README y trabajo.md).



### 2.4 Entornos

- Desarrollo local: pc.Server + pc.Client sirviendo desde ASP.NET Core; Postgres/SQL Server vía contenedores; SQLite para reportería temporal.

- QA: despliegue automatizado vía pipeline, seeds controlados por migraciones.

- Producción: configuración externa de cadenas de conexión y secrets, pipelines aprobados manualmente.



### 2.5 Estructura interna sugerida

- SIAD.Core/

  - Domain/Entities

  - Domain/Enums

  - Contracts/DTOs

  - Contracts/Interfaces

  - Common/Exceptions

- SIAD.Data/

  - Context (DbContext y factory)

  - Configurations (Fluent API)

  - Migrations

  - Seed

- SIAD.Services/

  - Interfaces

  - Implementations

  - Mappings (perfiles AutoMapper)

  - Validators

- SIAD.Reports/

  - Definitions (clases XtraReport)

  - Layouts (.repx)

  - Storage

- pc.Client

  - Pages / Components

  - Features/<Modulo>

  - wwwroot

- pc.Server

  - Controllers / Endpoints

  - Reporting

  - Integración con SIAD.Services



## 3. Estrategia de migración

1. **Inventario funcional**: mapear cada área del sistema legado a módulos Blazor (clientes, cobranza, bancos, contabilidad, etc.).

2. **Refactor de acceso a datos**: trasladar entidades y repositorios a SIAD.Data y SIAD.Services, reemplazando Dapper por EF Core donde sea viable.

3. **Servicios de negocio**: encapsular reglas existentes (plan de pagos, asignación de órdenes, conciliaciones) en servicios reutilizables.

4. **UI Blazor**: reimplementar workflows con componentes DevExpress (grids, formularios, dialogs, report viewer) asegurando accesibilidad.

5. **Reportería**: migrar cada RDLC a DevExpress Reports, versionando layouts y conectores de datos.

6. **Seguridad**: integrar Identity/claims con soporte para políticas, roles y autenticación externa si aplica.

7. **Pruebas**: agregar pruebas unitarias e integración (repositorios, servicios, componentes UI con bUnit) por módulo.



## 4. Etapas del plan



### Mes 1 - Administración y seguridad

- Construir estructura multicapa, configurar DevExpress y EF Core.

- Migrar catálogos maestros (clientes, proveedores, cuentas contables, medidores, ciclos).

- Implementar autenticación (Identity) y gestión de roles/políticas.

- Entregable: portal con login, menú base y catálogos clave operativos.



### Mes 2 - Ventas / facturación

- Migrar entidades de facturación (Factura, FacturaDetalle) y lógica de totales/impuestos.

- Construir pantallas DevExpress (listado, alta/edición, reimpresión) y pruebas de flujo completo.

- Migrar reportes de factura y consumos.



### Mes 3 - Compras

- Trasladar módulos de proveedores, compromisos y órdenes de compra.

- Implementar UI con grids editables, estados y reportes de compra.



### Mes 4 - Bancos

- Migrar cuentas por cobrar/pagar, conciliación y pagos.

- Construir pantallas de conciliación, asignación de transacciones y reportes bancarios.



### Mes 5 - Contabilidad

- Migrar catálogo de cuentas, centros de costo y generación automática de asientos.

- Implementar vistas Diario, Mayor, balances y reportes financieros.



### Mes 6 - Reportería avanzada y despliegue

- Migrar todos los reportes RDLC a DevExpress Reports y habilitar designer embebido.

- Ajustar UX/UI final, optimizar carga diferida y realizar pruebas integrales.

- Ejecutar capacitación y despliegue en QA/piloto/producción.



## 5. Arranque del trabajo (Sprint 0)

1. **Preparación técnica**

   - Configurar repositorio multicapa y pipelines CI/CD.

   - Parametrizar cadenas de conexión y secretos fuera de código fuente.

   - Verificar licencias DevExpress y accesos a repositorios.

2. **Sincronización del equipo**

   - Asignar responsables por módulo (administración, ventas, compras, bancos, contabilidad, reportería).

   - Definir tablero de seguimiento (Azure Boards, Jira, etc.) y ritmos de reunión.

3. **Infraestructura de datos**

   - Crear dumps actualizados de Postgres y scripts para ambientes locales.

   - Revisar dependencias de Dapper y plan de sustitución por EF Core o servicios REST.

4. **Backlog detallado**

   - Refinar historias para Mes 1 con criterios de aceptación y tareas técnicas.

   - Identificar riesgos iniciales (compatibilidad DevExpress, refactor de reportes, rendimiento).

5. **Entorno de trabajo**

   - Configurar plantillas de PR, flujos de release y convenciones de commits.

   - Establecer métricas iniciales (time-to-merge, cobertura pruebas, bugs críticos).





## 6. Verificacion de convenciones SQL
1. Ejecutar `python tools/db_schema_pascalcase_helper.py --root Database --format text > catalogo_nombres.txt` antes de regenerar entidades o DTOs. El script recorre todos los .sql de Database/, detecta tablas/columnas y sugiere sus equivalentes PascalCase/camelCase para validar discrepancias.
2. Revisar el archivo generado (o la salida JSON usando `--format json`) para confirmar que las convenciones deseadas coinciden con los nombres snake_case originales. Ajustar los scripts SQL o documentar excepciones antes de cualquier scaffolding.
3. Adjuntar el catalogo resultante dentro de la evidencia de la tarea (PR, ticket o carpeta de entregables) para que la auditoría pueda comparar rápidamente los nombres originales vs. los sugeridos.

**Criterios de aceptación adicionales**
- El catalogo generado por `tools/db_schema_pascalcase_helper.py` debe compartirse junto con la documentación del sprint.
- Ninguna tabla o columna puede migrarse sin tener una equivalencia aprobada en dicho catalogo.

