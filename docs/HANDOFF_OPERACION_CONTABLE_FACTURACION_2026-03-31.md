# Handoff: Operacion Contable Real de Facturacion

Fecha: 2026-03-31

## Objetivo de este documento

Dejar documentado el punto exacto donde quedo el analisis para retomarlo despues, sin mezclarlo con reporteria ERSAPS ni con los cambios del flujo de empresas.

El foco aqui es unicamente la operacion contable real por proceso:

- facturacion movil / lectora
- captacion
- miscelaneos
- notas de credito y debito
- bancos

## Contexto de negocio que ya quedo claro

De la conversacion y de las transcripciones de reunion:

- Cada movimiento comercial deberia actualizar el saldo comercial al momento.
- Idealmente, cada movimiento deberia dejar su efecto contable sin depender de cierres manuales posteriores.
- La contabilidad no deberia quedar sujeta a que alguien recuerde ejecutar cierres o posteos manuales horas o dias despues.
- La trazabilidad importante para auditoria no solo es el mayor contable, sino la relacion entre la cuenta contable y el auxiliar comercial por cliente/documento.

Fuentes revisadas:

- `Prestadoras/docs/puertocortes.html`
- `Prestadoras/docs/puertocortes2.html`
- `Prestadoras/docs/REVISION_SISTEMA_FACTURACION_CONTABILIDAD.md`

## Resumen ejecutivo

Hoy no existe una sola ruta contable para todos los procesos.

Hay al menos tres estilos de integracion coexistiendo:

1. Ruta heredada WS / lectora
   Usa logica propia en SQL y depende de `con_regla_integracion` y `servicios.cont_account_id`.

2. Ruta de comprobantes contables con plantilla
   Usa `sp_con_generar_comprobante` + `con_plantilla_partida_hdr/dtl`.
   Es la ruta mas cercana a un motor contable central.

3. Ruta bancaria especializada
   Genera movimiento bancario y partida desde el modulo de bancos, con su propia composicion de lineas.

El principal problema arquitectonico no es solo la reporteria.
El problema es que distintos procesos comerciales generan contabilidad por caminos diferentes.

## Lo que quedo confirmado por proceso

### 1. Facturacion movil / lectora

Estado actual:

- La app lectora calcula localmente la factura.
- El servidor no recalcula importes; registra lo que la app envia.
- La contabilidad de esta ruta sigue en una SP heredada.

Evidencia principal:

- `Prestadoras/docs/REVISION_SISTEMA_FACTURACION_CONTABILIDAD.md`
- `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`

Hallazgos:

- `sp_lectura_v2` resuelve una regla activa en `con_regla_integracion` para `VENTAS/FAC`.
- Busca `con_tipo_transaccion`.
- Inserta directamente en `con_partida_hdr` y `con_partida_dtl`.
- Obtiene cuentas de ingreso por `servicios.cont_account_id`.
- Postea con `sp_con_postear_poliza`.

Conclusion:

- Esta es la ruta mas heredada y la menos alineada con una arquitectura centralizada.
- Aqui hay cambios potenciales en app, WS y SQL.

### 2. Captacion

Estado actual:

- Primero impacta comercialmente factura y saldo del cliente.
- Luego genera la transaccion del auxiliar.
- Despues genera la poliza, salvo cuando entra por flujo bancario integrado.

Evidencia principal:

- `Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`

Hallazgos:

- Crea `transaccion_abonado` tipo `201`.
- Actualiza saldo comercial por documento.
- Si no integra bancos, genera poliza por `sp_con_generar_comprobante`.
- Si integra bancos, delega primero al flujo bancario.

Conclusiones:

- Captacion ya esta bastante mas cerca del motor central de comprobantes.
- El saldo comercial y la contabilidad si estan conectados, pero depende del subtipo de flujo.

### 3. Miscelaneos

Estado actual:

- Ya se comporta mas como facturacion comercial que como un simple cargo aislado.

Evidencia principal:

- `Prestadoras/SIAD.Services/FacturacionMiscelaneos/FacturacionMiscelaneosService.cs`

Hallazgos:

- Crea `factura`.
- Crea `factura_detalle`.
- Crea `transaccion_abonado`.
- Valida que cada concepto tenga `miscelaneos_catalogo.cont_account_id`.
- Genera poliza `VENTAS/MIS` mediante `sp_con_generar_comprobante`.

Conclusiones:

- Tecnica y funcionalmente, miscelaneos ya esta muy cerca de un submodulo de facturacion.
- La observacion del usuario tiene sentido: probablemente conviene evolucionarlo como un modulo de facturacion mas completo, no dejarlo como una pieza suelta.

### 4. Notas de credito y debito

Estado actual:

- Impactan el auxiliar comercial.
- No se encontro integracion automatica equivalente al motor central de comprobantes.

Evidencia principal:

- `Prestadoras/SIAD.Services/NotasCreditoDebito/NotasCreditoDebitoService.cs`

Hallazgos:

- Inserta `ajuste`.
- Inserta `ajustes_detalle`.
- Inserta `transaccion_abonado` tipo `205` o `206`.
- Actualiza saldo comercial del cliente.
- No se observo llamada a `sp_con_generar_comprobante`.
- No se observo posteo automatico de poliza desde este servicio.

Conclusiones:

- Aqui hay un hueco real.
- Si la meta es que cada transaccion relevante deje contabilidad al momento, notas es uno de los primeros puntos a corregir.

### 5. Bancos

Estado actual:

- Tiene un motor propio y mas especializado.

Evidencia principal:

- `Prestadoras/SIAD.Services/Bancos/BanTransaccionesService.cs`

Hallazgos:

- Trabaja sobre `ban_kardex`.
- Resuelve cuenta contable del banco por `ban_cuenta.cont_account_id`.
- Construye lineas de contracuenta.
- Registra partida por `sp_registrar_partida_contable`.
- Luego postea con `sp_con_postear_poliza`.

Conclusiones:

- Bancos no usa exactamente la misma ruta de lectora.
- Tampoco usa exactamente la misma abstraccion que `sp_con_generar_comprobante`.
- Es una tercera familia de integracion.

## Motor contable central actual

La pieza mas reusable y alineada a una arquitectura objetivo hoy es:

- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`

Puntos clave:

- `sp_con_generar_comprobante`
- `sp_con_postear_poliza`
- `sp_con_revertir_poliza`
- `con_plantilla_partida_hdr`
- `con_plantilla_partida_dtl`

Conclusion:

- Si se busca una sola fuente de verdad para la generacion de polizas comerciales, este motor es el mejor candidato base.

## Lo que dijo la reunion y sigue vigente

Ideas fuertes que salieron varias veces en las reuniones:

- cada transaccion deberia ir dejando contabilidad
- no depender de cierres manuales olvidados
- el saldo comercial debe quedar actualizado en el documento al momento
- la conciliacion contra bancos debe simplificarse
- la trazabilidad entre comercial y contabilidad no se debe perder

Esto sigue alineado con el analisis tecnico actual.

## Tensiones o decisiones que aun no se han cerrado

### A. Facturacion movil / lectora

Pendiente definir:

- Si el calculo debe seguir viviendo en la app movil.
- Si el WS debe seguir armando la poliza en SQL heredado.
- O si esa ruta debe migrar al mismo motor de comprobantes central.

### B. Miscelaneos

Pendiente definir:

- Si se mantiene como componente separado.
- O si evoluciona a un modulo de facturacion mas formal dentro del dominio comercial.

Observacion del usuario:

- "miscelaneos podria ser hacerlo mejor como un modulo de facturacion completo"

Esa observacion queda marcada como linea valida de rediseño.

### C. Notas credito/debito

Pendiente definir:

- Si deben generar poliza automaticamente siempre.
- Si deben anular/revertir o generar una poliza de ajuste.
- Como se relacionan con periodo abierto y correcciones dentro/fuera de periodo.

### D. Unificacion de rutas contables

Pendiente definir:

- Cual sera la ruta unica objetivo para generar polizas.
- Que piezas heredadas salen.
- Que piezas se mantienen temporalmente por compatibilidad.

## Propuesta de orden para retomar

Cuando se vuelva a este tema, el orden sugerido es:

1. Facturacion movil / lectora
   Porque es la ruta mas heredada y la que impacta app + WS + PostgreSQL.

2. Notas credito/debito
   Porque hoy tienen auxiliar comercial pero no una integracion contable equivalente.

3. Captacion
   Para afinar coherencia con bancos y evitar duplicidad de rutas.

4. Miscelaneos
   Para decidir si se queda como flujo independiente o se formaliza como modulo de facturacion.

5. Bancos
   Para ajustar integracion final y reconciliacion con el resto del circuito.

## Pregunta rectora para la siguiente sesion

La proxima vez no arrancar por reporteria ni por ERSAPS.
Arrancar por esta pregunta:

"Cuando ocurre un movimiento comercial, cual debe ser la unica ruta correcta para que nazca la poliza?"

Si esa pregunta se responde por proceso, el resto de decisiones se ordena mucho mejor.

## Referencias de codigo revisadas

- `Prestadoras/docs/REVISION_SISTEMA_FACTURACION_CONTABILIDAD.md`
- `Prestadoras/docs/puertocortes.html`
- `Prestadoras/docs/puertocortes2.html`
- `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`
- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
- `Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`
- `Prestadoras/SIAD.Services/FacturacionMiscelaneos/FacturacionMiscelaneosService.cs`
- `Prestadoras/SIAD.Services/NotasCreditoDebito/NotasCreditoDebitoService.cs`
- `Prestadoras/SIAD.Services/Bancos/BanTransaccionesService.cs`

