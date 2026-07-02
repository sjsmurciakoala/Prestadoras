# Diseño — Requerimiento de pago en mora (24/48/72h)

Fecha: 2026-06-24
Rama: `Modulo_Caja_1.0`
Estado: **Aprobado** (Emilio, 2026-06-24).

## 1. Objetivo

En **Facturación → Cobranza → Clientes para cobros**, reemplazar la generación de
"cartas de cobro" por un **Requerimiento de pago en mora** imprimible, con el **plazo
seleccionable** (24 / 48 / 72 horas) desde un combobox al momento de generar.

## 2. Decisiones (cerradas con el usuario, 2026-06-24)

- **Formato:** **réplica exacta de las imágenes** compartidas (no la carta genérica),
  reutilizando el mecanismo HTML imprimible que ya existe.
- **Tabla de saldos:** réplica fiel desde el ledger `transaccion_abonado`.
- **Recargos = 0 por ahora** (el ledger no almacena mora/recargo como transacción;
  en las capturas también van en 0). Columna lista para llenarse a futuro sin reescribir.
- **Reemplaza** el botón "Generar cartas" actual.
- **Número atado al plazo:** 24h → **#1**, 48h → **#2**, 72h → **#3**.
- **Tamaño carta (8.5×11) con márgenes 0.5" en los 4 lados.**
- Incluye **bloque de firmas**, **slogan** y **línea "Observación:"** manuscrita al pie.

## 3. Documento (réplica exacta de las imágenes)

Sobre `CobranzaController.RenderCartasHtml` (mismo flujo imprimible), reproduciendo el
layout de las capturas:

- `@page { size: Letter; margin: 0.5in }`.
- **Encabezado centrado**: nombre legal de la empresa (`AGUAS DE PUERTO CORTES S.A. DE C.V.`)
  en grande/negrita; debajo, centrado: `REQUERIMIENTO DE PAGO EN MORA # N`; luego una
  **regla negra gruesa**. (Sin logo a la izquierda; es el header de las imágenes.)
- **Bloque de datos** (dos columnas):
  - Izquierda: `Clave`, `Propietario`, `Dirección`, y en una línea `Medidor · Ciclo · Libreta · Secuencia`.
  - Derecha: `Fecha Emisión`, `No. Identidad`.
- `Estimado Usuario :`
- `Hacemos de su conocimiento que a la fecha tiene una mora Pendiente de LPS.{total}`.
- **Tabla de saldos** (ver §4) con encabezados de dos niveles:
  `DESCRIPCION | SALDOS ANTERIORES (SALDOS, RECARGOS) | SALDOS MES ACTUAL (SALDOS, RECARGOS) | TOTAL`.
- `TOTAL mora:` a la derecha, en negrita.
- **Texto legal (3 párrafos, verbatim de la imagen)**:
  1. "Por antes expuesto se le brinda un plazo de **{N} HORAS** para realizar un plan de pago,
     lo cual debera presentarse a las oficinas de Atencion al Cliente; DEPARTAMENTO DE COBRANZAS."
  2. "En caso contrario {EMPRESA}, da por terminado el plazo que se le concedio y procede a la
     recuperacion total de su obligacion a traves de la via JUDICIAL, por medio de nuestro apoderado legal."
  3. "Asi mismo hacemos de su conocimiento que al ser trasladado a esta instancia incurre en un
     cargo del 25% por concepto de honorarios de abogado por el valor en mora. Esperando una pronta
     respuesta a este llamado."
- **Slogan centrado**: "NO SE APRECIA EL VALOR DEL AGUA HASTA QUE SE SECA EL POZO".
- **Bloque de firmas** (alineado a la derecha, con líneas): `Recibida por:`, `Identidad:`,
  `Telefono:`, `Fecha Recibido:`.
- Abajo a la izquierda: línea + `Unidad de Cobranzas A.P.C.`.
- **Línea final** `Observacion: ______` en blanco (se llena a mano).

## 4. Datos de la tabla (réplica fiel desde el ledger)

Reutiliza el detalle que ya arma la carta (`ObtenerSaldosClienteAsync` →
`CobranzaSaldoDetalleDto` con `Periodo`, `TipoServicio`, `SaldoDetalle` por movimiento
pendiente de `transaccion_abonado`):

- **Una fila por servicio** (`TipoServicio`: Agua Potable, Alcantarillado, Tasa…).
- **Mes Actual** = movimientos cuyo `Periodo` es el más reciente del cliente.
  **Saldos Anteriores** = períodos previos. (Período "más reciente" se determina parseando
  `YYYY/M`.)
- Importe = `SaldoDetalle` (saldo pendiente del movimiento), sumado a la celda que
  corresponda.
- **Recargos = 0** en ambas columnas (sin fuente en el ledger).
- **TOTAL mora** = `SaldoTotal` del cliente (saldo del ledger).

## 5. Backend

- `GenerarCartasCobroRequest` (en `ClientesCobroDtos.cs`) suma `int PlazoHoras` (24/48/72).
- Persistir **`plazo_horas`** en `cln_carta_cobro_hdr` para conservar plazo/número en
  reimpresiones. `GenerarCartasCobroAsync` lo guarda; `ObtenerCartaLoteAsync` lo devuelve
  en `CartaCobroHdrDto`/`CartaCobroLoteDto`.
- El desglose se calcula en el render desde el `Detalle` ya existente (sin nueva consulta).
- Validación: `PlazoHoras ∈ {24,48,72}` (default 24 si viene inválido).
- **Datos extra del encabezado** (para el formato exacto): `CartaCobroClienteDto` suma
  `Identidad, Secuencia, Libreta, Medidor`. `ObtenerCartaLoteAsync` los llena con una consulta
  por clave: `cliente_maestro` (`maestro_cliente_identidad`, `maestro_cliente_secuencia`,
  `maestro_cliente_indicativo_ruta` = Libreta) + `maestro_medidor.maestro_medidor_numero`
  (vía `cliente_detalle.maestro_medidor_id`). Tenant-safe (`company_id`).
  - **Asunciones a verificar en UI:** `Libreta ← indicativo_ruta`, `Ciclo ← ciclos_id`.

## 6. UI (`ClientesParaCobros.razor`)

- Botón "Generar cartas" → **"Generar requerimiento"**.
- Al hacer clic, popup con `DxComboBox` **Plazo (24/48/72 h)** + botón Generar (patrón del
  popup de "acción en lote" ya existente).
- Tras generar, abre el HTML imprimible en pestaña nueva (como hoy).

## 7. Base de datos (regla mirror)

- Script `Database/2026-06-24_add_plazo_horas_carta_cobro.sql`:
  `ALTER TABLE cln_carta_cobro_hdr ADD COLUMN IF NOT EXISTS plazo_horas int;`
- Aplicar en mirror `siad_v3_restore` y replicar en SRV `siad_v3` ([[feedback-db-mirror]]).
- Re-scaffold de la entidad `cln_carta_cobro_hdr` (o agregar la propiedad en partial).

## 8. Pruebas

- Test de integración (`SIAD.Tests`, BEGIN…ROLLBACK): generar requerimiento con
  `PlazoHoras=48` y verificar que el lote persiste `plazo_horas=48`; reimpresión conserva
  el valor. Verificar el reshape Mes Actual/Anteriores sobre un cliente con movimientos en
  ≥2 períodos.

## 9. Fuera de alcance (YAGNI)

- Cálculo de recargo/mora (columnas en 0 por ahora).
- Réplica pixel-exacta del reporte legacy (se adapta el chasis actual).
- Persistir un correlativo de requerimientos por cliente (el "#N" sale del plazo, no de un
  contador).
