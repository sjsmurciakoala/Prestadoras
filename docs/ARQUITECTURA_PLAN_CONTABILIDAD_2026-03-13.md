# Arquitectura y Plan de Contabilidad - 2026-03-13

Fecha: 2026-03-13
Estado: Decision de arquitectura para cierre de demo del 2026-03-16
Alcance: Captacion, bancos, lectura movil/webservice y catalogos contables

## 1) Objetivo
Definir una arquitectura coherente para cerrar la fase actual de contabilidad automatica sin romper el flujo movil, dejando claro:

- cual es la fuente de verdad runtime
- cual es la ruta unica de posteo/reversa
- que queda como legado temporal
- que se va a limpiar antes del 2026-03-16

## 2) Decision de arquitectura

### 2.1 Decision principal
- `con_plantilla_partida_hdr` y `con_plantilla_partida_dtl` quedan como fuente de verdad `runtime` para los flujos que usan el motor de comprobantes.
- `con_regla_integracion` queda como configuracion legacy temporal para `sp_lectura_v2` y el flujo movil.
- `sp_con_postear_poliza` y `sp_con_revertir_poliza` quedan como unica ruta de posteo/reversa contable.

### 2.2 Interpretacion correcta
- `sp_con_postear_poliza` no crea polizas desde cero.
- `sp_con_postear_poliza` toma una poliza ya creada en estado `DRAFT`, valida y afecta saldos.
- La composicion de la poliza puede venir de:
  - `sp_con_generar_comprobante` para flujos por plantilla
  - `sp_registrar_partida_contable` para bancos
  - `sp_lectura_v2` para lectura movil

### 2.3 Excepcion explicita
- `sp_lectura_v2` queda como excepcion temporal.
- Motivo: hoy necesita lineas dinamicas por `servicios.cont_account_id`.
- El motor por plantilla actual usa `account_id` fijo por linea y no resuelve lineas dinamicas por servicio.

## 3) Rol de cada pieza

| Pieza | Rol correcto | Estado al 2026-03-13 |
|---|---|---|
| `cfg_document_type` | Identifica documento de negocio dentro de un modulo (`FAC`, `REC`, etc.) | Vigente |
| `con_tipo_transaccion` | Clasifica la poliza contable (`type_id`) | Vigente |
| `con_regla_integracion` | Configuracion funcional de cuentas por modulo/documento/escenario | Vigente solo para lectura movil legacy |
| `con_plantilla_partida_hdr/dtl` | Definicion ejecutable de lineas y formulas de poliza | Fuente runtime para captacion |
| `con_partida_hdr/dtl` | Resultado generado de la poliza | Vigente |
| `con_saldo_cuenta` | Saldos mayorados | Solo se altera via posteo/reversa central |
| `sp_con_postear_poliza` | Posteo/mayorizacion | Ruta unica |
| `sp_con_revertir_poliza` | Reversa | Ruta unica |

## 4) Estado actual por flujo

| Flujo | Compositor actual | Posteo actual | Fuente de configuracion actual | Estado objetivo inmediato |
|---|---|---|---|---|
| Lectura movil / WS | `sp_lectura_v2` | `sp_con_postear_poliza` | `con_regla_integracion` + `servicios.cont_account_id` | Dejar como excepcion temporal |
| Captacion lectora | `sp_con_generar_comprobante` | `sp_con_postear_poliza` | `con_plantilla_partida_*` | Mantener y estabilizar |
| Captacion manual | Flujo mixto con legado + comprobante central | `sp_con_postear_poliza` o flujo banco | Mixto | Limpiar para demo |
| Captacion miscelaneos | `sp_con_generar_comprobante` | `sp_con_postear_poliza` o flujo banco | `con_plantilla_partida_*` | Mantener y estabilizar |
| Bancos | `sp_registrar_partida_contable` | `sp_con_postear_poliza` | Configuracion bancaria y contable | Mantener como compositor especializado |

## 5) Que significa "plantillas = verdad runtime"
- Si un flujo usa `sp_con_generar_comprobante`, la configuracion efectiva debe venir de `con_plantilla_partida_*`.
- Ese flujo no debe depender de `con_regla_integracion` en runtime.
- Si se cambia una cuenta o formula de un flujo por plantilla, el cambio valido es el que quede en la plantilla.

## 6) Que significa "reglas = solo movil legacy"
- La pantalla `/contabilidad/reglas-integracion` deja de representar la verdad general del sistema.
- Su alcance funcional pasa a ser temporalmente:
  - soporte de `sp_lectura_v2`
  - configuracion legacy del flujo movil
- Mientras no exista UI de plantillas, la configuracion de plantillas queda por script/seed controlado.

## 7) Consecuencia operativa importante
- Insertar o actualizar una fila en `con_regla_integracion` no actualiza automaticamente `con_plantilla_partida_*`.
- Si se quiere que una regla tenga efecto en flujos por plantilla, se necesita:
  - un script de sincronizacion `regla -> plantilla`
  - o una UI/servicio que materialice esa sincronizacion

## 8) Impacto por modulo

### 8.1 Lectura movil / WebService
- No cambiar endpoint ni payload antes del 2026-03-16.
- No cambiar la app Android antes del 2026-03-16.
- Mantener `sp_lectura_v2` como compositor temporal.
- Asegurar que siga:
  - creando factura
  - creando poliza
  - posteando por `sp_con_postear_poliza`
  - resolviendo cuentas por servicio como hoy

### 8.2 Captacion
- Captacion debe quedar limpia para demo.
- Objetivo:
  - cero dependencia conceptual de `con_regla_integracion`
  - cero dependencia operativa de SPs legacy manuales si es posible
  - comprobante y reversa por ruta central
- En pagos bancarios:
  - mantener reutilizacion de `BanTransaccionesService`
  - evitar doble contabilizacion

### 8.3 Bancos
- No migrar bancos a plantillas antes del 2026-03-16.
- Mantener bancos como compositor especializado.
- Asegurar:
  - una sola poliza por evento
  - un solo posteo
  - reversa exacta

## 9) Que no se va a hacer antes del 2026-03-16
- No mover el calculo de la app movil al servidor.
- No reescribir `sp_lectura_v2` al motor por plantilla actual.
- No eliminar `con_regla_integracion`.
- No eliminar `sp_registrar_partida_contable` de bancos.
- No crear una UI completa de plantillas.

## 10) Que si se debe hacer antes del 2026-03-16

### Fase 1 - Alinear documentacion y decision
Objetivo: que no existan dos narrativas tecnicas incompatibles.

1. Documentar explicitamente:
- `plantillas = verdad runtime`
- `reglas = movil legacy`
- `posteo = ruta unica`

2. Dejar por escrito la excepcion de lectura movil:
- motivo funcional
- alcance temporal
- criterio futuro de cierre

### Fase 2 - Limpiar captacion para demo
Objetivo: dejar captacion consistente y vendible.

1. Revisar y eliminar dependencia directa de `sp_registrar_posteo_manual`.
2. Revisar y eliminar dependencia directa/fallback de `sp_reversar_posteo_manual`.
3. Mantener:
- lectora
- manual
- miscelaneos
- con evidencia contable central
4. Revalidar E2E:
- registro
- reversa
- banco
- sin doble poliza

### Fase 3 - Formalizar uso de reglas solo para movil
Objetivo: que `con_regla_integracion` no siga pareciendo la verdad general.

1. Documentar que la UI `reglas-integracion` queda restringida a lectura movil legacy.
2. Evaluar si conviene:
- ocultarla del menu general de contabilidad
- o renombrarla documentalmente como configuracion legacy de lectura

### Fase 4 - Plantillas controladas por script
Objetivo: que los flujos por plantilla tengan mantenimiento controlado.

1. Mantener scripts de bootstrap/seed de plantillas.
2. Agregar un script claro para:
- `VENTAS/FAC`
- `VENTAS/REC`
3. El script debe:
- crear o actualizar `con_plantilla_partida_hdr`
- borrar y recrear `con_plantilla_partida_dtl` segun definicion vigente

### Fase 5 - Evidencia de cierre
Objetivo: tener material presentable el 2026-03-16.

1. Matriz por flujo:
- compositor
- posteador
- fuente de configuracion
- multiempresa
- reversa

2. Evidencia tecnica:
- `con_partida_hdr`
- `con_partida_dtl`
- `con_saldo_cuenta`
- casos de reversa
- periodo cerrado

## 11) Archivos y SPs a tocar

### 11.1 Captacion
- `Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`
- Eliminar mezcla manual legacy si es viable
- Mantener motor por plantilla y ruta central

### 11.2 Catalogos contables
- `Prestadoras/SIAD.Services/Contabilidad/ContabilidadCatalogosService.cs`
- Ajustar documentacion/alcance de reglas

### 11.3 Seeds / scripts de plantillas
- `Prestadoras/Database/ddl_v3/20260310_seed_plantilla_minima_e2e.sql`
- Crear script de actualizacion operacional de plantillas `VENTAS/FAC` y `VENTAS/REC`

### 11.4 Lectura movil
- `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`
- No reescritura profunda antes del 2026-03-16
- Solo validacion y documentacion de su condicion excepcional

### 11.5 Bancos
- `Prestadoras/SIAD.Services/Bancos/BanTransaccionesService.cs`
- Sin migracion a plantilla por ahora
- Solo validacion de no doble posteo

## 12) Criterio de cierre al 2026-03-16
Se considera cierre aceptable si se cumple todo lo siguiente:

1. Captacion funciona E2E con contabilidad central.
2. La decision de arquitectura queda documentada.
3. `con_regla_integracion` queda explicitamente marcada como legado movil.
4. `con_plantilla_partida_*` queda reconocida como verdad runtime de captacion.
5. No hay evidencia de doble posteo ni de impacto directo a `con_saldo_cuenta` fuera de `sp_con_postear_poliza`.
6. Existe evidencia SQL y funcional para sustentar la demo.

## 13) Cierre posterior al 2026-03-16
Despues de la demo, el siguiente cierre real sera uno de estos dos caminos:

### Opcion A
- extender el motor por plantilla para soportar lineas dinamicas por servicio
- migrar `sp_lectura_v2` al motor de plantillas
- retirar uso runtime de `con_regla_integracion`

### Opcion B
- aceptar oficialmente dos compositores:
  - plantillas para captacion
  - compositor dinamico para lectura
- mantener un solo posteo central

## 14) Recomendacion final
Para el 2026-03-16, la recomendacion es:

1. `con_plantilla_partida_*` como verdad runtime de captacion.
2. `con_regla_integracion` solo para lectura movil legacy.
3. `sp_con_postear_poliza` y `sp_con_revertir_poliza` como unica ruta de posteo/reversa.
4. `sp_lectura_v2` documentado como excepcion temporal.
5. Captacion limpia para demo y respaldada con evidencia.
