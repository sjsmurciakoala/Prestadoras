# Análisis: deudas a proveedores no reflejadas en la migración SIMAFI

**Fecha:** 2026-07-21
**Contexto:** La migración de compromisos de SIMAFI (`docs/MIGRACION_PROVEEDORES_SIMAFI_MAPEO_2026-07-10.md`) cargó las 4,297 órdenes de la ventana 2025-01-01→hoy con `status_transacc = TRUE` (decisión D9: "históricas = procesadas"). Los proveedores reportan que dentro de esa información **hay deudas que siguen vivas**. Este análisis identifica, con los datos extraídos en staging (`stg_simafi_ordenesp`, `stg_simafi_maestroche`), cuáles compromisos **no tienen el pago aplicado**.

Análisis de solo lectura sobre el mirror `siad_v3_restore@localhost`. No se modificó ningún dato.

---

## 1. Cómo registra SIMAFI el pago (regla descubierta y verificada)

- En `ordenesp`, cada orden de compromiso tiene líneas presupuestarias (`valorp = debe`, con cuenta de gasto). Cuando el pago se aplica, SIMAFI **estampa en esas líneas el documento (`docu` = `CKnnn` cheque o `NDnnn` nota de débito) y la fecha de pago (`fechap`)**.
- La correlación es perfecta en la ventana: las 3,003 líneas con `docu` tienen `fechap`; las 254 líneas sin `docu` no tienen `fechap`.
- Verificación cruzada contra el maestro de cheques (`stg_simafi_maestroche`, 6,115 cheques, bandera `borr='X'` = anulado):
  - 0 cheques estampados inexistentes en el maestro.
  - Ninguna orden mezcla líneas pagadas y sin pagar (el estampado es todo-o-nada por orden).
- Matiz importante: el estampado significa "se emitió un pago", **no necesariamente por el total**. En pagos parciales el cheque sale por el abono y el resto se acredita (`haber`) a una cuenta por pagar dentro de la misma orden; los abonos siguientes llegan por vouchers (`vou='VE-…'`) en órdenes posteriores.

## 2. Resultado principal: 228 órdenes con deuda — L6,800,878.98

| Componente | Órdenes | Monto |
|---|---:|---|
| Compromisos **sin ningún pago aplicado** (sin estampar) | 227 | L6,786,503.98 |
| Compromiso estampado con **cheque anulado sin reemplazo** (orden 34683, CK326) | 1 | L14,375.00 |
| **Total** | **228** | **L6,800,878.98** |

Todas estas órdenes se migraron al portal como **procesadas** (`status_transacc = TRUE`), por eso el sistema no muestra la deuda.

Detalle completo por orden: [`docs/2026-07-21_deudas_simafi_ordenes_sin_pago.csv`](2026-07-21_deudas_simafi_ordenes_sin_pago.csv) (número de orden, fecha, monto, proveedor migrado, beneficiario, cuenta CxP, concepto, observaciones).

Ajustes aplicados a la lista cruda de 227+ órdenes sin estampar:

- **Orden 33356** (EDNA PATRICIA RAMIREZ VEGA, L7,837.57): excluida — tiene cheque vivo CK17448 en el maestro; el estampado nunca se actualizó pero sí está pagada.
- **Orden 33287** (ANDRES ABELINO CALDERON, L12,000): incluida — es una de las 8 órdenes "excepción" cuyo `valorp` no trae cuentas; no tiene documento de pago ni cheque en el maestro.
- **Órdenes 34474 y 35711** (PROVEEDORA DE SERVICIOS MULTIPLES, L11,500 c/u): existe un cheque vivo CK4346 por L11,500 **sin estampar en ninguna orden**; probablemente paga UNA de las dos. Ambas quedan en la lista con observación — la deuda real de ese par es ~L11,500, no L23,000.

### Top proveedores por deuda sin pago

| Cód. | Proveedor | Órdenes | Deuda |
|---|---|---:|---|
| 0211 | MARTIN EMILIO BELISLE PINEDA | 2 | 653,950.00 |
| 0700 | CARLOS ROBERTO SANTAMARIA GUARDADO | 1 | 409,487.36 |
| 0704 | RAUL EDUARDO MEJIA HERNANDEZ | 2 | 408,720.00 |
| 0679 | CARLOS ALBERTO MEJIA ESCOBAR | 1 | 400,107.66 |
| 0089 | MUNICIPALIDAD DE PUERTO CORTES | 20 | 379,163.62 |
| 0275 | PEDRO ALBERTO COLBOURNE BEAUMONT | 2 | 288,000.00 |
| 0510 | SERVICIOS MULTIPLES MATIAS | 7 | 276,561.50 |
| 0710 | INVERSIONES & TRANSPORTES BARROW GOMEZ | 1 | 255,875.00 |
| 0517 | INVERSIONES MULTIPES DACEEM | 5 | 243,199.97 |
| 0374 | DAVID HUMBERTO DE LEON CARRERO | 2 | 212,805.57 |
| SINPROV | (32 órdenes sin proveedor enlazable: bancos, instituciones, personas) | 32 | 712,025.08 |

## 3. Resultado secundario: remanentes de pagos parciales — ~L560K a proveedores

Compromisos que **sí** se estamparon como pagados, pero cuyo cheque cubrió solo un abono; el resto quedó acreditado en la CxP y no se terminó de pagar. Neteo por (cuenta, beneficiario): créditos `haber` menos abonos posteriores por voucher, excluyendo re-emisiones de cheques anulados.

| Cuenta | Beneficiario | Saldo vivo |
|---|---|---:|
| 211-01-01-05-01 | SERVICENTRO EL PORVENIR (dos grafías del mismo proveedor) | 222,199.40 |
| 211-02-01-01-108 | PROYECTOS INDUSTRIALES Y SERVICIOS | 120,000.00 |
| 211-01-01-09-137 | SELBIN ALEXANDER RODRIGUEZ ALVAREZ | 105,000.00 |
| 211-01-01-09-162 | CARLOS ALBERTO MEJIA ESCOBAR | 81,000.00 |
| 795-01-01-01-01 | INVERSIONES Y MULTISERVICIOS TREBOL (caso verificado a mano: compromiso 67,499.49; abonos 20,499.49 + 20,000) | 27,000.00 |
| 211-01-01-09-03 | DAYSI LILIANA MARTEL GAMEZ | 3,155.37 |
| 211-01-01-07-01 | ANIBAL ROBERTO NUÑEZ BULNES | 1,300.30 |
| 211-01-01-09-92 | TORNILLERIA PORTEÑA | 788.83 |
| | **Subtotal cuentas de proveedores** | **≈560,443.90** |

El neteo completo (53 grupos) está en [`docs/2026-07-21_deudas_simafi_remanentes_parciales.csv`](2026-07-21_deudas_simafi_remanentes_parciales.csv), clasificado por tipo de cuenta. Los demás grupos **no son deuda a proveedores**: deducciones de planilla por pagar (L77K), retenciones por pagar (L46K), un activo por anticipos al SAR (L262K) y ajustes menores en cuentas de gasto (L33K).

## 4. Advertencias

1. **Pagos por ND no verificables al 100%:** el maestro migrado solo cubre cheques. Las 578 órdenes estampadas con `ND` se dan por pagadas por el estampado mismo; verificar contra el libro bancario requeriría extraer más tablas de `bdsimafi`.
2. **Ventana 2025→hoy:** deudas de compromisos anteriores a 2025 no están en los datos extraídos y quedan fuera de este análisis.
3. **32 órdenes sin pagar cuelgan de `SINPROV`** (L712K): son beneficiarios no enlazables a proveedor (municipalidad ya que no está como proveedor, bancos, personas). Revisar caso por caso a quién se le debe.
4. Los montos de remanentes parciales dependen del match por nombre de beneficiario; hay grafías duplicadas (p. ej. SERVICENTRO/SERVICIENTRO) que se señalan pero no se fusionaron automáticamente.

## 5. Siguiente paso sugerido (no ejecutado)

Los 228 compromisos están en `prv_compromiso_hdr` con `status_transacc = TRUE` y sin abonos registrados. Para que el portal refleje la deuda hay dos caminos (decisión del usuario):

- **(a)** Marcar esas 228 órdenes como pendientes (`status_transacc = FALSE`), con lo que aparecen en Órdenes de Pago Directo como no procesadas; o
- **(b)** Dejarlas procesadas y registrar la deuda/abonos con el módulo nuevo de abonos a compromisos (rama `Cambios_almacen1.0`), cargando los abonos reales y dejando el saldo vivo.

Para los pagos parciales (§3), la opción (b) es la natural: registrar los abonos conocidos y dejar el saldo.
