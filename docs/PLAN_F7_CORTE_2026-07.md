# F7 — Plan de corte de la unificación de cobranza (2026-07)

**Estado**: BORRADOR para revisión — no se ejecuta nada de este documento
hasta aprobarlo.

## 1. Punto de partida

F1–F6 están mergeadas a `main` (PRs #40–#45). Hoy el sistema opera en
**dual-write**: cada cobro/emisión escribe el modelo nuevo (documentos:
`factura_detalle.montovalor_saldo`, `adm_pago`, `adm_pago_aplicacion`,
`cln_plan_pago_*`) **y** el espejo legacy (`transaccion_abonado`). La
equivalencia entre ambos mundos está auditada (850/850 exactos en copia09) y
fijada por tests (`SaldoDocumentosTests`, `PlanCuotasTests`).

F7 apaga el lado legacy. Es la única fase con impacto operativo real y por
eso su ejecución va **pegada a la ventana de deploy a 0.9**.

Nada de F1–F6 está aplicado en 0.9 todavía: viajan juntos en esta ventana
los 9 DDL `uc_*` + los de F7.

## 2. Censo de escritores de `transaccion_abonado` (verificado 2026-07-29)

| Escritor | Qué escribe | Destino en F7 |
|---|---|---|
| `sp_lectura_v3` | cargos de factura (espejo del detalle) | **Deja de escribirlos** — la factura ES el documento. Solo pierde el bloque INSERT; cálculo intacto. |
| `CobroService` (motor) | espejo 201/202 del pago | Se elimina el espejo; el pago vive en `adm_pago`. |
| `sp_ban_ws_pagar` | espejo 202 por factura | Ídem (el `adm_pago` de F5 queda como registro). |
| `sp_adm_emitir_nota_credito` / `_debito` | espejo 205/206 | Dejan de escribirlo; NC/ND ya ajustan `montovalor_saldo` de líneas (posteo por config F5-contable). **Verificar en implementación** que el ajuste documental es completo antes de quitar el espejo. |
| `CobranzaService` | espejo `PLAN-PR` (prima, F6) | Muere: la prima ya es cuota mes 0 (documento). |
| `FacturacionMiscelaneosService` | espejo del misceláneo | Muere: el misceláneo emite factura (documento) desde F4. |
| `AbonoService` | **recibos pendientes de banco (estado 'P')** | ÚNICO uso sin casa nueva: se migra a tabla propia `adm_recibo_banco_pendiente` (id, company, cliente, factura_id, monto, estado, generado_por/fecha, anulado_*). La conciliación automática del WS (que hoy los busca por 'P') pasa a leerla. |
| SPs muertos: `sp_lectura`, `sp_lectura_v2`, `sp_posteo`, `sp_registrar_posteo_manual`, `sp_registrar_posteo_lectoras`, `sp_reversar_posteo_manual`, `sp_actualizar_detalle_posteolectora` + los 2 del plan maestro que solo hacen UPDATE: `sp_actualizar_factura_pago`, `sp_actualizar_detalle_posteomanual` | — | DROP (ya sin callers desde F2b/apertura única). **NO** se dropea `fn_getclientesaldos_posteomanual` (viva, mismo archivo fuente — advertencia del plan maestro). |

## 3. Trabajo de código y DDL (pre-ventana, en rama `feat/uc-f7-corte`)

Orden de hitos, cada uno con suite verde:

1. **H1 — Recibos pendientes a casa propia**: tabla
   `adm_recibo_banco_pendiente` + `AbonoService`/conciliación WS leyendo de
   ahí; migración de los 'P' vivos en el DDL.
2. **H2 — Apagar espejos**: quitar los INSERT de espejo en los 4 servicios
   C# y 4 SPs vivos del censo. Los tests que asertan "legacy == documentos"
   se convierten en tests de solo-documentos.
3. ~~**H3 — Re-migración de cartera como documentos**~~
   ⚠️ **SUPERSEDED (2026-07-29) — NO IMPLEMENTAR.** La migración total de SIMAFI
   (ver [PLAN_MIGRACION_SIMAFI_TOTAL_2026-07.md](PLAN_MIGRACION_SIMAFI_TOTAL_2026-07.md)
   y [M6_VALIDACION_MIGRACION_SIMAFI_2026-07.md](M6_VALIDACION_MIGRACION_SIMAFI_2026-07.md))
   ya cargó **la historia real completa** con numeración original: 3,896,909
   facturas, 9,331,049 líneas, 12,173,095 movimientos y **2,837,660 pagos en
   `adm_pago`** con sus 9,393,969 aplicaciones. Validado en local: 25,530 de
   25,530 clientes con saldo idéntico a SIMAFI.
   - **No hay residuo que convertir**, así que las facturas sintéticas
     `SI-<clave>` no van. Crearlas ahora duplicaría cartera.
   - La parte que la decisión del 2026-07-30 le sumaba a H3 —escribir los pagos
     históricos como `adm_pago` para que los reportes no dependan de la tabla
     congelada— **ya está hecha**.

   Texto original, conservado solo como referencia de lo que se descartó:
   script `fn_uc_f7_migrar_residuo_a_documentos(company_id)`:
   - Por cada cliente con residuo vigente `SALDO_ANTERIOR/SALDO_INICIAL` ≠ 0:
     una factura `numfactura = 'SI-<clave>'`, `tipofacturacion 'S'`, estado
     `A`, con **una línea** `tiposervicio = 'SALDO_ANTERIOR'` y
     `montovalor = montovalor_saldo = residuo`. Cobrable por caja Y por banco
     (entra sola a `fn_ban_ws_pendientes`) y financiable por convenio.
   - Idempotente (si ya existe `SI-<clave>` no duplica) y con reporte de
     control (clientes, total migrado, diff contra residuo).
   - **En 0.9 se corre tras re-validar la migración comercial de los 2
     ciclos** — el residuo real es la cartera SIMAFI.
4. ~~**H4 — SP saldo final + freeze**~~ ✅ **HECHO (8f77502, 332 verdes)**.
   SP v7 sin residuo (control de residuo=0 con abort; en bases de prueba se
   salta con `-c siad.forzar_freeze=on`), candado por trigger (el REVOKE no
   alcanza al superusuario con el que conecta el portal) + REVOKE + rol
   `siad_migracion`, y trigger de sincronía F1 fuera. El candado destapó dos
   defectos: la CONCILIACIÓN era un escritor del espejo que el censo de H2 no
   vio (era trigger, no SP) y `CerrarCajaAsync` sumaba el espejo muerto (todo
   cierre habría dado 0). Además la reconstrucción de `factura` en la migración
   perdió sus 3 triggers — repuestos; los triggers NO viajan con DROP+RENAME.
4bis. **H4b — Paridad funcional de la caja única (BLOQUEA a H5)**

   Auditoría del 2026-07-29 contra el módulo viejo `CaptacionPagos` (pestañas
   *Lectoras*, *Misceláneos*, *Manual*): la pantalla `/facturacion/caja` tiene el
   motor completo detrás, pero **le faltan dos entradas que el propio plan
   maestro §5.1 pedía** y sin las cuales el flujo de caja no se puede reproducir.
   H5 retira las pantallas viejas, así que esto debe ir **antes**.

   | Falta | Estado del motor | Por qué importa |
   |---|---|---|
   | **Buscar por N° de factura/recibo** | `CobroService` ya cobra por documento | Es el flujo *Lectoras*: el cajero tiene el recibo impreso en la mano y teclea el número. Hoy solo se busca por cliente. |
   | **Fecha de pago editable** | `CobroCrearDto.FechaPago` existe y el motor la respeta (`?? DateTime.Now.Date`) | Sin esto no se registran cobros de días anteriores — el caso normal cuando el lector entrega la recaudación después. |

   Ambas son trabajo de UI contra un motor que ya las soporta.

   **Decisión del usuario (2026-07-29): la distribución NO se hace editable.**
   La pestaña *Manual* vieja dejaba al cajero ajustar los montos por servicio;
   se confirma que alcanza con el reparto automático por
   `adm_desglose_abono_porcentaje` (hoy 60/30/5/5, administrable en
   `/tarifario/desglose-abonos`). No se replica esa edición manual.

5. **H5 — Limpieza** — **H5a HECHA (1e83e5f, 328 verdes, −3,309 líneas)**:
   DROP de los SPs muertos (13 firmas) + overload 1-arg + tabla
   `tipo_transaccion` con su entidad EF; módulo `CaptacionPagos`
   (service/controller/client) RETIRADO — sus 2 únicos métodos vivos (lookup de
   clientes y bancos) viven en `CatalogosCobroService` bajo `api/cobros/*`;
   los 4 correlativos con carrera (§3.8 del plan maestro, pendiente que F6 dejó)
   migrados a la serie atómica. **H5b HECHA**: vista de vigencia →
   `vw_rep_movimiento_vigente` (modelo nuevo) con swap de los 9 rep_* y
   comparación antes/después sobre copia09 (rango 01→29-jul, company 2).
   Veredicto: `categoria_corte` hash idéntico; TODA otra diferencia quedó
   explicada al centavo y a favor del modelo nuevo —
   (a) cobros post-candado H4 que el espejo congelado ya no recibe
   (4 clientes de las pruebas de caja, exactos), (b) 21 cargos con
   `recibo=0` del origen SIMAFI sin factura posible (7 clientes,
   L2,976.30, todos estado C = pagados años atrás, hermanos del hallazgo
   M6), (c) convenios con cuotas fechadas a FUTURO en el ledger (hasta
   2027) que el reporte viejo excluía del corte — el nuevo los asienta en
   la emisión, consistente con `sp_obtener_cliente_saldo`; el reporte
   viejo mostraba 3,962 para un cliente cuyo saldo oficial es 62,064,
   (d) reagrupación de ciclos legacy → catálogo en el desglose (total al
   centavo). Totales all-time por cliente: 25,519/25,530 exactos; los 11
   restantes = (a) + (b). Residuo `TransaccionId`: resuelto en sesión
   aparte (`ea77edf`).
   Detalle original: DROP de los 7 SPs muertos + overload 1-arg de
   `sp_obtener_cliente_saldo` (cross-company, documentado en
   `SaldoCrossCompanyTests` que pasa a fijar que YA NO EXISTE) + tabla vacía
   `tipo_transaccion` (arrastra entidad EF + DbSet) + retiro de
   `AbonoService`/`CaptacionPagosService` (lo vivo se muda a `CobroService`:
   listado de documentos y recibos pendientes) + actualizar
   `ESTADOS_DOCUMENTOS_COMERCIALES.md`.

### Decisión del usuario (2026-07-30): NADA legacy se conserva

Rechazada la desviación que proponía conservar la vista como histórica. En
consecuencia:

- `vw_transaccion_abonado_vigente` **se retira** (como decía el plan maestro).
- Los 9 reportes `rep_*` pasan a leer EXCLUSIVAMENTE el modelo nuevo:
  cargos/saldos desde documentos (facturas/cuotas) y pagos desde `adm_pago`
  (+ aplicaciones). Segunda pasada de reportes dentro de F7 (H5).
- La re-migración (H3) crece: además de la cartera como documentos
  `SI-<clave>`, los **pagos históricos** de la migración comercial se
  escriben como `adm_pago` (canal 1, fecha histórica) para que los reportes
  de períodos pasados no dependan de la tabla congelada.
- `transaccion_abonado` queda como archivo congelado SIN lectores en el
  código (solo auditoría manual por SQL si algún día hace falta).

## 4. La ventana de deploy a 0.9 (runbook resumido)

Amplía el patrón de [RUNBOOK_DEPLOY_2026-07.md](RUNBOOK_DEPLOY_2026-07.md);
el detalle fino se escribe al cerrar H1–H5. Secuencia:

1. Congelar operación (fuera de horario de caja; banco en ventana propia
   como F8).
2. `backup_bd_simple.ps1` de la BD de 0.9.
3. Aplicar en orden los 9 DDL `uc_*` F1–F6 + los de F7 (H1/H3/H4/H5). Humo
   SQL tras cada uno (el runbook trae los SELECT).
4. **Auditoría pre-corte**: equivalencia legacy vs documentos por cliente
   sobre la data real → **si diff ≠ 0 se investiga o se aborta** (restore).
5. Correr `fn_uc_f7_migrar_residuo_a_documentos` + su reporte de control.
6. Auditoría post-migración (saldo por cliente == saldo legacy pre-corte).
7. Freeze (H4) — desde aquí el rollback es restore de backup.
8. Publish: portal (`-Solo portal`) + BancosWs (`-Solo bancosws`) — cada
   host explícito, NUNCA `-Solo todos` (regla del runbook). MobileApi no
   cambió binario en F4–F7 (los SP hacen el trabajo), no se publica.
9. Humo funcional: cobrar y reversar en caja real; réplica del caso golden
   del WS contra el banco de pruebas; snapshot de un lector con mora;
   estado de cuenta y los 4 reportes clave; crear/cobrar una cuota de plan.
10. Rollback: restore del backup + binarios previos (carpeta `publish_*`
    anterior). El corte no deja estados intermedios: hasta el paso 7 el
    dual-write sigue siendo compatible con los binarios viejos.

## 5. Riesgos y decisiones abiertas

| Riesgo / decisión | Postura propuesta |
|---|---|
| NC/ND: ¿el ajuste documental (líneas) es completo sin su espejo? | Verificar con tests dedicados en H2 ANTES de quitar el espejo; si falta algo, se completa en el mismo hito. |
| Reportes históricos post-corte | Vista congelada (desviación §3). |
| Cartera migrada ≠ residuo esperado en 0.9 | Auditoría con abort explícito (paso 4); la migración comercial de los 2 ciclos se re-valida antes de la ventana. |
| App de lectores | Cero cambios de contrato: el snapshot/mora leen el SP (misma firma). El humo del paso 9 lo confirma con un teléfono real. |
| Mora sobre facturas `SI-<clave>` | La factura sintética entra al saldo (igual que el residuo hoy) — mora idéntica. La fecha de emisión será la del corte: **no** reinicia antigüedad porque la mora se calcula sobre saldo, no sobre fecha de factura (verificar en H3 con el test de mora). |

## 6. Qué NO entra en F7

- Estilos de la vista de caja (tarea #13, diferida por el usuario).
- Retiro de la vista de vigencia (desviación §3).
- Financiamiento de convenios sobre cartera aún no migrada (queda resuelto
  por la propia re-migración).
