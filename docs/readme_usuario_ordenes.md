# Guía para Usuarios – Módulo de Órdenes

## 1. Acceso y pestañas
- Ingresa a **Órdenes** desde el menú.
- Usa las pestañas superiores para alternar entre:
  - **Órdenes de Trabajo Agua**
  - **Órdenes de Corte**
  - **Órdenes Alcantarillado**
- Cada pestaña filtra automáticamente las órdenes por departamento.

## 2. Filtros principales
Todos los filtros se encuentran sobre la tabla y pueden combinarse.

| Filtro | Descripción |
| ------ | ----------- |
| **Búsqueda libre** | Escribe clave, concepto, usuario o empleado y presiona **Buscar**. |
| **Estado / Tipo / Cliente (clave)** | Combos con búsqueda en vivo: escribe para ver coincidencias en la lista. |
| **Fechas** | Define un rango *Desde* y *Hasta*. |
| **Acciones** | Botones **Buscar** y **Limpiar** aplican o reinician los filtros. |

> **Tip:** mientras escribes en un combo se despliegan las coincidencias sin cerrar la lista. Si lo que escribiste no existe se limpia automáticamente para evitar errores.

## 3. Tabla de órdenes
- Muestra número, clave, propietario, dirección, fecha, **fecha de creación**, concepto, tipo, estado (con badge de color) y usuario asignado.
- Activa la columna de checkboxes para seleccionar varias órdenes y asignarlas en lote.
- Haz clic sobre el número para abrir el detalle de la orden.

## 4. Crear una orden
1. Haz clic en **Nueva orden**.
2. Completa los campos requeridos:
   - **Cliente**, **Tipo** y **Empleado** usan combos con búsqueda en vivo.
   - **Fecha** no permite valores anteriores al día actual.
   - **Concepto** es obligatorio (usa el memo de texto).
3. Opcionalmente agrega Personas y Saldo estimado.
4. Presiona **Guardar**. El sistema valida que los valores existan en el catálogo y muestra un mensaje de éxito. Si falta información se mostrará un aviso indicando los campos a completar.

## 5. Asignar órdenes
1. Selecciona una o varias filas en la tabla principal.
2. Haz clic en **Asignar órdenes seleccionadas**.
3. Indica:
   - **Usuario** que recibirá la orden.
   - **Estado** al que pasarán las órdenes.
   - **Empleado** (si aplica). Todos los campos aceptan búsqueda en vivo.
4. Revisa el resumen y presiona **Asignar**. El sistema confirmará el total de órdenes actualizadas.

## 6. Notificaciones y errores
- Mensajes exitosos aparecen en verde y los errores en rojo.
- Cada operación relevante también muestra un *toast* (notificación emergente) en la esquina inferior.
- Si un catálogo no se puede cargar (por ejemplo, estados o tipos), se muestra un mensaje explicando qué catálogo falló para que puedas reintentar.

## 7. Buenas prácticas
- Usa los filtros antes de crear órdenes para evitar duplicados.
- Verifica la pestaña activa antes de asignar; el departamento cambia según la pestaña.
- Si escribes algo que no existe en un combo, deja que el sistema lo limpie; así evitas búsquedas o guardados inválidos.

¡Listo! Con estos pasos puedes consultar, crear y asignar órdenes de trabajo de forma rápida y segura. Disfruta la búsqueda en vivo y mantén tus catálogos al día.
