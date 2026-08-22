# Menú lateral — inventario, jerarquía y comparación entre ramas

> **Fuente de verdad:** [`apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs`](../apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs).
> Este documento es un **espejo** de ese archivo; si discrepan, manda el código.
>
> Captura: rama `feat/almacen-integracion-contable`, commit `4b94c85` (2026-08-20).
> Totales: **7 secciones · 21 grupos · 109 opciones navegables** (137 `Id` en el archivo).

Sirve para tres cosas:

1. Saber **qué opción existe y dónde vive** en la jerarquía (§2 y §3).
2. **Comparar ramas** antes de mezclar: verificar si una opción que existe aquí falta en otra rama
   (o al revés) (§4 y §5).
3. Guardar el **diseño propuesto de permisos del menú**, que hoy **no está implementado** (Anexo A).

---

## 1. Cómo se decide hoy si una opción se ve

En [`apc.Client/Layout/SidebarNavigation.razor`](../apc.Client/Layout/SidebarNavigation.razor):

1. `SoloSuperAdmin = true` → sólo el rol `RoleNames.SuperAdministrador` la ve. Son 5 opciones, todas
   en Configuración › Sistema (marca 🔒).
2. `RequiredCapability` → depende de la **configuración de la empresa**, no de permisos. Hoy existe
   una sola capacidad, `ChequeManual`: la opción se oculta si la empresa no tiene un tipo de
   transacción de salida que emita cheque (marca ⚙).
3. Un **grupo** desaparece cuando ninguno de sus hijos queda visible; una **sección** desaparece
   cuando se queda sin opciones.

**El menú NO filtra por los permisos finos de `PermissionNames`.** Un usuario autenticado que no sea
super administrador ve todas las demás opciones, aunque la API le rechace el acceso al entrar. El
diseño para agregar ese filtrado está en el Anexo A; si una rama lo implementa, se notará como un
campo nuevo en `SidebarNavItem` — revisarlo al comparar.

## 2. Resumen

| Sección | Grupos | Opciones |
|---|---:|---:|
| (sin sección: Inicio) | 0 | 1 |
| Administración | 7 | 25 |
| Bancos | 0 | 5 |
| Contabilidad | 3 | 12 |
| Inventario | 4 | 28 |
| Talento Humano | 1 | 3 |
| Configuración | 6 | 35 |
| **Total** | **21** | **109** |

## 3. Inventario de opciones

Marcas: 🔒 = `SoloSuperAdmin` · ⚙ = `RequiredCapability`.
La columna «permiso propuesto» **no está implementada** — ver Anexo A.

### (sin sección)

| Grupo | Opción | Id | Ruta | Marcas | Permiso propuesto ⚠ | Notas |
|---|---|---|---|---|---|---|
| — | Inicio | `home` | `/` | | — | Sin permiso: la portada no expone datos de módulo. |

### Administración

| Grupo | Opción | Id | Ruta | Marcas | Permiso propuesto ⚠ | Notas |
|---|---|---|---|---|---|---|
| Clientes | Clientes | `clientes` | `/clientes` | | `Ventas.Clientes.View` | |
| Clientes | Solicitudes | `solicitudes` | `/solicitudes` | | `Ventas.Clientes.View` o `Inventario.View` | Deuda backend † |
| Clientes | Facturas App | `app-facturas` | `/mi-app/facturas` | | `Configuracion.View` | La API (`FacturasAppController`) exige configuración. |
| Caja | Caja | `fact-caja` | `/facturacion/caja` | | `Ventas.Caja.View` | |
| Caja | Consulta de cobros | `fact-caja-consulta` | `/facturacion/caja/consulta` | | `Ventas.Caja.View` | |
| Caja | Cajas | `fact-caja-cajas` | `/facturacion/cajas` | | `Ventas.Caja.View` | |
| Facturación | Misceláneos | `fact-miscelaneos` | `/facturacion/miscelaneos` | | `Ventas.FacturacionMiscelaneos.View` | |
| Facturación | Consulta misceláneos | `fact-consulta-misc` | `/facturacion/miscelaneos/consulta` | | `Ventas.FacturacionMiscelaneos.View` | |
| Facturación | Catálogo misceláneos | `fact-catalogo-misc` | `/facturacion/miscelaneos/catalogo` | | `Ventas.FacturacionMiscelaneos.View` | |
| Facturación | Notas Crédito/Débito | `fact-notas` | `/facturacion/notas` | | `Ventas.NotasCreditoDebito.View` | |
| Facturación | Calendario de facturación | `fact-calendario-facturacion` | `/facturacion/calendario-facturacion` | | `Ventas.View` | Recurso propio (`calendario_facturacion`) cuya policy admite `module.ventas.view`. |
| Facturación | Períodos comerciales | `fact-periodos-comerciales` | `/facturacion/periodos-comerciales` | | `Ventas.View` | Recurso propio (`periodos_comerciales`), igual que el anterior. |
| Cobranza | Gestión de cobranza | `fact-cobranza-main` | `/facturacion/cobranza` | | `Ventas.Cobranza.View` | |
| Cobranza | Cortes masivos | `fact-cortes-masivos` | `/facturacion/cobranza/cortes-masivos` | | `Ventas.Cobranza.View` | |
| Cobranza | Acciones de cobranza | `fact-acciones-cobranza` | `/facturacion/cobranza/acciones-cobranza` | | `Ventas.Cobranza.View` | |
| Cobranza | Historial de bitácora | `fact-historial-bitacora` | `/facturacion/cobranza/historial-acciones` | | `Ventas.Cobranza.View` | |
| Cobranza | Bloqueo de clientes | `fact-bloqueo-clientes` | `/facturacion/cobranza/bloqueo-clientes` | | `Ventas.Cobranza.View` | |
| Cobranza | Clientes para cobros | `fact-clientes-cobros` | `/facturacion/cobranza/clientes-cobros` | | `Ventas.Cobranza.View` | |
| Cobranza | Cartera vencida | `fact-cartera-vencida` | `/facturacion/cobranza/cartera-vencida` | | `Ventas.Cobranza.View` | |
| Órdenes y campo | Órdenes | `ordenes` | `/ordenes` | | `Inventario.View` o `Ventas.Clientes.View` | Deuda backend † |
| Órdenes y campo | Mapa | `mapa` | `/mapa` | | `Inventario.View` o `Ventas.Clientes.View` | Deuda backend † |
| Tarifario operativo | Cliente servicio | `tarv3-cliente-servicio` | `/tarifario/cliente-servicio-v3` | | `Ventas.Clientes.View` | |
| Tarifario operativo | Conflictos | `tarv3-conflictos` | `/tarifario/conflictos-v3` | | `Ventas.Clientes.View` | |
| Informes | Panel de informes | `informes-panel` | `/informes` | | `Reporteria.View` | |
| Informes | Catálogo | `informes-catalogo` | `/informes/catalogo` | | `Reporteria.View` | |

### Bancos

| Grupo | Opción | Id | Ruta | Marcas | Permiso propuesto ⚠ | Notas |
|---|---|---|---|---|---|---|
| — | Gestión de bancos | `bn-gestion` | `/contabilidad/bancos` | | `Bancos.View` | |
| — | Config. transacciones | `bn-transacciones` | `/bancos/configuracion_transacciones` | | `Bancos.View` | |
| — | Cheques emitidos | `bn-cheques` | `/bancos/cheques` | | `Bancos.View` | |
| — | Nuevo cheque manual | `bn-cheque-manual` | `/bancos/cheques/manual` | ⚙ `ChequeManual` | `Bancos.View` | Se oculta si la empresa no tiene un tipo de transacción que emita cheque. |
| — | Configuración | `bn-config` | `/bancos/configuracion` | | `Bancos.View` | |

### Contabilidad

| Grupo | Opción | Id | Ruta | Marcas | Permiso propuesto ⚠ | Notas |
|---|---|---|---|---|---|---|
| Partidas | Partidas | `cb-polizas` | `/contabilidad/partidas` | | `Contabilidad.View` | |
| Partidas | Partidas de facturación | `cb-partidas-facturacion` | `/contabilidad/partidas-facturacion` | | `Contabilidad.View` | |
| Partidas | Informe de partidas | `informes-partidas` | `/informes/partidas-contabilidad` | | `Reporteria.View` | |
| Catálogo contable | Plan de cuentas | `cb-plan-cuentas` | `/contabilidad/plan-cuentas` | | `Contabilidad.View` | |
| Catálogo contable | Centros de costo | `cb-centros-costo` | `/contabilidad/centros-costo` | | `Contabilidad.View` | |
| Catálogo contable | Terceros | `cb-terceros` | `/contabilidad/terceros` | | `Contabilidad.View` | |
| Catálogo contable | Diarios contables | `cb-diarios` | `/contabilidad/diarios` | | `Contabilidad.View` | |
| Catálogo contable | Tipos de comprobantes | `cb-tipos-comprobantes` | `/contabilidad/tipos-transaccion` | | `Contabilidad.View` | |
| — | Períodos contables | `cb-periodos` | `/contabilidad/periodos` | | `Contabilidad.View` | |
| Integración | Integración Contable | `cb-integracion` | `/contabilidad/empresas/integracion` | | `Contabilidad.View` | Recurso `contabilidad/integracion`, cuya policy admite `module.contabilidad.view`. |
| Integración | Configuración Sistema | `cb-config-sistema` | `/contabilidad/empresas/configuracion` | | `Contabilidad.View` | |
| — | Presupuesto | `presupuesto` | `/presupuesto/configuraciones` | | `rol:Contabilidad` o `Contabilidad.View` | Único ítem que dependería de una policy de **rol** (`CanContabilidad` = Admin o Contabilidad), la que exigen sus páginas. |

### Inventario

| Grupo | Opción | Id | Ruta | Marcas | Permiso propuesto ⚠ | Notas |
|---|---|---|---|---|---|---|
| Almacén | Artículos | `alm-articulos` | `/almacen/articulos` | | `Inventario.View` | |
| Almacén | Estado de cuenta de artículos | `alm-kardex` | `/almacen/kardex` | | `Inventario.View` | |
| Almacén | Existencias por bodega | `alm-existencias-bodega` | `/almacen/existencias-bodega` | | `Inventario.View` | |
| Almacén | Movimientos por bodega | `alm-kardex-bodega` | `/almacen/kardex-bodega` | | `Inventario.View` | |
| Almacén | Valuación de inventario | `alm-valuacion` | `/almacen/valuacion-inventario` | | `Inventario.View` | |
| Almacén | Alertas de stock | `alm-alertas` | `/almacen/alertas-stock` | | `Inventario.View` | |
| Movimientos | Movimientos de almacén | `alm-movimientos` | `/almacen/movimientos` | | `Inventario.Movimientos.View` | |
| Movimientos | Traslados entre bodegas | `alm-traslados` | `/almacen/traslados` | | `Inventario.Traslados.View` | |
| Movimientos | Órdenes de compra | `alm-ordenes-compra` | `/almacen/ordenes-compra` | | `Compras.View` | |
| Movimientos | Recepción de compras | `alm-recepciones` | `/almacen/compras/recepciones` | | `Compras.View` | |
| Movimientos | Pagos a proveedores | `alm-pagos-compra` | `/almacen/compras/pagos` | | `Compras.View` | |
| Movimientos | Consulta de compras | `alm-compras` | `/almacen/compras` | | `Compras.View` | |
| Movimientos | Carga inicial | `alm-carga-inicial` | `/almacen/carga-inicial` | | `Inventario.CargaInicial.View` | |
| Movimientos | Requisiciones | `alm-requisiciones` | `/almacen/requisiciones` | | `Inventario.Requisiciones.View` | |
| Movimientos | Descargos | `alm-descargos` | `/almacen/descargos` | | `Inventario.Descargos.View` | |
| Proveedores | Proveedores | `prov-lista` | `/proveedores` | | `Proveedores.View` | |
| Proveedores | Antigüedad de saldos | `prov-antiguedad-saldos` | `/proveedores/antiguedad-saldos` | | `Proveedores.AntiguedadSaldos.View` | |
| Proveedores | Retenciones | `prov-retenciones` | `/proveedores/retenciones` | | `Proveedores.Retenciones.View` | |
| Proveedores | Declaración de retenciones | `prov-retenciones-declaracion` | `/proveedores/retenciones/declaracion` | | `Proveedores.Retenciones.View` | Misma API que Retenciones (`proveedores/retenciones`). |
| Proveedores | Evaluación | `prov-evaluacion` | `/proveedores/evaluacion` | | `Proveedores.Evaluacion.View` | |
| Proveedores | Incidencias de recepción | `prov-incidencias` | `/proveedores/incidencias` | | `Proveedores.Incidencias.View` | |
| Catálogos de almacén | Tipos de artículos | `alm-tipos-articulo` | `/almacen/tipos-articulo` | | `Inventario.View` | |
| Catálogos de almacén | Categorías por unidad | `alm-categorias-unidad` | `/almacen/categorias-unidad` | | `Inventario.View` | |
| Catálogos de almacén | Unidades de medida | `alm-unidades` | `/almacen/unidades-medida` | | `Inventario.View` | |
| Catálogos de almacén | Conceptos de movimiento | `alm-conceptos-movimiento` | `/almacen/conceptos-movimiento` | | `Inventario.ConceptosMovimiento.View` | |
| Catálogos de almacén | Términos de pago | `alm-terminos-pago` | `/almacen/terminos-pago` | | `Inventario.View` | |
| Catálogos de almacén | ISV en compras | `alm-isv-compras` | `/almacen/isv-compras` | | `Inventario.View` | |
| Catálogos de almacén | Bodegas | `alm-bodegas` | `/almacen/bodegas` | | `Inventario.View` | |

### Talento Humano

| Grupo | Opción | Id | Ruta | Marcas | Permiso propuesto ⚠ | Notas |
|---|---|---|---|---|---|---|
| — | Empleados | `th-empleados` | `/talento-humano/empleados` | | `TalentoHumano.View` | |
| Catálogos | Cargos | `th-cargos` | `/talento-humano/cargos` | | `TalentoHumano.View` | |
| Catálogos | Departamentos | `th-departamentos` | `/talento-humano/departamentos` | | `TalentoHumano.View` | |

### Configuración

| Grupo | Opción | Id | Ruta | Marcas | Permiso propuesto ⚠ | Notas |
|---|---|---|---|---|---|---|
| Catálogos comerciales | Barrios | `mant-barrios` | `/mantenimientos/barrios` | | `Configuracion.View` | |
| Catálogos comerciales | Ciclos | `ciclos` | `/ciclos` | | `Inventario.View` o `Configuracion.View` | Deuda backend † |
| Catálogos comerciales | Libretas | `libretas` | `/libretas` | | `Inventario.View` o `Configuracion.View` | Deuda backend † |
| Catálogos comerciales | Medidores | `medidores` | `/medidores` | | `Inventario.View` o `Configuracion.View` | Deuda backend † |
| Catálogos comerciales | Clases de medidor | `mant-clases-medidor` | `/mantenimientos/clases-medidor` | | `Configuracion.View` | |
| Catálogos comerciales | Condiciones de lectura | `fact-condiciones-lectura` | `/facturacion/condiciones-lectura` | | `Ventas.View` | |
| Catálogos comerciales | Código de cliente | `mant-codigo-cliente` | `/mantenimientos/codigo-cliente` | | `Ventas.Clientes.View` | |
| Catálogos comerciales | Abogados | `abogados` | `/abogados` | | `Inventario.View` o `Configuracion.View` | Deuda backend † |
| Catálogos comerciales | CAI offline | `tarv3-cai-offline` | `/tarifario/cai-offline` | | `Ventas.Clientes.View` | |
| Catálogos de cobranza | Motivos de Notas C/D | `mant-motivos-notas` | `/facturacion/notas/motivos` | | `Ventas.NotasCreditoDebito.View` | |
| Catálogos de cobranza | Acciones de cobranza | `mant-acciones-cobranza` | `/mantenimientos/acciones-cobranza` | | `Ventas.Cobranza.View` | Catálogo servido por el módulo de cobranza. |
| Catálogos de cobranza | Observaciones cobranza | `mant-observaciones-cobranza` | `/mantenimientos/observaciones-cobranza` | | `Ventas.Cobranza.View` | Catálogo servido por el módulo de cobranza. |
| Catálogos de cobranza | Recargo por mora | `mant-recargo-mora` | `/mantenimientos/recargo-mora` | | `Ventas.Cobranza.View` o `Configuracion.View` | Deuda backend † |
| Tarifario | Cuadros tarifarios | `tarv3-cuadros` | `/tarifario/cuadros` | | `Ventas.Clientes.View` | |
| Tarifario | Maestro servicios | `tarv3-maestro-servicios` | `/tarifario/maestro-servicios-v3` | | `Ventas.Clientes.View` | |
| Tarifario | Distribución de abonos | `tarv3-desglose-abonos` | `/tarifario/desglose-abonos` | | `Ventas.Clientes.View` | |
| Tarifario | Ajustes tarifarios | `mant-ajustes-tarifarios` | `/mantenimientos/ajustes-tarifarios` | | `Ventas.Clientes.View` | Deuda backend † |
| Tarifario | Impuestos | `mant-impuestos` | `/mantenimientos/impuestos` | | `Configuracion.View` | |
| Tarifario | Retenciones | `mant-retenciones` | `/mantenimientos/retenciones` | | `Configuracion.Retenciones.View` | |
| Catálogos de proveedor | Tipos de proveedor | `mant-tipos-proveedor` | `/mantenimientos/tipos-proveedor` | | `Proveedores.View` | |
| Catálogos de proveedor | Tipos de contacto | `mant-tipos-contacto` | `/mantenimientos/tipos-contacto` | | `Proveedores.View` | |
| Sistema | Usuarios | `param-usuarios` | `/parametros/usuarios` | 🔒 | — | |
| Sistema | Roles y permisos | `param-roles` | `/parametros/roles` | 🔒 | — | |
| Sistema | Usuarios App | `app-usuarios` | `/mi-app/usuarios` | | `Configuracion.View` | |
| Sistema | Empresas | `cb-empresas` | `/contabilidad/empresas` | | `Contabilidad.View` | El cambio de empresa (tenant switch) sigue siendo sólo Super Administrador. |
| Sistema | Crear empresa | `cb-crear-empresa` | `/contabilidad/empresas/nueva` | | `Contabilidad.View` | Igual que Empresas. |
| Sistema | Branding del Portal | `param-branding` | `/parametros/branding` | 🔒 | — | |
| Sistema | Tipos de documento (SAR) | `tipos-documento-fiscal` | `/tipos-documento-fiscal` | | `Configuracion.View` | |
| Sistema | Correo y notificaciones | `cfg-correo` | `/configuracion/correo` | 🔒 | — | |
| Sistema | Configuración de auditoría | `auditoria-config` | `/auditoria/configuracion` | 🔒 | — | |
| Sistema | Bitácora de maestros | `auditoria-bitacora-maestros` | `/auditoria/bitacora-maestros` | | `Configuracion.View` | |
| Sistema | Diseño Web (informes) | `informes-reportes` | `/informes/reportes` | | `Reporteria.View` | |
| Sistema | Datasets Web (informes) | `informes-datasets` | `/informes/datasets` | | `Reporteria.View` | |
| Cuenta | Mi cuenta | `user-account` | `/Account/Manage` | | — | Sin permiso a propósito: siempre debe haber salida de la sesión. |
| Cuenta | Cerrar sesión | `logout` | `/Account/Logout` | | — | Sin permiso a propósito: siempre debe haber salida de la sesión. |

† Ver «Deudas detectadas al mapear» en el Anexo A.

---

## 4. Cómo comparar contra otra rama

El inventario se puede reextraer de cualquier rama sin abrir el portal ni compilar: las opciones son
literales en un solo archivo.

### Por ruta (recomendado)

La ruta (`NavigateUrl`) es la clave estable: identifica la pantalla aunque la rama haya cambiado el
rótulo, el ícono, la sección o el `Id`.

```bash
F=apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs
grep -o 'NavigateUrl = "[^"]*"' $F | sed 's/.*= //;s/"//g' | sort -u > /tmp/menu-actual.txt
git show <rama>:$F | grep -o 'NavigateUrl = "[^"]*"' | sed 's/.*= //;s/"//g' | sort -u > /tmp/menu-otra.txt
comm -23 /tmp/menu-otra.txt /tmp/menu-actual.txt   # están en <rama> y NO en la actual
comm -13 /tmp/menu-otra.txt /tmp/menu-actual.txt   # están en la actual y NO en <rama>
```

### Por Id (secundario)

Los `Id` detectan además secciones y grupos (que no tienen ruta), y son los que el acordeón persiste
en el navegador. **Advertencia:** una rama puede renombrarlos en bloque — es lo que hizo
`Cambios_almacen2.0` (`fac-caja` en vez de `fact-caja`, `ope-clientes` en vez de `clientes`…), y
entonces el diff por `Id` marca ~100 falsas diferencias. Si el diff por `Id` sale enorme y el de
rutas no, es un renombrado, no opciones nuevas.

```bash
F=apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs
grep -o 'Id = "[^"]*"' $F | sed 's/Id = //;s/"//g' | sort > /tmp/ids-actual.txt
git show <rama>:$F | grep -o 'Id = "[^"]*"' | sed 's/Id = //;s/"//g' | sort > /tmp/ids-otra.txt
diff /tmp/ids-otra.txt /tmp/ids-actual.txt
```

### Qué revisar cuando aparece una diferencia

1. ¿La opción falta porque la rama es **más vieja** (la funcionalidad todavía no existía) o porque
   alguien la **quitó a propósito**? El `git log` del archivo lo dice.
2. Si la opción existe en la otra rama, ¿existe también su **página** (`apc.Client/Pages/...`) y su
   endpoint? Una entrada de menú sin página es un 404 al mezclar.
3. Si es una opción nueva, ¿lleva `SoloSuperAdmin` o `RequiredCapability` donde corresponde?
4. ¿Cambió de sección o de grupo? La ruta no lo detecta; el `Id` del grupo sí.
5. ¿La rama agregó un campo nuevo a `SidebarNavItem` (p. ej. el filtrado por permisos del Anexo A)?
   Entonces cada opción que se traiga necesita ese campo, y el permiso debe existir en
   `SIAD.Core/Constants/PermissionNames.cs` **y** estar registrado como policy en `apc/Program.cs` y
   `apc.Client/Program.cs`.
6. Regenerar este documento (§6) si la rama que queda como base agrega, quita o mueve opciones.

## 5. Estado comparativo al 2026-08-20

Contra `feat/almacen-integracion-contable` (`4b94c85`), comparando por ruta:

| Rama | Opciones | Le faltan (vs. la actual) | Tiene y la actual no |
|---|---:|---|---|
| `main` (2026-08-05) | 87 | 22: todo Talento Humano; proveedores (antigüedad de saldos, retenciones, declaración, evaluación, incidencias); compras (órdenes, recepciones, pagos, carga inicial, ISV, términos de pago, conceptos de movimiento); almacén (movimientos, traslados, valuación, existencias y movimientos por bodega); mantenimientos/retenciones; correo | — |
| `feat/almacen-comercial` (`8866e9f`, 2026-08-18) | 107 | 3: Talento Humano completo | `/almacen/existencia-negativa` |
| `Cambios_almacen2.0` (2026-08-06) | 98 | 15 | 4: `/facturacion/captacion/caja`, `/facturacion/captacion/reverso`, `/facturacion/captacion/abonos-especiales(/consulta)` |

Dos diferencias que **no** son opciones olvidadas:

- **`/almacen/existencia-negativa`** existe sólo en `feat/almacen-comercial`. En la rama actual esa
  pantalla se eliminó a propósito el 2026-08-20 (commit `4b94c85`: la salida de inventario nunca deja
  existencia negativa, se borró el interruptor completo). No reintroducirla al mezclar.
- **`/facturacion/captacion/*`** de `Cambios_almacen2.0` es el flujo legacy de captación de pagos.
  `CaptacionPagosService` fue eliminado en la unificación de cobros; esas cuatro rutas están muertas
  y no deben volver (ver `CLAUDE.md`, sección de cobros).

## 6. Cómo regenerar este documento

```bash
grep -n 'Label = \|Text = \|SoloSuperAdmin\|RequiredCapability' apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs
```

Las secciones se leen por `Label`, los grupos son los `SidebarNavItem` con `Children`, y las opciones
son los que traen `NavigateUrl`. Verificar que los totales del encabezado cuadren:
`opciones = líneas con NavigateUrl` y `Id totales = secciones + grupos + opciones`.

---

## Anexo A — Filtrado por permisos (propuesta, NO implementada)

> ⚠ Nada de este anexo está en el código: el campo `RequiredPolicies` **no existe** en el árbol de
> trabajo, ni en ninguna rama, ni en la historia del repositorio. Es el diseño levantado el
> 2026-08-20 para cerrar la brecha descrita en §1. La columna «permiso propuesto» de §3 es la
> propuesta de mapeo, no lo que el menú evalúa hoy.

### Orden de evaluación propuesto

1. **Super Administrador** (`RoleNames.SuperAdministrador`) ve todo: no se evalúa ninguna policy.
2. `SoloSuperAdmin = true` → la opción sólo existe para ese rol.
3. `RequiredPolicies` → basta con **UNA** de las policies listadas (OR). Se evalúan con
   `IAuthorizationService`, que aplica la misma regla que la API: permiso fino, permiso de módulo o
   permiso legacy `module.<modulo>` (ver `PermissionNames.Policies`).
4. `RequiredCapability` → configuración de la empresa, no permisos.
5. Un grupo no lleva permiso propio: desaparece cuando ninguno de sus hijos queda visible.
6. Una sección desaparece cuando se queda sin opciones.

Reglas de borde deliberadas:

- **Inicio** y **Cuenta** (Mi cuenta / Cerrar sesión) no llevan permiso: un usuario sin permisos
  todavía puede cerrar sesión.
- Si una policy **no está registrada** en el host que evalúa, la opción **se muestra** (fail-open).
  El menú no es la frontera de seguridad — la API sí; ocultar por un error de registro dejaría al
  usuario sin acceso a algo que sí puede usar.
- El menú filtraría **lo mismo que exige la API** siempre que la API exija algo. Donde el controlador
  pide un módulo que no corresponde al dominio de la pantalla, la opción lleva **dos** policies (OR)
  para no ocultársela a quien sí puede entrar; esos casos son deuda del backend, no del menú.

### Deudas detectadas al mapear (backend, no menú) †

Estas pantallas exigen un módulo que no corresponde a su dominio, o no exigen permiso alguno.
Mientras no se corrijan, su opción llevaría dos policies en OR:

| Pantalla | Controlador | Exige hoy | Debería exigir |
|---|---|---|---|
| Solicitudes | `SolicitudesController` | `inventario` | `ventas/clientes` |
| Órdenes | `OrdenesController` | `inventario` | `ventas` |
| Ciclos | `CiclosController` | `inventario` | `configuracion` |
| Libretas | `LibretasController` | `inventario` | `configuracion` |
| Medidores | `MedidoresController` | `inventario` | `configuracion` |
| Abogados | `AbogadosController` | `inventario` | `configuracion` |
| Mapa | `MapController` | sólo `[Authorize]` | `ventas/clientes` |
| Recargo por mora, Ajustes tarifarios | `MantenimientosController` | sólo `[Authorize]` | `configuracion` / `ventas` |

### Riesgo al implementarlo

Hoy el menú muestra todo a cualquier usuario autenticado. Con el filtrado activo, un rol sin claims
de permiso **vería sólo Inicio y Cuenta**. Los roles reales ya deben tener permisos (la API los exige
desde antes), pero conviene revisar en `/parametros/roles` los roles del portal antes de publicar,
en particular los que se crearon a mano.
