# Indice del corte Lectura V3

Fecha: 2026-04-17

Este indice organiza los documentos del corte de integracion para que podamos seguir una sola linea de trabajo y saber que ya esta implementado y que aun falta.

## Documentos

1. [PLAN_01_SP_ADM_CALCULAR_FACTURA_LECTURA_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_01_SP_ADM_CALCULAR_FACTURA_LECTURA_2026-04-17.md)
   - motor de calculo servidor.

2. [PLAN_02_SP_LECTURA_V3_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_02_SP_LECTURA_V3_2026-04-17.md)
   - persistencia comercial de lectura y factura.

3. [PLAN_03_ACTUALIZAR_LECTURA_V3_WS_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_03_ACTUALIZAR_LECTURA_V3_WS_2026-04-17.md)
   - contrato WCF/JSON para el WS.

4. [PLAN_04_PORTAL_MANTENIMIENTOS_MOTOR_TARIFARIO_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_04_PORTAL_MANTENIMIENTOS_MOTOR_TARIFARIO_2026-04-17.md)
   - vistas minimas del portal para operar el motor.

5. [PLAN_05_APP_LECTORES_V3_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_05_APP_LECTORES_V3_2026-04-17.md)
   - cambio del app Android a cliente de captura.

6. [PLAN_06_OFFLINE_SYNC_LECTURA_V3_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_06_OFFLINE_SYNC_LECTURA_V3_2026-04-17.md)
   - estrategia offline, snapshot y sincronizacion posterior.

7. [PLAN_INTEGRACION_LECTURA_WS_MOTOR_TARIFARIO_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_INTEGRACION_LECTURA_WS_MOTOR_TARIFARIO_2026-04-17.md)
   - plan maestro de integracion.

8. [ESTADO_CORTE_LECTURA_V3_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/ESTADO_CORTE_LECTURA_V3_2026-04-17.md)
   - estado consolidado: cumplido vs pendiente por cada plan.

9. [PLAN_VALIDACION_PREVIA_CICLO_FACTURACION_2026-04-23.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_VALIDACION_PREVIA_CICLO_FACTURACION_2026-04-23.md)
   - validacion previa para demostrar que el sistema ya puede generar periodo/ciclo y facturar antes de la prueba E2E completa.

## Regla de trabajo

Los planes ya fueron cerrados como linea de implementacion base.

Para seguimiento operativo diario, el documento principal ahora es:

- [ESTADO_CORTE_LECTURA_V3_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/ESTADO_CORTE_LECTURA_V3_2026-04-17.md)

## Orden recomendado de ejecucion

1. verificar datos operativos, generar periodo/ciclo y confirmar facturacion base;
2. cerrar prueba E2E app con `ActualizarLecturaV3`;
3. guardar y reconciliar snapshot offline V3;
4. limpiar dependencias legacy cuando la ruta V3 quede estable.
