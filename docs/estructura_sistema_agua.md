# Estructura del Sistema de Facturación de Agua y Plan de Cuentas

## 1. Introducción

Este documento resume cómo funciona el sistema de facturación de agua y cómo se conecta con el plan de cuentas contable.

Flujo principal:

Lectura de medidor -> Cálculo de consumo -> Aplicación de tarifas -> Generación de factura -> Registro contable -> Pago del cliente

------------------------------------------------------------------------

# 2. Plan de cuentas (con_plan_cuentas)

El sistema clasifica las cuentas por `account_type`. La numeración (1, 2, 3, 4, etc.) depende del plan de cuentas de cada empresa.

  Tipo de cuenta | Descripción
  ------------- | -----------
  ACTIVO        | Bienes y derechos de la empresa
  PASIVO        | Deudas y obligaciones
  CAPITAL       | Patrimonio de la empresa
  INGRESO       | Ingresos por ventas/servicios
  GASTO         | Costos y gastos
  MEMORANDA     | Cuentas de orden (informativas)

El sistema no depende de códigos específicos. Puedes cargar un plan NIIF y luego migrar al plan APC. El cambio implica remapear cuentas en `servicios` y `con_regla_integracion`. Los asientos históricos deben conservar las cuentas antiguas (inactivarlas, no borrarlas).

------------------------------------------------------------------------

# 3. Activos (account_type = ACTIVO)

Representan bienes o dinero que posee la empresa.

Ejemplos:

- 1.1.1.01 Caja
- 1.1.1.02 Bancos
- 1.1.2.01 Cuentas por cobrar clientes

------------------------------------------------------------------------

# 4. Pasivos (account_type = PASIVO)

Representan deudas u obligaciones de la empresa.

Ejemplos:

- Sueldos por pagar
- Impuestos por pagar
- Proveedores

------------------------------------------------------------------------

# 5. Patrimonio (account_type = CAPITAL)

Representa el capital de la empresa.

Ejemplos:

- Capital social
- Reservas
- Resultados acumulados

------------------------------------------------------------------------

# 6. Ingresos (account_type = INGRESO)

Ingresos por los servicios prestados.

Ejemplos:

- Ingreso por agua potable
- Ingreso por alcantarillado
- Otros cargos

------------------------------------------------------------------------

# 7. Gastos (account_type = GASTO)

Costos y gastos de operación.

Ejemplos:

- Energía eléctrica
- Mantenimiento
- Combustible
- Sueldos

------------------------------------------------------------------------

# 8. Flujo del sistema de facturación

    Lectura de medidor
          |
    Calcular consumo
          |
    Aplicar tarifas
          |
    Agregar cargos
          |
    Generar factura
          |
    Registrar contabilidad
          |
    Registrar pago

------------------------------------------------------------------------

# 9. Cálculo de consumo

    Consumo = LecturaActual - LecturaAnterior

Ejemplo:

    Lectura anterior = 120
    Lectura actual = 138
    Consumo = 18 m3

------------------------------------------------------------------------

# 10. Aplicación de tarifas

El sistema usa dos tablas según el tipo de cliente:

- `tarifas`: clientes sin medidor (tarifa fija por tipo, categoría y letra).
- `tarifas_contador`: clientes con medidor (rangos con mínimo, máximo, cuota, valor base y alquiler).

Las letras se administran en `letras` y se asignan al cliente. La tarifa se busca por `tipo + categoria_id + codigo` (letra).

------------------------------------------------------------------------

# 11. Cargos adicionales

Se configuran por servicio y se agregan a la factura:

- Alcantarillado
- ERSAPS
- Fondo ambiental
- Otros cargos

Config tablas relacionadas:

- `configuracion_tasas`
- `cobros_adicionales`

------------------------------------------------------------------------

# 12. Registro contable de la factura

La factura genera un asiento contable. El crédito de ingresos debe salir de la cuenta contable del servicio (`servicios.cont_account_id`).

Ejemplo:

  Cuenta                                   Debe   Haber
  ---------------------------------------- ------ -------
  CxC clientes (regla de integración)       179
  Ingreso por agua potable (servicio)              114
  Ingreso por alcantarillado (servicio)            57
  Otros cargos (servicio)                           8

Si un servicio no tiene cuenta contable, se usa la cuenta crédito definida en `con_regla_integracion` como respaldo.

------------------------------------------------------------------------

# 13. Registro del pago

Cuando el cliente paga:

  Cuenta         Debe   Haber
  -------------- ------ -------
  Caja/Banco     179
  CxC clientes          179

------------------------------------------------------------------------

# 14. Integración contable automática

Piezas principales:

- `con_tipo_transaccion`: define el tipo de comprobante (FACTURACION, PAGO, NOTA_CREDITO, NOTA_DEBITO) y si es automático.
- `con_regla_integracion`: define cuentas por defecto (CxC, caja/banco, impuestos).
- `con_plantilla_partida_*`: solo aplica a movimientos con líneas fijas.

Para facturación de servicios no se recomienda plantilla, porque el crédito de ingresos es dinámico por servicio.

------------------------------------------------------------------------

# 15. Tablas principales del sistema

- `cliente_maestro`
- `maestro_medidor`
- `historicomedicion`
- `historicosinmedidor`
- `tarifas`
- `tarifas_contador`
- `letras`
- `servicios`
- `configuracion_tasas`
- `cobros_adicionales`
- `factura`
- `factura_detalle`
- `transaccion_abonado`

------------------------------------------------------------------------

# 16. Flujo completo del sistema

    Cliente
       |
    Medidor
       |
    Lectura / Consumo
       |
    Tarifa
       |
    Factura
       |
    Cuenta por cobrar
       |
    Pago
       |
    Caja / Banco

------------------------------------------------------------------------

# 17. Resumen

El sistema conecta tres componentes:

1. Operación: lecturas y consumos.
2. Facturación: tarifas y cargos adicionales.
3. Contabilidad: plan de cuentas y asientos automáticos.
