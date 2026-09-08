# Módulo Activos Fijos — plan y prototipo del registro

**Fecha:** 2026-09-08
**Estado:** F1 (registro) implementado en local, sin commit. SQL **aplicado y verificado en el mirror
`siad_v3_restore`** el 2026-09-08; el servidor sigue pendiente.
**Base de referencia:** desarrollo de Merendon (`Merendon_portaweb`) + histórico migrado de SIMAFI que ya
vive en esta base (`af_activo_fijo`, script `Database/2026-07-01_alm_almacen_af_activo_fijo.sql`).

---

## 1. De dónde partimos

### 1.1 Lo que ya existe en este repositorio

| Objeto | Qué es | Estado |
|---|---|---|
| `af_activo_fijo` | Maestro de activos migrado de MySQL `bdsimafi.inventario`. Trae depreciación, vida útil, valor en libros y responsable. | Con datos, **sin ninguna pantalla ni servicio** |
| `af_activo_fijo_depreciacion` | Detalle mensual de depreciación, ex `bdsimafi.depreinve` (~44,579 filas en origen). | Con datos, sin uso |
| `con_activo_fijo`, `con_activo_tipo`, `con_deprecacion` | Tablas en inglés creadas por migraciones EF antiguas del módulo contable. | Sin UI, sin servicio, **sin uso real** |

O sea: los datos históricos están cargados desde julio, pero el módulo nunca se construyó.

### 1.2 Lo que aporta el desarrollo de Merendon

Merendon tiene tres modelos de activo fijo conviviendo, y de cada uno se toma algo distinto:

- **`inv_activo` + `inv_grupo_activo`** — el grupo del activo define el tiempo de depreciación y el
  porcentaje residual, y el activo los hereda. Además ubica el activo en una jerarquía
  sucursal → edificio → piso → oficina (cuatro tablas) y lo liga a un centro de costo.
- **`tb_acf_m_activo`** — la ficha más completa: proveedor, responsable, clase, oficina, departamento,
  marca, modelo, serie, matrícula, porcentaje de depreciación, mejoras acumuladas, valor residual, las
  fechas del ciclo (inicio, fin, paro, venta, última depreciación, última mejora), los valores
  derivados (total a depreciar, mensual, diario, al paro, al vender), **cinco cuentas de gasto con
  cinco porcentajes** para repartir el gasto, foto y tasa de cambio.
- **`act_historial_depreciacion`** — bitácora por corrida de depreciación con valor anterior, valor
  actual y usuario.

### 1.3 Qué se toma y qué se deja

| De Merendon | Decisión |
|---|---|
| El grupo/tipo que presta vida útil y % residual | **Se toma**, y se amplía: el tipo también presta las cuentas contables |
| Ubicación en cuatro tablas (sucursal/edificio/piso/oficina) | **Se aplana** a una sola tabla jerárquica por `padre_id` |
| Responsable, proveedor y clase como texto libre | **Se normaliza** contra `th_empleado` y `prv_proveedores` |
| Accesorios y componentes como dos campos de texto | **Se convierte** en tabla hija |
| Foto única en la ficha (`byte[]` o ruta) | **Se pospone** a F5, como galería de documentos |
| Cinco cuentas de gasto con cinco porcentajes | **Se pospone** a F2 y se decide con el contador (ver D3) |
| Tasa de cambio por activo | **Se descarta** por ahora: la operación es en lempiras |
| Bitácora de depreciación | **Se toma** en F2 |

---

## 2. Mejoras propuestas sobre la base

Estas son las once decisiones de diseño que separan el módulo nuevo de lo que hay en Merendon y en
SIMAFI. Las nueve primeras ya están en el prototipo.

1. **El tipo de activo es el centro.** Vida útil, método, porcentaje residual y las cuatro cuentas
   contables viven en `af_tipo_activo` y el activo las hereda. Es el mismo patrón que el repo ya usa
   para el tipo de artículo en almacén. El activo puede sobrescribir cualquiera de ellas.
2. **Estados con catálogo, no números sueltos.** El campo `estado` del histórico es un `SMALLINT` sin
   catálogo, más dos banderas sueltas (`descargado`, `vendido`). Se sustituye por `af_estado_activo`,
   con siete estados que declaran si el activo se deprecia y si ya salió del patrimonio. La pantalla
   muestra siempre el nombre, nunca el número.
3. **Responsable ligado al catálogo de empleados.** Deja de ser texto libre y apunta a `th_empleado`.
4. **Proveedor ligado al catálogo de proveedores.** Igual, contra `prv_proveedores`.
5. **Historial de asignación.** Merendon y SIMAFI solo guardan quién tiene el activo *hoy*. Aquí queda
   la traza completa en `af_activo_asignacion`, con vigencia y una sola fila abierta por activo. Es el
   respaldo para la toma física de inventario y para el descargo de responsabilidad.
6. **Componentes como filas.** Lo que en Merendon son dos campos de texto pasa a ser una tabla, para
   poder buscar por serie del componente y valorarlo por separado.
7. **Código autogenerado por tipo.** `VEH-000045`, con el prefijo que declara cada tipo. Se puede
   escribir a mano si el activo trae código propio, y el histórico de SIMAFI conserva el suyo.
8. **Marca la deuda del histórico.** El listado señala con un aviso los activos migrados a los que
   les falta tipo o estado, y hay un filtro para trabajarlos. El resumen dice cuántos son.
9. **Validación en la base, no en el formulario.** Las reglas (residual menor que la compra, acumulada
   que no supera lo depreciable, depreciación que no empieza antes de la compra, estado que no admite
   depreciación) viven en `sp_af_activo_guardar`, así que valen igual desde la pantalla, desde una
   importación o desde un script.
10. **Póliza de seguro y garantía con vencimiento.** No existían en ninguna de las dos bases y son la
    materia prima de un aviso automático (F5).
11. **Alta del activo desde la compra.** Que registrar la factura de compra de un activo ofrezca
    crearlo, en vez de recapturarlo. Requiere el módulo de compras que ya existe (F4).

---

## 3. Fases

| Fase | Alcance | Estado |
|---|---|---|
| **F1 — Registro** | Catálogos (tipos, ubicaciones, estados, métodos), maestro con ficha por pestañas, asignaciones, componentes, permisos y grupo de menú propio | **Implementada en local** |
| **F2 — Depreciación** | Corrida mensual idempotente por período contable: borrador, revisión, confirmación y partida contable. Bitácora por corrida. Reanudar el histórico de `af_activo_fijo_depreciacion` | Diseñada, sin implementar |
| **F3 — Bajas, ventas y mejoras** | Descargo con pérdida en retiro, venta con ganancia o pérdida, y capitalización de mejoras que recalcula la cuota | Sin diseñar |
| **F4 — Enganche con compras** | Crear el activo desde la factura de compra o la orden de compra, heredando proveedor, factura y valor | Sin diseñar |
| **F5 — Toma física e informes** | Etiquetas con código de barras, hoja de toma física por ubicación o responsable, conciliación, documentos adjuntos, avisos de vencimiento de póliza | Sin diseñar |

---

## 4. Qué entrega la F1

### 4.1 Base de datos

Script único: [`Database/2026-09-08_af_activos_fijos_f1_registro.sql`](../../Database/2026-09-08_af_activos_fijos_f1_registro.sql).
**Aplicado en el mirror el 2026-09-08**, con respaldo previo. Pendiente en el servidor.

Tablas nuevas:

- `af_metodo_depreciacion` — catálogo de sistema, cuatro métodos, solo línea recta marcada como implementada.
- `af_estado_activo` — catálogo de sistema, siete estados con `permite_depreciar` y `es_final`.
- `af_tipo_activo` — por empresa; presta vida útil, método, % residual y cuatro cuentas.
- `af_ubicacion` — por empresa, jerárquica por `padre_id`.
- `af_activo_asignacion` — historial con vigencia; índice único parcial garantiza una sola fila abierta.
- `af_activo_componente` — componentes y accesorios del activo.

`af_activo_fijo` recibe diecinueve columnas nuevas, todas nulas: las referencias normalizadas
(`tipo_activo_id`, `estado_activo_id`, `ubicacion_id`, `metodo_depreciacion_id`, `empleado_id`,
`cod_proveedor`, `centro_costo_id`), los datos que faltaban (`marca`, `placa`, `codigo_barra`,
fechas de depreciación, póliza y garantía) y las cuatro de auditoría. Las columnas de texto libre del
histórico no se tocan: quedan como respaldo del dato de origen.

Tres columnas cambian de tipo, todas ampliaciones sin pérdida:

| Columna | Antes | Después | Por qué |
|---|---|---|---|
| `valor_rescate` | `NUMERIC(7,2)` | `NUMERIC(14,2)` | El tope heredado era L. 99,999.99: el residual de un vehículo no cabía |
| `vida_util_anios` | `NUMERIC(3,0)` | `NUMERIC(4,1)` | Solo admitía enteros; no había forma de poner 2.5 años |
| `valor_venta` | `NUMERIC(11,2)` | `NUMERIC(14,2)` | Se alinea con `valor_compra` |

Acceso a datos: doce funciones `fn_af_*` y ocho procedimientos `sp_af_*`. Todo el SQL vive en la base,
según la regla del proyecto; el C# solo invoca.

### 4.2 Backend

- DTOs en `SIAD.Core/DTOs/ActivosFijos/`.
- `CatalogosActivosFijosService` y `ActivosFijosService` en `SIAD.Services/ActivosFijos/`, con Dapper
  sobre las funciones y procedimientos. Sin LINQ y sin SQL de negocio embebido.
- Controladores en `apc/Controllers/ActivosFijos/`, delgados: validan, delegan y traducen el
  `RAISE EXCEPTION` de Postgres a un 400 con el mensaje tal cual, que ya está redactado para el usuario.

### 4.3 Permisos

Módulo nuevo `activosfijos` en el catálogo, con dos recursos:

- `activos` — el maestro. Incluye `asignar` como permiso propio: quien lleva el control físico
  reasigna sin poder tocar valores ni cuentas contables.
- `catalogos` — tipos y ubicaciones. Sin `Delete`: se desactivan, no se borran.

### 4.4 Pantallas

Grupo de menú nuevo **Activos fijos**, con dos entradas: Registro de activos y Catálogos.

- `/activos-fijos/activos` — listado con cuatro tarjetas de resumen (activos en el patrimonio, valor de
  compra, depreciación acumulada frente a valor en libros, y pendientes de completar), filtros por
  texto, tipo, estado y un interruptor para ver solo los registros incompletos.
- `/activos-fijos/activos/nuevo` y `/activos-fijos/activos/{id}` — ficha con pestañas: identificación,
  compra y depreciación, contabilidad y seguros, componentes e historial de asignación. Al elegir el
  tipo, el formulario rellena vida útil, método y valor residual. Los derivados (cuota mensual, cuota
  diaria, valor en libros y fin de vida útil) los calcula la base y la ficha los muestra en solo lectura.
- `/activos-fijos/tipos` y `/activos-fijos/ubicaciones` — los dos catálogos.

Todas siguen el estándar de grid del repositorio.

---

## 5. Decisiones abiertas

| # | Pregunta | Recomendación | Impacto si se decide distinto |
|---|---|---|---|
| **D1** | ¿Extender `af_activo_fijo` o crear un maestro nuevo y migrar? | **Extender**, como está hecho. Es aditivo, conserva el histórico y su detalle de depreciación, y no obliga a un corte | Un maestro nuevo implicaría migrar los datos y decidir qué pasa con `af_activo_fijo_depreciacion` |
| **D2** | ¿Qué hacer con `con_activo_fijo`, `con_activo_tipo` y `con_deprecacion`? | Marcarlas como muertas y borrarlas en una limpieza aparte, una vez confirmado que están vacías | Si tuvieran datos, habría que consolidarlas antes de F2 |
| **D3** | ¿El gasto de depreciación se reparte entre varios centros de costo, como en Merendon (cinco cuentas con porcentajes)? | Preguntar al contador antes de F2. El prototipo deja `centro_costo_id` en la tabla pero fuera del formulario | Si la respuesta es sí, F2 necesita una tabla de distribución por activo |
| **D4** | ¿La depreciación arranca el mes de la compra o el siguiente? ¿Se deprecia el mes de la baja? | Preguntar al contador. Es la regla que define la corrida de F2 | Cambia el cálculo de la primera y la última cuota |
| **D5** | ¿Se necesita un valor en libros alterno? El histórico trae `valor_libros_alterno` (ex `valor_depr2`) con semántica sin confirmar | Dejarlo intacto hasta que alguien lo explique | Si es una depreciación fiscal paralela, F2 tiene que llevar dos libros |
| **D6** | `meses_depreciados` es `NUMERIC(2,0)`: tope de 99 meses, poco más de 8 años | Ampliar en F2, cuando el motor la use | Un activo de 20 años desbordaría la columna |
| **D7** | ¿Se migran los códigos de SIMAFI o se renumera todo con el formato nuevo? | Conservarlos. El correlativo nuevo solo mira los códigos con formato `PREFIJO-000000`, así que conviven | Renumerar rompe la trazabilidad con las etiquetas físicas puestas |
| **D8** | En el histórico, la depreciación real vive en `valor_depreciado` y `depreciacion_acumulada` está en 0 en las 829 filas. ¿Se unifica el dato? | Sí, con un backfill antes de F2. Hoy las funciones de lectura devuelven el mayor de las dos, que es un parche de presentación | Sin unificar, el motor de F2 tendría que arrastrar la ambigüedad en cada corrida |
| **D9** | `IN-02-02-31` está 100% depreciado y conserva L. 3,737.52 en libros, el 1% de su valor de compra | Preguntar al contador si es un residual simbólico del legacy o un error del dato | Si es error, se corrige en el mismo backfill de D8 |

---

## 6. Verificación hecha contra datos reales

El script se aplicó al mirror y se probó el flujo completo dentro de una transacción con `ROLLBACK`.
Lo comprobado:

- El tipo de activo presta correctamente vida útil, método, porcentaje residual y las tres cuentas.
- El código se autogenera con el prefijo del tipo: `VEH-000001`.
- Los derivados salen bien. Con compra de L. 800,000, residual del 10% y 5 años de vida útil: valor
  depreciable L. 720,000, cuota mensual L. 12,000, cuota diaria L. 394.52 y fin de vida útil al
  2031-01-15.
- Las cuentas contables se heredan del tipo cuando el activo no trae las suyas.
- La asignación inicial se abre sola y queda como vigente.
- La validación de código duplicado responde con el mensaje correcto.
- Las 829 filas del histórico quedaron intactas: mismo conteo y mismos totales que antes de aplicar.

**Hallazgo:** el histórico trae la depreciación en `valor_depreciado`, no en `depreciacion_acumulada`.
Son L. 16,048,909.78 que se habrían mostrado como cero. Ver D8.

## 7. Pendientes de esta fase

1. Aplicar el script en el servidor `siad_v4` (lo decide el usuario; ver el runbook).
2. Sembrar los tipos de activo y las ubicaciones reales de la empresa.
3. Conceder los permisos `module.activosfijos.*` a los roles que correspondan.
4. Prueba de humo en el portal con sesión iniciada.
5. Pruebas de integración en `SIAD.Tests` para `sp_af_activo_guardar` (herencia del tipo, validaciones,
   apertura y cierre de la asignación vigente) y para `sp_af_activo_asignar`.
6. Registrar `af_activo_fijo`, `af_tipo_activo` y `af_ubicacion` en la bitácora de maestros.
7. Decidir D3 y D4 con el contador antes de empezar F2.
