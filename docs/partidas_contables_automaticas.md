# Partidas Contables Automaticas - Lectura V2

## Objetivo
Generar la poliza contable de facturacion en el momento de registrar la lectura (`sp_lectura_v2`), sin IVA por ahora.

## Alcance implementado
- Registro de factura y detalle desde `sp_lectura_v2`.
- Generacion automatica de poliza en:
  - `public.con_partida_hdr`
  - `public.con_partida_dtl`
- Debito fijo a CxC desde regla de integracion.
- Credito dinamico por servicio usando `servicios.cont_account_id`.
- Fallback al credito por defecto de regla (`FAC_NETO`) si el servicio no tiene cuenta.
- Control de no duplicidad de poliza por factura.

## Scripts involucrados
Ejecutar en este orden:

1. `Prestadoras/Database/2026-03-04_add_company_cont_account_to_servicios.sql`
2. `Prestadoras/Database/2026-03-04_update_con_tipo_transaccion_automatic.sql`
3. `Prestadoras/Database/2026-03-04_add_cfg_document_type_apc.sql`
4. `Prestadoras/Database/2026-03-04_add_reglas_integracion_apc.sql`
5. `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`

## Requisitos de datos
Para `cfg_company.code = 'APC'` debe existir:

- `cfg_document_type`:
  - `VENTAS/FAC` (factura)
- `con_tipo_transaccion`:
  - codigo `FAC` activo
- `con_regla_integracion`:
  - `module='VENTAS'`
  - `scenario_code='FAC_NETO'`
  - `is_active=true`
  - `debit_account_id` (CxC) y `credit_account_id` (ingreso default) no nulos
- `con_periodo_contable`:
  - un periodo con `status_id = 0` que cubra la fecha de lectura
- `servicios`:
  - `company_id` asignado
  - `cont_account_id` opcional (si no existe, cae al credito default de regla)

## Como se arma la partida contable (paso a paso)
Implementado en `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`.

1. Se calcula `v_total` sumando `DetalleServicios[].Monto`.
2. Si `v_total <= 0`, no se crea poliza.
3. Se obtiene empresa `APC` en `cfg_company`.
4. Se obtiene `document_type_id` de `VENTAS/FAC` en `cfg_document_type`.
5. Se obtiene `type_id` de `con_tipo_transaccion` con codigo `FAC`.
6. Se obtiene regla `FAC_NETO` activa en `con_regla_integracion`:
   - Debito: CxC clientes.
   - Credito default: ingresos.
   - Centro de costo opcional.
7. Se valida periodo contable abierto (`status_id = 0`) para la fecha de lectura.
8. Se valida si ya existe poliza para esa factura (`module='VENTAS'`, `document_type='FAC'`, `document_id=factura_id`):
   - Si existe, no duplica.
   - Si no existe, inserta cabecera en `con_partida_hdr`.
9. Inserta linea 1 (debito total):
   - Cuenta CxC (regla `FAC_NETO.debit_account_id`).
   - Monto = `v_total`.
10. Recorre `DetalleServicios` y por cada monto:
   - Busca `servicios.cont_account_id`.
   - Si no hay, usa `FAC_NETO.credit_account_id`.
   - Inserta linea de credito por el monto del servicio.
11. Si no hubo lineas de credito por detalle, inserta una sola linea de credito default por `v_total`.
12. Validacion final:
   - Debe == Haber (tolerancia 0.01).
   - Si no cuadra, lanza excepcion y revierte la operacion.

## Ejemplo real validado
Factura `1-126783`:

- Total factura actual (`factura_detalle`) = `150.00`
- Poliza:
  - Debito: cuenta `110302`, `150.00` (CxC)
  - Creditos:
    - cuenta `510102`, `120.00` (AGUA POTABLE)
    - cuenta `510102`, `30.00` (ALCANTARILLADO)
- Debito total = Credito total = `150.00`
- Relacion 1:1 entre factura y poliza (sin duplicados)

## Validaciones SQL recomendadas
```sql
-- 1) Total del documento actual
SELECT f.id, f.numfactura, f.saldototal, COALESCE(SUM(fd.montovalor),0) AS total_factura_actual
FROM public.factura f
LEFT JOIN public.factura_detalle fd ON fd.factura_id = f.id
WHERE f.id = :factura_id
GROUP BY f.id, f.numfactura, f.saldototal;

-- 2) Cabecera de poliza
SELECT poliza_id, document_number, document_id, total_debit, total_credit
FROM public.con_partida_hdr
WHERE module='VENTAS' AND document_type='FAC' AND document_id=:factura_id;

-- 3) Lineas de poliza
SELECT h.poliza_id, h.document_number, d.line_number, pc.code AS cuenta, d.debit_amount, d.credit_amount, d.description
FROM public.con_partida_hdr h
JOIN public.con_partida_dtl d ON d.poliza_id = h.poliza_id
JOIN public.con_plan_cuentas pc ON pc.account_id = d.account_id
WHERE h.document_id = :factura_id
ORDER BY d.line_number;
```

## Interpretacion de saldos
- `factura.saldototal`: saldo acumulado del cliente (saldo anterior + documento actual).
- `SUM(factura_detalle.montovalor)`: total del documento actual.
- La poliza contabiliza el documento actual, no el acumulado historico.

## Errores comunes y causa
1. `No existe regla FAC_NETO activa en con_regla_integracion`
   - Falta regla o no esta activa para la empresa APC.

2. `No hay periodo contable abierto (status_id = 0) para la fecha ...`
   - No existe periodo abierto para la fecha de lectura.
   - O el periodo existe con otro estado (`status_id = 1` precierre o `status_id = 2` cerrado).

3. `500 System.ServiceModel.ServiceActivationException` en WS
   - Host WCF bloqueado por memoria minima.
   - Ajuste aplicado en `WSappLectores/WS_APC/Web.config`:
     - `minFreeMemoryPercentageToActivateService="0"` (entorno local/desarrollo).

## Siguiente etapa sugerida
Implementar asientos automaticos de cobro:

- `COB_CAJA`: Debe Caja / Haber CxC
- `COB_BANCO`: Debe Banco / Haber CxC

Usar `con_regla_integracion` segun metodo de pago.
