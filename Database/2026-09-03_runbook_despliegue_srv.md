# Runbook de despliegue a SRV — Cierre de las cuatro tandas abiertas

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-09-03
**Alcance:** 14 scripts. Cierra las tandas de `2026-08-22`, `2026-08-27`, `2026-08-31` y
`2026-09-01`, que estaban todas abiertas a la vez.
**Estado:** **APLICADO Y VERIFICADO** el 2026-09-03.

> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`.
>
> ⚠️ Este runbook **cierra** los cuatro anteriores. No re-ejecutar aquellos.

---

## 1. Por qué hubo que hacer esto de golpe

No existe tabla de control de migraciones para el esquema funcional, así que el estado real se
dedujo comprobando la existencia de cada objeto en la base. Resultado: de los 63 scripts de la
rama, 49 estaban aplicados y **14 no**, repartidos en cuatro tandas que se habían ido
acumulando.

El binario nuevo los necesita: se verificó que el código invoca `fn_pst_disponible`,
`sp_pst_comprometer_documento`, `vw_pst_ejecucion_presupuestaria`, `fn_apr_puede_autorizar`,
`apr_bitacora`, `cfg_aprobacion_nivel`, `cfg_formato_fiscal` y `fn_prv_cxp_resumen`. Publicar sin
ellos habría roto Presupuesto, Compromisos, Aprobaciones, Formato fiscal y CxP de proveedores.

---

## 2. El orden importaba

`2026-08-31_apr_niveles_01_estructura.sql` y `2026-08-27_pst_compromiso_01_estructura.sql`
reemplazan **el mismo** CHECK, `ck_alm_orden_compra_estado`. Aplicados al revés, la tanda del 27
borra el estado `7` en silencio y el módulo de aprobación deja de poder escribirlo.

Se aplicó en el orden correcto y se verificó el CHECK en cada paso:

| Momento | `ck_alm_orden_compra_estado` |
|---|---|
| Después de la tanda del 08-27 | `IN (1,2,3,4,5,6,9)` |
| Después de la tanda del 08-31 | `IN (1,2,3,4,5,6,7,9)` ✅ |

---

## 3. Qué se aplicó, en orden

Cada script trae su propio `BEGIN/COMMIT`, así que es atómico por sí solo; se corrieron con
`-v ON_ERROR_STOP=1`, uno a uno, verificando el objeto resultante antes de pasar al siguiente.

| # | Script | Verificado por |
|---|---|---|
| 1 | `2026-08-13_prv_estado_cuenta.sql` | 3 funciones de estado de cuenta |
| 2 | `2026-08-22_cfg_formato_fiscal.sql` | tabla `cfg_formato_fiscal` |
| 3 | `2026-08-22_cfg_formato_fiscal_seed.sql` | 2 filas sembradas |
| 4 | `2026-08-22_bitacora_config_formato_fiscal.sql` | — |
| 5 | `2026-08-22_prv_cxp_unificada.sql` | `fn_prv_cxp_documentos`, `fn_prv_cxp_resumen` |
| 6 | `2026-08-27_pst_compromiso_01_estructura.sql` | `pst_compromiso`, `pst_movimiento`, `cfg_presupuesto_control` |
| 7 | `2026-08-27_pst_compromiso_02_funciones.sql` | `fn_pst_disponible` |
| 8 | `2026-08-27_pst_compromiso_03_procedimientos.sql` | `sp_pst_comprometer_documento` |
| 9 | `2026-08-27_pst_compromiso_04_vistas.sql` | 4 vistas `vw_pst_*` |
| 10 | `2026-08-27_pst_compromiso_05_proveedores_bancos.sql` | `sp_pst_afectar_valor_real` |
| 11 | `2026-08-31_apr_niveles_01_estructura.sql` | `apr_bitacora`, `cfg_aprobacion_*` |
| 12 | `2026-08-31_apr_niveles_02_funciones.sql` | (sus funciones las reemplaza el #14) |
| 13 | `2026-08-31_apr_niveles_03_requisicion.sql` | `alm_requisicion_aprobacion` |
| 14 | `2026-09-01_apr_niveles_04_limite_por_aprobador.sql` | `fn_apr_puede_autorizar`, `fn_apr_tramo_de` |
| 15 | `2026-09-01_pst_disponible_sin_truncar.sql` | `fn_pst_disponible` (reemplazo) |

**Nota sobre el #12.** `fn_apr_escalera` y `fn_apr_es_aprobador` **no existen** después del
despliegue, y eso es lo correcto: el script #14 las elimina explícitamente y las sustituye por el
modelo con límite por aprobador. Su propia nota de verificación dice `debe_ser_cero`. Ninguna
está referenciada por el código.

### Dos que parecían faltar y no faltaban

`2026-08-12_con_integracion_compras_modulo.sql` y `2026-08-18_alm_retencion_compras_unificada.sql`
ya estaban aplicados. La primera comprobación los dio por pendientes porque buscó sus columnas en
la tabla equivocada (`con_integracion_asiento` en vez de `con_integracion_config`;
`alm_compra_cxp_abono` en vez de `prv_retencion_hdr`). Ambos son idempotentes y re-ejecutar el
primero no causó daño.

---

## 4. Respaldo previo

Ninguno de los 14 scripts borra ni migra datos —los `UPDATE` que contienen están dentro de
cuerpos de funciones, no en el cuerpo del script—, así que el riesgo era exclusivamente de DDL:

- `Database/Backups/siad_v4_esquema_pre_despliegue_20260903_124233.sql` (1.5 MB, esquema completo)
- `Database/Backups/siad_v4_datos_tablas_alteradas_20260903_124233.sql` (4.6 KB)

---

## 5. Verificación posterior

**Objetos que el código invoca.** Se barrieron los `.cs` y `.razor` buscando referencias
calificadas con `public.`: **190 objetos nombrados, 187 presentes**.

Los 3 ausentes son huecos **anteriores** a esta rama —ningún script de `Database/` los crea y
ningún commit de la rama los introdujo—, así que no son regresión de este despliegue, pero
siguen abiertos:

| Objeto | Lo usa | Efecto |
|---|---|---|
| `consolidar_cuenta_bancos` | `CuentasBancosService.cs:169` | consolidación de cuentas falla |
| `sp_ban_kardex_conciliar` | `CuentasBancosService.cs:317` | conciliación de kardex falla |
| `fn_numero_letras` | `CobranzaService.cs:144` | monto en letras del convenio falla |

**Prueba de humo del portal contra `siad_v4`:** 0 errores de servidor. Respondieron Presupuesto
(configuraciones y ejecución, 224 filas), Compromisos, Aprobaciones (los 4 documentos), Formato
fiscal, CxP de proveedores, existencias por bodega, valuación, requisiciones, recepciones,
órdenes de compra y clientes.

**Los controles nacen apagados**, como manda el diseño: `cfg_aprobacion_control` tiene sus 4
documentos en `modo = 0` y `cfg_presupuesto_control` sus 4 filas. Aplicar esta tanda **no cambió
el comportamiento de ninguna pantalla**; dejó los objetos listos.

---

## 6. Efecto colateral esperado: el bloque CAI del portal

La emisión de facturas de lectura desde el portal pide folios como si fuera una ruta más. Al
probar el endpoint se reservó su bloque, que es lo previsto:

```
00L4   → 1-250      (teléfono)
00L1   → 251-500    (teléfono)
00L2   → 501-750    (teléfono)
PORTAL → 751-1000   ← nuevo, sin consumir (correlativo_actual 750)
```

Los rangos no se solapan: el portal no puede imprimir un folio que un equipo lleve en la cola sin
subir.

---

## 7. Lo que sigue pendiente y NO se resuelve con SQL

**Los CAI de nota de crédito y débito son de prueba.**

```
cai_id 8 → CAI-PRUEBA-NC-SOLO-LOCAL   (tipo 6)
cai_id 9 → CAI-PRUEBA-ND-SOLO-LOCAL   (tipo 7)
```

Anular una factura se hace con una nota de crédito por el total (`sp_adm_emitir_nota_credito`
pone `estado='N'`). Con estos CAI, esa nota lleva un código que el SAR no reconoce. **Hasta cargar
el CAI real, la anulación —y por tanto la refacturación— no debe usarse en producción.** La
emisión de facturas normales sí usa el CAI real (`cai_id 7`, correlativo en 281).

Ese dato lo emite el SAR; no hay script que lo genere.
