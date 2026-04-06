# Runbook Reset Contable ERSAPS DEV

Fecha base: 2026-03-30  
Objetivo: dejar limpia la contabilidad de una empresa en ambiente de desarrollo para cargar el plan de cuentas ERSAPS y luego reconstruir configuraciones operativas sobre una base consistente.

## 1) Cuando usar este runbook
- Ambiente de desarrollo o QA sin necesidad de preservar historial contable.
- Cuando el plan de cuentas actual es temporal, demo o NIF y se quiere arrancar limpio con ERSAPS.
- Antes de revisar en serio los flujos de:
  - facturacion movil / WS
  - captacion
  - miscelaneos
  - notas credito / debito
  - bancos

## 2) Que limpia
Script principal:
- [2026-03-30_reset_contabilidad_dev_para_plan_ersaps.sql](E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/Database/2026-03-30_reset_contabilidad_dev_para_plan_ersaps.sql)

El script:
- desvincula referencias a polizas en modulos operativos,
- pone en `NULL` referencias a cuentas contables en maestros operativos,
- borra configuraciones contables dependientes del catalogo,
- borra reglas, plantillas, saldos y movimientos contables,
- borra el plan de cuentas de la empresa.

## 3) Que conserva
- `cfg_company`
- clientes y catalogos comerciales
- facturas, cobros y maestros operativos
- `con_diario`
- `con_periodo_contable`
- `con_tipo_transaccion`
- `con_centro_costo`

Esto permite resetear contabilidad sin destruir el resto del sistema.

## 4) Tablas y referencias clave que quedan en null para remapear
- `servicio.cont_account_id`
- `miscelaneos_catalogo.cont_account_id`
- `ban_cuenta.cont_account_id`
- inventario y activo fijo si las columnas existen
- configuraciones de estados financieros

Eso es esperado. Despues de cargar ERSAPS hay que remapearlas.

## 5) Orden recomendado
1. Ejecutar el reset contable DEV.
2. Confirmar que `con_plan_cuentas`, `con_regla_integracion`, `con_plantilla_partida_*`, `con_partida_*` y `con_saldo_cuenta` queden en cero para la empresa.
3. Importar el plan de cuentas ERSAPS.
4. Remapear cuentas operativas minimas.
5. Recrear reglas o plantillas contables segun el flujo.
6. Validar operacion por modulo.

## 6) Como ejecutar el reset
Editar primero en el script:
- `tmp_reset_contabilidad_params.company_id`
- `tmp_reset_contabilidad_params.user_name`

Luego ejecutar:
- [2026-03-30_reset_contabilidad_dev_para_plan_ersaps.sql](E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/Database/2026-03-30_reset_contabilidad_dev_para_plan_ersaps.sql)

El mismo script ya trae:
- validacion minima posterior al reset,
- y conteo de pendientes para `servicio`, `ban_cuenta` y `miscelaneos_catalogo`.

## 7) Como cargar el plan ERSAPS
Plantillas disponibles:
- [PlanCuentasTemplate.xlsx](E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PlanCuentasTemplate.xlsx)
- [PlanCuentasTemplate.csv](E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PlanCuentasTemplate.csv)
- [PlanCuentas_Plantilla.xlsx](E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PlanCuentas_Plantilla.xlsx)

API existente:
- `GET /api/contabilidad/catalogos/plan-cuentas/plantilla`
- `POST /api/contabilidad/catalogos/plan-cuentas/import`

Servicio usado:
- [ContabilidadCatalogosService.cs](E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/SIAD.Services/Contabilidad/ContabilidadCatalogosService.cs)

Notas importantes del importador:
- hace insert/update por codigo,
- crea padres faltantes automaticamente,
- no elimina cuentas obsoletas.

Por eso el reset previo es la forma correcta de obtener un catalogo ERSAPS limpio.

## 8) Remapeo minimo antes de volver a operar
Antes de probar cualquier flujo, hay que revisar como minimo:
- servicios facturables:
  - `servicio.cont_account_id`
- miscelaneos:
  - `miscelaneos_catalogo.cont_account_id`
- bancos:
  - `ban_cuenta.cont_account_id`
- reglas legacy WS:
  - `con_regla_integracion`
- plantillas del motor nuevo:
  - `con_plantilla_partida_hdr`
  - `con_plantilla_partida_dtl`
- estados financieros:
  - `con_configuracion_balance`
  - `con_configuracion_linea_resultado`

## 9) Que se rompe si no remapeamos
Despues del reset y antes del remapeo:
- la facturacion movil / WS no podra resolver cuentas correctamente,
- captacion puede fallar al generar comprobantes,
- miscelaneos no tendra cuenta contable,
- bancos no tendra cuenta asociada,
- los estados financieros no tendran estructura cargada.

Eso no es un error del reset. Es parte normal del proceso.

## 10) Validacion minima posterior a la carga ERSAPS
1. El plan de cuentas carga completo y ordenado por codigo.
2. Las cuentas padre e hija respetan la jerarquia esperada.
3. Los `cont_account_id` minimos quedan remapeados.
4. Existe al menos una ruta operativa funcional:
- un servicio facturable,
- un banco,
- un concepto miscelaneo,
- una plantilla o regla de integracion activa.

## 11) Siguiente trabajo despues del reset
Una vez cargado ERSAPS y remapeado lo minimo, revisar en este orden:
1. facturacion movil / WS
2. captacion
3. miscelaneos
4. notas credito / debito
5. bancos

La reporteria financiera y el balance de comprobacion se corrigen despues de estabilizar esos flujos.
