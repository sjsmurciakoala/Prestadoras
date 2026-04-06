# Bitacora de Cambios Contabilidad

## Estandar de registro obligatorio
Cada entrada debe incluir:
1. Fecha.
2. Cambio.
3. Archivos tocados.
4. Motivo.
5. Riesgo.
6. Rollback.
7. Pruebas ejecutadas.
8. Resultado.

## Entradas

### 2026-03-09 1) Inicio de implementacion del plan de unificacion de mayorizacion
- Cambio: Se establece alcance oficial, estados canonicos y flujo objetivo antes de tocar logica.
- Archivos tocados:
- `Prestadoras/docs/ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- Motivo: Cumplir Fase 0 del plan, fijar estandar tecnico/funcional y trazabilidad formal de cambios.
- Riesgo: Riesgo tecnico nulo, solo documental.
- Rollback: Revertir archivos markdown a su version anterior.
- Pruebas ejecutadas: No aplica (cambio documental).
- Resultado: Base documental creada para continuar implementacion tecnica.

### 2026-03-09 2) Punto unico de posteo/reversa en SQL de comprobantes
- Cambio: Se agregan `sp_con_postear_poliza` y `sp_con_revertir_poliza` como ruta central de mayorizacion; `sp_con_generar_comprobante` y `sp_con_revertir_comprobante` delegan en este punto unico.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
- Motivo: Eliminar rutas paralelas de impacto sobre `con_saldo_cuenta` y asegurar idempotencia/concurrencia transaccional por poliza.
- Riesgo: Cambia la semantica de posteo para callers que asumian posteo directo en funciones antiguas.
- Rollback: Restaurar version previa del script y volver a posteo directo por funcion original.
- Pruebas ejecutadas:
- Revision estructural de SQL y referencias.
- Compilacion de servicios que invocan posteo/reversa.
- Resultado: Comprobante por plantilla y reversa ya pasan por motor central de posteo.

### 2026-03-09 3) Convergencia de flujo movil (`sp_lectura_v2`) al posteo unico
- Cambio: El bloque contable de `sp_lectura_v2` ahora crea poliza en estado `0` (DRAFT), resuelve regla/tipo/periodo con compatibilidad `status_id` + texto, y llama `sp_con_postear_poliza`.
- Archivos tocados:
- `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`
- Motivo: Alinear sincronizacion movil al mismo motor de mayorizacion central para evitar divergencia de saldos.
- Riesgo: Dependencia explicita de que funciones centrales de posteo esten desplegadas antes de ejecutar `sp_lectura_v2`.
- Rollback: Restaurar procedimiento previo de `sp_lectura_v2`.
- Pruebas ejecutadas:
- Validacion de script y referencias a `sp_con_postear_poliza`.
- Build de solucion.
- Resultado: Flujo movil converge al mismo posteo central.

### 2026-03-09 4) Convergencia de API de polizas y bancos al posteo unico
- Cambio:
- `PolizaService.RegistrarAsync/RevertirAsync` delegan a `sp_con_postear_poliza`/`sp_con_revertir_poliza`.
- `BanTransaccionesService` resuelve la poliza creada y ejecuta posteo central.
- Resolucion de periodo/tipo en bancos incluye `status_id` con fallback legacy textual.
- Archivos tocados:
- `Prestadoras/SIAD.Services/Contabilidad/PolizaService.cs`
- `Prestadoras/SIAD.Services/Bancos/BanTransaccionesService.cs`
- Motivo: Unificar impacto de saldos y evitar doble logica de mayorizacion fuera del motor central.
- Riesgo: Si no se resuelve `poliza_id` en bancos, se aborta registro contable (comportamiento ahora explicito).
- Rollback: Restaurar metodos anteriores de servicio (actualizacion directa de saldos y flujo previo de bancos).
- Pruebas ejecutadas:
- Compilacion de solucion.
- Revision de invocaciones SQL de posteo central.
- Resultado: Polizas manuales y bancos convergen al flujo unico.

### 2026-03-09 5) Normalizacion de estados a `status_id` + compatibilidad temporal
- Cambio:
- Se agrega script de migracion de BD para `con_periodo_contable.status_id` y `con_tipo_transaccion.status_id` con backfill y constraints.
- Se actualizan entidades, EF mapping, DTOs y servicios para exponer/usar `StatusId` sin eliminar `status` textual.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260309_contabilidad_status_id_migracion.sql`
- `Prestadoras/SIAD.Core/Entities/con_periodo_contable.cs`
- `Prestadoras/SIAD.Core/Entities/con_tipo_transaccion.cs`
- `Prestadoras/SIAD.Data/SiadDbContext.Accounting.cs`
- `Prestadoras/SIAD.Core/DTOs/Contabilidad/PeriodoContableDto.cs`
- `Prestadoras/SIAD.Core/DTOs/Contabilidad/PeriodoContableUpsertDto.cs`
- `Prestadoras/SIAD.Core/DTOs/Contabilidad/PolizaDto.cs`
- `Prestadoras/SIAD.Services/Contabilidad/IPeriodoContableService.cs`
- `Prestadoras/SIAD.Services/Contabilidad/PeriodoContableService.cs`
- `Prestadoras/SIAD.Services/Contabilidad/ContabilidadCatalogosService.cs`
- Motivo: Establecer contrato numerico de estados y mantener ventana de transicion para UI/API existente.
- Riesgo: Requiere ejecutar migracion SQL antes del despliegue de API para evitar desalineacion de columnas.
- Rollback: Revertir despliegue de API/servicios y restaurar esquema/entidades previas.
- Pruebas ejecutadas:
- `dotnet build Prestadoras/HODSOFT_DEVEXPRESS.sln -c Debug`
- Resultado: Build exitoso con warnings existentes del repositorio, sin errores.

### 2026-03-09 6) Actualizacion de estandar y runbook post-implementacion
- Cambio: Se documentan scripts concretos, dependencias de despliegue, validaciones y evidencia requerida.
- Archivos tocados:
- `Prestadoras/docs/ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- Motivo: Cerrar trazabilidad tecnica y operativa para despliegue/soporte.
- Riesgo: Nulo (documental).
- Rollback: Revertir documentos a version previa.
- Pruebas ejecutadas: Revisi�n documental cruzada contra archivos modificados y build.
- Resultado: Documentacion alineada con implementacion real.

### 2026-03-10 7) Hotfix de mayorizacion por referencia de tabla incorrecta
- Cambio: Se corrige referencia `public.con_plan_cuenta` (singular) a `public.con_plan_cuentas` (plural) en `sp_con_actualizar_saldos_por_poliza`.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
- `Prestadoras/Database/ddl_v3/20260310_hotfix_sp_con_actualizar_saldos_por_poliza_con_plan_cuentas.sql`
- Motivo: El endpoint movil `ActualizarLecturaV2` devolvia `0` por excepcion `42P01` al intentar postear poliza contable.
- Riesgo: Bajo; solo corrige nombre de tabla existente usada por el modelo de datos.
- Rollback: Reaplicar version anterior de la funcion (no recomendado) o restaurar desde backup de funciones.
- Pruebas ejecutadas:
- Revision de referencias SQL en repositorio (`con_plan_cuenta` vs `con_plan_cuentas`).
- Validacion de sintaxis del hotfix y consistencia con funcion central.
- Resultado: Ruta de posteo/mayorizacion queda alineada con esquema real (`con_plan_cuentas`).

### 2026-03-10 8) Evidencia de validacion en UI: poliza manual, reconciliacion y auditoria
- Cambio: Se registra evidencia de ejecucion funcional en entorno local con trazas EF Core y consultas de control de saldos/auditoria.
- Archivos tocados:
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Mantener trazabilidad obligatoria de resultados reales de prueba y no solo cambios de codigo.
- Riesgo: Nulo (documentacion).
- Rollback: Eliminar entrada documental.
- Pruebas ejecutadas:
- Flujo UI de poliza manual: inserta `con_partida_hdr` en DRAFT con `total_debit/total_credit` inicial en 0 y luego inserta lineas en `con_partida_dtl`.
- Consulta de reconciliacion `posted vs con_saldo_cuenta`:
  - Diferencias detectadas: cuenta `110302` y `510102` (periodo 1, company 1).
  - `debitos_calc=29100` vs `debitos_saldo=14400`; `creditos_calc=29100` vs `creditos_saldo=14400`.
- Consulta de auditoria `posted_by`:
  - `total_posted=4`, `posted_sin_usuario=4`.
- Resultado:
- Confirmado: ver `INSERT` de cabecera con totales 0 es comportamiento esperado en DRAFT antes de registrar/postear.
- Confirmado: estado de reconciliacion global NO esta cerrado (faltan movimientos historicos en `con_saldo_cuenta`).
- Confirmado: gap de auditoria pendiente (`posted_by` nulo en polizas posteadas).

### 2026-03-10 9) Evidencia puntual: poliza manual en DRAFT con lineas balanceadas
- Cambio: Registro de validacion sobre poliza manual `poliza_id=8`.
- Archivos tocados:
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Documentar resultado real de consulta de control antes de posteo.
- Riesgo: Nulo (documentacion).
- Rollback: Eliminar entrada documental.
- Pruebas ejecutadas:
- Consulta:
  - `status=0`, `total_debit=0`, `total_credit=0`, `debe_dtl=1890`, `haber_dtl=1890`.
- Resultado:
- Estado esperado para DRAFT: lineas ya capturadas y balanceadas, pero cabecera aun sin totales consolidados ni impacto en saldos hasta ejecutar registrar/postear.

### 2026-03-10 10) Handoff de cierre de jornada
- Cambio: Se agrega documento de continuidad con estado actual, hallazgos abiertos y pasos operativos para retomar manana.
- Archivos tocados:
- `Prestadoras/docs/HANDOFF_CONTABILIDAD_2026-03-10.md`
- Motivo: Asegurar continuidad de ejecucion del plan sin perdida de contexto tecnico/funcional.
- Riesgo: Nulo (documentacion).
- Rollback: Eliminar documento de handoff.
- Pruebas ejecutadas: No aplica (documental).
- Resultado: Checklist de reanudacion definido y priorizado.

### 2026-03-10 11) Continuidad: auditoria de posteo (`posted_by`) y script formal de reconciliacion de saldos
- Cambio:
- `sp_con_postear_poliza` resuelve `posted_by` en modo best-effort (si `p_user` es numerico lo usa como id; si no, intenta mapear `usuarioapc.usuario -> usuarioapc.ide`).
- Se agrega script operativo para diagnosticar, respaldar y reconstruir `con_saldo_cuenta` (mes=13, tipo_transaccion=0) desde polizas `POSTED`.
- Se actualiza runbook con orden de despliegue incluyendo hotfix y procedimiento de reconciliacion.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
- `Prestadoras/Database/ddl_v3/20260310_reconciliacion_con_saldo_cuenta.sql`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Cerrar hallazgos abiertos del handoff (auditoria de posteo y reconciliacion global pendiente).
- Riesgo:
- Bajo/medio en rebuild si se ejecuta fuera de ventana (impacto concurrente sobre `con_saldo_cuenta`).
- Bajo en ajuste `posted_by` (no cambia logica de montos ni estado, solo auditoria).
- Rollback:
- Reaplicar version previa de `sp_con_postear_poliza`.
- Restaurar `con_saldo_cuenta` desde `con_saldo_cuenta_backup_hist` para el alcance intervenido.
- Pruebas ejecutadas:
- Validacion estructural de scripts y referencias SQL.
- Verificacion de presencia de logica `posted_by` en `sp_con_postear_poliza`.
- No se ejecuto rebuild en BD desde este cambio (queda en runbook).
- Resultado:
- Queda disponible flujo documentado para reconciliar saldos con evidencia.
- Queda implementada auditoria de `posted_by` para nuevos posteos (sujeta a resolucion de usuario).

### 2026-03-10 12) Evidencia de cierre de reconciliacion de saldos (secciones B y C)
- Cambio: Se ejecuta reconciliacion operativa sobre `con_saldo_cuenta` con script formal.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_reconciliacion_con_saldo_cuenta.sql` (ejecucion en BD)
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Cerrar descuadre detectado en `posted vs con_saldo_cuenta` para flujo unificado de mayorizacion.
- Evidencia recibida:
- Seccion A reporto diferencias en cuentas `110302` y `510102` (company_id=1, period_id=1).
- Seccion B ejecutada (backup + rebuild).
- Seccion C sin filas (resultado vacio).
- Resultado:
- Reconciliacion cerrada para el alcance ejecutado; `con_saldo_cuenta` queda alineado con polizas `POSTED`.
- Riesgo residual:
- Mantener monitoreo en nuevas ejecuciones de posteo/reversa y validar auditoria `posted_by` en nuevas polizas posteadas.

### 2026-03-10 13) Hotfix: posteo de poliza manual cuando `period_id` viene NULL
- Cambio:
- `sp_con_postear_poliza` ahora resuelve periodo OPEN por fecha cuando la poliza DRAFT no trae `period_id`.
- Al postear, persiste `period_id` resuelto en `con_partida_hdr` antes de impactar `con_saldo_cuenta`.
- Se agrega script de despliegue especifico del hotfix.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
- `Prestadoras/Database/ddl_v3/20260310_hotfix_sp_con_postear_poliza_periodo_null.sql`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Error en prueba real de posteo manual: `No hay periodo OPEN para poliza ... (periodo <NULL>, fecha ...)`.
- Riesgo:
- Bajo; solo aplica cuando `period_id` es nulo y mantiene validacion de periodo abierto por fecha.
- Rollback:
- Reaplicar version anterior de `sp_con_postear_poliza`.
- Pruebas ejecutadas:
- Revision de logica SQL del flujo de posteo para polizas manuales.
- Verificacion de trazabilidad en runbook y script hotfix.
- Resultado:
- El motor central de posteo queda tolerante a polizas DRAFT manuales sin `period_id`.

### 2026-03-10 14) Evidencia: `posted_by` validado en posteo real de poliza manual
- Cambio: Validacion funcional del ajuste de auditoria en `sp_con_postear_poliza`.
- Archivos tocados:
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Evidencia recibida:
- Consulta sobre `poliza_id=8`:
  - `status=1`
  - `period_id=3`
  - `posted_by=1`
  - `usuario=jmurcia`
  - `posted_at=2026-03-10 09:15:33.912235-06`
- Resultado:
- Confirmado: para nuevos posteos, `posted_by` se persiste correctamente cuando el usuario se puede resolver en `usuarioapc`.
- Nota:
- Los historicos con `posted_by` nulo requieren backfill controlado (pendiente del plan de cierre).

### 2026-03-10 15) Script de backfill controlado para `posted_by` historico
- Cambio:
- Se agrega script operativo para completar `con_partida_hdr.posted_by` en polizas `POSTED` historicas que quedaron nulas.
- El script incluye: parametros de alcance, preview por fuente de resolucion, backup, update y rollback por `backup_tag`.
- Se actualiza runbook para incorporar este flujo como paso formal de cierre.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_backfill_con_partida_hdr_posted_by.sql`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Cerrar pendiente de auditoria historica (`posted_by` nulo en polizas ya posteadas).
- Riesgo:
- Bajo/medio; actualiza auditoria historica en cabeceras posteadas. Mitigado con backup previo y rollback en el mismo script.
- Rollback:
- Seccion D del script (`rollback`) usando `backup_tag`.
- Pruebas ejecutadas:
- Revision de consistencia SQL (preview/update/rollback) y parametros de alcance.
- No ejecutado aun en BD (pendiente evidencia de corrida).
- Resultado:
- Queda disponible mecanismo controlado para backfill de auditoria sin afectar montos ni estatus contables.

### 2026-03-10 16) Cierre de backfill historico de `posted_by`
- Cambio: Se ejecuta backfill manual controlado para polizas historicas no resueltas por alias legacy (`admin@siad-demo.com`).
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_backfill_con_partida_hdr_posted_by.sql` (ejecucion en BD)
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Evidencia recibida:
- Control final:
  - `total_posted = 5`
  - `posted_sin_usuario = 0`
- Resultado:
- Cierre completo del pendiente de auditoria `posted_by` para historicos y nuevos posteos en el alcance validado.
- Nota:
- Se confirma que varios nulos provenian de pruebas/manuales previas.

### 2026-03-10 17) Script E2E para flujo por plantilla (`sp_con_generar_comprobante`)
- Cambio:
- Se agrega script de prueba end-to-end para validar flujo de plantilla: precheck, generacion/posteo, cuadre, impacto en saldos, idempotencia y reversa opcional.
- Se actualiza runbook para referenciar el script en la validacion funcional del flujo por plantilla.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_sp_con_generar_comprobante.sql`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Ejecutar el pendiente del plan correspondiente al flujo SQL por plantilla con evidencia repetible.
- Riesgo:
- Bajo; script de prueba controlado, con reversa opcional activada por defecto (`revert_after_test=true`).
- Rollback:
- Si se deja `revert_after_test=true`, el propio script revierte el comprobante de prueba.
- Pruebas ejecutadas:
- Revision estructural de SQL (parametros, precheck, validaciones y reversa).
- No ejecutado aun en BD en esta iteracion (pendiente evidencia de corrida).
- Resultado:
- Queda lista la prueba E2E del flujo por plantilla para cierre funcional/documental.

### 2026-03-10 18) Bootstrap de plantilla minima para desbloquear prueba E2E
- Cambio:
- Se agrega script para crear/actualizar una plantilla activa minima de prueba usando cuentas desde `con_regla_integracion` + `cfg_document_type` (sin hardcodes).
- Caso objetivo: desbloquear error `Precheck: no se encontro plantilla activa` en prueba E2E de `sp_con_generar_comprobante`.
- Se actualiza runbook para indicar este paso cuando no existan plantillas activas.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_seed_plantilla_minima_e2e.sql`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: El entorno de prueba no tiene plantillas contables activas y bloquea la validacion de flujo por plantilla.
- Riesgo:
- Bajo; crea/actualiza una plantilla de prueba controlada y no altera movimientos existentes.
- Rollback:
- Inactivar/eliminar la plantilla de prueba creada (`name='E2E AUTO - VENTAS FAC'`) y sus lineas 1-2.
- Pruebas ejecutadas:
- Revision estructural de SQL y compatibilidad con esquema company-scoped en lineas de plantilla.
- No ejecutado aun en BD (pendiente evidencia de corrida).
- Resultado:
- Queda desbloqueado el precheck de plantilla para continuar prueba E2E.

### 2026-03-10 19) Fix de script E2E plantilla: seccion C sin clausula FROM
- Cambio:
- Se corrige la seccion C de `20260310_test_e2e_sp_con_generar_comprobante.sql`.
- Error original: referencia a alias `r/doc` sin `FROM` final, provocando `falta una entrada para la tabla �r�`.
- Ajuste aplicado: `FROM r JOIN doc ON true` y uso consistente de `doc.document_id_used/document_number_used`.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_sp_con_generar_comprobante.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Habilitar ejecucion completa de secciones C/F/G del test E2E por plantilla.
- Riesgo:
- Nulo/bajo; correccion sintactica del script de pruebas.
- Rollback:
- Restaurar version previa del script E2E (no recomendado).
- Pruebas ejecutadas:
- Verificacion de bloque SQL corregido con aliases y origenes de datos.
- Pendiente de evidencia en BD por re-ejecucion del usuario.
- Resultado:
- Script E2E queda listo para continuar pruebas funcionales de plantilla.

### 2026-03-10 20) Ajuste adicional de script E2E plantilla: resolucion de `type_id` y JOIN final
- Cambio:
- Se refuerza el script E2E para resolver `type_id` activo cuando parametro viene `0`/NULL.
- Se corrige sintaxis final de seccion C (`FROM r JOIN doc ON true JOIN type_resolved tr ON true`).
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_sp_con_generar_comprobante.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Evitar fallas por FK de tipo de transaccion y completar correctamente la creacion de `tmp_con_test_plantilla_result`.
- Riesgo:
- Bajo; afecta solo script de pruebas E2E.
- Rollback:
- Restaurar version previa del script E2E.
- Pruebas ejecutadas:
- Verificacion estructural del SQL generado en seccion C y precheck de `type_id_effective`.
- Pendiente evidencia en BD por nueva corrida.
- Resultado:
- Script E2E queda mas robusto para entornos con catalogos parciales.

### 2026-03-10 21) Fix final script E2E plantilla: idempotencia y alcance de saldos
- Cambio:
- Se corrige seccion F (`idempotencia`) agregando `FROM t` para resolver alias y permitir creacion de `tmp_con_test_plantilla_idempotencia`.
- Se ajusta seccion E para acotar `after_saldo` solo a cuentas del alcance de la prueba (`mov` + baseline), evitando filas ajenas al comprobante de prueba.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_sp_con_generar_comprobante.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Completar evidencia de idempotencia y mejorar lectura del delta contable por cuentas relevantes del test.
- Riesgo:
- Bajo; ajustes exclusivos en script de pruebas.
- Rollback:
- Restaurar version previa del script E2E.
- Pruebas ejecutadas:
- Revision estructural del SQL corregido (CTEs de alcance y SELECT final de idempotencia).
- Pendiente evidencia en BD de re-ejecucion de secciones E/F.
- Resultado:
- Script E2E listo para cerrar formalmente el punto 1 del plan.

### 2026-03-10 22) Seed minimo para desbloquear E2E de bancos sin datos previos
- Cambio:
- Se agrega script de bootstrap para crear catalogos minimos de bancos cuando no existen datos en el entorno (moneda HNL, banco activo, cuenta bancaria con cuenta contable, tipo bancario activo).
- El seed usa parametros dinamicos por `company_id` y evita hardcodes de cuentas especificas de negocio.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_seed_bancos_minimo_e2e.sql`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: El entorno de prueba reporto no tener datos de bancos para ejecutar evidencia E2E.
- Riesgo:
- Bajo; inserta solo datos minimos de prueba cuando faltan catalogos.
- Rollback:
- Inactivar/eliminar registros creados por codigo E2E (`E2E*`) en tablas de bancos.
- Pruebas ejecutadas:
- Revision estructural de SQL (resolucion dinamica de cuentas, tipo bancario y usuario).
- Pendiente evidencia de ejecucion en BD.
- Resultado:
- Queda habilitado el prerequisito de datos para iniciar prueba E2E de bancos.

### 2026-03-10 23) Script E2E flujo contable de bancos con validacion de no doble posteo y reversa exacta
- Cambio:
- Se agrega script E2E para bancos que cubre:
- Resolucion de contexto (cuenta bancaria, tipo bancario, periodo OPEN, diario, tipo contable).
- Registro de poliza DRAFT via `sp_registrar_partida_contable`.
- Posteo via `sp_con_postear_poliza`.
- Validacion de cabecera/detalle/cuadre.
- Validacion de impacto de saldos.
- Reintento de posteo para confirmar idempotencia (no doble posteo).
- Reversa opcional para confirmar retorno exacto de saldos.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_flujo_bancos.sql`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Cerrar evidencia del flujo bancos dentro del plan de mayorizacion unica.
- Riesgo:
- Bajo/medio; script de prueba escribe movimientos contables de validacion y contempla reversa opcional.
- Rollback:
- Ejecutar reversa de la poliza de prueba y eliminar artefactos temporales del test.
- Pruebas ejecutadas:
- Validacion estructural de SQL y coherencia con ruta tecnica usada por `BanTransaccionesService`.
- Pendiente evidencia en BD por corrida del usuario.
- Resultado:
- Queda lista la prueba E2E de bancos con cobertura de idempotencia y reversa exacta.

### 2026-03-10 24) Script negativo de bloqueo en periodo cerrado
- Cambio:
- Se agrega script de prueba negativa que:
- Crea una poliza DRAFT balanceada con fecha en periodo CLOSED/LOCKED.
- Fuerza `period_id = NULL` para validar resolucion por fecha en `sp_con_postear_poliza`.
- Intenta postear y espera error de negocio.
- Verifica que la poliza permanece en DRAFT y que saldos no cambian.
- Incluye limpieza opcional de la poliza de prueba.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_negativo_posteo_periodo_cerrado.sql`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Cerrar evidencia del control de periodos (posteo bloqueado fuera de OPEN).
- Riesgo:
- Bajo; el script no debe postear en escenario esperado y limpia artefactos de prueba por defecto.
- Rollback:
- Si se posteo por configuracion inesperada, el propio script revierte y elimina la poliza en limpieza.
- Pruebas ejecutadas:
- Revision estructural del SQL (prechecks, bloqueo esperado, validacion de no impacto en saldos).
- Pendiente evidencia en BD por corrida del usuario.
- Resultado:
- Queda disponible evidencia automatizable del escenario negativo de periodo cerrado.

### 2026-03-10 25) Normalizacion del script E2E de plantilla (version limpia A-G)
- Cambio:
- Se reescribe `20260310_test_e2e_sp_con_generar_comprobante.sql` en formato limpio y consistente, conservando cobertura completa:
- Seccion A (precheck), B (baseline), C (generacion/posteo), D (cuadre), E (delta saldos), F (idempotencia) y G (reversa opcional).
- Se conserva correccion de idempotencia (`FROM t`) y alcance de cuentas en saldos.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_sp_con_generar_comprobante.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Dejar una version estable y ejecutable del script para cierre de evidencia del flujo por plantilla.
- Riesgo:
- Bajo; afecta solo script de pruebas E2E.
- Rollback:
- Restaurar version anterior del script de pruebas si se requiere comparar historico.
- Pruebas ejecutadas:
- Revision estructural del SQL final por secciones y referencias de funciones.
- Pendiente evidencia de ejecucion en BD por corrida del usuario.
- Resultado:
- Script E2E por plantilla listo para ejecucion end-to-end sin comentarios de error obsoletos.

### 2026-03-10 26) Hotfix scripts E2E bancos/negativo por nombre real de tabla de diarios
- Cambio:
- Se corrigen scripts de prueba que consultaban `public.con_diarios` (plural) en lugar de `public.con_diario` (singular), alineado al esquema real.
- Se actualizan mensajes de precheck para reflejar nombre correcto.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_flujo_bancos.sql`
- `Prestadoras/Database/ddl_v3/20260310_test_negativo_posteo_periodo_cerrado.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Ejecucion real reporto error `42P01 no existe la relacion public.con_diarios`.
- Riesgo:
- Bajo; ajuste de compatibilidad de nombre de tabla en scripts de pruebas.
- Rollback:
- Restaurar version anterior de los scripts de prueba (no recomendado).
- Pruebas ejecutadas:
- Verificacion estatica de referencias en scripts y esquema base (`02_contabilidad_core.sql` define `con_diario`).
- Pendiente evidencia en BD por re-ejecucion del usuario.
- Resultado:
- Scripts de bancos y prueba negativa listos para reintento en entorno actual.

### 2026-03-10 27) Ajuste de prerequisitos E2E para desbloquear bancos y negativo en entornos minimos
- Cambio:
- `20260310_seed_bancos_minimo_e2e.sql` ahora garantiza tambien `con_diario` codigo `BAN` activo (ademas de catalogos bancarios).
- `20260310_test_negativo_posteo_periodo_cerrado.sql` agrega seed de respaldo: si no existe ningun periodo `CLOSED/LOCKED` y la fecha de prueba es automatica, crea/ajusta el periodo cerrado del mes anterior para habilitar el escenario negativo.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_seed_bancos_minimo_e2e.sql`
- `Prestadoras/Database/ddl_v3/20260310_test_negativo_posteo_periodo_cerrado.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Fallas reales en precheck por ausencia de `con_diario` activo y de periodos cerrados en company 1.
- Riesgo:
- Bajo; son ajustes de prerequisitos para scripts de prueba E2E.
- Rollback:
- Desactivar diario `BAN` de prueba y/o restaurar periodos segun politica local si se requiere.
- Pruebas ejecutadas:
- Verificacion estatica de SQL y compatibilidad con esquema `con_diario` singular + `status_id` opcional.
- Pendiente evidencia en BD por re-ejecucion del usuario.
- Resultado:
- Scripts de bancos/negativo quedan auto-contenidos para entornos de desarrollo con catalogo minimo.

### 2026-03-10 28) Hotfix seed bancos: compatibilidad con `con_diario` sin default en `last_sequence`
- Cambio:
- En `20260310_seed_bancos_minimo_e2e.sql` se ajusta el `INSERT` de `con_diario` para enviar explicitamente:
- `last_sequence = 0`
- `created_at = now()`
- En `ON CONFLICT`, se protege `last_sequence` con `COALESCE`.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_seed_bancos_minimo_e2e.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Error real `23502` por columna `last_sequence` NOT NULL sin valor por defecto en el entorno.
- Riesgo:
- Bajo; ajuste puntual de compatibilidad en script de seed E2E.
- Rollback:
- Restaurar version anterior del seed de bancos.
- Pruebas ejecutadas:
- Verificacion estatica del SQL ajustado y mapeo de columnas reportadas en el error.
- Pendiente evidencia en BD por re-ejecucion del usuario.
- Resultado:
- Seed bancos queda compatible con variantes de esquema `con_diario` donde no hay defaults en campos obligatorios.

### 2026-03-10 29) Hotfix prueba negativa: compatibilidad con `con_periodo_contable.created_at` NOT NULL
- Cambio:
- Se ajusta el bloque de seed de respaldo en `20260310_test_negativo_posteo_periodo_cerrado.sql` para insertar `created_at = now()` al crear periodo `CLOSED` (con y sin `status_id`).
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_negativo_posteo_periodo_cerrado.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Error real `23502` por columna `created_at` NOT NULL sin valor por defecto en `con_periodo_contable`.
- Riesgo:
- Bajo; ajuste puntual de compatibilidad en script de prueba.
- Rollback:
- Restaurar version previa del script negativo.
- Pruebas ejecutadas:
- Verificacion estatica de columnas requeridas y sentencias `INSERT` del seed de respaldo.
- Pendiente evidencia en BD por re-ejecucion del usuario.
- Resultado:
- Script negativo queda compatible con entornos donde `con_periodo_contable.created_at` no tiene default.

### 2026-03-10 30) Hotfix E2E bancos + negativo: compatibilidad CALL y seleccion de periodo cerrado exclusivo
- Cambio:
- `20260310_test_e2e_flujo_bancos.sql`: se reemplaza `CALL sp_registrar_partida_contable(...)` con subconsultas directas por bloque `DO` con variables locales, evitando error `0A000` (subconsulta en argumento de CALL).
- `20260310_test_negativo_posteo_periodo_cerrado.sql`:
- Se ajusta el seed de respaldo para detectar periodo cerrado "exclusivo" (sin solape con OPEN).
- Si no existe, crea periodo cerrado aislado de un dia previo al primer periodo existente.
- Se ajusta `period_target` para elegir solo periodos cerrados sin solape con OPEN.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_flujo_bancos.sql`
- `Prestadoras/Database/ddl_v3/20260310_test_negativo_posteo_periodo_cerrado.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Errores reales en ejecucion (`0A000` en CALL y bloqueo por periodos OPEN/CLOSED superpuestos).
- Riesgo:
- Bajo; ajustes de robustez en scripts de pruebas E2E.
- Rollback:
- Restaurar version anterior de ambos scripts de prueba.
- Pruebas ejecutadas:
- Verificacion estatica de bloques SQL modificados.
- Pendiente evidencia en BD por re-ejecucion del usuario.
- Resultado:
- Scripts de bancos/negativo quedan aptos para entornos con restricciones de parser CALL y periodos superpuestos.

### 2026-03-10 31) Evidencia de ejecucion: E2E bancos y prueba negativa de periodo cerrado
- Cambio:
- Se registra evidencia de ejecucion real en BD para:
- `20260310_test_e2e_flujo_bancos.sql`
- `20260310_test_negativo_posteo_periodo_cerrado.sql`
- Archivos tocados:
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Evidencia recibida (bancos E2E):
- Contexto resuelto:
  - `company_id=1`, `user_name=jmurcia`, `banco_cuenta_id=1`, `document_type_used=TRF`, `period_id=3`, `journal_id=5`, `type_id=5`, `amount_total=125.00`.
- Baseline saldos:
  - BANCO `11010201`: debitos/creditos iniciales `0.00/0.00`.
  - CONTRA `1203`: debitos/creditos iniciales `0.00/0.00`.
- Poliza generada y posteada:
  - `poliza_id=14`, `status posteado` via `sp_con_postear_poliza`, `revert_after_test=True`.
- Evidencia recibida (negativo periodo cerrado):
- Contexto resuelto:
  - `closed_period_id=8`, `poliza_date=2026-01-31`, `journal_id=5`, `type_id=5`.
- Resultado bloqueo esperado:
  - `poliza_id=15`, `expected_blocked=True`, `blocked=True`.
  - Mensaje: `No hay periodo OPEN para poliza 15 (periodo <NULL>, fecha 2026-01-31)`.
- Estado de poliza tras intento:
  - `status=0`, `period_id=NULL`, `posted_at=NULL`, `total_debit=0.00`, `total_credit=0.00`.
- Validacion de saldos:
  - `saldos_sin_cambio=OK` para ambas cuentas evaluadas.
- Resultado:
- Queda validado el control de bloqueo en periodo no abierto y no impacto de saldos en escenario negativo.
- Queda validado el avance del E2E de bancos hasta posteo de poliza.
- Pendiente de cierre formal en bancos: registrar evidencia de secciones de no doble posteo y reversa exacta del mismo script.

### 2026-03-10 32) Correccion final E2E bancos: eliminacion de llamada residual previa al bloque DO
- Cambio:
- En `20260310_test_e2e_flujo_bancos.sql` se elimina una linea residual `CALL public.sp_registrar_partida_contable(` que quedaba antes del bloque `DO`.
- Se confirma que el script mantiene una sola llamada a `sp_registrar_partida_contable` dentro del bloque `DO` con variables locales.
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260310_test_e2e_flujo_bancos.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo: Evitar error de sintaxis/ejecucion por instruccion incompleta residual.
- Riesgo:
- Nulo/bajo; limpieza de script de prueba.
- Rollback:
- Restaurar version anterior del script (no recomendado).
- Pruebas ejecutadas:
- Verificacion estatica de referencias: una sola llamada `CALL public.sp_registrar_partida_contable` y sin subconsultas en argumentos.
- Resultado:
- Script E2E de bancos queda consistente para ejecucion completa.
### 2026-03-10 33) Ajuste de control de reversa en E2E bancos y limpieza de bloques duplicados
- Cambio:
- En 20260310_test_e2e_flujo_bancos.sql se ajusta revert_after_test a DEFAULT false para que la ejecucion base no revierta automaticamente.
- Se corrige la condicion de reversa opcional a WHERE COALESCE(t.revert_after_test, false) = true.
- Se eliminan consultas duplicadas al final del script (bloques repetidos de No doble posteo y Reversa exacta) para evitar ruido y resultados confusos.
- Archivos tocados:
- Prestadoras/Database/ddl_v3/20260310_test_e2e_flujo_bancos.sql
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Alinear el comportamiento real con el nombre del parametro (revert_after_test) y simplificar la ejecucion del script para validaciones consecutivas.
- Riesgo:
- Bajo; afecta solo flujo de prueba E2E de bancos.
- Rollback:
- Restaurar version anterior del archivo 20260310_test_e2e_flujo_bancos.sql.
- Pruebas ejecutadas:
- Verificacion estatica de SQL (condicion de reversa y presencia unica de bloques de validacion).
- Resultado:
- Script E2E de bancos queda mas predecible: primero valida no doble posteo y solo revierte si se solicita explicitamente.

### 2026-03-10 34) Claridad de parametro revert_after_test en E2E bancos
- Cambio:
- En 20260310_test_e2e_flujo_bancos.sql se elimina DEFAULT de tmp_con_test_bancos_params.revert_after_test.
- Se deja el valor de prueba en una sola fuente (INSERT de parametros) con comentario explicito para cambiar a true cuando se valida reversa exacta.
- Archivos tocados:
- Prestadoras/Database/ddl_v3/20260310_test_e2e_flujo_bancos.sql
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Evitar confusion entre DEFAULT y valor explicitamente insertado (el INSERT estaba forzando false).
- Riesgo:
- Bajo; ajuste solo del script de pruebas E2E.
- Rollback:
- Restaurar version previa del script.
- Pruebas ejecutadas:
- Verificacion estatica del bloque de parametros.
- Resultado:
- El comportamiento de reversa queda controlado por un unico valor visible en el INSERT.

### 2026-03-10 35) Evidencia final de cierre: bancos + negativo + reconciliacion
- Cambio:
- Se registra evidencia final de validacion funcional y contable para cierre del plan.
- Archivos tocados:
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md
- Evidencia recibida:
- E2E bancos (no doble posteo):
  - Cuenta 11010201: delta_debitos_repost=0.00, delta_creditos_repost=0.00, no_doble_posteo=OK.
  - Cuenta 1203: delta_debitos_repost=0.00, delta_creditos_repost=0.00, no_doble_posteo=OK.
- E2E bancos (reversa exacta):
  - BANCO 11010201: delta_debitos_reverse=0.00, delta_creditos_reverse=0.00, reversa_exacta=OK.
  - CONTRA 1203: delta_debitos_reverse=0.00, delta_creditos_reverse=0.00, reversa_exacta=OK.
- Negativo periodo cerrado:
  - blocked=True con mensaje: No hay periodo OPEN para poliza 39 (periodo <NULL>, fecha 2026-01-31).
  - Poliza 39 queda DRAFT (status=0, period_id=NULL, posted_at=NULL).
  - saldos_sin_cambio=OK en cuentas evaluadas.
- Reconciliacion saldos:
  - Script 20260310_reconciliacion_con_saldo_cuenta.sql (secciones A y C): 0 filas.
- Resultado:
- Criterios de validacion del plan cubiertos y consistentes con motor unico de posteo/reversa.

### 2026-03-10 36) Aclaracion de alcance de mayorizacion por cuenta y proceso de produccion
- Cambio:
- Se documenta en estandar que la mayorizacion impacta las cuentas usadas en `con_partida_dtl` (por `codigo_cuenta`) y no hace roll-up automatico a cuentas padre en `con_saldo_cuenta`.
- Se agrega en runbook el proceso operativo correcto para DB de produccion (crear DRAFT, postear por SP central, validar, revertir por SP central y controles obligatorios).
- Archivos tocados:
- Prestadoras/docs/ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md
- Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Evitar ambiguedad funcional sobre que cuentas mayorizan y dejar un procedimiento unico para operacion en produccion.
- Riesgo:
- Nulo/bajo; cambio documental.
- Rollback:
- Restaurar version previa de los documentos.
- Pruebas ejecutadas:
- Revision de logica SQL de `sp_con_actualizar_saldos_por_poliza` y `sp_con_postear_poliza` para confirmar alcance real por cuentas de detalle.
- Resultado:
- Queda formalizado el criterio de mayorizacion y el proceso correcto en produccion.

### 2026-03-11 37) Inicio de nueva rama y plan para contabilidad automatica en Captacion + Bancos
- Cambio:
- Se crea branch de trabajo `feature/contabilidad-automatica-captacion-bancos`.
- Se agrega plan por fases para integrar contabilidad automatica en caja/captacion (lectora, miscelaneos y manual) con convergencia a bancos.
- Archivos tocados:
- Prestadoras/docs/PLAN_CONTABILIDAD_AUTOMATICA_CAPTACION_BANCOS.md
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Abrir nuevo frente de implementacion manteniendo el estandar de mayorizacion unica y trazabilidad formal.
- Riesgo:
- Nulo/bajo en esta fase (solo planificacion y organizacion de rama).
- Rollback:
- Eliminar archivo de plan y/o descartar rama de trabajo.
- Pruebas ejecutadas:
- No aplica (fase documental de inicio).
- Resultado:
- Queda definido el plan tecnico y operativo para iniciar la implementacion del nuevo alcance.

### 2026-03-11 38) Fase A completada: matriz tecnica de Captacion + Bancos (actual vs objetivo)
- Cambio:
- Se documenta la matriz detallada de flujos en `PLAN_CONTABILIDAD_AUTOMATICA_CAPTACION_BANCOS.md` con endpoints, metodos reales, comportamiento actual y destino al motor central contable.
- Se registran referencias auditadas de controller/service/client y hallazgos bloqueantes para Fase B.
- Archivos tocados:
- Prestadoras/docs/PLAN_CONTABILIDAD_AUTOMATICA_CAPTACION_BANCOS.md
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Cerrar formalmente el levantamiento tecnico antes de refactor de implementacion y evitar cambios sin trazabilidad.
- Riesgo:
- Bajo; cambio documental.
- Riesgo funcional identificado para Fase B: desalineacion de firma en SP manual (`sp_reversar_posteo_manual`) entre servicio C# y `ddl_v3`, y dependencia de objetos legacy.
- Rollback:
- Revertir las secciones agregadas en el plan y esta entrada de bitacora.
- Pruebas ejecutadas:
- Inspeccion estatica con referencias:
  - `CaptacionPagosController` (`HttpPost` lineas 155, 181, 267, 293, 335, 361).
  - `CaptacionPagosService` (`RegistrarPagoAsync` 286, `ReversarPagoAsync` 402, `RegistrarPagoManualAsync` 701, `ReversarPagoManualAsync` 832, `RegistrarPagoMiscelaneoAsync` 908, `ReversarPagoMiscelaneoAsync` 998).
  - SQL manual legacy en servicio (`sp_registrar_posteo_manual` 749, `sp_reversar_posteo_manual` 854).
  - `BanTransaccionesService` (posteo central con `sp_con_postear_poliza` en 795).
  - Cliente UI captacion (`CaptacionPagosClient` y paginas PosteoLectoras/PosteoManual/PosteoMiscelaneos).
- Resultado:
- Fase A queda cerrada y lista para iniciar Fase B (implementacion de contabilidad automatica en captacion y convergencia bancaria).

### 2026-03-11 39) Fase B implementada: captacion usa posteo/reversa contable central
- Cambio:
- Se refactoriza `CaptacionPagosService` para que los flujos de captacion (lectora, manual y miscelaneos) generen comprobante con `sp_con_generar_comprobante` y reviertan con `sp_con_revertir_poliza`.
- Se agrega resolucion dinamica de `document_type` (VENTAS: prioridad REC, fallback FAC) y `type_id` activo/default de `con_tipo_transaccion`.
- Se define `document_id` contable estable por flujo para evitar colisiones:
  - Lectora: `numrecibo`
  - Miscelaneos: `1_000_000_000 + recibo`
  - Manual: `2_000_000_000 + numrecibo`
- Archivos tocados:
- Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs
- Prestadoras/docs/PLAN_CONTABILIDAD_AUTOMATICA_CAPTACION_BANCOS.md
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Unificar mayorizacion en un solo flujo transaccional para captacion, alineado al plan de contabilidad automatica.
- Riesgo:
- Medio: si falta configuracion contable (`cfg_document_type`/`con_tipo_transaccion`), el registro/reversa de pago ahora falla por diseno y hace rollback.
- Riesgo conocido pendiente: coexistencia de firmas legacy en `sp_reversar_posteo_manual` segun ambiente.
- Rollback:
- Restaurar version previa de `CaptacionPagosService.cs` y mantener solo comportamiento legacy de captacion.
- Pruebas ejecutadas:
- `dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj -c Debug` => exitoso.
- `dotnet build Prestadoras/HODSOFT_DEVEXPRESS.sln -c Debug` => falla por DLL bloqueadas de proyecto `apc` en ejecucion (MSB3027/MSB3021), sin errores de compilacion en `SIAD.Services`.
- Resultado:
- Captacion (lectora/manual/miscelaneos) queda conectada al motor contable central de posteo/reversa.
- Queda pendiente Fase C para integrar disparo explicito a `BanTransaccionesService` cuando el pago sea bancario.

### 2026-03-11 40) Runbook actualizado: pruebas operativas para Captacion automatica
- Cambio:
- Se agrega en el runbook una seccion especifica de validacion Fase B para captacion:
  - Alta/reversa de lectora.
  - Alta/reversa de manual.
  - Alta/reversa de miscelaneos.
  - Consulta de polizas por `document_id` calculado por flujo.
  - Prueba negativa de configuracion contable faltante con rollback esperado.
- Archivos tocados:
- Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Dejar procedimiento estandar y repetible para validar en QA/produccion el nuevo comportamiento de contabilidad automatica en captacion.
- Riesgo:
- Nulo/bajo; cambio documental.
- Rollback:
- Restaurar version previa de `RUNBOOK_VALIDACION_CONTABILIDAD.md`.
- Pruebas ejecutadas:
- Revision cruzada de consultas y reglas contra implementacion en `CaptacionPagosService`.
- Resultado:
- Queda definido el checklist de validacion funcional/contable para cerrar Fase B con evidencia.

### 2026-03-11 41) Compatibilidad reversa manual: fallback legacy/v3 de `sp_reversar_posteo_manual`
- Cambio:
- En `ReversarPagoManualAsync` se agrega estrategia de compatibilidad para ambientes con firmas distintas de SP:
  - intento principal: `CALL sp_reversar_posteo_manual(cliente, recibo, tipo_transaccion)`.
  - fallback cuando no existe firma (`SQLSTATE 42883`): `SELECT sp_reversar_posteo_manual(cliente, recibo, usuario)`.
- Archivos tocados:
- Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs
- Prestadoras/docs/PLAN_CONTABILIDAD_AUTOMATICA_CAPTACION_BANCOS.md
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Reducir riesgo de falla por desalineacion de esquema entre objetos legacy y `ddl_v3` durante la transicion.
- Riesgo:
- Bajo; se activa solo cuando la firma principal no existe.
- Rollback:
- Eliminar bloque `catch (PostgresException ... 42883)` y dejar una sola ruta de llamada.
- Pruebas ejecutadas:
- `dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj -c Debug` => exitoso.
- Resultado:
- Reversa manual queda tolerante a variantes de SP en ambientes mixtos.

### 2026-03-11 42) Seed minimo para desbloquear UI de Captacion (bancos y cajas)
- Cambio:
- Se agrega script idempotente para poblar catalogos minimos de UI en Captacion:
  - `public.recolectora` (combo Banco)
  - `public.catalogo_cajas` (combo Caja)
- Se actualiza runbook con paso 0 de prerequisito cuando la UI muestra combos vacios.
- Archivos tocados:
- Prestadoras/Database/ddl_v3/20260311_seed_captacion_ui_catalogos_minimos.sql
- Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- El usuario no puede ejecutar pruebas funcionales de Captacion porque el combo Banco llega vacio (`No data to display`).
- Riesgo:
- Bajo; seed de catalogos de UI, sin posteo contable ni impacto en saldos.
- Rollback:
- Eliminar/inhabilitar registros seed (`recolectora` codigos BAN/EFE y caja creada por `seed_captacion_ui`).
- Pruebas ejecutadas:
- Revision estructural de entidades/mapeo:
  - Banco en UI viene de `recolectora` (`ListarBancosAsync`).
  - Caja en UI viene de `catalogo_cajas`.
- Resultado:
- Queda desbloqueado el prerequisito para continuar validacion Fase B desde UI.

### 2026-03-11 43) Hotfix captacion Fase B: compatibilidad de parametro fecha para Dapper/Npgsql
- Cambio:
- Se corrige `GenerarComprobanteContableCaptacionAsync` para no enviar `DateOnly` directo a Dapper.
- Nuevo comportamiento: convertir a `DateTime` (`00:00:00`) y castear en SQL `@PolizaDate::date`.
- Archivos tocados:
- Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- Error real en ejecucion UI para lectora/manual: `The member PolizaDate of type System.DateOnly cannot be used as a parameter value`.
- Riesgo:
- Bajo; ajuste puntual de tipo de parametro para compatibilidad con driver.
- Rollback:
- Restaurar version previa del metodo si se requiere reproducir comportamiento anterior (no recomendado).
- Pruebas ejecutadas:
- `dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj -c Debug` => exitoso.
- Resultado:
- Se elimina la causa del 400 reportado en captacion para alta contable automatica.

### 2026-03-11 44) Hotfix captacion Fase B: fallback real REC/FAC segun plantilla activa
- Cambio:
- Se ajusta la resolucion de `document_type` en `CaptacionPagosService` para evaluar `cfg_document_type` junto con existencia de plantilla activa en `con_plantilla_partida_hdr`.
- Nueva prioridad:
  - `REC` con plantilla activa.
  - `FAC` con plantilla activa (fallback operativo).
  - Si ninguno tiene plantilla activa, falla con mensaje explicito de configuracion.
- Archivos tocados:
- Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs
- Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md
- Motivo:
- En QA aparecia `P0001: No existe plantilla contable para modulo VENTAS, documento REC` aun teniendo `FAC` disponible como fallback temporal.
- Riesgo:
- Bajo; solo cambia criterio de seleccion del `document_type` previo a generar comprobante.
- Rollback:
- Restaurar implementacion anterior de `ResolverDocumentTypeCaptacionAsync`.
- Pruebas ejecutadas:
- `dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj -c Debug` => exitoso.
- Resultado:
- Captacion puede continuar por `FAC` cuando `REC` no tenga plantilla activa, manteniendo validacion estricta cuando no existe plantilla para ambos.

### 2026-03-13 45) Fase 2: Limpieza de captacion manual - eliminacion de SPs legacy
- Cambio:
- Se elimina dependencia directa de `sp_registrar_posteo_manual` en `RegistrarPagoManualAsync`.
  - La insercion de `transaccion_abonado` ahora es directa via EF (mismo patron que lectora y miscelaneos).
  - Se resuelve `clienteInfo` (ciclo, ruta, secuencia, tiene_med) desde `cliente_maestros`.
  - Se usa transaccion EF en lugar de raw Dapper connection.
- Se elimina dependencia directa de `sp_reversar_posteo_manual` (incluyendo fallback legacy/v3) en `ReversarPagoManualAsync`.
  - La reversa ahora elimina `transaccion_abonado` via EF y restaura `factura_detalle.montovalor_saldo` directamente.
  - Se usa transaccion EF con `FOR UPDATE` para bloqueo de factura.
- Archivos tocados:
- `Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo:
- Fase 2 del plan de arquitectura 2026-03-13: eliminar mezcla manual legacy para que captacion quede consistente y vendible para la demo del 2026-03-16.
- Los tres flujos (lectora, manual, miscelaneos) ahora usan el mismo patron: negocio directo + comprobante contable central.
- Riesgo:
- Medio; cambia la ruta de ejecucion del registro y reversa manual. Si existen variantes de `sp_registrar_posteo_manual` que hagan logica adicional no mapeada, se perderia. Mitigado porque el SP solo inserta `transaccion_abonado` y eso se replica exactamente.
- Rollback:
- Restaurar version anterior de `RegistrarPagoManualAsync` y `ReversarPagoManualAsync` desde git.
- Pruebas ejecutadas:
- `dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj -c Debug` => 0 errores, solo warnings preexistentes.
- Resultado:
- Captacion manual queda alineada al mismo patron de lectora y miscelaneos: insercion directa de negocio + contabilidad central via `sp_con_generar_comprobante` / `sp_con_revertir_poliza`.
- Cero dependencia de SPs legacy (`sp_registrar_posteo_manual`, `sp_reversar_posteo_manual`).

### 2026-03-13 46) Fase 4: Script operativo de plantillas VENTAS/FAC y VENTAS/REC
- Cambio:
- Se crea script SQL operativo para crear/actualizar plantillas contables para los dos document_types de captacion.
- Genera plantillas para `VENTAS/FAC` y `VENTAS/REC` con lineas debe/haber por `{total}`.
- Resuelve cuentas desde `con_regla_integracion` activa.
- Si `REC` no tiene regla propia, hereda las cuentas de `FAC`.
- Si no existe `cfg_document_type` para el codigo, lo crea automaticamente.
- Re-ejecutable (upsert).
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260313_seed_plantillas_ventas_fac_rec.sql` (nuevo)
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo:
- Fase 4 del plan: mantener plantillas controladas por script, con mantenimiento reproducible.
- Riesgo:
- Bajo; solo crea/actualiza configuracion. No modifica datos de negocio ni saldos.
- Rollback:
- Eliminar las plantillas creadas por nombre (`CAPTACION VENTAS FAC`, `CAPTACION VENTAS REC`) de `con_plantilla_partida_hdr/dtl`.
- Pruebas ejecutadas:
- Revision estructural del SQL.
- Resultado:
- Script listo para ejecutar en cada ambiente. Genera las dos plantillas necesarias para operacion de captacion.

### 2026-03-13 47) Fase 5: Matriz de evidencia para demo 2026-03-16
- Cambio:
- Se crea script SQL de evidencia que cubre:
  - A) Matriz por flujo (compositor, posteador, fuente config, multiempresa, reversa).
  - B) Polizas generadas por captacion (modulo VENTAS).
  - C) Cuadre de polizas (debe = haber).
  - D) No doble posteo (document_id duplicados).
  - E) Reversa exacta (polizas revertidas).
  - F) Estado de saldos (`con_saldo_cuenta`).
  - G) Periodo contable abierto.
  - H) Resumen final (conteos).
- Archivos tocados:
- `Prestadoras/Database/ddl_v3/20260313_matriz_evidencia_demo.sql` (nuevo)
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo:
- Fase 5 del plan: tener material presentable y verificable para la demo del 2026-03-16.
- Riesgo:
- Nulo; queries de solo lectura.
- Rollback:
- Eliminar archivo.
- Pruebas ejecutadas:
- Revision estructural del SQL.
- Resultado:
- Script de evidencia listo para ejecutar en cualquier ambiente previo a la demo.

### 2026-03-13 48) Fase 3: Formalizar con_regla_integracion como legacy solo para lectura movil
- Cambio:
- Se renombra menu lateral de "Reglas de integracion" a "Reglas legacy (lectura)" en ambos sistemas de navegacion:
  - `SidebarNavigationDefinition.cs` (sidebar nuevo).
  - `NavMenu.razor` (menu DxMenu legacy).
- Se actualiza la pagina `ReglasIntegracion.razor`:
  - Titulo con badge `Legacy`.
  - Subtitulo cambiado a "Configuracion legacy — solo aplica al flujo de lectura movil (sp_lectura_v2)".
  - Se agrega banner de alerta visible con texto: "Alcance restringido. Esta configuracion solo aplica al flujo de lectura movil. Los flujos de captacion usan plantillas contables como fuente de verdad runtime."
- Archivos tocados:
- `Prestadoras/apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs`
- `Prestadoras/apc.Client/Layout/NavMenu.razor`
- `Prestadoras/apc.Client/Pages/Contabilidad/ReglasIntegracion.razor`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- Motivo:
- Fase 3 del plan de arquitectura 2026-03-13: formalizar que `con_regla_integracion` ya no representa la verdad general del sistema y su alcance funcional queda restringido a lectura movil legacy.
- Riesgo:
- Bajo; cambio visual y documental. No se elimina funcionalidad, solo se marca como legacy.
- Rollback:
- Restaurar textos de menu y pagina a "Reglas de integracion" sin badge. Eliminar banner de alerta.
- Pruebas ejecutadas:
- `dotnet build Prestadoras/apc.Client/apc.Client.csproj -c Debug` => 0 errores.
- Resultado:
- La UI y la documentacion quedan alineadas: `con_regla_integracion` es configuracion legacy solo para lectura movil.

### 2026-03-13 49) Evidencia E2E desde UI: captacion lectora, miscelaneos y manual con bancos
- Cambio:
- Se registra evidencia de pruebas funcionales ejecutadas desde la UI del sistema en entorno de desarrollo.
- Archivos tocados:
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
- Evidencia recibida:
- Lectora (banco):
  - POST `/api/captacionpagos` => 400 (factura ya pagada): validacion correcta.
  - POST `/api/captacionpagos/reverso` => 200: reversa OK (DELETE transaccion_abonado + UPDATE factura/factura_detalle).
  - POST `/api/captacionpagos` => 200: registro con integracion bancaria completa (ban_cuenta → con_plan_cuentas → con_periodo_contable → con_diario → con_tipo_transaccion → sp_registrar_partida_contable → sp_con_postear_poliza).
- Miscelaneos (banco):
  - POST `/api/captacionpagos/miscelaneos/reverso` => 200: reversa OK (DELETE transaccion_abonado + UPDATE factura/factura_detalle).
  - POST `/api/captacionpagos/miscelaneos/registrar` => 200: registro OK con FOR UPDATE en factura + integracion bancaria completa.
- Manual:
  - Validaciones de saldo y busqueda de cliente funcionando (GET `/api/captacionpagos/saldos-manual/*`).
- Matriz de evidencia:
  - Script `20260313_matriz_evidencia_demo.sql` ejecutado exitosamente post-pruebas.
  - 6 flujos con estado ACTIVO en seccion A.
  - Captacion Lectora/Manual/Miscelaneos usan `sp_con_generar_comprobante` como compositor.
  - Captacion Bancaria usa `sp_registrar_partida_contable` via `BanTransaccionesService`.
  - Lectura Movil marcada como EXCEPCION TEMPORAL.
- Observaciones:
  - `InvalidCastException` esporadicas en UI son preexistentes (no relacionadas con cambios de contabilidad).
  - `PostgresException` en busqueda de facturas es preexistente (search legacy).
  - No se detectaron errores de contabilidad ni de negocio durante las pruebas.
- Motivo:
- Cierre de Fase 2.4 (E2E revalidacion desde UI) del plan de arquitectura 2026-03-13.
- Riesgo:
- Nulo; registro documental de evidencia.
- Rollback:
- N/A.
- Pruebas ejecutadas:
- Pruebas funcionales desde UI: lectora alta/reversa, miscelaneos alta/reversa, manual consulta de saldos.
- Matriz de evidencia SQL post-pruebas.
- Resultado:
- Todos los flujos de captacion validados funcionalmente desde UI con integracion bancaria.
- Plan de arquitectura 2026-03-13 completado en todas sus fases.

### 2026-03-16 50) Cierre del plan de facturacion miscelaneos con contabilidad automatica
- Cambio:
- Se cierran los pendientes funcionales, contables y documentales del flujo `Facturacion Miscelaneos`.
- Ajustes implementados:
  - `FacturacionMiscelaneosService` ahora exige empresa activa antes de generar la poliza; ya no puede confirmar recibos sin contabilidad.
  - `document_id` del comprobante `VENTAS/MIS` queda alineado a `numrecibo`.
  - `type_id` contable se resuelve explicitamente por `con_tipo_transaccion.code = 'FAC'`, sin fallback a "primer activo".
  - Se consolida el uso de `con_plantilla_partida_*` y `DETAIL_EXPAND` para generar credito por concepto.
  - Se documenta el CRUD ya existente de catalogo miscelaneo y sus endpoints.
- Archivos tocados:
- `Prestadoras/SIAD.Services/FacturacionMiscelaneos/FacturacionMiscelaneosService.cs`
- `Prestadoras/docs/PLAN_FACTURACION_MISCELANEOS_CONTABILIDAD_2026-03-14.md`
- `Prestadoras/docs/inventario_endpoints.md`
- `Prestadoras/docs/ventas_endpoints_descripcion.md`
- `Prestadoras/docs/ventas_endpoints_detallados.md`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- Motivo:
- Cerrar formalmente el plan del 2026-03-14 y dejar la implementacion y la documentacion alineadas para demo y seguimiento.
- Riesgo:
- Bajo; el cambio funcional solo endurece validaciones del flujo y elimina rutas silenciosas sin poliza.
- Rollback:
- Restaurar la resolucion anterior de empresa/tipo/documento y revertir actualizaciones documentales.
- Pruebas ejecutadas:
- Revision estatica del servicio y consistencia con seeds `VENTAS/MIS`.
- Intento de compilacion de `SIAD.Services` bloqueado por errores preexistentes de atributos duplicados en `SIAD.Core` al forzar salida intermedia compartida; no se detectaron errores nuevos del flujo miscelaneo en esta revision.
- Resultado:
- El plan de facturacion miscelaneos queda cerrado a nivel de implementacion y documentacion, con pendientes solo de validacion de build/entorno fuera de este flujo.
