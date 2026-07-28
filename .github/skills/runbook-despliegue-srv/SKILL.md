---
name: runbook-despliegue-srv
description: Usar siempre que se cree, agregue o edite un script SQL en `Database/` del repo SIAD/Prestadoras (cambios de estructura o datos que después se aplican al servidor de producción `siad_v3` @ 172.16.0.9), o cuando el usuario prepare/junte una tanda de cambios de BD para subir al servidor de la VPN. Palabras clave: script SQL, Database/, despliegue, SRV, servidor, migración, runbook. Responde en español.
---

# Runbook de despliegue a SRV (registro de cambios de BD)

## Propósito

Todo script `.sql` de `Database/` que cambie la base termina aplicándose al servidor de
producción `siad_v3`. Para que nada se pierda ni se aplique en desorden, cada script debe
quedar **reflejado en el runbook de despliegue** (`Database/*_runbook_despliegue_srv.md`),
con su lugar en el orden y su verificación. Este skill mantiene ese runbook al día.

**Principio:** si creaste un `.sql` en `Database/`, lo registrás en el runbook. Ningún
script se queda sin su fila.

## Cuándo aplica

- Acabás de **crear o editar** un script `.sql` timestamped en `Database/` (estructura o datos).
- El usuario habla de **subir/aplicar los cambios al servidor**, preparar el despliegue, o la
  tanda pendiente de SRV.

**No aplica a:** consultas de solo lectura, migraciones de datos one-shot ya ejecutadas
(p. ej. SIMAFI landing/transform), o cambios que no tocan `Database/`.

## Flujo

1. **Ubicá el runbook activo:** el `Database/*_runbook_despliegue_srv.md` más reciente. Si no
   hay ninguno (o la tanda anterior ya se desplegó y cerró), creá
   `Database/AAAA-MM-DD_runbook_despliegue_srv.md` **tomando como plantilla el más reciente**
   (mismas secciones: propósito, antes de empezar, advertencias, tabla de orden, detalle por
   paso, estado presunto, versionado).
2. **Leé el script** y clasificalo (tabla §Clasificación): naturaleza, ¿re-ejecutable?,
   dependencias (qué debe correr antes) y si asume un `company_id`.
3. **Insertá la fila** en la tabla "Orden de aplicación" en la **posición correcta por
   dependencia**, no por fecha: la estructura va antes que los datos que la llenan; un
   `DELETE`+`INSERT` de catálogo va antes que su backfill.
4. **Agregá la sección de detalle** del paso: qué hace, comando
   `psql "$SRV" -v ON_ERROR_STOP=1 -f Database/<script>.sql`, una consulta **«¿ya aplicado?»**
   y la verificación posterior.
5. **Marcá ⚠️** si el script es destructivo, **NO** re-ejecutable, cambia datos/saldos, o
   depende de un `company_id`.
6. **Actualizá "Estado presunto"** (mirror sí / SRV pendiente, según lo que sepas). Nunca lo
   verifiques conectándote a la BD.

## Clasificación (referencia rápida)

| Naturaleza | Ejemplos | ¿Re-ejecutable? |
|---|---|:--:|
| Aditivo | `ADD COLUMN IF NOT EXISTS`, `CREATE TABLE/INDEX IF NOT EXISTS`, widening de varchar | Sí |
| Datos idempotente | `UPDATE … WHERE col IS NULL`, `INSERT … WHERE NOT EXISTS` | Sí |
| Datos destructivo/one-shot | `DELETE`+`INSERT` de catálogo, `TRUNCATE`, `UPDATE` masivo | ⚠️ NO — "una sola vez" |
| Objetos | `CREATE OR REPLACE VIEW/FUNCTION` | Sí (ojo si cambia resultados) |
| Respaldo/rollback | script que restaura una versión anterior | **No es un paso** — listar aparte |

## Reglas

- **No te conectás a la BD ni aplicás nada.** Este skill **solo documenta** (aplicar es del
  usuario; ver `psql-runner`).
- El **orden manda por dependencia**, no por nombre alfabético ni solo por fecha.
- Un script de **rollback/backup nunca es un paso de aplicación**: se lista aparte como
  "NO aplicar".
- Registrá también en la sección de **versionado (git)** si el `.sql` quedó untracked.
- Todo en **español**; solo identificadores, comandos y SQL en su forma técnica.

## Coordinación con otros skills

- **Antes** de crear/aplicar el `.sql`: `guardia-estructura-bd` (tarjeta de aprobación).
  **Después** de crearlo: este skill lo registra en el runbook.
- Plantilla y ejemplo vivo: `Database/2026-07-23_runbook_despliegue_srv.md`.

## Errores comunes

- Crear el `.sql` y olvidar la fila en el runbook. → Todo script se registra.
- Ordenar por fecha ignorando dependencias. → La estructura/seed va antes que su backfill.
- Meter el script de backup/rollback como un paso más. → Va aparte, marcado "NO aplicar".
- Decir "ya está aplicado en SRV" sin saberlo. → Es presunto; cada paso lleva su «¿ya aplicado?».
- Redactar el runbook en inglés. → Siempre en español.
