# 📋 Estructura Simplificada de Caja.razor

## El Problema (Fue)
❌ La página tenía **3 formas diferentes de registrar pagos** + auditoría  
❌ No estaba claro qué sección hacer qué  
❌ Usuario: *"NO ENTIENDO UN CARAJO"*

## La Solución (Ahora)
✅ **2 secciones claras** con propósitos definidos  
✅ Jerarquía visual evidente  
✅ Flujo lógico: *Ver histórico* → *Registrar nuevos pagos*

---

## 📊 Sección 1: Control de Arqueo Diario

**¿Qué es?**  
Panel de **vista/consulta** de resumen de arqueos por fecha y caja

**¿Qué hace?**  
- Filtra arqueos por: Caja, Fecha Inicio, Fecha Fin
- Muestra tabla con:
  - **Fecha**: Día del arqueo
  - **Caja**: Nombre de la caja (Caja 1, Caja 2, etc)
  - **# Pagos**: Cantidad de transacciones registradas
  - **Total Recaudado**: Suma total de dinero recibido

**¿Cuándo usar?**  
- 👀 Para **revisar y auditar** cuánto dinero entra por caja/día
- 📈 Para reportes históricos
- ✅ Para confirmar que los pagos se registraron correctamente

**¿Puedo editar aquí?**  
❌ NO. Es solo lectura.

**¿Puedo registrar un pago aquí?**  
❌ NO. Es solo para **auditar/consultar**.

---

## 💳 Sección 2: Posteo por Modalidad (3 tabs)

**¿Qué es?**  
Panel de **registro activo** de nuevos pagos, separados por cómo se recibe el dinero

### Tab 1: 📱 Lectoras
- Para pagos que llegan por **medidores/equipos de lectura**
- Usuario: Selecciona cliente → Carga saldos de lectura → Confirma distribución
- Sistema: Actualiza estado en `factura_detalle` y `transaccion_abonado`

### Tab 2: 🛠️ Misceláneos  
- Para pagos por servicios **diversos** (reparaciones, instalaciones, etc)
- Usuario: Busca factura → Entra monto → Registra
- Sistema: Crea abono en tabla transaccional

### Tab 3: ✋ Manual **← AQUÍ ESTÁS**
- Para pagos de **servicios regulares** (agua, alcantarillado, otros)
- Usuario:
  1. Selecciona cliente (ej: CLI-DEMO-001)
  2. Hace clic en **"Cargar saldos"** → Ve lista de facturas pendientes
  3. Hace clic en **una fila de la grilla** → Se llenan campos abajo
  4. Ingresa distribución (cuánto de agua, alcantarillado, etc)
  5. Hace clic en **"Registrar"** → Se guarda el pago

---

## 🎯 Flujo Correcto para Registrar Pago Manual

```
1. LEER DINERO FISICO (cliente da plata)
   ↓
2. ABRIR → Captación en Caja → Posteo por Modalidad → Manual
   ↓
3. Seleccionar Cliente:
   - DxComboBox "Seleccione cliente"
   - Escribir: CLI-DEMO-001
   ↓
4. Click "Cargar saldos"
   - GET /api/captacionpagos/saldos-manual/CLI-DEMO-001
   - Grilla se llena con 10+ facturas pendientes
   ↓
5. Click en UNA FILA de la grilla (ej: ReciboPendiente 2025-001)
   - La tarjeta abajo se llena con:
     * Valor total
     * Campos distribuibles: Agua, Alcantarillado, Otros
   ↓
6. Ingresa distribución:
   - Agua: $50
   - Alcantarillado: $30
   - Otros: $20
   - (Total debe coincidir)
   ↓
7. Click "Registrar Pago"
   - POST /api/captacionpagos/posteo-manual
   - Guarda en BD
   - Toast verde: "¡Pago registrado!"
   ↓
8. Ir a → Control de Arqueo Diario
   - Buscar fecha de hoy
   - Verificar que el pago aparece en tabla
```

---

## ⚠️ Errores Comunes

| Problema | Causa | Solución |
|----------|-------|----------|
| "Registrar" no responde | No seleccionaste fila en grilla | Haz clic en UNA fila primero |
| Error 400 al registrar | Falla BD (SP) | Ver logs en Console.Developer |
| Grilla vacía | Cliente sin facturas pendientes | Usa CLI-DEMO-001 (tiene datos de prueba) |
| Tarjeta abajo no se llena | Grid sin SelectedDataItemChanged | Ya está fijo ✅ |

---

## 🔧 Cambios Realizados Esta Sesión

### Antes ❌
- Caja.razor tenía **285 líneas** de código confuso
- 3 secciones solapadas de registro:
  - "Registro Rápido de Pago" (formulario manual simple)
  - "Consultar Pago" (búsqueda por factura)  
  - "Posteo por Modalidad" (registro distribuido)
- **Auditoría de Arqueo** (solo lectura)
- Usuario no sabía cuál usar

### Después ✅
- Caja.razor ahora tiene **367 líneas** (limpias y enfocadas)
- Solo 2 secciones claras:
  - **Auditoría**: "Control de Arqueo Diario" (lectura)
  - **Registro**: "Posteo por Modalidad" (3 tabs de escritura)
- Alerta azul explica qué hacer
- Tab Manual abierto por defecto (`tabActual = 2`)

### PosteoManual.razor ✅
- **Agregado**: Event binding `SelectedDataItemChanged="OnSeleccionadoChanged"`
- **Agregado**: Handler que actualiza campos al seleccionar fila
- **Resultado**: Ahora el click en grilla **sí llena** la tarjeta de distribución

---

## ❌ Bloqueador Actual: Error PostgreSQL

**Problema**: Cuando haces clic en "Registrar", POST retorna 400:
```
Npgsql.PostgresException thrown
POST /api/captacionpagos/posteo-manual
Bad Request
```

**Causa**: Las stored procedures fallan:
- `sp_actualizar_detalle_posteomanual` → NO ACTUALIZA factura_detalle
- `sp_registrar_posteo_manual` → NO REGISTRA transacción

**Solución (Próximo paso)**: 
1. Revisar logs de PostgreSQL para error exacto
2. Validar que SPs existen en BD
3. Validar parámetros coinciden
4. Ejecutar manualmente en psql para debug

---

## 📚 Referencias

- [PosteoManual.razor](../apc.Client/Pages/Facturacion/CaptacionPagos/PosteoManual.razor) - Componente de registro manual
- [CaptacionPagosService.cs](../SIAD.Services/CaptacionPagos/CaptacionPagosService.cs) - Lógica de backend
- [CaptacionPagosController.cs](../apc/Controllers/CaptacionPagos/CaptacionPagosController.cs) - Endpoints

---

**Last Updated**: 2026-01-15 | **Status**: UI Clarificada, Registro Manual sin funcionar (SP issue)
