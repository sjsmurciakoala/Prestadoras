# Vista PosteoManual - Guía de Funcionalidad y Prueba

## Resumen General

La vista **PosteoManual** permite registrar pagos de servicios (agua, alcantarillado, otros) de forma manual, distribuyendo el pago entre los diferentes tipos de servicio de una factura. También permite reversar pagos registrados.

## Flujo de Funcionamiento

### 1. **Carga de Cliente**
   - **Acción**: Selecciona un cliente del combo "Cliente" (ej: CLI-DEMO-001)
   - **Backend**: Llama a `GET /api/captacionpagos/clientes` para llenar la lista de clientes
   - **Componente**: `<DxComboBox>` con FilteringMode.Contains para búsqueda rápida

### 2. **Carga de Saldos**
   - **Acción**: Haces clic en botón "Cargar saldos"
   - **Backend**: Llama a `GET /api/captacionpagos/saldos-manual/{clienteClave}`
     - Ejecuta función PostgreSQL: `fn_getclientesaldos_posteomanual('CLI-DEMO-001')`
   - **Datos Retornados**:
     - `ReciboActual`: Número del recibo (factura)
     - `ReciboAnterior`: Recibo anterior (para historial)
     - `Valor`: Saldo total de la factura
     - `DistribucionAgua`: Monto destinado a agua
     - `DistribucionAlcantarillado`: Monto destinado a alcantarillado
     - `DistribucionOtros`: Monto destinado a otros servicios (misc, otros)
     - `DetalleId`: ID del primer detalle (para actualización)
   - **UI**: Se llena el `<DxDataGrid>` con los saldos pendientes

### 3. **Grid de Saldos**
   - **Columnas**:
     | Columna | Editable | Tipo | Descripción |
     |---------|----------|------|-------------|
     | Recibo Actual | No | int | Número de factura |
     | Recibo Anterior | No | int | Recibo previo (historial) |
     | Valor Total | No | decimal | Saldo total pendiente |
     | Agua (editable) | **SÍ** | decimal | Parte de agua a pagar |
     | Alcantarillado (editable) | **SÍ** | decimal | Parte de alcantarillado a pagar |
     | Otros (editable) | **SÍ** | decimal | Parte de otros servicios |
     | Total Editado | Calculado | decimal | Suma automática de las 3 anteriores |

   - **Selección**: Haces clic en una fila del grid
     - Se activa la tarjeta de distribución manual debajo
     - Se cargan los valores de esa factura en los campos editables

### 4. **Card de Distribución Manual**
   - **Mostrado cuando**: Se selecciona una fila en el grid
   - **Secciones**:
     
     **a) Info del Recibo**:
     - Muestra número de recibo seleccionado
     - Muestra valor total del recibo
     
     **b) Campos Editables** (DxSpinEdit):
     - `Agua`: Cantidad a acreditar por agua
     - `Alcantarillado`: Cantidad a acreditar por alcantarillado
     - `Otros`: Cantidad a acreditar por otros servicios
     - **Validación**: La suma NO debe exceder el valor total
     
     **c) Botones de Acción**:
     - **"Registrar Pago" (Verde)**: Guarda el pago distribuido
     - **"Reversar Pago" (Rojo)**: Cancela/revierte el pago anterior

### 5. **Registrar Pago**
   - **Acción**: Haces clic en "Registrar Pago"
   - **Validaciones**:
     - Cliente debe estar seleccionado
     - Recibo debe estar seleccionado
     - Al menos una distribución debe ser > 0
     - La suma no debe exceder el saldo total
   
   - **Backend**: Llama a `POST /api/captacionpagos/registrar-manual`
     - **Datos enviados**:
       ```json
       {
         "clienteClave": "CLI-DEMO-001",
         "numRecibo": 3075062,
         "banco": "BanPaís",
         "valor": 7200.50,
         "distribucion": [
           { "tipoServicio": "AGUA", "monto": 3000.00 },
           { "tipoServicio": "ALCANTARILLADO", "monto": 2200.50 },
           { "tipoServicio": "OTROS", "monto": 2000.00 }
         ],
         "usuario": "admin"
       }
       ```
     
     - **Transacción en BD**:
       1. **sp_actualizar_detalle_posteomanual** (para cada distribución):
          - Actualiza `factura_detalle.montovalor_saldo` restando el monto acreditado
       2. **sp_registrar_posteo_manual**:
          - Inserta registro en `transaccion_abonado`:
            - `cliente_clave`: CLI-DEMO-001
            - `tipotransaccion`: '201' (Posteo Manual)
            - `partida`: '002'
            - `banco`: BanPaís
            - `debitos`: Total distribuido
            - `fecha_docu`: HOY
            - `usuario`: admin
          - Actualiza estado de factura a 'C' (Cobrado)
   
   - **Respuesta**: 
     - Toast SUCCESS: "Pago registrado correctamente"
     - Grid se actualiza (recibo desaparece si está 100% pagado)
     - Campo se limpian

### 6. **Reversar Pago**
   - **Acción**: Haces clic en "Reversar Pago"
   - **Backend**: Llama a `POST /api/captacionpagos/reversar-manual`
     - Ejecuta `sp_reversar_posteo_manual`:
       1. Elimina registro de `transaccion_abonado` con tipo_transaccion = '201'
       2. Restaura montos en `factura_detalle`
       3. Cambia estado de factura de 'C' a 'A'
   
   - **Respuesta**: 
     - Toast SUCCESS: "Pago reversado correctamente"
     - Recibo reaparece en el grid con saldo completo

## Datos de Prueba Insertados

### Cliente: CLI-DEMO-001

| Recibo | NumFactura | Fecha Emisión | Saldo Total | Estado | Servicios | Detalle |
|--------|-----------|---------------|-------------|--------|-----------|---------|
| 3075062 | F3076003 | Hace 1 día | 7,200.50 | A | Agua: 3,000 / Alc: 2,200.50 / Otros: 2,000 | 3 líneas |
| 3075061 | F3076002 | Hace 8 días | 4,750.00 | A | Agua: 2,500 / Alc: 2,250 | 2 líneas |
| 3075060 | F3076001 | Hace 15 días | 8,500.00 | A | Agua: 3,500 / Alc: 2,000 / Misc: 3,000 | 3 líneas |

**Total pendiente para CLI-DEMO-001**: 20,450.50

## Pasos para Probar la Vista

### Test Case 1: Registro de Pago Completo
1. Abre la aplicación y navega a `/facturacion/captacion/caja`
2. Selecciona "Posteo Manual"
3. Selecciona cliente **CLI-DEMO-001**
4. Haz clic en **"Cargar saldos"**
   - ✓ Deberían aparecer 3 recibos en el grid (3075062, 3075061, 3075060)
5. Selecciona la fila con recibo **3075062** (7,200.50)
   - ✓ Aparece la tarjeta de distribución
6. Deja los valores automáticos (agua: 3,000 / alc: 2,200.50 / otros: 2,000)
7. Haz clic en **"Registrar Pago"**
   - ✓ Toast: "Pago registrado correctamente"
   - ✓ Recibo 3075062 desaparece del grid (está 100% pagado)
8. Verifica en BD:
   ```sql
   SELECT * FROM transaccion_abonado 
   WHERE cliente_clave = 'CLI-DEMO-001' 
   ORDER BY fecha_docu DESC LIMIT 1;
   ```
   - ✓ Nueva transacción con tipotransaccion='201', debitos=7200.50

### Test Case 2: Pago Parcial
1. Selecciona recibo **3075061** (4,750.00)
2. Edita campos:
   - Agua: 1,500 (en lugar de 2,500)
   - Alcantarillado: 1,750 (en lugar de 2,250)
   - Otros: 0 (sin cambios)
   - **Total editado**: 3,250.00
3. Haz clic en **"Registrar Pago"**
   - ✓ Recibo permanece en grid con nuevo saldo: 1,500 (4,750 - 3,250)

### Test Case 3: Reversión de Pago
1. Selecciona un recibo con pago registrado
2. Haz clic en **"Reversar Pago"**
   - ✓ Toast: "Pago reversado correctamente"
   - ✓ Recibo reaparece con saldo original

### Test Case 4: Validación de Distribución
1. Selecciona recibo 3075060 (8,500.00)
2. Intenta editar:
   - Agua: 5,000
   - Alcantarillado: 4,000
   - Otros: 1,000
   - **Total**: 10,000 (EXCEDE el saldo de 8,500)
3. Haz clic en **"Registrar Pago"**
   - ✓ Debería mostrar error: "La suma de la distribución excede el saldo disponible"

## Arquitectura Técnica Involucrada

### Frontend (apc.Client)
- **PosteoManual.razor**: Componente Blazor con DxDataGrid, DxComboBox, DxSpinEdit
- **CaptacionPagosClient.cs**: Cliente HTTP con métodos:
  - `GetSaldosPosteoManualAsync()`
  - `RegistrarPagoManualAsync()`
  - `ReversarPagoManualAsync()`

### Backend (apc)
- **CaptacionPagosController.cs**: Endpoints REST
  - `GET /api/captacionpagos/saldos-manual/{clienteClave}`
  - `POST /api/captacionpagos/registrar-manual`
  - `POST /api/captacionpagos/reversar-manual`

### Services (SIAD.Services)
- **CaptacionPagosService.cs**: Lógica empresarial
  - `ObtenerSaldosPosteoManualAsync()`: Usa Dapper + fn_getclientesaldos_posteomanual
  - `RegistrarPagoManualAsync()`: Maneja transacciones, llama SPs
  - `ReversarPagoManualAsync()`: Revierte cambios

### Database (PostgreSQL siad_v3)
- **Tabla factura**: Encabezado de facturas
- **Tabla factura_detalle**: Líneas detalladas por tipo de servicio
- **Tabla transaccion_abonado**: Registro de transacciones (débitos/créditos)
- **Función fn_getclientesaldos_posteomanual()**: Retorna saldos con distribución
- **Procedimiento sp_actualizar_detalle_posteomanual()**: Acredita montos
- **Procedimiento sp_registrar_posteo_manual()**: Registra transacción
- **Procedimiento sp_reversar_posteo_manual()**: Revierte transacción

## Estado Actual

✅ **Completamente Implementado y Funcional**:
- UI con grid y distribución
- Validaciones en cliente y servidor
- Integración con Dapper para SPs
- Transacciones en BD
- Datos de prueba listos

🔍 **Listo para UAT (User Acceptance Testing)**

