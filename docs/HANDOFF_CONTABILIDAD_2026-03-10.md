# Handoff Contabilidad - 2026-03-10

## Estado al cierre
- Plan de unificacion de mayorizacion sigue vigente.
- Flujo movil `ActualizarLecturaV2` validado: retorna `1`, genera factura y poliza contable.
- Hotfix aplicado para posteo central:
  - `con_plan_cuenta` -> `con_plan_cuentas`.
- Ajuste de auditoria aplicado en `sp_con_postear_poliza`:
  - intenta guardar `posted_by` con `p_user` numerico o por mapeo `usuarioapc.usuario -> usuarioapc.ide`.
- Script formal de reconciliacion/rebuild disponible:
  - `Prestadoras/Database/ddl_v3/20260310_reconciliacion_con_saldo_cuenta.sql`.
- Script formal de backfill de auditoria disponible:
  - `Prestadoras/Database/ddl_v3/20260310_backfill_con_partida_hdr_posted_by.sql`.
- Poliza manual validada en POSTED:
  - `poliza_id=8`, `status=1`, `period_id=3`.
  - `posted_by=1` (`jmurcia`) y `posted_at` registrado.

## Hallazgos abiertos
1. Semantica de facturacion legacy confirmada:
- `factura.saldototal` = saldo acumulado cliente (no solo cargo del periodo).
- Contabilidad automatica contabiliza el documento actual (detalle), no el acumulado.

2. Cierre funcional E2E pendiente:
- Ejecutar y documentar evidencia completa en flujo bancos y flujo por plantilla.
- Validar escenario de posteo en periodo cerrado y control de no doble posteo.

## Que si esta correcto
- Cuadre contable por documento probado:
  - `con_partida_dtl`: Debe = Haber.
  - `con_partida_hdr`: en polizas posteadas, `status=1` y totales consistentes.
- `servicios.cont_account_id` mapeado correctamente (si dos servicios apuntan a la misma cuenta, ambos creditos van a la misma cuenta).

## Primer bloque para continuar (orden recomendado)
1. Ejecutar pruebas E2E de flujo por plantilla (`sp_con_generar_comprobante`) y documentar evidencia.
2. Ejecutar pruebas E2E de flujo bancos y documentar evidencia.
3. Probar posteo en periodo cerrado (debe bloquear) y documentar mensaje/control.
4. Re-ejecutar consulta de auditoria global:
```sql
SELECT
  COUNT(*) FILTER (WHERE status = 1) AS total_posted,
  COUNT(*) FILTER (WHERE status = 1 AND posted_by IS NULL) AS posted_sin_usuario
FROM public.con_partida_hdr;
```
5. Cerrar fase documental en bitacora + runbook.

## Criterio de cierre de la fase
- Reconciliacion sin diferencias.
- `posted_by` resuelto para nuevos posteos y plan de backfill aplicado.
- Evidencia completa en:
  - `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
  - `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`

## Archivos clave para retomar
- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
- `Prestadoras/Database/ddl_v3/20260310_hotfix_sp_con_actualizar_saldos_por_poliza_con_plan_cuentas.sql`
- `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`
- `Prestadoras/docs/BITACORA_CAMBIOS_CONTABILIDAD.md`
- `Prestadoras/docs/RUNBOOK_VALIDACION_CONTABILIDAD.md`
