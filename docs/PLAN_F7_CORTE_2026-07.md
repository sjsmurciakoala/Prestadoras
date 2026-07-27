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
3. **H3 — Re-migración de cartera como documentos**: script
   `fn_uc_f7_migrar_residuo_a_documentos(company_id)`:
   - Por cada cliente con residuo vigente `SALDO_ANTERIOR/SALDO_INICIAL` ≠ 0:
     una factura `numfactura = 'SI-<clave>'`, `tipofacturacion 'S'`, estado
     `A`, con **una línea** `tiposervicio = 'SALDO_ANTERIOR'` y
     `montovalor = montovalor_saldo = residuo`. Cobrable por caja Y por banco
     (entra sola a `fn_ban_ws_pendientes`) y financiable por convenio.
   - Idempotente (si ya existe `SI-<clave>` no duplica) y con reporte de
     control (clientes, total migrado, diff contra residuo).
   - **En 0.9 se corre tras re-validar la migración comercial de los 2
     ciclos** — el residuo real es la cartera SIMAFI.
4. **H4 — SP saldo v5 final + freeze**: quitar el término residuo del SP
   (post-migración queda en 0), `REVOKE INSERT/UPDATE/DELETE ON
   transaccion_abonado` salvo rol `siad_migracion`, y trigger de espejos
   numéricos F1 fuera (ya no entran filas).
5. **H5 — Limpieza**: DROP de los 7 SPs muertos + overload 1-arg de
   `sp_obtener_cliente_saldo` (cross-company, documentado en
   `SaldoCrossCompanyTests` que pasa a fijar que YA NO EXISTE) + tabla vacía
   `tipo_transaccion` (arrastra entidad EF + DbSet) + retiro de
   `AbonoService`/`CaptacionPagosService` (lo vivo se muda a `CobroService`:
   listado de documentos y recibos pendientes) + actualizar
   `ESTADOS_DOCUMENTOS_COMERCIALES.md`.

### Desviación propuesta al plan original

El plan decía "retiro de `vw_transaccion_abonado_vigente`". Propongo
**conservarla como vista histórica de solo lectura**: los 9 reportes `rep_*`
la usan para rangos históricos pre-corte (esa historia solo existe ahí) y la
tabla queda congelada — la vista es inocua y evita reescribir los reportes
una segunda vez. Se retira cuando la historia deje de consultarse
(post-cierre anual, fuera de alcance de F7).

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
