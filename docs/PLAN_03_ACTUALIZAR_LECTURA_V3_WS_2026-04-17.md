# Plan 03: `ActualizarLecturaV3` en WS

Fecha: 2026-04-17

Objetivo: definir el nuevo contrato WCF/JSON para que la app de lectura capture datos y el servidor calcule y devuelva la factura al instante.

---

## 1. Estado actual

Hoy existe:

- `IAPCService.ActualizarLecturaV2`
- `APCService.ActualizarLecturaV2`
- modelo `MedidorModeloV2`

Hoy el WS:

- recibe lectura y detalle por servicio;
- no recalcula montos;
- solo serializa y llama `sp_lectura_v2`.

Eso hay que cambiar.

---

## 2. Estado objetivo

Debe existir:

- `IAPCService.ActualizarLecturaV3`
- `APCService.ActualizarLecturaV3`
- modelo de request V3
- modelo de response V3

La version `V3` debe:

- recibir datos minimos de captura;
- invocar `sp_lectura_v3`;
- devolver la factura lista al app.

Ademas debe convivir con una estrategia offline:

- si hay red, responde factura oficial;
- si no hay red, la app usa snapshot local, emite factura formal y luego sincroniza esa misma factura.

---

## 3. Contrato de request

### 3.1 Debe conservar

- `Anio`
- `Mes`
- `Contador`
- `FechaLecturaActual`
- `Usuario`
- `LecturaActual`
- `Ser3`
- `Ser4`
- `Observacion`
- `CondicionLectura`
- `LecturaPromedio`
- `NumeroFactura`
- `CorrelativoCai`
- `IdCai`
- `TieneMedidor`
- `Clave`
- `Imagen`
- `Informativo`

### 3.2 Debe dejar de usar como fuente

- `Consumo`
- `Categoria`
- `Descuento`
- `DetalleServicios`

Se pueden aceptar temporalmente si el app viejo los sigue mandando, pero:

- no se usan para calcular;
- solo se registran para diagnostico o comparacion.

### 3.3 Recomendacion

Crear un request nuevo, por ejemplo `MedidorModeloV3`, con:

- campos de captura;
- un bloque opcional `DebugLegacy` si queremos comparar contra el app viejo.

---

## 4. Contrato de response

La respuesta ya no debe ser solo `1` o `0`.

Debe devolver un objeto estructurado:

- `Success`
- `Codigo`
- `Mensaje`
- `NumeroFactura`
- `Cliente`
- `LecturaAnterior`
- `LecturaActual`
- `Consumo`
- `Total`
- `DetalleServicios`
- `Warnings`

### 4.1 DetalleServicios de respuesta

Cada linea debe incluir:

- `ServicioCodigo`
- `Descripcion`
- `Monto`
- `Origen`

### 4.2 Motivo

Esto es necesario porque el lector debe:

- mostrar la factura al cliente;
- imprimirla;
- y confirmar el total en sitio.

---

## 5. Manejo de errores

El endpoint debe responder claramente:

- `Success = false`
- `Codigo = "PERIODO_CERRADO"` / `CLIENTE_NO_CONFIGURADO` / etc.
- `Mensaje` amigable para usuario

Y seguir dejando traza tecnica en:

- log del WS
- cabecera de diagnostico si todavia se requiere

---

## 6. Convivencia con V1 y V2

Durante la transicion:

- `ActualizarLectura` se conserva
- `ActualizarLecturaV2` se conserva
- `ActualizarLecturaV3` se agrega

La regla es:

- V1/V2 = compatibilidad
- V3 = ruta nueva oficial

No debemos romper la app actual mientras se adapta.

---

## 6.1 Relacion con offline

`ActualizarLecturaV3` no resuelve por si solo el modo offline.

Pero si define el contrato oficial que la app debe emular cuando este sin conectividad.

Por eso la respuesta V3 debe ser:

- estable;
- serializable;
- y comparable contra una factura emitida localmente en offline.

Tambien debemos prever un endpoint posterior de sincronizacion de lecturas offline,
sin mezclarlo con el primer corte del endpoint online.

Regla adicional:

- el servidor no debe renumerar silenciosamente una factura ya emitida por el app.

---

## 7. Archivos a tocar

### 7.1 WS

- `WSappLectores/WS_APC/IAPCService.cs`
- `WSappLectores/WS_APC/APCService.svc.cs`
- `WSappLectores/WS_APC/Modelos/MedidorModeloV2.cs`

### 7.2 Posible recomendacion

No conviene reciclar `MedidorModeloV2`.

Mejor crear:

- `MedidorModeloV3`
- `LecturaRespuestaV3`
- `LecturaServicioDetalleRespuesta`

---

## 8. Criterio de aceptacion

Este bloque queda listo cuando:

1. el app puede invocar `ActualizarLecturaV3`;
2. el endpoint no depende de montos enviados por cliente;
3. devuelve factura completa;
4. devuelve errores claros;
5. V1/V2 siguen operando mientras dura la migracion.
