# Diseño — Documentos generados por tipo de acción de cobranza

Fecha: 2026-06-29
Estado: Aprobado (con refinamiento de implementación, ver §2)

## Problema

Algunos tipos de acción de cobranza deben **generar un documento** que se entrega
al cliente. El caso inicial es la acción `1 = Envío de Carta de Cobranza Prejudicial`.
Hoy `RegistrarAccionAsync` solo escribe la bitácora (`cln_accion_cobranza`); no
produce ningún documento. Se necesita:

1. Un mecanismo **data-driven** que marque qué tipos de acción generan documento y cuál.
2. Que al registrar esa acción se **genere y entregue** el documento (PDF).
3. Poder **reimprimir** el documento exacto desde la bitácora (snapshot).

## Decisiones (brainstorming)

- **Motor:** DevExpress Reporting (`XtraReport` → `ExportToPdf`), consistente con el resto.
- **Disparo:** auto al guardar + botón de reimpresión en la bitácora.
- **Persistencia:** **snapshot** — se archiva el PDF generado, ligado al registro de la bitácora.
- **Alcance inicial:** solo la acción `1`. El mecanismo queda genérico.

## 1. Modelo de datos

### `axl_accion_cobranza` (+2 columnas)
- `genera_documento boolean NOT NULL DEFAULT false`
- `documento_codigo varchar(50) NULL` — identifica el generador de documento.

Seed: `cod_accion = 1` → `genera_documento = true`, `documento_codigo = 'CARTA_COBRANZA_PREJUDICIAL'`.

### `cln_accion_cobranza_documento` (tabla nueva, tenant-scoped)
| col            | tipo         | nota                                            |
|----------------|--------------|-------------------------------------------------|
| id             | serial PK    |                                                 |
| company_id     | bigint       | `ICompanyScopedEntity`                          |
| accion_id      | int FK       | → `cln_accion_cobranza.id`                       |
| documento_codigo | varchar(50) | redundante para trazabilidad                   |
| nombre_archivo | varchar(200) |                                                 |
| contenido      | bytea        | PDF snapshot                                     |
| generado_en    | timestamp    |                                                 |
| generado_por   | varchar(100) | usuario de sesión                                |

Scripts SQL timestamped en `Database/` + regla **DB Mirror** (aplicar en `siad_v3_restore`).

## 2. Refinamiento vs. diseño aprobado

El diseño aprobado ligaba la acción a un `informe_id` de `rep_catalogo_informes`. Al
revisar la infraestructura, ese catálogo está orientado a **reportes de diseñador web
con datasets SQL**. Una **carta legal de formato fijo** se modela mejor como un
`XtraReport` **construido en código** y resuelto por un **código de documento**
(`documento_codigo`). Ventajas: render a PDF directo para el snapshot, sin depender del
catálogo de datasets por empresa, y fácil de iterar cuando llegue el formato real.
(El placeholder actual se reemplaza tocando una sola clase de reporte.)

## 3. Componentes

- **`SIAD.Reports`**
  - `Documentos/CartaCobranzaPrejudicialReport.cs` — `XtraReport` code-built, data-bound a un DTO de carta (placeholder con membrete + datos cliente + total adeudado + plazo).
  - `Documentos/IDocumentoCobranzaGenerator.cs` + `DocumentoCobranzaGenerator.cs` — dado `documento_codigo`, `clienteClave`, arma el DTO (reusando datos de cliente/saldo), construye el reporte y devuelve `(nombreArchivo, byte[] pdf)`. Registrado en DI.
- **`SIAD.Core`**
  - Entidad `cln_accion_cobranza_documento`; +columnas en `axl_accion_cobranza`.
  - DTOs: `AccionCobranzaCrudDto`/`SaveDto` (+`GeneraDocumento`, `DocumentoCodigo`), `AccionDocumentoDto` (metadatos de reimpresión).
- **`SIAD.Services/Cobranza`**
  - `RegistrarAccionAsync`: tras guardar la bitácora, si la acción `genera_documento`, llama al generador, guarda el snapshot y devuelve el id del documento. Render best-effort: si falla, la acción queda registrada y se reporta el fallo.
  - `ObtenerDocumentoAccionAsync(accionId)` → bytes del snapshot para descarga/reimpresión.
- **`apc/Controllers/CobranzaController`**
  - `POST /api/cobranza/acciones` ahora devuelve `{ accionId, documentoId? }`.
  - `GET /api/cobranza/acciones/{accionId}/documento` → PDF (snapshot).
- **`apc.Client`**
  - `AccionesCobranza.razor` (bitácora): al guardar, si vino `documentoId`, abre el PDF; columna con botón "Documento" en filas con snapshot.
  - `Mantenimientos/AccionesCobranza.razor`: checkbox "Genera documento" + combo de `documento_codigo` (catálogo fijo de códigos disponibles).

## 4. Errores

- Generación de PDF **best-effort post-commit**: la bitácora nunca se pierde. Si falla,
  `ErrorNotice` avisa "acción registrada, documento no generado" y se puede reintentar
  desde la fila (botón regenera y guarda snapshot).
- Reimpresión sirve los bytes archivados (no regenera) → documento idéntico al enviado.

## 5. Pruebas (xUnit, `BEGIN...ROLLBACK`)

1. Acción con `genera_documento` crea fila en `cln_accion_cobranza_documento` con PDF no vacío.
2. Acción sin flag no genera documento.
3. `ObtenerDocumentoAccionAsync` devuelve exactamente los bytes guardados.

## 6. Pendiente del usuario

- Formato real de la Carta de Cobranza Prejudicial (membrete, texto legal, campos, firma).
  Hasta entonces se usa el placeholder de `CartaCobranzaPrejudicialReport`.
