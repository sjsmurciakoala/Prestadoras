# Manual de usuario — Carga inicial de existencias (Almacén)

**Módulo**: Almacén → Compras → Carga inicial
**Ruta**: `/almacen/carga-inicial`
**Fecha del documento**: 2026-07-31
**Dirigido a**: personal de almacén y contabilidad que va a operar el corte por primera vez

> Este manual describe **qué hace la pantalla y cómo se opera**. El diseño técnico está en
> [docs/plans/2026-07-29-carga-inicial-existencias-kardex-design.md](../plans/2026-07-29-carga-inicial-existencias-kardex-design.md)
> y el guion de ejecución del corte real en
> [docs/plans/2026-07-31-fase8-ejecucion-corte-inventario.md](../plans/2026-07-31-fase8-ejecucion-corte-inventario.md).

---

## 1. Para qué sirve

Es la pantalla que le dice al sistema **cuánto inventario tiene la empresa el día que arranca, y
cuánto vale**. Es el punto cero del almacén.

La analogía: el kardex funciona como un libro de banco. Cada compra suma, cada descargo resta, y
el saldo sale de sumar el libro. Pero al migrar desde SIMAFI la mercadería **ya está físicamente
en la bodega** y el libro nuevo está en blanco. La carga inicial escribe el asiento de apertura —
*"el 31 de julio, en la bodega 01, había 500 tornillos a L.12.50 cada uno"* — y a partir de ahí
todo movimiento futuro se apoya en esa base.

Sin este paso el kardex arranca en cero y no cuadra con la bodega física. Por eso se hace **una
sola vez**, y por eso la pantalla obliga a simular antes de ejecutar.

**Concepto clave — el "par":** todo en esta pantalla se cuenta por **par = artículo + bodega**. El
mismo tornillo en dos bodegas son dos pares y se abren por separado.

---

## 2. Anatomía de la pantalla

Se lee de arriba hacia abajo, en cuatro bloques.

### 2.1 Los seis contadores (arriba)

Se llenan solos al entrar y resumen el ensayo (*qué pasaría si ejecutara ahora*):

| Contador | Qué significa |
|---|---|
| **Pares pendientes** | Combinaciones artículo–bodega con existencia y sin apertura todavía |
| **Posteables** | Los que están listos: tienen existencia y tienen costo |
| **Sin costo** | Hay mercadería pero el artículo vale 0 → hay que teclear el costo |
| **Negativas** | La existencia está en negativo → hay que sanearla antes |
| **Valor a sembrar** | Dinero total que entrará al inventario (solo de los posteables) |
| **Artículos afectados** | Cuántos artículos distintos se tocan |

A la derecha del título, un indicador muestra el estado del corte: 🔓 **Corte abierto** o
🔒 **Corte cerrado**, y la fecha de corte si ya se ejecutó alguna vez.

### 2.2 La barra de controles

- **Bodega** — filtro. En un almacén grande lo normal es cortar **una bodega a la vez**.
  "Todas las bodegas" muestra el universo completo.
- **Fecha de corte** *(obligatoria)* — es la fecha contable de **todos** los asientos del lote.
  Se persiste al ejecutar y la pantalla la vuelve a mostrar en visitas siguientes.
- **Tamaño de lote** — cuántas filas procesa cada corrida (200 por defecto, máximo 5000).
- **Simular** / **Ejecutar corte** / **Cerrar apertura** — los tres botones del flujo.

### 2.3 La tabla "Pares sin costo"

Solo aparece si hay filas en esa condición. Es donde se teclea el costo a mano, con su botón
**"Postear costos capturados (N)"**.

### 2.4 La grilla del universo

El detalle línea por línea: clase, código, artículo, bodega, existencia, costo y valor. Tiene
filtro por clase, selector de columnas y recuerda su configuración entre sesiones.

---

## 3. Las cinco clases

Cada fila viene clasificada y el color indica si se puede seguir:

| Clase | Color | Significado | ¿Bloquea el cierre? |
|---|---|---|---|
| **Posteable** | 🟢 verde | Listo: tiene existencia y costo. Entra solo al ejecutar. | No |
| **Sin costo** | 🟡 amarillo | Hay mercadería pero el artículo tiene valor unitario 0. | **Sí** |
| **Negativa** | 🔴 rojo | La existencia está en menos (error heredado). | **Sí** |
| **Descontinuado** | ⚪ gris | Artículo dado de baja que conserva existencia. | No |
| **Bodega inactiva** | ⚪ gris | Ubicación deshabilitada que conserva existencia. | No |

Dos criterios deliberados:

- Las dos últimas clases **se muestran a propósito**: existencia sin respaldo es existencia sin
  respaldo, y hay que verla para decidir qué hacer con ella.
- Si una fila está negativa **y** sin costo, se reporta como **Negativa**. Lo negativo es el
  problema que se resuelve primero; el costo se decide durante el saneo.

---

## 4. Flujo de trabajo, paso a paso

### Paso 1 — Filtrar por bodega
Revise y cierre bodega por bodega. El filtro afecta la simulación, la grilla y la ejecución.

### Paso 2 — Fijar la fecha de corte
Es la fecha del inventario físico / del cierre del sistema viejo, no necesariamente la de hoy.

> ⚠️ **Dato duro medido en el espejo de la base:** el histórico de SIMAFI en el kardex llega
> hasta el **2025-11-19**. La fecha de corte debe ser **igual o posterior** a esa, o el punto
> cero quedaría antes de movimientos que ya existen.

### Paso 3 — Simular
Es un ensayo: **no escribe absolutamente nada**. Refresca contadores y grilla. Puede repetirlo
las veces que quiera sin costo alguno.

### Paso 4 — Resolver lo amarillo y lo rojo
- **Sin costo** → teclee el costo en la tabla de captura y presione *Postear costos capturados*.
  Ese costo entra **directo al asiento**; no se guarda en ninguna tabla intermedia — el asiento
  es el registro. Si recarga la pantalla, lo ya tecleado no se pierde.
- **Negativas** → salga a hacer un ajuste de inventario (ver §6) y vuelva.

### Paso 5 — Ejecutar corte
Una ventana de confirmación indica cuántos asientos, con qué fecha y por cuánto dinero. Al
aceptar, el sistema procesa hasta *tamaño de lote* filas e informa cuántas quedaron posteadas y
cuántas omitidas, **con el motivo de cada omisión**.

Repetir el botón **no duplica nada**: cada apertura tiene una huella única y el lote es
reanudable. Si una fila se omitió por concurrencia, la siguiente corrida la toma.

### Paso 6 — Cerrar apertura
El candado final. El servidor **no deja cerrar** si queda algo pendiente en **cualquier** bodega:
ni un par sin costo, ni uno negativo, ni uno posteable sin postear. El gate es global a
propósito: no se cierra el corte de la empresa porque una bodega esté lista.

---

## 5. Las tres reglas que hay que entender antes de tocar el botón

**1. El kardex no se edita ni se borra.**
Un asiento mal costeado no se corrige: se **revierte** y se vuelve a abrir. Por eso la pantalla
obliga a simular primero y pide respaldo de la base en la confirmación. La ventaja es que nadie
puede alterar el histórico por debajo; el precio es que hay que teclear con cuidado.

**2. Cerrar no cierra el almacén para siempre.**
El candado aplica solo a los pares **preexistentes** (los que traían existencia del sistema
viejo). Un artículo dado de alta después **abre normalmente**, con fecha de hoy, desde el maestro
de artículos o desde la pestaña de ubicaciones, sin pasar por esta pantalla.

**3. Los permisos están partidos a propósito.**

| Acción | Permiso requerido |
|---|---|
| Consultar, simular, ejecutar, capturar costos | Inventario → `carga_inicial` |
| **Cerrar apertura** | **Configuración** (módulo, sin recurso) |
| **Reabrir un par** | **Configuración** (módulo, sin recurso) |

No es un capricho: `ModuleAuthorize` hace *fallback* al permiso de módulo, así que un sub-recurso
de Inventario sería un superconjunto de `module.inventario.*` y no restringiría a nadie.
Valorizar y sellar todo el inventario no puede quedar al alcance de cualquier digitador de bodega.

---

## 6. Existencias negativas

### Qué significan
Que el kardex registró **más salidas que entradas**: el sistema cree que despachó 8 unidades de
algo que tenía 6. Es imposible físicamente, así que siempre es un dato malo heredado.

### Qué hace la pantalla
1. Marca la fila con etiqueta 🔴 **Negativa** y muestra la existencia en rojo.
2. La suma al contador *Negativas*.
3. **La excluye del lote**: *Ejecutar corte* solo procesa las filas posteables. Se puede ejecutar
   el corte con negativas pendientes sin ningún problema.
4. **Bloquea el cierre**, en toda la empresa.

### Por qué no se puede abrir "tal cual"
Aunque se intentara forzar, el motor la rechaza:

> *"La apertura por reconciliación exige que la ubicación ya tenga existencia positiva."*

El asiento de carga inicial **describe** el saldo existente y le siembra un costo. Sembrar
"−6 unidades a L.12.00" declararía un inventario de valor negativo y, como el kardex no se puede
editar después, quedaría fijo para siempre corrompiendo el costo promedio de la primera compra
que entre.

### Cómo se sanea
**Con un ajuste de inventario de ENTRADA que la lleve a cero.** Nunca con un `UPDATE` a la base.

Ruta: **Artículos → (el artículo) → pestaña Existencias → "Registrar ajuste"**

- **Clase**: ENTRADA
- **Cantidad**: exactamente la que la deja en 0 (si está en −6, ponga 6)
- **Costo unitario**: obligatorio y **mayor que cero** — el motor no acepta ninguna entrada a
  costo 0, porque corrompería el promedio ponderado
- **Motivo**: obligatorio, queda en el kardex como auditoría

Al llegar a 0 la fila **desaparece del universo del corte** (la pantalla solo lista pares con
existencia distinta de cero) y deja de bloquear el cierre.

> ⚠️ **Lleve la fila exactamente a 0, no a positivo.** Si de −6 hace una entrada de 10 para
> quedar en 4, el costo promedio se pondera contra un saldo negativo y sale distorsionado. Si
> necesita dejar existencia, hágalo en dos pasos: primero el saneo a 0, después la apertura o el
> ajuste que corresponda.

> Si la ubicación está deshabilitada, hay que reactivarla antes: el ajuste rechaza ubicaciones
> inactivas.

### ¿Puede volver a pasar después del corte?
No por la vía normal. El motor rechaza cualquier salida que dejaría el saldo bajo cero:

> *"El ajuste dejaría la existencia en negativo (−2)."*

Las negativas visibles hoy son cicatrices del sistema anterior, no algo que el libro nuevo pueda
producir.

### Caso real del cliente

Medición del ensayo sobre el **espejo** de la base al **2026-07-31**: 3 filas negativas de 244
pares, las tres en la bodega PRIN, 10 unidades en total.

| Código | Descripción | Existencia | `valor_unitario` |
|---|---|---:|---:|
| `0147` | TAPÓN DE COPA 3" PVC POTABLE | −6.00 | 0.0000 |
| `5039` | CINTA EPSON 2190 SO15335 | −2.00 | **−317.5650** |
| `0167` | UNIÓN PVC POTABLE DE 6" | −2.00 | 360.0000 |

Para `0167` sirve el costo del propio artículo. Para `0147` y `5039` hay que teclear un costo
real. Además, conviene **corregir el `valor_unitario` de `5039`** en el maestro de artículos: un
costo negativo no es válido en ningún flujo posterior.

> Los números son del espejo; el servidor de producción puede haberse movido desde el respaldo.
> Vuelva a simular contra el servidor antes de operar.

---

## 7. Pares sin costo

El artículo tiene `valor_unitario = 0`, así que no hay con qué valorizar la apertura. El motor
**no postea nada a costo cero**: una apertura a 0 corrompería el promedio ponderado de la primera
compra que entre, y no habría forma de arreglarlo después.

Se resuelve tecleando el costo en la tabla de captura de la propia pantalla. El costo capturado
**no se guarda en ninguna tabla intermedia**: viaja directo al asiento, que queda marcado con la
observación *"Carga inicial (costo capturado a mano)"*.

---

## 8. Si se equivoca

| Situación | Qué hacer |
|---|---|
| Costeó mal una apertura y **no hay movimientos posteriores** | Acción **Reabrir** (permiso de Configuración): revierte la apertura y crea una nueva con el costo correcto, atómicamente y con motivo obligatorio. Funciona **aunque el corte esté cerrado** — es la única vía de corrección, por eso no se bloquea. |
| Ya hay compras o descargos **después** de la apertura | El sistema **no** deja reabrir (dejaría colgando todo lo posterior). La corrección es un **ajuste de inventario de clase VALOR**. |
| El lote entero salió mal | El kardex no se puede editar: el único camino de vuelta es **restaurar el respaldo**. De ahí la advertencia de la pantalla. |

Cada reapertura queda registrada en el kardex con su motivo.

---

## 9. Preguntas frecuentes

**¿Puedo simular sin miedo?**
Sí. La simulación no escribe absolutamente nada.

**¿Y si le doy dos veces a Ejecutar?**
No se duplica. Cada apertura se identifica con una huella única derivada del par; un segundo
intento devuelve lo ya asentado sin escribir.

**¿Tengo que hacer todo el corte de un jalón?**
No. Se puede ejecutar por bodega y por lotes, e ir avanzando. Lo único que exige tenerlo todo
resuelto es el **cierre**.

**¿Qué pasa con los artículos descontinuados o las bodegas inactivas?**
Se listan pero no bloquean nada. Decida si la existencia es real (y la abre) o si hay que
sanearla con un ajuste.

**¿El costo de apertura lleva ISV?**
No. Por decisión del contador (2026-07-30), `valor_unitario` no incluye ISV y la configuración
queda con `costo_apertura_incluye_isv = false`.

---

## 10. Referencias técnicas

| Pieza | Archivo |
|---|---|
| Pantalla | [apc.Client/Pages/Almacen/CargaInicialInventario.razor](../../apc.Client/Pages/Almacen/CargaInicialInventario.razor) |
| Cliente HTTP | [apc.Client/Services/Almacen/CargaInicialClient.cs](../../apc.Client/Services/Almacen/CargaInicialClient.cs) |
| Controlador | [apc/Controllers/Almacen/CargaInicialController.cs](../../apc/Controllers/Almacen/CargaInicialController.cs) |
| Servicio de dominio | [SIAD.Services/Almacen/CargaInicialInventarioService.cs](../../SIAD.Services/Almacen/CargaInicialInventarioService.cs) |
| Motor de posteo (validaciones y cálculo) | [SIAD.Services/Almacen/InventarioPostingService.cs](../../SIAD.Services/Almacen/InventarioPostingService.cs) |
| Ajustes de inventario | [SIAD.Services/Almacen/AjusteInventarioService.cs](../../SIAD.Services/Almacen/AjusteInventarioService.cs) |
| DTOs y clases del universo | [SIAD.Core/DTOs/Almacen/CargaInicialDtos.cs](../../SIAD.Core/DTOs/Almacen/CargaInicialDtos.cs) |
| Pruebas | [SIAD.Tests/Almacen/CargaInicialTests.cs](../../SIAD.Tests/Almacen/CargaInicialTests.cs) |

**Tablas involucradas**: `alm_articulo_bodega` (existencia por par, fuente de verdad),
`alm_kardex` (los asientos, inmutable), `alm_config_inventario` (política del corte:
`base_costo_apertura`, `costo_apertura_incluye_isv`, `fecha_corte_apertura`, `apertura_cerrada`),
`alm_ajuste_inventario` (documentos de saneo).

**Estado de implementación al 2026-07-31**: la mecánica está implementada y probada contra el
espejo (suite de Almacén 144/144). El corte real **todavía no se ha ejecutado** — ese es el paso
que la pantalla está esperando, con su guion en
[docs/plans/2026-07-31-fase8-ejecucion-corte-inventario.md](../plans/2026-07-31-fase8-ejecucion-corte-inventario.md).
