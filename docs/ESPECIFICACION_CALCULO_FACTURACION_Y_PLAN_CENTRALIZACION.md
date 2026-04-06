# Especificacion de calculo de facturacion y plan de centralizacion

Objetivo: describir exactamente como se calcula hoy la factura en la app de lectura y definir un plan para centralizar el calculo en servidor.

---

## 1) Donde se calcula hoy

- El calculo de factura se hace en **la app Android** (`GenerarFactura.java` + `UtilidadesBD.java`).
- El WS solo **recibe** montos ya calculados y los guarda con `sp_lectura`.
- El backend no recalcula montos para lecturas del WS.

---

## 2) Insumos del calculo (app)

### Datos del medidor (SQLite)

- `TieneMedidor`
- `Categoria`
- `Tipo` (string, se usa como numero)
- `Codigo` (tarifa global)
- `LecturaAnterior`, `LecturaActual`, `Consumo`
- `CondicionLectura` (MIN, PND, PD, N)
- `Promedio`
- `ValorDescuento` (porcentaje)
- `Ser1..Ser10` (banderas de servicios)
- Saldos anteriores por servicio
- Recargos por servicio

### Tarifas y cobros (SQLite)

- `Tarifa` (global sin medidor)
- `TarifaContador` (por rangos)
- `CobroAdicional` (por categoria)
- `Configuracion` (IDs fijos 1..6)

---

## 3) Seleccion de tarifas (logica actual)

### 3.1 Sin medidor

- Usa tabla `Tarifa` (global).
- Filtro:
  - `Tipo = Medidor.Tipo`
  - `Categoria = Medidor.Categoria`
  - `Codigo = Medidor.Codigo`
- Resultado:
  - `ValorAgua = Tarifa.Valor`
- No usa rangos.

### 3.2 Con medidor

- Usa tabla `TarifaContador`.
- Filtro:
  - `Tipo = 1` (hardcode)
  - `Categoria = Medidor.Categoria`
- Orden:
  - `ORDER BY Tipo, Categoria` (no usa Min/Max para filtrar).
- Algoritmo por rangos:
  - Se recorre la lista completa de rangos.
  - Se calcula el consumo en tramos.
  - Primera fila suma `Cuota * consumo_tramo + Agua (base)`.
  - Filas siguientes suman `Cuota * consumo_tramo`.
  - Si `Alquiler > 0` y hay consumo en el tramo, se suma `Alquiler`.
  - Si consumo total = 0, se asigna `ValorAgua = BaseAgua`.
  - `BaseAgua` es el ultimo valor `Agua > 0` encontrado en el loop.

---

## 4) Condicion de lectura

- `MIN` o `PND`:
  - `LecturaActual = LecturaAnterior`
  - `Consumo = 0`
- `PD`:
  - `LecturaActual = LecturaAnterior + Promedio`
  - `Consumo = Promedio`
- `N`:
  - no modifica lectura

---

## 5) Cobros adicionales

### 5.1 Fuente

- Se cargan por categoria desde `CobroAdicional` en SQLite.
- Se valida aplicacion con `GetAplicarCobroAdicional`, que lee `serBase{IdCobro}` del medidor.

### 5.2 Formula

- Para cada cobro aplicable:
  - `Valor = (PorcentajeAgua * ValorAgua) + (PorcentajeAlcantarilla * ValorAlcantarilla)`
  - Si `IdCobro == 2` (alcantarillado), se asigna `ValorAlcantarilla = Valor`.

### 5.3 Cobros con descuento

- Se separan en:
  - Aplica descuento = S
  - Aplica descuento = N

---

## 6) Descuento

### 6.1 Configuracion usada

- Agua: `IdConfiguracion = 5`
- Alcantarillado: `IdConfiguracion = 6`

### 6.2 Regla actual

- Si `ValorDescuento > 0`:
  - `ValorDescuentoAgua = min(ValorAgua * %descuento, LimiteAgua)`
  - `ValorAgua = ValorAgua - ValorDescuentoAgua`
  - Se descuenta **solo Alcantarillado** (IdCobro = 2):
    - `ValorDescuentoAlc = min(ValorCobro * %descuento, LimiteAlc)`
    - `ValorCobro = ValorCobro - ValorDescuentoAlc`
  - Otros cobros con "AplicaDescuento = S" no reciben descuento (solo alcantarillado).

---

## 7) Saldos atrasados y recargos

- Se leen desde campos del medidor en SQLite:
  - Saldos anteriores: agua, alcantarillado, ambiental, convenio, ersap, gestion legal, otros, ser6..ser10
  - Recargos: agua, ambiental, convenio, ersap, gestion legal, otros, ser6..ser10
- Se suman completos.

---

## 8) Total final

`ValorCuotaTotal = ValorAgua + CobrosAdicionales + SaldosAtrasados + Recargos`

- Redondeo a 2 decimales.
- Si total < 0, se fuerza a 0.
- Se actualiza el Medidor local y el CAI local.

---

## 9) Valores que se envian al WS

En `SendDataToServer`:

- `taservi1 = ValorAgua`
- `taservi2 = CobroAdicional Id 2`
- `taservi3 = CobroAdicional Id 3`
- `taservi4 = CobroAdicional Id 4`
- `Consumo`, `LecturaActual`, `CondicionLectura`, `DescuentoAplicado`, etc.

El WS guarda estos valores en `sp_lectura` y genera:
`historicomedicion`, `factura`, `factura_detalle`, `transaccion_abonado`.

---

## 10) Problemas actuales

- Calculo en el movil, no centralizado.
- Dependencia de IDs fijos de configuracion (1..6).
- Hardcode de `Tipo = 1` en TarifaContador.
- Descuento solo aplica a alcantarillado aunque otros cobros esten marcados con descuento.
- Diferencias posibles entre dispositivos si hay datos locales desactualizados.

---

## 11) Plan de centralizacion (fase 1: facturacion)

### Paso 1: Congelar reglas actuales

- Tomar esta especificacion como regla base.
- Crear casos de prueba (golden cases) con resultados esperados.

### Paso 2: Motor de calculo en servidor

- Crear un procedimiento o servicio:
  - `sp_calcular_factura` o equivalente.
- Entradas:
  - lectura, consumo, condicion, promedio, categoria, tiene medidor, codigo, tipo, descuento, ser1..ser10.
- Fuentes:
  - Tarifa, TarifaContador, CobrosAdicionales, Configuracion.
- Salidas:
  - `taservi1..4`, `descuento_aplicado`, `total`, detalle de cobros.

### Paso 3: Integrar con `sp_lectura`

- `sp_lectura` debe usar el calculo del servidor.
- El WS debe ignorar los montos enviados por la app (o validarlos y loggear diferencias).

### Paso 4: Mover a app solo lecturas

- La app envia solo lecturas y datos basicos.
- Se elimina calculo local progresivamente.

### Paso 5: Limpiar hardcode

- Reemplazar IDs fijos con configuraciones por codigo/rol.
- Parametrizar `Tipo` para TarifaContador.
- Formalizar en tabla de reglas.

---

## 12) Decision recomendada

- Centralizar en servidor para garantizar consistencia.
- Mantener app como cliente simple de captura.
- Validar que el motor en servidor replica exactamente el calculo actual antes de cambiar la app.

---

## 13) Contrato nuevo propuesto (V2)

### Endpoint sugerido (WS)

- `POST /ActualizarLecturaV2`
- Recibe detalle por servicio (sin `taservi1..4`).

### JSON propuesto (ejemplo)

```
{
  "Anio": 2026,
  "Mes": 2,
  "Contador": "SENS-8842-X",
  "FechaLecturaActual": "2026-02-26",
  "Usuario": "jmurcia",
  "LecturaActual": 800,
  "Consumo": 200,
  "Ser3": "N",
  "Ser4": "N",
  "Observacion": "",
  "CondicionLectura": "N",
  "LecturaPromedio": 120,
  "NumeroFactura": "1-126800",
  "CorrelativoCai": 123,
  "IdCai": 7,
  "TieneMedidor": "S",
  "Clave": "0019818989",
  "Informativo": "",
  "Descuento": 0,
  "Categoria": "9",
  "DetalleServicios": [
    { "ServicioCodigo": "SRV001", "Descripcion": "AGUA POTABLE", "Monto": 1600.00 },
    { "ServicioCodigo": "SRV002", "Descripcion": "ALCANTARILLADO", "Monto": 12800.00 }
  ]
}
```

### SP nuevo

- `sp_lectura_v2` (ver `Prestadoras/Database/2026-02-26_add_sp_lectura_v2.sql`)
- Acepta `p_detalle jsonb` y registra:
  - `factura`
  - `factura_detalle` por servicio
  - `transaccion_abonado` por servicio
 - Nota: en V2, `historicomedicion.taservi1..4` se setean en `0` (ya no representan montos).
