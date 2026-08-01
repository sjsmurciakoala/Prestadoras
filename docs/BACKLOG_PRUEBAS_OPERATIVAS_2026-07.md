# Backlog — pruebas operativas del equipo (julio 2026)

Fuente: hojas de "Requerimientos, inconsistencias y dudas detectadas en pruebas
del nuevo sistema" (incluye pruebas del 16-07-2026) entregadas el 27-07-2026.
Triage contra el estado real del código a esa fecha. Se trabaja por fases; lo
marcado ✅ ya quedó resuelto por la unificación de cobranza (PRs #40–#42).

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

### Cobranza / convenios (va con F6 del plan de unificación)
- Anticipo de cuotas en convenio de pago.
- Anular convenio de pago.
- Cargar gestión legal al cliente mediante nota de débito.
- Cliente **bloqueado por cobranza** no debería poder recibir recibo/cobro.
- Carta prejudicial: correlativo de avisos enviados al cliente; formato.
- Carta de cobro: incluir número de medidor.
- Bitácora: registrar quién ejecutó la acción.
- Reimprimir cartas ya enviadas.
- Búsqueda en gestión de cobranza no funciona.

### Estado de cuenta (va con F4)
- Filtrar por rangos de fecha.

### Notas de crédito / débito — ✅ RESUELTO (lote 2, 2026-08-01, PR #54)
- ~~Colocar número de cuenta del cliente~~ → "Cuenta No." en el documento
  impreso, columna del listado y la búsqueda filtra por clave.
- ~~Ver todas las NC/ND de un cliente~~ → buscar la clave en Notas emitidas.
- ~~Reimprimir NC/ND y vista previa antes de guardar~~ → botón Imprimir por
  fila + Vista previa en el popup (emite con el mismo SP y revierte: formato
  exacto con marca de agua, sin persistir ni consumir correlativo).
- También quedó (2026-07-31): **Informe de banco diario** en Reportería →
  Cobranza (rep_banco_diario + layout publicado, tope 31 días).

### Clientes
- Campo "Acueducto" a nivel de cliente (no del medidor).
- RTN no obligatorio al crear.
- Error técnico al guardar/actualizar clientes existentes (reproducir y corregir).
- Maestro: abogado asignado; casilla de estudio socioeconómico.

### Facturación / tarifario
- Renombrar "Posteo de caja" → nomenclatura acordada (la vista ya es "Caja").
- Cambio de categoría (Doméstico→Comercial) debe generar partida contable.
- Condición de lectura nueva no genera efecto (gap conocido codigo→tipo, L8).
- Dudas de tasas (fondo ambiental / ERSAPS) al crear cliente y sección "No aplica" — sesión de aclaración de configuración tarifaria.

### Reportes
- **Informe de banco diario** (indispensable para operación) — nueva función `rep_*` + plantilla.

### Órdenes de trabajo
- Módulo de mantenimiento/creación de usuarios.
- Reactivar ubicación GPS de cuadrillas (existía contra SIMAFI).
- Hora de emisión de la orden estática/incorrecta.
- Foto y coordenadas GPS no visibles en el portal.
