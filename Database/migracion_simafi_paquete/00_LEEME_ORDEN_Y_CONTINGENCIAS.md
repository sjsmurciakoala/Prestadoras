# Paquete de migración SIMAFI → SIAD (preparado 2026-08-04 para la sesión del 05)

Scripts numerados, en orden, con el candado del legacy resuelto y la tabla de
contingencias al final. **Todos los transformadores son los MISMOS scripts ya
validados en local al centavo (M6: 25,530/25,530 clientes exactos,
L 48,858,786.58)** — los archivos numerados son envoltorios documentados que
los ejecutan en orden con `\ir`; no hay lógica nueva que pueda descuadrar.

## Antes de abrir psql — SIEMPRE

```bat
set PGCLIENTENCODING=UTF8        (PowerShell: $env:PGCLIENTENCODING='UTF8')
```
Sin esto, los acentos de los SPs/mensajes quedan corruptos (bug real del 04-08).

## Los dos escenarios de uso

| Escenario | Qué se corre |
|---|---|
| **A. Sesión de mañana (plan del PDF): M5+M7 en LOCAL** | Solo `01` (verificación), `70` (re-validar M6 sigue en 0), `80` (derivados) y `90` (respaldo contable pre-M7). La cartera YA está migrada en copia09 — NO se re-corre M3/M4 en local. |
| **B. Corrida completa (cutover en el servidor, ventana futura)** | `01` → `02` (staging fresco) → `10`…`60` en orden → `70` → `80` → `90`. Requiere: SIMAFI congelado (nadie facturando/cobrando), backup previo, portal apagado. |

⚠️ Si mañana la intención es el escenario B contra el servidor: eso es el
**cutover** y tiene prerequisitos duros (ventana 0.9 aplicada primero, SIMAFI
congelado, decisión de convenios respondida). Confirmarlo antes de tocar nada.

## Mapa de tablas (origen → destino)

| SIMAFI (simafi_stg) | SIAD | Script |
|---|---|---|
| `maestrosep` | `cliente_maestro` (+detalle) | 10 |
| ledger (`facturacion` + archivadas unificadas) — cargos `debitos > 0` | `factura` + `factura_detalle` | 20, 30, 40 |
| ledger — créditos | `transaccion_abonado` (histórico congelado) + `adm_pago` | 20, 40, 60 |
| pagos → facturas (SIMAFI no lo guarda) | `adm_pago_aplicacion` por **FIFO** | 60 |
| numeración de recibos | `adm_documento_secuencia` | 50 |

## Orden y tiempos medidos (corrida completa local, disco mecánico USB)

| # | Archivo | Qué hace | Tiempo medido |
|---|---|---|---|
| 01 | `01_verificacion_previa.sql` | Pre-vuelo: staging, espacio, candados, conteos | 1–2 min |
| 02 | `02_refresco_staging.md` | Receta M1: volcar MySQL → simafi_stg (LATIN1) | 1–3 h |
| 10 | `10_clientes.sql` | maestrosep → cliente_maestro (idempotente) | ~10 min |
| 20 | `20_documentos.sql` | prep + ledger → factura/detalle/movimientos | **3.5–4.7 h** |
| 30 | `30_cierre_documentos.sql` | Cierra las 2 brechas conocidas (piloto + sin maestro) | ~15 min |
| 40 | `40_correccion_cargos_pagos.sql` | Criterio débitos>0 (ND 105) + limpia ceros | ~30 min |
| 50 | `50_secuencias.sql` | Resiembra numeración de recibos | 1 min |
| 60 | `60_pagos_fifo.sql` | adm_pago + aplicación FIFO (2.8M pagos, 9.4M aplic.) | ~1 h |
| 70 | `70_validacion_final.sql` | **ACEPTACIÓN M6** — si no cuadra, NO seguir | ~20 min |
| 80 | `80_recalculo_derivados.sql` | estado_id y derivados post-carga (triggers off) | ~10 min |
| 90 | `90_contabilidad_m7_respaldo.sql` | Respaldo _pre_m7 (la limpieza/re-migración M7 es aparte) | 5 min |

Ejecución de cada paso: `psql -h <host> -U postgres -d <base> --set ON_ERROR_STOP=1 -f NN_archivo.sql`
corriendo psql DESDE esta carpeta (los `\ir` son relativos).

## ⚠️ CONTINGENCIAS — qué puede salir mal y qué hacer

| # | Síntoma | Causa | Solución |
|---|---|---|---|
| 1 | `transaccion_abonado está CONGELADA…` | El freeze de F7 (posterior a estos scripts) bloquea la escritura | Ya resuelto: los wrappers 20–60 abren `SET siad.permitir_escritura_legacy='on'` y lo cierran al final. Si corres un script suelto SIN wrapper, ábrelo tú a mano en esa sesión |
| 2 | Acentos tipo `crÃ©dito` en mensajes/nombres | psql sin PGCLIENTENCODING=UTF8 | Setearlo ANTES de todo (arriba). Si ya pasó: re-aplicar los scripts afectados con el encoding correcto |
| 3 | Nombres de clientes con `Ã`/`?` tras refrescar staging | El volcado de MySQL no se hizo en LATIN1 | Repetir el volcado con `--default-character-set=latin1` (receta en 02). NO intentar "arreglar" con UPDATEs |
| 4 | Postgres se cae a media carga / `could not read block` | Disco USB (el riesgo #1 conocido) | NO matar postgres: recovery ~4 min. Los scripts son idempotentes/por bloques → re-ejecutar el paso. Regla: reconstruir, nunca UPDATE masivo. Si se repite, mover la sesión a otro día/disco |
| 5 | `70` no cuadra (clientes ≠ saldo del ledger) | Carga incompleta o staging desfasado del origen | PARAR. `docs/simafi_m2/m6b.sql` desglosa por cliente/causa. Comparar contra el LEDGER reconstruido (suma débitos−créditos), NUNCA contra `clientesaldos` (desfasado, es solo informativo). Diferencias conocidas aceptadas: 5 cargos sin recibo en el origen |
| 6 | Duplicación de cartera (saldos ×5) | Trampa del código `01` / código `12` como cargo | Ya resuelto en 20/40 (criterio `debitos>0`, `12` excluido). Si aparece: se corrió un script viejo — restaurar backup y usar SOLO los de este paquete |
| 7 | Folios de recibo duplicados o con prefijo raro post-migración | Secuencia desfasada | Correr/repetir `50` |
| 8 | `disk full` | La carga agrega ~8–10 GB entre datos e índices | Verificado en `01`. Liberar espacio ANTES; en el peor caso `VACUUM` no ayuda a mitad de carga — restaurar y reintentar con espacio |
| 9 | La carga va lentísima (proyección de días) | Se está corriendo fila a fila / NOT EXISTS por fila | Son los scripts equivocados — los del paquete usan carga por bloques (nota de estrategia en cabecera de M3b) |
| 10 | Portal/app escribiendo durante la carga | Alguien emitió/cobró a media migración | Portal APAGADO y SIMAFI congelado durante todo el escenario B. Si pasó: las brechas del tipo "piloto" se cierran con la técnica de `30`, pero hay que re-validar `70` completo |
| 11 | `estado_id` descuadrado de la letra al final | Cargas con triggers deshabilitados | `80` lo recalcula y su auditoría debe dar 0 (lección del 02-08) |
| 12 | Se necesita deshacer TODO | — | Backup completo previo (`backup_bd_simple.ps1`) es el único rollback total. Por eso es obligatorio en `01` |
| 13 | Convenios: ¿migrar la cartera de convenios de SIMAFI? | L 7,336,806.47 viven en el módulo de convenios (no está en staging) | DECISIÓN DEL NEGOCIO pendiente: preguntar al operador si se sigue cobrando. Sin respuesta escrita, el cutover no cierra (M5 del plan) |
| 14 | Contabilidad: ¿cuándo M7? | Re-migrar partidas con comprobantes originales borra las 12,095 actuales | `90` SOLO respalda. La limpieza y re-migración M7 se corre con el plan del PDF y el respaldo verificado |

## Credenciales

No hay credenciales en estos archivos (regla del repo). Origen MySQL: el
servidor y usuario de la sesión M1 de julio (anotados fuera del repo).
