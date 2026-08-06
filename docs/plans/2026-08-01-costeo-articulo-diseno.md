# Diseño — Costeo de artículos en Almacén (costo anterior / actual / promedio)

Fecha: 2026-08-01
Estado: **propuesta**, sin implementar. Nada de esto está aplicado a ninguna base de datos.

---

## 1. Respuesta corta

Son tres lecturas del mismo libro, no tres campos que alguien teclea:

- **Costo promedio (WAC)** — `valor_inventario / existencia` del par (artículo, bodega). Es el **único** costo
  con efecto contable: con él sale toda la mercadería y con él vale el inventario.
  Vive en `alm_articulo_bodega` (`costo_promedio` YA EXISTE, pasa a derivarse de `valor_inventario` NUEVA).
- **Costo actual (último costo de entrada)** — a cuánto entró la última vez. Referencia comercial, no valoriza.
  Vive en `alm_articulo_bodega.ultimo_costo` (YA EXISTE) + su procedencia (`ultimo_costo_kardex_id` NUEVA).
- **Costo anterior** — el promedio vigente justo ANTES del asiento. Es atributo **del movimiento**, no del
  artículo: vive en `alm_kardex.costo_promedio_anterior` (NUEVA).

Ojo con el vocabulario: en Centura `COSTO_ACTUAL` **era** el promedio (`SALDO_MONETARIO/CANTIDAD_STOCK`,
`GA_IN.APT:1155-1161`). Con la definición de arriba los tres son distintos y ninguno es editable.

---

## 2. Modelo de datos

### `alm_articulo_bodega` — fuente de verdad del costeo (una fila por par)

| Columna | Tipo | Estado | Para qué sirve |
|---|---|---|---|
| `existencia` | `NUMERIC(15,2)` | **EXISTE** | Unidades del par |
| `costo_promedio` | `NUMERIC(12,4)` | **EXISTE** | WAC vigente. Pasa de estado independiente a **derivado** de `valor_inventario` |
| `ultimo_costo` | `NUMERIC(12,4)` | **EXISTE** | Costo de la última entrada con costo propio |
| `valor_inventario` | `NUMERIC(17,4) NOT NULL DEFAULT 0` | **NUEVO** | Saldo monetario del par. Estado **primario**: magnitud aditiva, misma precisión que `alm_kardex.total/debe/haber` (`SiadDbContext.Almacen.cs:205-207`) |
| `ultimo_costo_kardex_id` | `INT NULL` | **NUEVO** | Asiento que fijó `ultimo_costo`: procedencia + restitución determinista en la reversa |
| `ultimo_kardex_id` | `INT NULL` | **NUEVO** | Último asiento aplicado al par: detecta escrituras por SQL directo y da el "costo anterior" del par vía join |
| `existencia_comprometida` / `existencia_transito` | `NUMERIC(15,2)` | **EXISTE, sin escritor** | Reserva y tránsito. Fuera de alcance de esta entrega |

### `alm_kardex` — libro inmutable (`trg_alm_kardex_inmutable`, SQLSTATE K0001)

| Columna | Tipo | Estado | Para qué sirve |
|---|---|---|---|
| `valor_unitario` | `NUMERIC(14,4)` | **EXISTE** | Costo del asiento |
| `total` / `debe` / `haber` | `NUMERIC(17,4)` | **EXISTE** | Importe del movimiento |
| `existencia_resultante` | `NUMERIC(15,2) NULL` | **EXISTE** | Q después del asiento |
| `costo_promedio_resultante` | `NUMERIC(12,4) NULL` | **EXISTE** | C después del asiento |
| `existencia_anterior` | `NUMERIC(15,2) NULL` | **NUEVO** | Q antes |
| `valor_anterior` | `NUMERIC(17,4) NULL` | **NUEVO** | V antes |
| `valor_resultante` | `NUMERIC(17,4) NULL` | **NUEVO** | V después. Es el dato que hace el libro reconstruible |
| `costo_promedio_anterior` | `NUMERIC(12,4) NULL` | **NUEVO** | **El "costo anterior"** |
| `movimiento_tipo` | `VARCHAR(30) NULL` | **NUEVO** | Nombre de `TipoMovimientoInventario`. Sin esto la reversa no distingue `CargaInicialNueva` de `CargaInicialReconciliacion` (ver §4) |
| `costo_base_sin_isv` | `NUMERIC(14,4) NULL` | **NUEVO** | Costo del asiento sin ISV |
| `isv_capitalizado` | `NUMERIC(14,4) NULL` | **NUEVO** | **Monto** de ISV metido al costo (no un flag): es lo único que permitirá cuantificar el día que se pase de COSTO a FISCAL |

Todas nacen `NULL` para el histórico SIMAFI (`uuid IS NULL`): `ADD COLUMN` es DDL y pasa el trigger, un backfill
**no**. Se protegen con `CHECK ... NOT VALID` (exime las filas viejas, sí valida los INSERT nuevos).

### `alm_articulo` — consolidado, solo rollup

| Columna | Tipo | Estado | Para qué sirve |
|---|---|---|---|
| `valor_inventario` | `NUMERIC(17,4) NOT NULL DEFAULT 0` | **NUEVO** | Σ `valor_inventario` de bodegas ACTIVAS. Lo escribe `ArticuloRollupService` |
| `existencia` / `existencia_minima` / `cantidad` | `NUMERIC` | **EXISTE** | Rollup ya implementado (`ArticuloRollupService.cs:57-63`) |
| `valor_unitario` | `NUMERIC(12,4)` | **EXISTE — se congela** | Semilla legacy SIMAFI del corte (`CargaInicialInventarioService.cs:392,436`). Deja de alimentar el KPI y la columna del maestro |
| `cuenta_contable` | `VARCHAR(20)` | **EXISTE — deprecada** | Ver decisión 3 |

**No se crea** `alm_articulo.costo_promedio`: el consolidado se **deriva** en proyección
(`valor_inventario / existencia`). Se almacena lo aditivo, se deriva el cociente.

**No se crea** `costo_mas_alto` (el legacy lo calculaba mal: lo sembraba leyendo `COSTO_ULTIMO`,
`GA_IN.APT:1148-1154`). Si se pide:
`SELECT MAX(valor_unitario) FROM alm_kardex WHERE uuid IS NOT NULL AND ingresos>0 AND articulo_id=? AND bodega_id=?`.

**Prerrequisito verificado:** `Database/2026-07-14_alm_fk_compuestas_tenant.sql:101-105` crea la clave alterna
`(company_id, id)` **solo** en `alm_bodega` y `alm_articulo`. `alm_kardex` **no la tiene**. Para que
`ultimo_costo_kardex_id` / `ultimo_kardex_id` lleven FK compuesta tenant-safe hay que crear antes
`uq_alm_kardex_company_id UNIQUE (company_id, id)` (índice nuevo sobre ~47.215 filas, segundos; el
`ADD CONSTRAINT` no dispara el trigger de inmutabilidad, por la misma razón documentada en ese script,
líneas 69-76). **Nunca** `REFERENCES alm_kardex(id)` a secas.

---

## 3. Reglas de actualización

### Fórmula canónica (promedio ponderado móvil por par)

Con `Q` = `existencia`, `V` = `valor_inventario`, `C` = `costo_promedio` = `round(V/Q, 4)`:

```
ENTRADA de q unidades a costo propio c:
    valorMovido = round(q * c, 4)
    Q' = Q + q
    V' = V + valorMovido
    C' = (Q' > 0) ? round(V' / Q', 4) : c
```

Es algebraicamente lo mismo que hoy hace `InventarioPostingService.Calcular:439-443`, con una diferencia
decisiva: hoy el numerador se **reconstruye** en cada movimiento desde un promedio ya redondeado a 4 decimales;
con `V` el numerador se **arrastra** y no fuga centavos.

**Redondeo:** `costoPosteo` se redondea a 4 decimales **en el origen** (`RecepcionCompraService.cs:636-638`, hoy
no se redondea y Postgres lo trunca al insertar mientras el promedio se pondera con el valor sin redondear). Así
lo almacenado es lo usado.

### Tabla de movimientos

| Tipo (`TipoMovimientoInventario`) | Existencia | `valor_inventario` | Costo promedio | Costo actual (`ultimo_costo`) | Costo anterior (asiento) |
|---|---|---|---|---|---|
| `CargaInicialNueva` **EXISTE** | `Q' = q` | `V' = q·c` | `= c` | `= c` | `0` |
| `CargaInicialReconciliacion` **EXISTE** | sin cambio | `V' = Q·c` | `= c` | `= c` | `0` |
| `Compra` **EXISTE** | `+q` | `+q·c` | ponderado | `= c` | promedio previo |
| `AjustePositivo` **EXISTE** | `+q` | `+q·c` | ponderado | `= c` | promedio previo |
| `AjusteNegativo` **EXISTE** | `−q` | `−q·C` (barrido) | **sin cambio** | sin cambio | sin cambio |
| `SalidaDescargo` **EXISTE** | `−q` | `−q·C` (barrido) | **sin cambio** | sin cambio | sin cambio |
| `AjusteValor` **EXISTE (corregido)** | sin cambio | `V' = Q·c` | `= c` | **sin cambio** | promedio previo |
| `Reversa` **EXISTE (corregida)** | según original | `∓ total del asiento original` | derivado | **restituido** vía `ultimo_costo_kardex_id` | promedio previo |
| `TrasladoSalida` / `TrasladoEntrada` | **NUEVOS** | valor que sale = valor que entra | derivado | **no toca** | promedio previo |
| `DevolucionConsumo` | **NUEVO** | `+` al costo del descargo original | ponderado | **no toca** | promedio previo |

**`ultimo_costo` solo lo mueven las entradas de mercadería con costo propio** (Compra, AjustePositivo,
CargaInicial). Hoy `InventarioPostingService.cs:96-99` lo pisa en toda entrada con `ingresos > 0`, incluida la
reversa de un descargo (`Calcular:486`), donde el "costo" es el promedio con que salió — eso contradice el
COMMENT de la columna y el XML doc de `alm_articulo_bodega.cs:38`.

### Tres correcciones obligatorias del motor (defectos verificados en el código actual)

1. **`AjusteValor` no asienta dinero.** `InventarioPostingService.cs:143-145` graba
   `total = cantidadAsiento * costoAsiento` con `cantidad` obligada a 0 (`ValidarAsync:321-324`) y
   `Calcular:456-458` devuelve `ingresos=0, salidas=0`. Una revaluación de L.100.000 mueve `costo_promedio` y
   deja **cero** rastro en el libro inmutable. Corrección: `Δ = round(Q·c,4) − V`; `total = |Δ|`;
   `debe = Δ>0?Δ:0`; `haber = Δ<0?−Δ:0`. Es decir, `debe/haber` pasan a depender del **signo del valor**, no de
   la dirección de las unidades.
2. **`AjusteNegativo` no exige `costo_promedio > 0`.** `ValidarAsync:311-318` solo comprueba que no quede
   negativo, a diferencia de `SalidaDescargo:337-344`. Hoy un ajuste de salida sobre un par sin costo graba un
   asiento a valor 0 en un libro que no admite UPDATE. Se le agrega la misma guarda.
3. **La reversa no restituye `ultimo_costo`.** Al revertir una compra el promedio vuelve pero `ultimo_costo` se
   queda con el costo del documento anulado. Con `ultimo_costo_kardex_id` la restitución es determinista.

Y una corrección documental: `SIAD.Services/Almacen/IRecepcionCompraService.cs:50-54` sigue afirmando que el
promedio no vuelve atrás en una reversa; el motor sí des-pondera desde el 2026-07-31.

---

## 4. Casos borde

| Caso | Regla |
|---|---|
| **Stock 0 al entrar** | `Q'= q > 0` ⇒ `C' = c`. Nunca hay división por cero (ya cubierto, `Calcular:440`) |
| **Stock queda en 0 tras salir** | **Regla de barrido:** la última salida se lleva **todo** el valor restante (`V' = 0`, `valorMovido = V`). Sin esto quedan existencia 0 con valor 0,0003 y ningún reporte cuadra jamás. `costo_promedio` se **conserva** como memoria del costo (0 significaría "vale nada"), la UI lo pinta atenuado como "sin existencia" |
| **Stock negativo (nuevo)** | Prohibido generarlo. Se conservan las tres guardas actuales (`:313-317`, `:345-349`, `:371-378`) |
| **Stock negativo heredado** (0147, 5039, 0167 — 10 unidades) | **El motor rechaza todo movimiento sobre un par con `Q < 0`**, con mensaje accionable. Nunca se calcula `V/Q` con `Q` negativo: da un costo positivo a partir de un valor negativo. Se sanean **una sola vez antes del corte** (decisión 1) |
| **Costo 0 en una entrada** | Rechazo (`ExigirCostoPositivo:393-403`, YA EXISTE). Se extiende a la salida por la vía del `costo_promedio` del par |
| **Costo NULL** | No existe: las columnas son `NOT NULL DEFAULT 0`; el 0 significa "sin costo conocido" y bloquea toda salida |
| **`V < 0` con `Q > 0`** (solo alcanzable por reversa o devolución) | Se **rechaza** el posteo con mensaje accionable. Hoy `Calcular:495-497` conserva el promedio vigente, es decir inventa valor en silencio |
| **Reversa** | Devuelve exactamente el importe del asiento original, leído del propio asiento. Se conservan las prohibiciones de revertir una reversa (`:365-370`) y de dejar existencia negativa (`:371-378`) |
| **Reversa de una reconciliación — trampa real** | `CargaInicialReconciliacion` escribe `ingresos = existencia` sin mover la existencia (`Calcular:427-430`), y la reversa cae en `existencia = fila.existencia − cantidad` (`:489`) ⇒ **vacía la bodega**. Hoy es inocuo solo porque `CargaInicialInventarioService.ReabrirAsync:292-320` postea de inmediato una `CargaInicialNueva` en la misma transacción. Con `alm_kardex.movimiento_tipo` (NUEVA) la reversa sabe qué revertir y deja de depender de heurísticas sobre `documento_tipo` |
| **Traslado** (NUEVO) | Dos asientos en **una** transacción. El costo **viaja con la mercadería**: la entrada en destino vale exactamente lo que valió la salida en origen. `Δvalor` del sistema = 0. `TipoDocumentoInventario.Traslado` y `alm_kardex.bodega_destino_id` YA EXISTEN |
| **Devolución a proveedor / NC de compra** (NUEVO) | Es una **reversa parcial** de la línea de recepción: sale al costo con que **entró ese documento**, proporcional a lo devuelto. No al promedio |
| **Devolución de material no consumido** (NUEVO) | Entra al costo con que salió (`alm_kardex.valor_unitario` del descargo original). Sin trazabilidad, entra al promedio vigente — que es **neutro** por construcción |
| **Carga inicial** | Base = `alm_config_inventario.base_costo_apertura` (`VALOR_UNITARIO` \| `MANUAL`), `costo_apertura_incluye_isv = false`. YA EXISTE |
| **Corrección retroactiva** | El kardex no se corrige, se revierte. Apertura mal costeada sin movimientos posteriores ⇒ `ReabrirAsync` (reversa + nueva apertura, atómica). Con movimientos posteriores ⇒ `AjusteValor` (que ahora sí deja importe en el libro). **Un costo mal tecleado no recupera las salidas ya valorizadas al promedio contaminado**: por eso la validación de captura de la decisión 2 |

---

## 5. ISV: cuándo entra al costo

La política **no cambia** — ya está implementada y funciona:

- **Capa 1 (por artículo):** `alm_tipo_articulo.impuesto_tasa_id` → `cfg_impuesto_tasa` **vigente a la fecha del
  documento** (`RecepcionCompraService.cs:702-733`). Artículo sin tipo o tipo sin tasa ⇒ 0%.
- **Capa 2 (por empresa):** `cfg_compra_isv.tratamiento` `'COSTO' | 'FISCAL'`
  (`Database/2026-07-30_cfg_compra_isv.sql:36-45`), leída en `ResolverSiCapitalizaIsvAsync:740-747`.
- **Override por factura:** `alm_compra_hdr.detallar_isv` (detallar = **no** capitaliza). Es el port directo de
  la casilla `cb3` del legacy.
- **Cómo entra:** `costoPosteo = CostoUnitario + (isvRenglon / Cantidad)` cuando capitaliza (`:636-638`). Se
  prorratea **por unidad**, que es lo que pondera el promedio.
- **El motor no decide nada de ISV:** recibe un costo y pondera.

**Lo que se agrega:** `alm_kardex.costo_base_sin_isv` e `isv_capitalizado` (NUEVAS, **montos**). Hoy la decisión
se pierde tras el posteo y el riesgo declarado en
`docs/plans/2026-07-29-configuracion-isv-compras-design.md:265,485` (el promedio mezclando dos bases fiscales) es
**indetectable**. Con el monto por asiento, una consulta responde "cuánto ISV quedó capitalizado en las
existencias vivas" el día que se migre de COSTO a FISCAL.

**Lo que NO entra al costo hoy:** flete (`flete_seguro`), otros gastos y el descuento global de cabecera solo
afectan `cabecera.total` (`RecepcionCompraService.cs:684-688`). Ver decisión 2.

---

## 6. Concurrencia y descuadre

**Lo que ya funciona y no se toca:**

1. `SELECT * FROM alm_articulo_bodega WHERE company_id = {companyId} AND id = {id} FOR UPDATE` con el
   `company_id` **dentro del SQL crudo** (`InventarioPostingService.cs:232-243`) — porque EF compone su filtro de
   tenant por encima y el candado se tomaría antes de filtrar por empresa.
2. `uuid` v5 determinista + índice único `(company_id, uuid)`; el corte por idempotencia ocurre **antes** del
   candado (`:61-78`), así un reintento no bloquea.
3. Todo el posteo en una transacción con un solo `SaveChanges`, aplicando por **asignación** (`:94-99`), nunca
   `+=`.

Dos recepciones simultáneas del mismo par: la segunda espera al COMMIT de la primera y pondera sobre el `V` ya
actualizado. El índice único **no** las detiene (sus `uuid` difieren): el candado es lo único que las serializa.

**Lo que se agrega:**

- **Jerarquía de bloqueo por contrato**, documentada en `IInventarioPostingService`: siempre
  `alm_articulo_bodega` **antes** que `alm_articulo` (el rollup corre después, `:160-161`). Cualquier camino
  nuevo que invierta ese orden introduce deadlock.
- **Orden ascendente de `alm_articulo_bodega.id`** cuando un movimiento toca dos filas (traslado), para que A→B y
  B→A simultáneos no hagan deadlock.
- `SET LOCAL lock_timeout` en el posteo, para que un candado colgado falle con mensaje en vez de dejar la UI
  esperando.
- **Rollup en un solo statement.** Hoy `ArticuloRollupService.RecomputeAsync:44-52` hace un `GroupBy` fuera de
  candado y recién después el `ExecuteUpdateAsync:57-63`: dos posteos concurrentes en bodegas distintas del mismo
  artículo pueden dejar el consolidado atrasado. Se cambia a un único `UPDATE ... FROM (SELECT sum ...)`. Se
  mantiene fuera de `SaveChanges` por las dos razones ya documentadas ahí (bitácora de maestros y token `xmin`).

**Cómo se detecta el descuadre.** En Postgres **no existe ninguna función, SP ni trigger que calcule o proteja
costos**: los dos únicos triggers del módulo son guardianes negativos (`trg_alm_kardex_inmutable` K0001 y
`trg_alm_*_blindaje` K0002). Esa regla se mantiene — el costeo vive en C# — y se compensa con auditoría de **solo
lectura**:

- `fn_alm_replay_par(company, articulo_id, bodega_id)` (NUEVA, solo lectura): replayea el libro nuevo del par y
  devuelve `(existencia, valor, costo_promedio)`.
- `v_alm_costeo_descuadre` (NUEVA): pares con `existencia ≠ 0` y costo 0; `existencia = 0` con valor ≠ 0; caché
  contra replay; pares cuyo promedio mezcla bases de ISV.
- `CosteoIntegridadService` (NUEVO, C#) + endpoint + pantalla, con acción "resincronizar la caché desde el
  kardex" que escribe **solo** `alm_articulo_bodega` y el rollup, **jamás** `alm_kardex`.

**Invariantes** (contrato escrito; se verifican con consulta, no con constraints):

- **I1** `alm_articulo_bodega.valor_inventario` = Σ(`debe` − `haber`) del par desde el asiento de carga inicial
  vigente.
- **I2** `existencia` = Σ(`ingresos` − `salidas`) desde ese mismo asiento (es la regla que ya aplica
  `KardexService.AplicarPuntoDeCorte:185-252`).
- **I3** `existencia = 0` ⇒ `valor_inventario = 0`.
- **I4** `alm_articulo.valor_inventario` = Σ de las bodegas activas.

**NO se crea** un índice único `(articulo, bodega, secuencia)`: derivaría de una caché reconstruible y un solo
desfase dejaría ese par imposible de postear para siempre, sin `UPDATE` posible sobre el libro para arreglarlo.
El `FOR UPDATE` ya serializa; el verificador detecta.

---

## 7. UI: qué se ve y qué se teclea

**Regla transversal: lo único editable a mano es el costo de ENTRADA de un documento. Los tres costos son siempre
de solo lectura, en toda la aplicación y por API.**

### Editable (lista cerrada — ya existe, no cambia)

| Dónde | Archivo |
|---|---|
| Costo de apertura al dar de alta el par | `apc.Client/Pages/Almacen/ArticuloUbicacionesTab.razor:273` |
| Costo del ajuste, clases ENTRADA y VALOR (en SALIDA queda `ReadOnly`) | `ArticuloUbicacionesTab.razor:396` |
| Costo de apertura de los pares SIN_COSTO | `CargaInicialInventario.razor:177` |
| Costo unitario por renglón en O/C y recepción | `OrdenCompraFormPage.razor:229`, `RecepcionCompraFormPage.razor:394` |

### Qué se muestra

- **`KardexArticulo.razor`** — es donde más se gana y **no cuesta base de datos**: `Total`,
  `ExistenciaResultante` y `CostoPromedioResultante` YA viajan en `KardexMovimientoDto` (`:20, :51, :54`) y YA los
  llena `KardexService.cs:105-111`, y la pantalla **no pinta ninguno**. Se agregan, a la derecha de "Valor unit."
  (`:235`): **Costo ant.**, **Costo prom.**, **Valor mov.**, **Saldo valorizado**. En los pre-corte van con
  guion, igual que `Saldo`. KPI nuevos: "Valor de entradas", "Valor de salidas", "Valor del inventario al
  cierre". Hoy las cuatro tarjetas son cantidades. Cada línea debe leerse como una frase: *"venía a 56,3889 →
  entraron 6 a 58,00 → queda a 56,7917, saldo L.13.630,00"*.
- **`ArticuloUbicacionesTab.razor`** — se conservan "Costo prom." y "Último costo" solo lectura
  (`:118, :124, :317, :329`) con su leyenda. Se agregan **Costo anterior** y **Valor**, y el tooltip de
  procedencia del último costo (documento + fecha, vía `ultimo_costo_kardex_id`). El toast del ajuste (`:872`)
  pasa a mostrar los tres.
- **`ArticulosList.razor`** — la columna "Valor inventario" (`:305`) y la tarjeta KPI (`:62`) dejan de calcularse
  como `existencia × valor_unitario` (`ArticulosService.cs:71` y `:213`) y pasan a `alm_articulo.valor_inventario`.
  Columna "Costo promedio" derivada, `Visible=false` por defecto. **Fin de las dos valorizaciones que hoy conviven
  sin conciliar.**
- **`ArticuloForm.razor`** — sigue **sin ningún campo de costo editable**. Se agrega un bloque "Costeo" de solo
  lectura (costo promedio consolidado, valor de inventario, último costo con enlace al asiento, costo anterior).
- **`ArticuloMovimientosPanel.razor`** — hoy no muestra ningún costo (`:86-183`). Se agregan "Valor unit." y
  "Costo prom.".
- **`StockAlertas.razor`** — "Costo reposición" (`:169`) y su KPI (`:45`) dejan de usar
  `alm_articulo.valor_unitario` y pasan a `ultimo_costo` del par, que es literalmente el costo de reposición.
- **Pantalla NUEVA "Valorización de inventario"** — `existencia × costo_promedio` por bodega y por tipo, con corte
  a fecha reconstruido del kardex. Prevista desde `docs/plans/2026-07-14-motor-movimientos-almacen.md:393-397` y
  nunca hecha. Es la pantalla que el contador va a pedir para cuadrar contra el mayor.
- **Pantalla NUEVA "Verificación de costeo"** — consume `v_alm_costeo_descuadre`.
- **Costo sugerido en O/C y recepción** — hoy sale de `alm_articulo_proveedor.costo`
  (`OrdenCompraService.cs:276`), columna congelada en la migración que ninguna pantalla puede escribir. Pasa a
  `ultimo_costo` del par de la bodega de destino, con el promedio como segunda referencia.

**Blindaje de `valor_unitario`:** `ArticulosService.CreateAsync:615` y `UpdateAsync:811` lo persisten desde
`ArticuloEditDto.ValorUnitario` **sin guardia**, aunque el formulario no lo exponga: un POST/PUT directo puede
fijar el costo de cabecera. Se blinda **en el servicio** (se ignora salvo apertura abierta + permiso de
configuración). **No** se quita del DTO: eso rompería el round-trip GET/PUT (`GetByIdAsync:483`) y el campo sigue
siendo la semilla del corte.

---

## 8. Plan de implementación por fases

### Fase A — Sin base de datos, entrega inmediata, riesgo cero

- `apc.Client/Pages/Almacen/KardexArticulo.razor`: pintar `Total` y `CostoPromedioResultante` (ya viajan, ya se
  llenan) + tarjeta "Saldo valorizado".
- `apc.Client/Pages/Almacen/ArticuloMovimientosPanel.razor`: agregar "Valor unit.".
- Corregir `SIAD.Services/Almacen/IRecepcionCompraService.cs:50-54` (doc obsoleta sobre la reversa) y el XML doc
  de `SIAD.Core/Entities/alm_articulo_bodega.cs:38`.

### Fase B — Núcleo del costeo (**antes del corte**; en el SRV no hay asientos con `uuid`, la ventana está abierta)

Scripts nuevos en `Database/`, cada uno por la skill `guardia-estructura-bd` y registrado con
`runbook-despliegue-srv`:

1. `2026-08-02_alm_kardex_valorizacion.sql` — `uq_alm_kardex_company_id UNIQUE (company_id, id)`; `ADD COLUMN`
   `existencia_anterior`, `valor_anterior`, `valor_resultante`, `costo_promedio_anterior`, `movimiento_tipo`,
   `costo_base_sin_isv`, `isv_capitalizado`; `CHECK ck_alm_kardex_valorizacion (uuid IS NULL OR ...) NOT VALID`;
   COMMENT explicando que el histórico SIMAFI queda NULL a propósito y la advertencia de re-ejecutabilidad frente
   a `trg_alm_kardex_inmutable`.
2. `2026-08-02_alm_articulo_bodega_valor.sql` — `valor_inventario`, `ultimo_kardex_id`, `ultimo_costo_kardex_id`
   con **FK compuestas** `(company_id, ...)`; backfill `valor_inventario = ROUND(existencia * costo_promedio, 4)`
   donde `existencia > 0`; `CHECK ck_alm_articulo_bodega_valor_cero NOT VALID`.
3. `2026-08-02_alm_articulo_valor_rollup.sql` — `alm_articulo.valor_inventario`; `CHECK valor_unitario >= 0
   NOT VALID`; COMMENT deprecando `valor_unitario`.

Código:

- Entidades: `SIAD.Core/Entities/alm_kardex.cs`, `alm_articulo_bodega.cs`, `alm_articulo.cs`.
- Mapeo: `SIAD.Data/SiadDbContext.Almacen.cs` — `HasPrecision` junto a los bloques existentes (kardex `:200-219`,
  par `:625-629`, artículo `:139`).
- **Motor** `SIAD.Services/Almacen/InventarioPostingService.cs`: `Calcular` (`:417-505`) pasa a llevar `(Q, V)`;
  bloque de aplicación (`:94-99`) escribe `valor_inventario`, `costo_promedio` derivado, `ultimo_kardex_id` y
  `ultimo_costo`/`ultimo_costo_kardex_id` **solo en entradas con costo propio**; asiento (`:116-155`) estampa los
  snapshots y `debe/haber` **por signo del valor**; guarda de `costo_promedio > 0` en `AjusteNegativo`
  (`:311-318`); rechazo de `Q < 0`; regla de barrido; reversa exacta y restitución de `ultimo_costo`.
- `SIAD.Services/Almacen/RecepcionCompraService.cs`: redondear `costoPosteo` a 4 en el origen (`:636-638`) y pasar
  `costo_base_sin_isv` / `isv_capitalizado` en `MovimientoInventarioDto`.
- Tests `SIAD.Tests/Almacen/InventarioPostingTests.cs`: cadena larga sin fuga de centavos; salida que vacía la
  bodega deja valor 0; `AjusteValor` asienta dinero con el signo correcto; reversa de compra que restituye
  `ultimo_costo`; `AjusteNegativo` sobre par sin costo debe fallar; I1/I2 sobre 20 movimientos. **Revisar las
  aserciones existentes `:136`, `:440-442`, `:633-634`, `:652`**: la regla de barrido rompe
  `total = cantidad × valor_unitario` en el asiento que deja la bodega en 0 (en el kardex manda el **importe**).

### Fase C — Consolidado y maestro

- `ArticuloRollupService.cs:44-63`: un solo `UPDATE ... FROM (SELECT sum ...)` que además escriba
  `valor_inventario`.
- `ArticulosService.cs`: `:71` (KPI) y `:213` (`ValorTotal`) desde el rollup; blindaje de `valor_unitario` en
  `:615` y `:811`; alertas (`:291`) desde `ultimo_costo`.
- `KardexService.cs`: llenar los campos nuevos en la proyección (`:91-113`) y llevar el **saldo valorizado**
  corrido en `AplicarPuntoDeCorte` (`:185-252`). Ampliar `KardexMovimientoDto`.
- UI de las Fases A + §7 completa.

### Fase D — Auditoría

- `2026-08-03_fn_alm_verificar_costeo.sql` (NUEVO): `fn_alm_replay_par` + `v_alm_costeo_descuadre`, **solo
  lectura**.
- `SIAD.Services/Almacen/CosteoIntegridadService.cs` (NUEVO) + controlador + permiso en `PermissionNames` /
  `PermissionEndpointCatalog` + pantalla.

### Fase E — Después del corte cerrado

- `2026-08-04_alm_traslado.sql` (NUEVO): `alm_traslado_hdr` / `alm_traslado`; valores `TrasladoSalida` /
  `TrasladoEntrada`; doble `FOR UPDATE` en orden ascendente de id.
- `DevolucionConsumo` y devolución a proveedor como reversa parcial.
- Servicio de posteo de descargos (hoy `DescargosService` y `RequisicionesService` son **solo consulta**, y
  `SalidaDescargo` únicamente lo ejercitan los tests).
- Cerrar la brecha SQL↔binario: `alm_requisicion_hdr` y `alm_descargo_hdr` existen en
  `Database/2026-08-01_alm_requisicion_descargo.sql` y **no tienen entidad ni DbSet**.

### Prerrequisitos de despliegue (no negociables)

1. **Fase 0(a):** confirmar en el SRV el "grupo B" (`Database/2026-07-30_pendientes_srv.md:72-146`): los seis
   scripts base del kardex, `2026-07-14_alm_documentos_bodega_posteo.sql` y `2026-07-14_cfg_impuestos.sql`. Su
   estado **no consta** y dejan de ser re-ejecutables una vez activo el trigger de inmutabilidad.
2. Pasos 19–26 del runbook, incluido el **paso 24** (mudanza PRIN→01), prerrequisito duro del corte.
3. Recién entonces el corte (Fase 8), que **no es SQL**: se opera desde el portal.

---

## 9. Decisiones pendientes (4)

**1. Fecha de corte (D4) y qué hacer con las 3 existencias negativas (D6).**
Sin la fecha no se ejecuta el corte; sin el saneo se ejecuta pero no se puede **cerrar** (el gate exige cero
negativas). Son 3 pares y 10 unidades: 0147 (−6, `valor_unitario` 0,0000), 5039 (−2, `valor_unitario`
**−317,5650**), 0167 (−2, 360,0000).
**Recomendación:** fecha de corte = último día del mes en que se aplique todo en el SRV (debe ser ≥ 2025-11-19,
último asiento del histórico). Las 3 negativas se sanean con un ajuste de entrada puntual **antes** del corte,
documentado en la observación del asiento; el costo de 5039 y 0147 se toma del último ingreso conocido en el
kardex, no del `valor_unitario`.

**2. ¿Flete, otros gastos y descuento global entran al costo del inventario?**
Hoy no: solo el ISV se capitaliza (`RecepcionCompraService.cs:684-688`). Contablemente el flete y los gastos de
importación **sí** son costo del inventario, pero prorratearlos hace que el costo del kardex deje de coincidir
renglón a renglón con la factura del proveedor.
**Recomendación por defecto:** **no** prorratear ahora, pero dejar el interruptor construido desde el primer día
— `alm_config_inventario.prorratea_gastos_compra BOOLEAN NOT NULL DEFAULT false` (NUEVA) — y **declararlo en la
pantalla de recepción** ("el flete no entra al costo"). Cambiarlo después mezcla bases en el promedio igual que el
ISV. Y agregar en la captura un aviso cuando el costo tecleado se aparte más de un X% del promedio o del último
costo del par: es el control más barato contra el único error irreversible (un dedazo de 5.800,00 por 58,00 entra
sin resistencia y contamina un libro inmutable).

**3. ¿Qué cuenta contable estampa el asiento?**
Hoy el motor copia `alm_articulo.cuenta_contable` (`InventarioPostingService.cs:111-114` y `:148`), columna que
`ArticulosService.cs:807-810` ya declara **deprecada** porque las cuentas se heredan del tipo
(`alm_tipo_articulo` tiene `cuenta_inventario`, `cuenta_costo_ventas`, `cuenta_ventas`, `cuenta_ajustes`,
`cuenta_devoluciones`). Como el kardex es inmutable, **cada asiento que se postee de aquí en adelante congela la
cuenta equivocada para siempre**.
**Recomendación:** cambiarlo **antes del corte** — el asiento estampa `alm_tipo_articulo.cuenta_inventario`, con
fallback a `alm_articulo.cuenta_contable` si el tipo no la tiene. (La partida contable de inventario en sí, D3,
sigue fuera de alcance.)

**4. ¿A qué costo sale una requisición: al promedio del día del despacho o al congelado al aprobar?**
En Centura la salida se valorizaba al promedio **congelado al momento de requisar** (hallazgo C-5,
`docs/centura-flujos/README_requisiciones_descargos.md:308-309`). En SIAD sale al promedio vigente al
**descargar**. Si una requisición se aprueba hoy y se despacha en tres semanas con dos compras de por medio, el
costo del consumo cambia respecto del sistema anterior.
**Recomendación:** mantener el promedio del **momento del descargo** (es lo implementado y lo contablemente
correcto: el material sale al costo que tiene cuando sale) y **declararlo por escrito** en el manual, para que
nadie lo lea como un error de migración.
