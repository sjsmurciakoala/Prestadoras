# ISV en los ingresos al almacén (Centura legacy) — análisis del flujo

Fecha de revisión: 2026-07-30
Fuente: `E:\Koala\Users\Dell\Documents\GitHub\SIAD_Centura\APP ZIP\GA_IN.APT` (módulo de Inventario)
Motivo: resolver empíricamente la decisión **D1** de [`docs/plans/2026-07-29-configuracion-isv-compras-design.md`](../plans/2026-07-29-configuracion-isv-compras-design.md) — ¿el ISV de compras va al costo del inventario o se separa?

> **Nota de método:** en Centura/SQLWindows el carácter `!` marca comentario. Varias líneas clave de este flujo están comentadas (código muerto) y **no** deben leerse como lógica vigente. Abajo se distingue explícitamente.

---

## 1. Respuesta corta

El sistema legacy **no fija una política única**: la decide el usuario **factura por factura**, con una casilla en la pantalla de recepción de factura de proveedor.

| Casilla `cb3` "Detallar Impuesto sobre Ventas" | Costo que entra al inventario |
|---|---|
| **Marcada** | Costo **SIN** ISV. El impuesto se guarda aparte, en su propia columna |
| **Desmarcada** | Costo **CON** ISV incluido (se capitaliza en el inventario) |

Y hay una regla automática: **si el producto tiene una tasa de impuesto configurada, el sistema fuerza la casilla a marcada y la deshabilita** — es decir, el comportamiento por defecto para artículos con ISV configurado es **separar el impuesto del costo**.

---

## 2. Evidencia en el código

### 2.1 La casilla

`GA_IN.APT:54114`

```
Check Box: cb3
    ...
    Title: Detallar Impuesto sobre Ventas
```

### 2.2 El costo que se postea al kardex

`GA_IN.APT:54235-54242` (función de actualización de kardex/existencias, formulario `frmFacturacionPRV`)

```
! Set clsKardex.nCosto= (tblFactura.colCostoActualPRVTotal ) /
                tblFactura.colCantRecUnidades          <-- COMENTADO (versión antigua: siempre sin ISV)
If cb3 = TRUE
    Set clsKardex.nCosto= (tblFactura.colCostoActualPRVTotal ) /
                    tblFactura.colCantRecUnidades       <-- SIN impuesto
Else
    Set clsKardex.nCosto= (tblFactura.colCostoActualPRVTotal + colImpuestoPRV) /
                    tblFactura.colCantRecUnidades       <-- CON impuesto sumado al costo
```

### 2.3 El detalle de la factura de proveedor (misma bifurcación, confirmación cruzada)

`GA_IN.APT:53665-53685` (evento `MU_COMPILE`)

- **`cb3 = TRUE`** → `INSERT INTO PRV_FACTURAS_DTL (… COSTO_UNITARIO … IMPUESTO) VALUES (… :colCostoActualPRV … :colImpuestoPRV)`
  Costo unitario **sin** impuesto; el impuesto viaja en su propia columna.
- **`cb3 = FALSE`** → el mismo INSERT usa **`:colcb3`** como `COSTO_UNITARIO`.
  Y `colcb3` se calcula en `GA_IN.APT:52122` como:
  ```
  Set colcb3 = colCostoActualPRV + (colImpuestoPRV / colCantidadRecibida)
  ```
  es decir, **costo unitario con el ISV prorrateado dentro**.

Las dos rutas (kardex y detalle de factura) son coherentes entre sí.

### 2.4 La regla automática que fuerza la casilla

`GA_IN.APT:53645-53656`

```
Loop
    ...
    If colISVPorcentaje > 0 OR colImpuestoPRV > 0
        Set cb3 = TRUE
        Break
If cb3
    Call SalDisableWindow( cb3 )     <-- se deshabilita: el usuario ya no puede desmarcarla
Else
    Call SalEnableWindow( cb3 )
```

Si **alguna** línea de la factura trae porcentaje o monto de ISV, la casilla se marca sola y se bloquea.

### 2.5 De dónde sale la tasa

`GA_IN.APT:53640-53641` (el SELECT que llena la grilla de la factura)

```
AND INV_PRODUCTOS.COD_IMPUESTO = AXL_IMPUESTOS.COD_IMPUESTO
AND OC_ORDENCOMP_DTL.NUM_ORDEN_COMPRA = :dfNumOrdenCompraX
```

La tasa se ancla al **producto** (`INV_PRODUCTOS.COD_IMPUESTO` → catálogo `AXL_IMPUESTOS`), no a la bodega ni al proveedor. Coincide con la decisión de diseño ya tomada para SIAD.

### 2.6 Cálculo del monto del impuesto

`GA_IN.APT:52110-52117`

```
Set colImpuestoPRV = colCostoActualPRVTotal * (1-dfPorcentajeDescuento/100) * colISVPorcentaje
If colImpuestoPRV > 0
    If cbDescuentoFijo
    Else
        ! Set colImpuestoPRV = colCostoActualPRVTotal * (1-dfPorcentajeDescuento/100) * colISVPorcentaje   <-- COMENTADO
        Set colImpuestoPRV = colCostoActualPRVTotal * colISVPorcentaje      <-- VIGENTE: SIN restar descuento
```

**Ojo:** cuando el descuento es porcentual, la versión vigente **recalcula el ISV sobre el bruto, sin restar el descuento**. La variante que sí lo restaba está comentada. Cuando el descuento es fijo (`cbDescuentoFijo`), conserva el valor que sí aplicó el descuento. Es una inconsistencia del legacy, no una regla deliberada documentada.

### 2.7 Cómo se recalcula el costo promedio

`GA_IN.APT:1152-1160`

```
UPDATE INV_EXISTENCIAS SET
    COSTO_MAS_ALTO = :nCostoMasAlto,
    COSTO_ULTIMO   = :nCosto,
    COSTO_ANTERIOR = COSTO_ACTUAL,
    COSTO_ACTUAL   = (SALDO_MONETARIO / CASE WHEN CANTIDAD_STOCK <> 0 THEN CANTIDAD_STOCK ELSE 1 END)
WHERE COD_BODEGA = :nCodBodega AND COD_PRODUCTO = :sCodProducto
```

- `COSTO_ACTUAL` **es** el costo promedio: saldo monetario ÷ cantidad en stock.
- El recálculo solo ocurre si la transacción está marcada como `bCambiaCosto`.
- El costo es **por bodega** (`INV_EXISTENCIAS` está por `COD_BODEGA` + `COD_PRODUCTO`), igual que `alm_articulo_bodega` en SIAD.
- `COSTO_ULTIMO` (último costo) y `COSTO_MAS_ALTO` se llevan aparte.

---

## 3. Objetos de base de datos involucrados

| Objeto | Rol en este flujo |
|---|---|
| `INV_PRODUCTOS` | Maestro de artículos. `COD_IMPUESTO` ancla la tasa; recibe `PRECIO_FOB` tras la recepción (`GA_IN.APT:54247`) |
| `AXL_IMPUESTOS` | Catálogo de impuestos (porcentaje) |
| `OC_ORDENCOMP_DTL` | Detalle de la orden de compra (origen de la recepción) |
| `PRV_FACTURAS_DTL` | Detalle de factura de proveedor. **`COSTO_UNITARIO` e `IMPUESTO` son columnas separadas** |
| `INV_EXISTENCIAS` | Existencia y costos **por bodega**: `SALDO_MONETARIO`, `CANTIDAD_STOCK`, `COSTO_ACTUAL` (promedio), `COSTO_ULTIMO`, `COSTO_ANTERIOR`, `COSTO_MAS_ALTO` |
| `INV_KARDEX` | Libro de movimientos. Tipo de transacción de compras: `'COM'` (`GA_IN.APT:54262`) |

---

## 4. Qué NO se pudo verificar en esta revisión

Estos puntos requieren la base de datos SQL Server del legacy o el código Delphi, y **no se afirman**:

1. **A qué cuenta contable va el ISV** cuando la casilla está marcada (si a una cuenta de crédito fiscal, a gasto, o si simplemente no genera asiento). El asiento contable no está en el fragmento analizado.
2. Si `bCambiaCosto` está activo para el tipo de transacción `'COM'` — de eso depende que la compra realmente mueva el promedio.
3. Cómo se comporta el flujo con **devoluciones** al proveedor (`INV_DEVOLUCIONES` tiene columna `ISV`, `GA_IN.APT:1668-1699`) y si revierte el costo simétricamente.
4. Si en producción los artículos tienen realmente `COD_IMPUESTO` poblado — de eso depende cuál de las dos ramas se ejecuta en la práctica.

---

## 5. Implicación para el diseño de SIAD

El hallazgo **no cierra** la decisión D1, pero la reencuadra:

1. **El legacy soporta las dos políticas**, y la elección es **por documento**, no por empresa. El diseño actual de SIAD la modela como política **por empresa + destino + vigencia**. Hay que preguntarle al contador si quiere conservar la flexibilidad por factura o unificarla.
2. **El comportamiento por defecto del legacy es separar el ISV del costo** (casilla forzada cuando el artículo tiene tasa). Es un indicio de cómo ha venido operando la empresa, **no** una decisión fiscal validada.
3. Que el costo se guarde sin ISV **no implica** por sí solo que se esté acreditando como crédito fiscal: solo significa que no se capitalizó. Falta ver el asiento (punto 4.1).
4. La base del ISV en el legacy **no resta el descuento porcentual** (§2.6). El diseño de SIAD asume base = precio − descuentos. Hay que confirmarlo con el contador (se relaciona con **D5**).

### Pregunta adicional sugerida para la sesión con el contador

> **D12 — En el sistema anterior, la pantalla de recepción de factura de proveedor tenía una casilla "Detallar Impuesto sobre Ventas" que decidía, factura por factura, si el ISV se sumaba al costo del material o se registraba aparte. ¿Quiere conservar esa flexibilidad por documento, o prefiere una política única para toda la empresa?**

---

## 6. Trazabilidad

| Regla | Fuente exacta |
|---|---|
| Casilla "Detallar Impuesto sobre Ventas" | `GA_IN.APT:54114` (título en `:54120`) |
| Costo al kardex, ambas ramas | `GA_IN.APT:54237-54242` |
| INSERT a `PRV_FACTURAS_DTL`, ambas ramas | `GA_IN.APT:53665-53685` |
| Costo unitario con ISV prorrateado (`colcb3`) | `GA_IN.APT:52122` |
| Forzado y bloqueo de la casilla | `GA_IN.APT:53645-53656` |
| Tasa anclada al producto | `GA_IN.APT:53640` |
| Cálculo del monto de ISV (sin restar descuento %) | `GA_IN.APT:52110-52117` |
| Fórmula del costo promedio | `GA_IN.APT:1152-1160` |
| Tipo de transacción de compras `'COM'` | `GA_IN.APT:54262` |
