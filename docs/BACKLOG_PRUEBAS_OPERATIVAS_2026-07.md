# Backlog — pruebas operativas del equipo (julio 2026)

Fuente: hojas de "Requerimientos, inconsistencias y dudas detectadas en pruebas
del nuevo sistema" (incluye pruebas del 16-07-2026) entregadas el 27-07-2026.
Triage contra el estado real del código a esa fecha. Se trabaja por fases; lo
marcado ✅ ya quedó resuelto por la unificación de cobranza (PRs #40–#42).

> **Verificación final 2026-08-04**: cada ✅ de este documento fue re-verificado
> contra la PANTALLA REAL y los datos (no solo contra el código) tras las
> pruebas en vivo del usuario. Criterio: "resuelto" = se ve y funciona donde el
> usuario lo busca. Los hallazgos nuevos de esas pruebas están al final del
> documento.

## ✅ Resuelto (verificar en la siguiente prueba operativa)

| Punto | Resolución |
|---|---|
| El abono se refleja **sumando** al saldo en el estado de cuenta (16-07) | Bug de la convención de estados invertida; corregido con saldo por vigencia (2026-07-16) y blindado en F1 con `adm_estado_pago` |
| "Aún no está habilitado el abono especial" | Unificado: la vista de Caja cobra parcial (= abono) y total en el mismo flujo |
| No despliega el banco destino al aplicar un abono | Vista de Caja: Forma de pago → BANCO → combo de cuenta bancaria |
| Número de cuenta correlativo automático al crear cuenta | Ya existe: código de cliente automático (`adm_codigo_cliente_config`) |
| Libreta amarrada al ciclo | Ya existe: libretas globales (`adm_libreta` sin ciclo) |
| Explicación del módulo Reverso | Integrado en Caja (Cobros del día → reversar con motivo, auditado, sin DELETE) |
| Prioridad de rebaja "no visible a qué servicio aplica" | `adm_pago_aplicacion` registra la aplicación línea a línea; ver también "aplicación por porcentajes" (en curso) |

## ✅ Resuelto — segunda tanda (2026-07-31, rama fix/pruebas-operativas-lote1)

| Punto | Resolución |
|---|---|
| Recibo para banco + conciliación automática | HECHO (F7 H1/H4b): papel pendiente sin tocar la factura; al saldarse por cualquier canal se anula como CUBIERTO |
| Abono normal automático 60/30/5/5 con prioridad | HECHO: otros cargos primero, resto por `adm_desglose_abono_porcentaje` — regla del motor, no presentación |
| Estado de cuenta: filtrar por rangos de fecha | Filtro Desde/Hasta en Movimientos; la ventana recorta filas pero el saldo corrido sigue siendo el histórico real. De paso: los cobros/facturas POST-corte ahora aparecen (el espejo congelado ya no los recibía) |
| Cliente bloqueado por cobranza podía cobrar/recibir recibo | Candado en el motor: ni cobro ni papel para banco con `bloqueado_cobranza`; mensaje dirige a Gestión de Cobranza |
| Error técnico al actualizar clientes existentes | Causa raíz: DNI obligatorio vs 6,300 clientes migrados con identidad vacía — TODA edición reventaba. DNI ahora opcional (unicidad solo si viene) |
| RTN obligatorio | Ya estaba resuelto (máscaras con dígitos opcionales); verificado en create/update/solicitud |
| Hora de emisión de la orden estática/incorrecta | Se guardaba en UTC (+6h) — ahora hora local del negocio |
| Búsqueda en gestión de cobranza | La causa era el lookup con lista precargada limitada; hoy es búsqueda remota al servidor (clave/nombre). Confirmar en la siguiente prueba |
| GPS de cuadrillas + foto/coordenadas en portal | HECHO (merge 2026-07-30): mapa con histórico de recorridos + visor de fotos en el detalle de la orden |
| "Posteo de caja" → renombrar | Obsoleto: la pantalla ya no existe, el flujo vive en la vista única Caja |

## 📌 Pendiente por módulo

### Cobranza / convenios — ✅ RESUELTO (lote 4, 2026-08-01)
- ~~Anticipo de cuotas en convenio~~ ✅ el motor cobra cuotas futuras y fuera
  de orden (verificado y fijado con test; la caja las lista todas).
- ~~Anular convenio de pago~~ ✅ botón Anular en `/facturacion/cobranza`
  (pestaña planes, con motivo obligatorio): lo cobrado queda como pago
  histórico; el saldo de las cuotas vivas VUELVE a las facturas de origen vía
  cln_plan_pago_traslado (FIFO), la factura recupera estado pendiente/parcial
  y el plan queda ANULADO (el grid ahora muestra el estado numérico real:
  ACTIVO/COMPLETADO/ANULADO, no el legacy "Pendiente").
- ~~Cargar gestión legal mediante ND~~ ✅ motivo de aumento GESTION_LEGAL
  sembrado (`2026-08-01_motivo_nd_gestion_legal.sql`): se emite la ND desde
  /facturacion/notas contra una factura del cliente con ese motivo y queda
  cobrable en caja como cualquier ND.
- ~~Cliente bloqueado por cobranza no debería poder recibir recibo/cobro~~ ✅ lote 1.
- ~~Carta prejudicial: correlativo de avisos; formato~~ ✅ lote 3 (2026-08-01):
  AVISO N.º por cliente (cuenta los snapshots archivados) + formato formal con
  membrete real de la empresa, cuenta y medidor.
- ~~Carta de cobro: incluir número de medidor~~ ✅ ya estaba (REQUERIMIENTO #N
  por cliente + medidor/libreta/secuencia en PDF y HTML) — verificado.
- ~~Bitácora: quién ejecutó la acción~~ ✅ ya estaba (`ejecutado_por` se guarda,
  se filtra y sale como columna del historial) — verificado.
- ~~Reimprimir cartas ya enviadas~~ ✅ lote 3: el historial gana botón
  Reimprimir (snapshot archivado); en Acciones de Cobranza ya existía.
- ~~Búsqueda en gestión de cobranza no funciona~~ ✅ lote 1 (lookup remoto).

### Estado de cuenta — ✅ RESUELTO (lote 1)
- ~~Filtrar por rangos de fecha~~ ✅ filtro Desde/Hasta en la pestaña
  Movimientos de la ficha del cliente (`/clientes/{id}`).

### Notas de crédito / débito — ✅ RESUELTO (lote 2, 2026-08-01, PR #54)
- ~~Colocar número de cuenta del cliente~~ → "Cuenta No." en el documento
  impreso, columna del listado y la búsqueda filtra por clave.
- ~~Ver todas las NC/ND de un cliente~~ → buscar la clave en Notas emitidas.
- ~~Reimprimir NC/ND y vista previa antes de guardar~~ → botón Imprimir por
  fila + Vista previa en el popup (emite con el mismo SP y revierte: formato
  exacto con marca de agua, sin persistir ni consumir correlativo).
- También quedó (2026-07-31): **Informe de banco diario** en Reportería →
  Cobranza (rep_banco_diario + layout publicado, tope 31 días).

### Clientes — ✅ RESUELTO (PRs #48, #59, #63)
- ~~Campo "Acueducto" a nivel de cliente~~ ✅ columna `cliente_maestro.acueducto`
  + backfill desde medidor; visible en la FICHA (`/clientes/{id}`), crear y
  editar. Nota: los migrados salen vacíos — SIMAFI no traía el dato.
- ~~RTN no obligatorio al crear~~ ✅ verificado (lote 1).
- ~~Error técnico al guardar/actualizar clientes~~ ✅ causa raíz DNI
  obligatorio vs 6,300 migrados sin identidad (lote 1).
- Maestro: **abogado asignado** ✅ (ficha + editable en el formulario) y **No
  cortable** ✅ editable con confirmación (falta mostrarlo en la ficha).
  **Estudio socioeconómico: PENDIENTE** — el dato existe en la BD pero no se
  muestra ni se edita en las pantallas reales (quedó en un componente huérfano;
  corrección corta programada).

### Facturación / tarifario
- ~~Renombrar "Posteo de caja"~~ ✅ obsoleto: la pantalla ya no existe, el
  flujo vive en la vista única Caja.
- ~~Cambio de categoría (Doméstico→Comercial) debe generar partida contable~~
  ✅ RESUELTO Y **VERIFICADO EN VIVO** (2026-08-04): el usuario cambió la
  categoría del cliente 090103219 en `/tarifario/cliente-servicio-v3` y se
  generaron y postearon las pólizas 36378 (Dom→Com) y 36379 (Com→Dom, reversa
  natural), L. 149.45 c/u, con bitácora en `cln_cliente_recategorizacion` y
  snapshot de factura actualizado. Funciona en LOS DOS flujos (tarifario V3
  vía equivalencia `adm_categoria_regulatoria.categoria_servicio_id`, y
  Editar Cliente). El aviso al guardar es un **alert fijo** con monto y N° de
  póliza (PR #66 — el toast original moría con el re-render del panel). Solo
  aplica con integración contable en modo POR_SERVICIO_CATEGORIA. Las pólizas
  se consultan en `/contabilidad/partidas`.
- Condición de lectura nueva no genera efecto (gap conocido codigo→tipo, L8).
- Dudas de tasas (fondo ambiental / ERSAPS) al crear cliente y sección "No aplica" — sesión de aclaración de configuración tarifaria.

### Reportes — ✅ RESUELTO (2026-07-31/08-01)
- ~~Informe de banco diario~~ ✅ Reportería → Cobranza → "Banco diario"
  (`rep_banco_diario`, layout diseñado, tope 31 días por rendimiento).

### Órdenes de trabajo
- Módulo de mantenimiento/creación de usuarios — PENDIENTE (bloqueado por el
  backend legacy 8086; se resuelve cuando se reemplace ese servicio).
- ~~Reactivar ubicación GPS de cuadrillas~~ ✅ segunda tanda (mapa con histórico).
- ~~Hora de emisión estática/incorrecta~~ ✅ segunda tanda (era UTC+6).
- ~~Foto y coordenadas GPS no visibles en el portal~~ ✅ segunda tanda (visor
  en el detalle de la orden).

## Hallazgos de las pruebas en vivo de agosto (2026-08-04)

Cosas que las pruebas del usuario destaparon y no estaban en las hojas
originales. Los ✅ ya están mergeados a main; los ⏳ son la cola de trabajo.

| Estado | Hallazgo | Detalle |
|---|---|---|
| ✅ | NC/ND no aparecían en el estado de cuenta | PR #65: la ND entra como CARGO y la NC como CRÉDITO en la pestaña Movimientos, con número de documento; anuladas se listan sin mover el saldo |
| ✅ | El aviso de la reclasificación no se veía | PR #66: alert fijo dismissible en vez de toast (el re-render lo mataba) |
| ✅ | Acueducto/partida "no funcionaban" | Causas raíz: campo en componente huérfano (PR #63), flujo real en tarifario no en Editar Cliente (PR #64), período contable de agosto sin abrir en la BD local (las partidas quedaban encoladas), y base caída por el disco USB durante las pruebas |
| ⏳ | **ND contra facturas migradas truena** | Las 3.9M facturas de SIMAFI no tienen número fiscal y `adm_nota_debito.factura_origen_numero` es NOT NULL — el SP debe usar el número de recibo como respaldo |
| ⏳ | **Estudio socioeconómico invisible** | Mostrarlo en la ficha real y hacerlo editable (formulario + DTOs); mostrar también "No cortable" en la ficha |
| ⏳ | **PDF del convenio de pago** | No existe; imprimir desde la pestaña de planes (membrete, cuotas, firmas) |
| ⏳ | **PDF del estado de cuenta** | No existe; hoy solo en pantalla (pestaña Movimientos) |
| ⚠️ | Cortes masivos sin tests automáticos | El flujo completo existe (lote → órdenes reales → impresión → cancelación al pagar) pero nadie lo ha validado punta a punta; probar con lote chico |

Recordatorios operativos de la BD local (`siad_v3_copia09`): abrir el período
contable de cada mes nuevo (sin él las partidas se encolan) — igual en
`siad_v3_test` o los tests de Presupuesto fallan; el disco USB E: sigue siendo
frágil (si Postgres "no levanta", suele ser eso + ~4 min de recovery).
