# Resumen De Conversaciones Del 2026-04-18

## Alcance
Este documento resume exclusivamente lo conversado y trabajado el `2026-04-18` sobre el corte del nuevo motor tarifario, la integración con lectura/app/ws y el portal V3.

No es una transcripción literal del chat. Es una bitácora resumida de decisiones, avances, validaciones y pendientes reales de ese día.

## Temas Principales Del Día
- estabilización del motor tarifario V3 en base de datos;
- resolución de tarifas base y derivadas;
- integración con lectura/facturación;
- operación offline en app Android;
- WS para snapshot offline y sincronización V3;
- documentación técnica del corte;
- primeras tareas formales para portal, CAI y reglas pendientes.

## 1. Motor Tarifario V3 Y Seed Inicial
Durante el día se cerró y validó la base del motor tarifario nuevo:

- se ejecutó el core del modelo tarifario;
- se ejecutaron los seeds APC;
- se corrigieron errores del seed, especialmente tipado de `vigencia_hasta`;
- se validó que `adm_servicio`, `adm_cuadro_tarifario` y `adm_regla_tarifaria` ya devolvieran datos;
- se incorporó `segmento_tarifario` para soportar correctamente combinaciones como:
  - doméstica baja/media/alta,
  - comercial pequeña/mediana/grande,
  - pública única,
  - industrial y casos especiales.

### Servicios ya aterrizados
- `AGUA_POTABLE`
- `ALCANTARILLADO`
- `TASA_AMBIENTAL`
- `TASA_SVA_ERSAPS`
- servicios eventuales del catálogo maestro

### Fuentes funcionales usadas ese día
- Plan de Arbitrios 2026
- tablas APC entregadas por el usuario
- `configuracioncobroadicional.csv`

## 2. Resolución Tarifaria Por Cliente
Se creó y ajustó la lógica para resolver tarifas por `adm_cliente_servicio`.

### Trabajo realizado
- creación de `sp_adm_resolver_tarifa_cliente_servicio`;
- corrección de errores de tipos en el `RETURN QUERY`;
- pruebas con clientes reales:
  - con medición,
  - sin medición,
  - alcantarillado;
- validación del cuadro correcto por:
  - servicio,
  - categoría,
  - condición de medición,
  - segmento,
  - vigencia.

### Casos validados ese día
- agua potable con medición;
- agua potable sin medición;
- alcantarillado doméstico;
- tasa ambiental;
- ERSAPS.

## 3. Servicios Derivados
Se completó la parte derivada del modelo para conceptos que no son líneas base directas.

### Trabajo realizado
- seed de `TASA_SVA_ERSAPS` desde `configuracioncobroadicional.csv`;
- creación de resolver derivado;
- validación de cálculo porcentual sobre:
  - agua,
  - alcantarillado;
- validación de `TASA_AMBIENTAL` en casos medidos y no medidos.

### Decisiones importantes
- `ALCANTARILLADO` se mantiene con cuadros APC explícitos;
- `TASA_AMBIENTAL` se mantiene con reglas/catálogos del nuevo modelo;
- `TASA_SVA_ERSAPS` sí se sembró desde configuración adicional, porque era el dato faltante.

## 4. Cálculo De Factura De Lectura
Se pasó del resolver puntual a una capa más cercana a operación real.

### SP creada
- `sp_adm_calcular_factura_lectura`

### Qué quedó resuelto
- cálculo de servicios base;
- cálculo de servicios derivados;
- armado de `taservi1..4`;
- devolución de detalle JSON;
- warnings de cálculo;
- soporte como contrato para WS y app offline.

### Validaciones mencionadas ese día
- cliente con medidor;
- cliente sin medidor;
- confirmación de subtotales y composición por servicio.

## 5. Persistencia De Lectura Y Factura
Se avanzó a persistencia completa del proceso de lectura.

### SP creada
- `sp_lectura_v3`

### Responsabilidades definidas/implementadas
- resolver cliente por clave;
- llamar a `sp_adm_calcular_factura_lectura`;
- actualizar `historicomedicion` o `historicosinmedidor`;
- cerrar factura activa previa cuando aplica;
- insertar `factura`;
- insertar `factura_detalle`;
- insertar `transaccion_abonado`;
- devolver comprobante estructurado.

### Pruebas indicadas ese día
- pruebas con rollback;
- validación con cliente medido y no medido;
- confirmación de líneas de detalle y transacciones creadas.

## 6. WS V3
Se definió e implementó el bloque principal del WS para el nuevo flujo.

### Trabajo realizado
- localización del WS real;
- incorporación de `ActualizarLecturaV3`;
- creación de modelos V3;
- ajustes de opcionales con `DBNull.Value`;
- build del proyecto del WS;
- identificación del endpoint local para pruebas.

### Objetivo funcional del WS
- que la app deje de enviar montos calculados manualmente;
- que la app envíe captura/lectura;
- que el servidor devuelva la factura formal y persistida.

## 7. App Android Y Operación Offline
Este fue uno de los focos más importantes del día.

### Lo trabajado
- diseño y documentación explícita de operación offline;
- snapshot formal de factura en SQLite;
- protección para que la factura emitida en app sea la misma que sube al servidor;
- estados de sincronización:
  - `PENDING_SYNC`
  - `SYNCED`
  - `SYNC_ERROR`
  - `SYNC_CONFLICT`
- integración inicial/fuerte del app con V3;
- build del APK;
- instalación del APK;
- preparación de pruebas con `adb reverse`.

### Decisión importante del día
La factura entregada al cliente en el app debe ser la factura formal.  
No se debe aceptar silenciosamente que el servidor confirme otra factura distinta.

## 8. Snapshot Offline V3
Se extendió el diseño para descargar el paquete operativo offline desde servidor.

### Trabajo realizado
- diseño del endpoint de snapshot offline V3;
- creación de función SQL para generar snapshot por cliente de lectura;
- creación del modelo de snapshot en WS;
- integración del app para descargar y guardar ese paquete offline;
- pruebas iniciales con ruta/ciclo de laboratorio.

## 9. Pruebas End-To-End Del Día
Se fueron cerrando pruebas técnicas y operativas de laboratorio.

### Se validó
- WS local corriendo;
- obtención de ciclo;
- obtención de ruta;
- obtención de snapshot offline V3;
- apertura del app;
- build del APK;
- instalación en dispositivo;
- primera descarga de información.

### Problemas detectados y corregidos ese día
- error de conversión de `Secuencia` en Android cuando venía como `String`;
- corrección para parseo tolerante de enteros provenientes del WS;
- errores de seed SQL;
- errores de tipos en funciones SQL;
- inconsistencias entre snapshot y sincronización.

## 10. Portal Y Documentación
Se aterrizó el trabajo en portal y se dejó más claro el roadmap.

### Documentos generados o consolidados ese día
- `PLAN_INTEGRACION_LECTURA_WS_MOTOR_TARIFARIO_2026-04-17.md`
- `PLAN_00_INDICE_CORTE_LECTURA_V3_2026-04-17.md`
- `PLAN_01_SP_ADM_CALCULAR_FACTURA_LECTURA_2026-04-17.md`
- `PLAN_02_SP_LECTURA_V3_2026-04-17.md`
- `PLAN_03_ACTUALIZAR_LECTURA_V3_WS_2026-04-17.md`
- `PLAN_04_PORTAL_MANTENIMIENTOS_MOTOR_TARIFARIO_2026-04-17.md`
- `PLAN_05_APP_LECTORES_V3_2026-04-17.md`
- `PLAN_06_OFFLINE_SYNC_LECTURA_V3_2026-04-17.md`
- `ESTADO_CORTE_LECTURA_V3_2026-04-17.md`

### Enfoque acordado
- no borrar lo viejo primero;
- mover el cálculo al servidor;
- sostener compatibilidad temporal;
- luego cerrar app, portal y corte legacy.

## 11. Tareas Estratégicas Que Nacieron Ese Día
Se identificaron como siguientes bloques estructurales:

### CAI y correlativo offline
- reserva de bloques;
- consumo seguro;
- conciliación;
- manejo de agotado, duplicado e idempotencia.

### Reglas pendientes
- tercera edad;
- topes;
- beneficios legales;
- reglas especiales por categoría y segmento.

### Portal V3
- ficha del cliente V3;
- `adm_cliente_servicio`;
- maestro de servicios;
- vista de conflictos;
- mantenimiento tarifario;
- pantalla de prueba de cálculo.

### Corte legacy
- apagar SPs legacy del WS;
- retirar cálculo legacy del app;
- retirar `ActualizarLecturaV2` al cerrar V3.

## 12. Decisiones Clave Del 2026-04-18
- el catálogo maestro de servicios del motor nuevo es la base del modelo;
- el app debe funcionar offline;
- la factura emitida en app debe ser la factura formal;
- el servidor no debe aceptar silenciosamente una factura distinta;
- el corte debe hacerse por fases, no borrando primero lo viejo;
- el portal debe alinearse al nuevo motor tarifario y no quedarse solo en tablas técnicas.

## 13. Estado De Cierre Del Día
Al cierre del `2026-04-18` el proyecto quedó, a nivel de conversación y ejecución, en este punto:

- motor tarifario V3 funcional en BD;
- servicios base y derivados ya sembrados y probados;
- cálculo de factura de lectura definido;
- persistencia V3 definida;
- WS V3 ya encaminado;
- app offline y sincronización muy avanzados;
- documentación técnica extensa ya creada;
- pendientes estratégicos identificados:
  - CAI/correlativo offline,
  - reglas pendientes,
  - portal operativo V3,
  - corte legacy.

## Referencias Relacionadas
- [PLAN_MODELO_TARIFARIO_CLIENTE_SERVICIO_2026-04-14.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_MODELO_TARIFARIO_CLIENTE_SERVICIO_2026-04-14.md)
- [PLAN_INTEGRACION_LECTURA_WS_MOTOR_TARIFARIO_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/PLAN_INTEGRACION_LECTURA_WS_MOTOR_TARIFARIO_2026-04-17.md)
- [ESTADO_CORTE_LECTURA_V3_2026-04-17.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/ESTADO_CORTE_LECTURA_V3_2026-04-17.md)
- [task_cai.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/task_cai.md)
- [task_reglas_pendientes.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/task_reglas_pendientes.md)
- [task_portal_cliente_v3.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/task_portal_cliente_v3.md)
- [task_corte_legacy.md](/E:/Koala/proyectos/HODSOFT_DEVEXPRESS/Prestadoras/docs/task_corte_legacy.md)
