# Runbook de Validacion Contabilidad

Fecha base: 2026-03-10
Objetivo: validar despliegue de unificacion de mayorizacion y normalizacion de estados.

## 1) Pre-requisitos
- Base de datos con respaldos previos de:
- `con_partida_hdr`
- `con_partida_dtl`
- `con_saldo_cuenta`
- `con_periodo_contable`
- `con_tipo_transaccion`
- Ventana de mantenimiento aprobada para reconciliacion contable.

## 2) Orden de despliegue recomendado
1. Ejecutar SQL central de posteo/reversa:
- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
2. Ejecutar hotfix de referencia de plan de cuentas:
- `Prestadoras/Database/ddl_v3/20260310_hotfix_sp_con_actualizar_saldos_por_poliza_con_plan_cuentas.sql`
3. Ejecutar hotfix de posteo para polizas DRAFT sin periodo:
- `Prestadoras/Database/ddl_v3/20260310_hotfix_sp_con_postear_poliza_periodo_null.sql`
4. Ejecutar convergencia de flujo movil:
- `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`
5. Ejecutar migracion de estados numericos:
- `Prestadoras/Database/ddl_v3/20260309_contabilidad_status_id_migracion.sql`
6. Ejecutar correccion canonica de periodos:
- `Prestadoras/Database/2026-03-26_fix_con_periodo_status_mapping.sql`
7. Desplegar API/servicios:
- `PolizaService`
- `BanTransaccionesService`
- `ContabilidadCatalogosService`
- `PeriodoContableService`
8. (Si aplica) Ejecutar backfill historico de auditoria:
- `Prestadoras/Database/ddl_v3/20260310_backfill_con_partida_hdr_posted_by.sql`

## 3) Checklist tecnico post-despliegue
1. Verificar existencia de funciones centrales:
- `public.sp_con_postear_poliza`
- `public.sp_con_revertir_poliza`
2. Verificar columnas nuevas:
- `con_periodo_contable.status_id`
- `con_tipo_transaccion.status_id`
3. Verificar backfill:
```sql
SELECT status, status_id, COUNT(*)
FROM public.con_periodo_contable
GROUP BY status, status_id
ORDER BY status_id;

SELECT status, status_id, COUNT(*)
FROM public.con_tipo_transaccion
GROUP BY status, status_id
ORDER BY status_id;
```
Esperado para periodos:
- `0 = ABIERTO`
- `1 = PRECIERRE`
- `2 = CERRADO`
4. Verificar auditoria de posteo:
```sql
SELECT
  COUNT(*) AS total_posted,
  COUNT(*) FILTER (WHERE posted_by IS NULL) AS posted_sin_usuario
FROM public.con_partida_hdr
WHERE status = 1;
```

## 4) Validacion funcional por flujo
1. Movil (`ActualizarLecturaV2`):
- Registrar lectura con detalle.
- Confirmar poliza en `con_partida_hdr` con `status = 1`.
- Confirmar impacto unico en `con_saldo_cuenta`.
2. Poliza manual (`/contabilidad/polizas`):
- Crear DRAFT.
- Ejecutar registrar/postear.
- Ejecutar revertir.
- Confirmar que saldos regresan al valor original.
3. Plantillas SQL (`sp_con_generar_comprobante`):
- Generar comprobante.
- Confirmar que posteo pasa por `sp_con_postear_poliza`.
- Si el precheck falla por plantilla inexistente, ejecutar bootstrap:
  - `Prestadoras/Database/ddl_v3/20260310_seed_plantilla_minima_e2e.sql`
- Script sugerido:
  - `Prestadoras/Database/ddl_v3/20260310_test_e2e_sp_con_generar_comprobante.sql`
4. Bancos:
- Registrar movimiento con contracuentas.
- Confirmar que se genera poliza y se postea por flujo central.
 - Si no hay catalogos de bancos, ejecutar bootstrap:
  - `Prestadoras/Database/ddl_v3/20260310_seed_bancos_minimo_e2e.sql`
  - Nota: este seed tambien activa/crea diario contable `BAN` (`con_diario`).
 - Script sugerido:
  - `Prestadoras/Database/ddl_v3/20260310_test_e2e_flujo_bancos.sql`
5. Periodo no abierto:
- Intentar posteo en periodo cerrado.
- Confirmar bloqueo con error de negocio.
 - Script sugerido:
  - `Prestadoras/Database/ddl_v3/20260310_test_negativo_posteo_periodo_cerrado.sql`

## 5) Validacion contable
1. Cuadre de poliza:
```sql
SELECT poliza_id, total_debit, total_credit
FROM public.con_partida_hdr
WHERE status = 1
  AND abs(total_debit - total_credit) > 0.01;
```
2. No doble posteo:
- Reintentar posteo sobre la misma poliza y confirmar que no duplica saldos.
 - Evidencia incluida en:
  - Seccion `F` de `20260310_test_e2e_sp_con_generar_comprobante.sql`
  - Seccion `No doble posteo` de `20260310_test_e2e_flujo_bancos.sql`
3. Reversa exacta:
- Postear y revertir misma poliza; validar delta cero en saldos.
 - Evidencia incluida en:
  - Seccion `G` de `20260310_test_e2e_sp_con_generar_comprobante.sql`
  - Seccion `Reversa exacta opcional` de `20260310_test_e2e_flujo_bancos.sql`

## 6) Reconciliacion y rebuild de saldos
1. Ejecutar script:
- `Prestadoras/Database/ddl_v3/20260310_reconciliacion_con_saldo_cuenta.sql`
2. Definir alcance en `tmp_con_recon_params`:
- `NULL,NULL` para todo.
- `company_id` y/o `period_id` para ejecucion parcial.
3. Ejecutar seccion A (diagnostico previo) y guardar salida.
4. Ejecutar seccion B (backup + rebuild) solo en ventana aprobada.
5. Ejecutar seccion C (validacion final); debe retornar 0 filas.
6. Registrar evidencia en `BITACORA_CAMBIOS_CONTABILIDAD.md`.

## 7) Backfill historico de `posted_by`
1. Ejecutar script:
- `Prestadoras/Database/ddl_v3/20260310_backfill_con_partida_hdr_posted_by.sql`
2. Definir alcance en `tmp_con_posted_by_backfill_params`:
- `NULL,NULL,NULL` para todo.
- `company_id` y/o rango de fechas de `posted_at` para ejecucion parcial.
3. Ejecutar seccion A y guardar:
- Conteo por fuente de resolucion.
- Lista de pendientes no resolubles.
4. Ejecutar seccion B (backup + update).
5. Ejecutar seccion C para confirmar remanente.
6. Si se requiere revertir, ejecutar seccion D con `backup_tag`.

## 8) Rollback operativo
1. Detener despliegue de API/servicios actual.
2. Restaurar funciones SQL previas (backup de scripts anteriores).
3. Restaurar tablas desde backup si hay inconsistencia de datos.
4. Documentar incidente y resolucion en `BITACORA_CAMBIOS_CONTABILIDAD.md`.

## 9) Evidencia obligatoria
- Fecha/hora de ejecucion.
- Scripts aplicados (nombre exacto).
- Flujo probado.
- Resultado por prueba.
- Incidencias y resolucion.

## 10) Estado de cierre (2026-03-10)
- E2E bancos:
- No doble posteo: OK.
- Reversa exacta: OK.
- Negativo periodo cerrado:
- Bloqueo de posteo: OK (sin periodo abierto).
- Saldos sin cambio: OK.
- Reconciliacion con_saldo_cuenta:
- Secciones A y C: 0 filas de diferencia.
- Estado general:
- Validacion funcional y contable completada para el alcance del plan.

## 11) Proceso correcto en DB de produccion (operacion diaria)
1. Crear/recibir la poliza en `DRAFT` (`con_partida_hdr.status = 0`) con lineas en `con_partida_dtl`.
2. Validar periodo y catalogos:
- Periodo contable en `status_id = 0` para la fecha de la poliza.
- Cuentas contables de movimiento (`con_plan_cuentas.allows_posting = true`).
- Tipo transaccion/documento/regla activa segun el flujo.
3. Ejecutar posteo solo por el motor central:
- `SELECT public.sp_con_postear_poliza(<company_id>, <poliza_id>, <usuario>);`
4. Confirmar resultado:
- `con_partida_hdr.status = 1`, `posted_at` y `posted_by` con valor.
- Cabecera balanceada (`total_debit = total_credit`).
- Impacto en `con_saldo_cuenta` para las cuentas de detalle de la poliza.
5. Si se requiere deshacer:
- `SELECT public.sp_con_revertir_poliza(<company_id>, <poliza_id>, <usuario>);`
- Verificar retorno a `status = 0` y reversa exacta de saldos.
6. Controles obligatorios:
- No actualizar manualmente `con_saldo_cuenta`.
- No cambiar `status` de poliza con `UPDATE` directo.
- No usar jobs/triggers paralelos para mayorizacion.
7. Control de cierre diario:
- Ejecutar consulta de descuadres (reconciliacion seccion C) y guardar evidencia.
- Si hay diferencias, aplicar procedimiento de reconciliacion controlada (seccion 6 de este runbook).

## 12) Validacion Fase B: Captacion con contabilidad automatica
0. Si la UI muestra vacios en combos Banco/Caja:
- Ejecutar:
  - `Prestadoras/Database/ddl_v3/20260311_seed_captacion_ui_catalogos_minimos.sql`
- Esto inserta catalogos minimos en:
  - `public.recolectora` (combo Banco)
  - `public.catalogo_cajas` (combo Caja)

0.1. Si falla con `No existe plantilla contable para modulo VENTAS, documento REC`:
- Confirmar si existe plantilla activa para `VENTAS/REC`.
- Si solo existe plantilla para `VENTAS/FAC`, captacion hara fallback a `FAC` (hotfix 2026-03-11).
- Si no existe plantilla activa ni para `REC` ni para `FAC`, ejecutar seed de plantilla:
  - `Prestadoras/Database/ddl_v3/20260310_seed_plantilla_minima_e2e.sql`
- En ese seed, ajustar parametros a `module='VENTAS'` y `document_type='REC'` (o `FAC` temporal si REC no esta listo).

1. Prueba lectora (UI o API `POST api/captacionpagos`):
- Registrar pago sobre factura pendiente.
- Confirmar respuesta exitosa del endpoint.
- Validar poliza contable:
```sql
SELECT poliza_id, status, module, document_type, document_id, document_number, posted_at, posted_by
FROM public.con_partida_hdr
WHERE module = 'VENTAS'
  AND document_id = <numrecibo>
ORDER BY poliza_id DESC
LIMIT 1;
```
- Esperado: `status=1` y `document_type` en `REC` (o `FAC` en fallback temporal).

2. Reversa lectora (`POST api/captacionpagos/reverso`):
- Ejecutar reversa de la misma factura.
- Validar misma poliza en `status=0` y saldos revertidos.

3. Prueba manual (`POST api/captacionpagos/posteo-manual`):
- Registrar posteo manual con distribucion valida.
- Buscar poliza por `document_id = 2000000000 + numrecibo`.
- Esperado: poliza creada y posteada (`status=1`).

4. Reversa manual (`POST api/captacionpagos/posteo-manual/reverso`):
- Ejecutar reversa por recibo.
- Esperado: poliza relacionada en `status=0`.

5. Prueba miscelaneos (`POST api/captacionpagos/miscelaneos/registrar`):
- Registrar pago miscelaneo.
- Buscar poliza por `document_id = 1000000000 + recibo`.
- Esperado: poliza creada y posteada (`status=1`).

6. Reversa miscelaneos (`POST api/captacionpagos/miscelaneos/reverso`):
- Ejecutar reversa.
- Esperado: poliza relacionada en `status=0`.

7. Prueba negativa obligatoria:
- Desactivar temporalmente configuracion contable (tipo transaccion o document_type activo) en ambiente de QA.
- Intentar registrar pago.
- Esperado: fallo controlado y rollback del pago (sin cambios en factura/transaccion_abonado/saldos contables).

## 13) Script operativo de plantillas VENTAS/FAC y VENTAS/REC
- Ejecutar ANTES de las pruebas de captacion si no existen plantillas activas:
  - `Prestadoras/Database/ddl_v3/20260313_seed_plantillas_ventas_fac_rec.sql`
- Este script:
  - Crea o actualiza plantillas para `VENTAS/FAC` y `VENTAS/REC`.
  - Resuelve cuentas desde `con_regla_integracion` activa.
  - Es re-ejecutable (upsert por nombre de plantilla).
- Ajustar `company_id` y `user_name` en `tmp_seed_params` segun el ambiente.

## 14) Matriz de evidencia para demo
- Ejecutar DESPUES de las pruebas funcionales E2E:
  - `Prestadoras/Database/ddl_v3/20260313_matriz_evidencia_demo.sql`
- Secciones:
  - A) Matriz por flujo (informativa).
  - B) Polizas generadas por captacion.
  - C) Cuadre de polizas (debe retornar 0 filas).
  - D) No doble posteo (debe retornar 0 filas).
  - E) Reversa exacta.
  - F) Estado de saldos.
  - G) Periodo contable abierto.
  - H) Resumen final.
- Guardar salida como evidencia para la demo del 2026-03-16.

## 15) Estado de limpieza de captacion manual (2026-03-13)
- `sp_registrar_posteo_manual`: ya no se usa desde el servicio C#. La insercion de `transaccion_abonado` es directa via EF.
- `sp_reversar_posteo_manual`: ya no se usa desde el servicio C#. La reversa es directa via EF.
- Los tres flujos (lectora, manual, miscelaneos) ahora usan el mismo patron:
  - Negocio directo (EF) + `sp_con_generar_comprobante` para contabilidad + `sp_con_revertir_poliza` para reversa.
  - Si el pago es bancario, se delega a `BanTransaccionesService` en lugar de contabilidad por plantilla.

## 16) Alcance de con_regla_integracion (legacy lectura movil)

### Que es
- `con_regla_integracion` es una tabla de configuracion que mapea cuentas contables por modulo, documento y escenario.
- Fue la fuente de verdad general del sistema para la integracion contable.

### Estado actual (2026-03-13)
- **Ya no es la fuente de verdad general.** Su alcance queda restringido a:
  - Flujo de lectura movil (`sp_lectura_v2`), que necesita lineas dinamicas por `servicios.cont_account_id`.
  - Soporte de la app Android y el webservice de lectura.
- Los flujos de captacion (lectora, manual, miscelaneos) usan `con_plantilla_partida_hdr/dtl` como fuente de verdad runtime.

### Consecuencias operativas
1. **Insertar/actualizar una fila en `con_regla_integracion` no actualiza automaticamente `con_plantilla_partida_*`.**
2. Si se quiere que un cambio de cuentas aplique a captacion, se debe:
   - Actualizar directamente `con_plantilla_partida_dtl`, o
   - Re-ejecutar el script de seed de plantillas: `20260313_seed_plantillas_ventas_fac_rec.sql`
3. La UI `reglas-integracion` muestra un banner de "Alcance restringido" y badge "Legacy" en el menu.

### Que no hacer
- No usar `con_regla_integracion` como referencia para configurar captacion.
- No asumir que cambiar una regla aqui afecta `sp_con_generar_comprobante`.
- No eliminar `con_regla_integracion` ni sus datos: lectura movil los necesita.

### Cierre futuro
- Opcion A: extender el motor por plantilla para soportar lineas dinamicas y migrar `sp_lectura_v2`.
- Opcion B: aceptar dos compositores (plantillas + dinamico) con un solo posteo central.
- En cualquier caso, `sp_con_postear_poliza` y `sp_con_revertir_poliza` siguen siendo la ruta unica.

## 17) Evidencia E2E desde UI (2026-03-13)

### Pruebas ejecutadas
1. **Lectora (banco)**: alta de pago con banco seleccionado → HTTP 200, integracion bancaria completa.
2. **Lectora reversa**: reversa de pago → HTTP 200, factura reabierta, transaccion_abonado eliminada.
3. **Miscelaneos (banco)**: alta de miscelaneo con banco → HTTP 200, FOR UPDATE en factura + banco.
4. **Miscelaneos reversa**: reversa → HTTP 200, saldos restaurados.
5. **Manual**: consulta de saldos por cliente → HTTP 200 en endpoint `/api/captacionpagos/saldos-manual/*`.

### Validaciones confirmadas
- No hay doble posteo: la validacion `EXISTS transaccion_abonado WHERE recibo AND tipo 201` bloquea correctamente registros duplicados (HTTP 400).
- FOR UPDATE funciona: bloqueo de fila de factura previo a registro/reversa.
- Integracion bancaria completa: flujo `ban_cuenta → con_plan_cuentas → con_periodo_contable → con_diario → con_tipo_transaccion`.
- Reversa bancaria: `AnularMovimientoAsync` ejecuta correctamente.
- Matriz de evidencia post-pruebas: 6 flujos ACTIVO.

### Excepciones preexistentes (no bloqueantes)
- `InvalidCastException`: ocurre esporadicamente en renderizado de UI (no relacionada con contabilidad).
- `PostgresException` en search: query legacy de busqueda de facturas que falla con ciertos patrones.

### Estado final del plan
| Fase | Descripcion | Estado |
|------|-------------|--------|
| 1 | Documentar decision de arquitectura | COMPLETADA |
| 2 | Limpiar captacion para demo | COMPLETADA |
| 2.4 | E2E revalidacion desde UI | COMPLETADA |
| 3 | Formalizar reglas como legacy movil | COMPLETADA |
| 4 | Plantillas controladas por script | COMPLETADA |
| 5 | Evidencia de cierre | COMPLETADA |
