# Runbook — Ventana de deploy a 172.16.0.9 (acumulado al 2026-08-04)

Cola completa desde la última ventana de DDLs (2026-07-20). **Nada de esto se
aplica fuera de la ventana**; el usuario decide la fecha. Ejecutar TODO en una
sola ventana: los binarios nuevos y los DDLs son interdependientes (el freeze
del legacy rompe al portal viejo, y el portal nuevo requiere el modelo
`adm_pago`).

Referencia del procedimiento general (publish por host, IIS/servicios, humo):
[RUNBOOK_DEPLOY_2026-07.md](RUNBOOK_DEPLOY_2026-07.md). Este documento es la
**cola específica** de esta ventana.

## 0. Preparación (antes de tocar nada)

1. **Backup completo** de `siad_v3` en 0.9:
   `Database/backup_bd_simple.ps1` → guardar en `Database/Backups/` con fecha.
   Es el rollback de TODA la ventana.
2. Verificar espacio en disco del servidor y que no haya usuarios activos.
3. Congelar el portal viejo (detener el sitio) ANTES de los DDLs de F7 — el
   binario viejo escribe `transaccion_abonado` y el freeze lo va a reventar.
4. Tener a mano `appsettings.Local.json` del servidor (credenciales reales) —
   los publish NO llevan credenciales.

## 1. DDLs en orden (33 scripts + 1 vista)

Aplicar con `psql --set ON_ERROR_STOP=1 -f <script>` en este orden. Todos son
idempotentes o transaccionales; si uno falla, PARAR y evaluar (no saltar).

### Bloque A — Almacén 2.0 / Presupuesto / Proveedores (Emilio)

| # | Script | Qué hace |
|---|---|---|
| 1 | `2026-07-21_cheques_numeracion_bitacora.sql` | ban_cheque + bitácora + numeración por cuenta |
| 2 | `2026-07-23_prv_compromiso_dtl_conceptodtl_1000.sql` | ensancha concepto |
| 3 | `2026-07-23_prv_compromiso_dtl_descripcion_1000.sql` | ensancha descripción |
| 4 | `2026-07-24_fix_fn_pst_next_id_dtl_ids_no_numericos.sql` | fix fn presupuesto |
| 5 | `2026-07-24_presupuesto_multitenant_company_id.sql` | company_id en pst_config (backfill co. 2) |
| 6 | `2026-07-27_bitacora_config_contactos.sql` | catálogo auditoría contactos |
| 7 | `2026-07-27_proveedor_contactos.sql` | prv_tipo_contacto + prv_proveedor_contacto |
| 8 | `2026-07-28_presupuesto_completar_ddl_valor_real.sql` | valor_real paso 18 |
| 9 | `viewPostgrets/view_lista_configuracion_presupuesto.sql` | vista actualizada (después del #8) |
| 10 | `2026-07-29_alm_articulo_activo.sql` | columna activo del artículo |

### Bloque B — Unificación de cobranza F1–F6

| # | Script | Qué hace |
|---|---|---|
| 11 | `2026-07-26_uc_f1_estados_numericos_catalogos.sql` | catálogos de estados numéricos + trigger sync factura |
| 12 | `2026-07-26_uc_f2_adm_pago_modelo.sql` | adm_pago + adm_pago_aplicacion (EL modelo de cobros) |
| 13 | `2026-07-26_uc_f2_cajas_fisicas.sql` | cajas físicas |
| 14 | `2026-07-26_uc_f3_caja_usuario.sql` | sesión de caja por usuario |
| 15 | `2026-07-27_uc_ajustes_caja.sql` | ajustes de caja |
| 16 | `2026-07-27_uc_recibo_banco_conciliacion.sql` | recibo pendiente para banco |
| 17 | `2026-07-28_uc_arqueo_caja_y_porcentajes.sql` | arqueo + adm_desglose_abono_porcentaje |
| 18 | `2026-07-28_uc_f4_lectura_carry_documentos.sql` | carry de documentos en lectura |
| 19 | `2026-07-28_uc_f4_sp_saldo_documentos.sql` | saldo por documentos |
| 20 | `2026-07-28_uc_f4_rep_saldos_vigencia.sql` | reportes de saldo |
| 21 | `2026-07-28_uc_f5_ws_adm_pago.sql` | WS bancario paga sobre adm_pago |
| 22 | `2026-07-29_uc_f6_plan_cuotas_documento.sql` | cuotas de convenio como documento |

### Bloque C — F7 (orden interno OBLIGATORIO: los SPs sin espejo van ANTES del freeze)

| # | Script | Qué hace |
|---|---|---|
| 23 | `2026-07-30_uc_f7_recibo_banco_pendiente.sql` | H1 recibo pendiente |
| 24 | `2026-07-30_uc_f7_nc_aplica_documento.sql` | H2 NC aplica a documento |
| 25 | `2026-07-30_uc_f7_nd_cobrable.sql` | H2b ND cobrable + saldo v6 |
| 26 | `2026-07-30_uc_f7_sps_sin_espejo.sql` | H2c SPs definitivos SIN espejo (emisión, NC/ND, saldo v7) |
| 27 | `2026-07-30_uc_f7_h4_freeze_legacy.sql` | H4 **CONGELA transaccion_abonado** (trigger) |
| 28 | `2026-07-30_uc_f7_h4b_conciliacion_sin_espejo.sql` | H4b conciliación |
| 29 | `2026-07-30_uc_f7_h5_limpieza_sql.sql` | H5 limpieza |
| 30 | `2026-07-30_uc_f7_h5_reportes_modelo_nuevo.sql` | H5 reportes al modelo nuevo |
| 31 | `2026-07-30_uc_f7_h5c_perf_reportes.sql` | H5c índices/perf (rep_transacciones 12 min) |
| 32 | `2026-07-30_idx_factura_busqueda_caja.sql` | índices búsqueda caja |

### Bloque D — GPS cuadrillas + reportes + fixes de agosto

| # | Script | Qué hace |
|---|---|---|
| 33 | `2026-07-28_historico_coordenadas_empleado.sql` | histórico GPS |
| 34 | `2026-07-28_idx_coordenadas_empleado_mapa.sql` | índice mapa |
| 35 | `2026-07-28_retencion_coordenadas_empleado.sql` | retención GPS |
| 36 | `2026-07-31_rep_banco_diario.sql` | informe banco diario (función + catálogo + layout) |
| 37 | `2026-08-01_cliente_acueducto.sql` | acueducto a nivel cliente + backfill |
| 38 | `2026-08-01_cliente_recategorizacion.sql` | bitácora de recategorización |
| 39 | `2026-08-01_motivo_nd_gestion_legal.sql` | motivo ND GESTION_LEGAL (MAX+1, verificar id) |
| 40 | `2026-08-02_saneo_factura_estado_id.sql` | saneo letra↔estado_id (verifica 0 descuadres) |
| 41 | `2026-08-04_categoria_regulatoria_equivalencia_contable.sql` | puente categoría tarifa↔contable |
| 42 | `2026-08-04_ncnd_factura_migrada_fallback_numrecibo.sql` | NC/ND contra facturas sin número fiscal |

### ⛔ NO aplicar en esta ventana

- `2026-07-28_m3a..e_*.sql` y `2026-07-29_m4_*.sql` — son la **migración total
  SIMAFI** (M2–M4): reconstruyen la cartera completa. Van en el **cutover**,
  ventana aparte con su propio plan.
- `2026-07-16_backup_sp_obtener_cliente_saldo.sql` — es rollback, no deploy.
- CAIs de prueba locales (`CAI-PRUEBA-*`) — datos de prueba, JAMÁS a 0.9.
- `appsettings.Local.json` local ni credenciales.

## 2. Datos post-DDL (misma ventana)

1. **Abrir el período contable de agosto** (202608) en 0.9 — sin él, todas las
   partidas nuevas se encolan (pantalla Períodos o INSERT como el de
   `con_periodo_contable` usado en local).
2. Verificar auditoría de estados: `SELECT estado, estado_id, count(*) FROM
   factura GROUP BY 1,2;` → solo pares A/1, B/4, C/2, N/3.
3. Verificar equivalencias de categoría regulatoria (4 filas con
   `categoria_servicio_id` no nulo).

## 3. Publish (los 3 hosts, explícitos — NO `-Solo todos`)

Según [RUNBOOK_DEPLOY_2026-07.md](RUNBOOK_DEPLOY_2026-07.md):
1. `./publish-onprem.ps1 -Solo portal` → desplegar.
2. `./publish-onprem.ps1 -Solo mobileapi` → desplegar (comparte SIAD.Services).
3. `./publish-onprem.ps1 -Solo bancosws` → desplegar (firma nueva de
   RegistrarMovimientoAsync vive en SIAD.Services).

## 4. Humo (mínimo, en este orden)

1. Login + ficha de cliente (Acueducto, Estudio socioeconómico, No cortable).
2. **Caja**: cobrar una factura en efectivo → recibo; verificar `adm_pago`.
3. Intentar cobrar un bloqueado → candado con mensaje.
4. Estado de cuenta: movimientos + botón **Imprimir PDF**.
5. Tarifario V3: cambiar categoría a un cliente con deuda → aviso fijo con
   póliza; verla en `/contabilidad/partidas`.
6. Notas: emitir ND (motivo Gestión legal) contra una factura **migrada** →
   debe emitir con el número de recibo; verla en el estado de cuenta.
7. Convenios: pestaña planes → **Imprimir** (PDF con cuotas y firmas).
8. Reportería: Banco diario (rango ≤31 días).
9. App lectores: login + GetCiclo + snapshot de un cliente.
10. WS bancario: consulta de saldo de un cliente (golden case).

## 5. Rollback

- DDLs + datos: restaurar el backup del paso 0 (`Database/restore_bd.ps1`).
- Binarios: re-desplegar el publish anterior (conservar el actual renombrado
  antes de copiar el nuevo).
- No hay rollback parcial: si el freeze (script 27) ya corrió y algo falla
  después, o se termina la ventana o se restaura completo.
