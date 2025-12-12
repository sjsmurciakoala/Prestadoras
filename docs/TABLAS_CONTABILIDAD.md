# 📊 Documentación de Tablas - Módulo de Contabilidad

## Concepto: Multi-Empresa (No Multitenant SaaS)

El sistema soporta **múltiples empresas/sucursales** dentro de una misma organización. Cada empresa tiene su:
- ✅ Plan de cuentas propio
- ✅ Configuración contable independiente
- ✅ Períodos contables separados
- ✅ Datos completamente aislados

**Ejemplo:**
```
GRUPO EMPRESA ABC (Organización raíz)
├── Sucursal Central (EMP-001)
├── Sucursal Zona 1 (EMP-002)
├── Sucursal Zona 2 (EMP-003)
└── Oficina Admin (EMP-004)
```

---

## 📋 Tabla de Equivalencias SQL Server vs PostgreSQL

| # | **SQL Server (MD_CONTAB)** | **PostgreSQL (SIAD)** | **Descripción** |
|---|---|---|---|
| 1 | `C01Config` | `con_configuracion_sistema` | Configuración principal del sistema contable por empresa |
| 2 | `C01BlSheet` | `con_configuracion_balance` | Estructura del estado de situación financiera (balance) |
| 3 | `C01BlProfLoss` | `con_configuracion_linea_resultado` | Líneas del estado de resultados (P&L) |
| 4 | `C01Account` | `con_plan_cuenta` | Plan de cuentas - catálogo de cuentas contables |
| 5 | `C01Periods` | `con_periodo_contable` | Períodos contables (mensual, trimestral, anual) |
| 6 | `Company` | `cfg_company` | Maestro de empresas/sucursales |
| 7 | `C01CentroCostos` | `con_centro_costo` | Centros de costo para asignación |
| 8 | N/A | `con_poliza` | Pólizas contables (documentos de movimiento) |
| 9 | N/A | `con_poliza_linea` | Líneas detalle de pólizas |
| 10 | N/A | `con_saldo_cuenta` | Saldos de cuentas por período |

---

## 🔍 Detalle de Tablas Principales

### 1. **con_configuracion_sistema** (Configuración Principal)
Almacena la configuración del sistema contable para cada empresa.

#### Campos Clave:
```sql
config_id                    BIGINT          -- ID único de configuración
company_id                   BIGINT          -- Referencia a empresa
fecha_ini_ejer               DATETIME        -- Fecha inicio ejercicio contable
fecha_fin_ejer               DATETIME        -- Fecha fin ejercicio contable
meses_calc                   INT             -- Meses calculados en el período
sep_codigo                   VARCHAR(1)      -- Separador de código ('.' '-' ';')
fmt_ctas                     VARCHAR(20)     -- Formato cuentas (ej: ###-###-##)
fmt_centros                  VARCHAR(20)     -- Formato centros costo
sym_acreedor                 VARCHAR(5)      -- Símbolo saldo acreedor (CR, -)
mto_maximo                   DECIMAL(28,2)   -- Monto máximo por transacción
frec_deprec                  VARCHAR(20)     -- Frecuencia depreciación
fec_ult_deprec               DATETIME        -- Fecha última depreciación
```

#### Campos de Cuentas de Utilidad (Histórico):
```sql
cod_util_acum_hist           VARCHAR(24)     -- Código utilidad acumulada histórica
cod_util_ejer_hist           VARCHAR(24)     -- Código utilidad del ejercicio histórico
cod_perd_acum_hist           VARCHAR(24)     -- Código pérdida acumulada histórica
cod_perd_ejer_hist           VARCHAR(24)     -- Código pérdida del ejercicio histórico
```

#### Campos de Cuentas de Utilidad (Inflación):
```sql
cod_util_acum_inf            VARCHAR(24)     -- Código utilidad acumulada inflación
cod_util_ejer_inf            VARCHAR(24)     -- Código utilidad ejercicio inflación
cod_perd_acum_inf            VARCHAR(24)     -- Código pérdida acumulada inflación
cod_perd_ejer_inf            VARCHAR(24)     -- Código pérdida ejercicio inflación
```

#### Campos de Títulos y Descripciones:
```sql
tit_est_result               VARCHAR(40)     -- Título estado de resultados
tit_balance_gral             VARCHAR(40)     -- Título balance general
descripcion_activo           VARCHAR(40)     -- Descripción ACTIVO
descripcion_pasivo           VARCHAR(40)     -- Descripción PASIVO
descripcion_capital          VARCHAR(40)     -- Descripción CAPITAL
desc_pasiv_cap               VARCHAR(40)     -- Descripción PASIVO y CAPITAL
desc_orden                   VARCHAR(40)     -- Descripción CUENTAS ORDEN
mostrar_orden                BOOLEAN         -- Mostrar cuentas de orden
mostrar_percontra            BOOLEAN         -- Mostrar percontra
```

---

### 2. **con_plan_cuenta** (Plan de Cuentas)
Catálogo jerárquico de cuentas contables por empresa.

#### Campos Clave:
```sql
id_account                   VARCHAR(24)     -- Código única de cuenta
company_id                   BIGINT          -- Empresa propietaria
id_parent                    VARCHAR(24)     -- Cuenta padre (relación jerárquica)
descripcion                  VARCHAR(80)     -- Descripción de la cuenta
descripcion_corta            VARCHAR(60)     -- Descripción abreviada
nivel                        TINYINT         -- Nivel jerárquico (1-N)
permitir_movimiento          BOOLEAN         -- ¿Permite movimientos?
permitir_presupuesto         BOOLEAN         -- ¿Permite presupuesto?
actividad                    TINYINT         -- Tipo actividad (0-4)
tipo_cuenta                  TINYINT         -- Tipo (Activo=0, Pasivo=1, Capital=2, etc)
tipo_hac                     TINYINT         -- Tipo HAC
permitir_centro_costo        BOOLEAN         -- ¿Requiere centro costo?
permitir_tercero             BOOLEAN         -- ¿Requiere tercero?
base_impositiva              BOOLEAN         -- ¿Es base impositiva?
desplazamiento_columna       TINYINT         -- Desplazamiento en reportes
permitir_banco               BOOLEAN         -- ¿Está en bancos?
permitir_moneda              BOOLEAN         -- ¿Multimoneda?
id_moneda                    VARCHAR(3)      -- Moneda por defecto
presupuesto                  DECIMAL(28,2)   -- Monto presupuestado
porcentaje_presupuesto       DECIMAL(28,2)   -- % presupuesto
permitir_monto               BOOLEAN         -- ¿Permite monto?
activo                       BOOLEAN         -- ¿Cuenta activa?
fecha_creacion               DATETIME        -- Fecha creación
id_cuenta_correccion         VARCHAR(24)     -- Cuenta de corrección relacionada
```

**Relación con Empresa:**
- Cada `con_plan_cuenta.company_id` pertenece a una única `cfg_company`
- Las cuentas de diferentes empresas NO se mezclan

---

### 3. **con_configuracion_linea_resultado** (Líneas Estado de Resultados)
Define la estructura de las líneas del estado de resultados para cada empresa.

#### Campos Clave:
```sql
id_linea_resultado           BIGINT          -- ID único
company_id                   BIGINT          -- Empresa
numero_linea                 SMALLINT        -- Número de línea (orden)
tipo_linea                   TINYINT         -- 0=Ingreso, 1=Costo, 2=Gasto
codigo_cuenta                VARCHAR(24)     -- Referencia a con_plan_cuenta
descripcion_linea            VARCHAR(80)     -- Descripción personalizada
nivel_indentacion            INT             -- Nivel de indentación
mostrar_subtotal             BOOLEAN         -- ¿Mostrar subtotal?
```

---

### 4. **con_configuracion_balance** (Líneas Estado Situación Financiera)
Define la estructura de líneas del balance general para cada empresa.

#### Campos Clave:
```sql
id_linea_balance             BIGINT          -- ID único
company_id                   BIGINT          -- Empresa
numero_linea                 SMALLINT        -- Número de línea
clase_balance                TINYINT         -- Clase (1-8 para diferentes secciones)
codigo_cuenta                VARCHAR(24)     -- Referencia a con_plan_cuenta
```

---

### 5. **con_periodo_contable** (Períodos Contables)
Períodos fiscales para cada empresa.

#### Campos Clave:
```sql
periodo_id                   SMALLINT        -- ID único del período
company_id                   BIGINT          -- Empresa
fecha_inicio                 DATE            -- Inicio período
fecha_cierre                 DATE            -- Cierre período
mes                          TINYINT         -- Mes (1-12)
anio                         INT             -- Año
estado                       VARCHAR(20)     -- ABIERTO, CERRADO, BLOQUEADO
es_periodo_actual            BOOLEAN         -- ¿Período actual?
```

---

## 🔗 Relaciones de Integridad Referencial

```
cfg_company
    ↓
    ├─→ con_configuracion_sistema (company_id)
    ├─→ con_plan_cuenta (company_id)
    ├─→ con_periodo_contable (company_id)
    ├─→ con_centro_costo (company_id)
    ├─→ con_poliza (company_id)
    └─→ con_configuracion_linea_resultado (company_id)
            ↓
            con_plan_cuenta (codigo_cuenta)

con_configuracion_linea_resultado
    ↓
    con_plan_cuenta (codigo_cuenta)

con_poliza
    ↓
    con_poliza_linea (poliza_id)
        ↓
        con_plan_cuenta (codigo_cuenta)
        con_centro_costo (id_centro_costo)
```

---

## 📝 Mejora de Nomenclatura (v2.1)

Se implementó un estándar de nomenclatura más conciso para mejorar legibilidad y mantenimiento:

| **Campo Anterior** | **Campo Nuevo** | **Razón** |
|---|---|---|
| `fecha_inicio_ejercicio` | `fecha_ini_ejer` | Menos verboso, mantiene claridad |
| `fecha_fin_ejercicio` | `fecha_fin_ejer` | Menos verboso, mantiene claridad |
| `meses_calculados` | `meses_calc` | Abreviatura estándar |
| `separador_codigo` | `sep_codigo` | Abreviatura común |
| `formato_cuentas` | `fmt_ctas` | Convención estándar |
| `formato_centros` | `fmt_centros` | Convención estándar |
| `symbol_saldo_acreedor` | `sym_acreedor` | Abreviatura común |
| `monto_maximo` | `mto_maximo` | Abreviatura común |
| `frecuencia_depreciacion` | `frec_deprec` | Abreviatura común |
| `ultima_depreciacion` | `fec_ult_deprec` | Consistencia de fecha |
| `codigo_cuenta_util_acumulada_historica` | `cod_util_acum_hist` | De 38 a 18 caracteres |
| `codigo_cuenta_util_acumulada_inflacion` | `cod_util_acum_inf` | De 38 a 17 caracteres |
| `titulo_estado_resultados` | `tit_est_result` | De 28 a 14 caracteres |
| `titulo_balance_general` | `tit_balance_gral` | De 27 a 16 caracteres |
| `descripcion_pasivo_capital` | `desc_pasiv_cap` | De 28 a 14 caracteres |

---

## 🚀 Scripting Útil

### Obtener configuración por empresa:
```sql
SELECT 
    c.config_id,
    e.code,
    e.name as empresa,
    c.fecha_ini_ejer,
    c.fecha_fin_ejer,
    c.fmt_ctas,
    c.sym_acreedor
FROM con_configuracion_sistema c
JOIN cfg_company e ON c.company_id = e.company_id
WHERE c.company_id = ?;
```

### Listar cuentas de una empresa:
```sql
SELECT 
    id_account as "Código",
    descripcion as "Descripción",
    tipo_cuenta as "Tipo",
    nivel as "Nivel",
    permitir_movimiento as "Movimiento"
FROM con_plan_cuenta
WHERE company_id = ?
ORDER BY id_account;
```

### Ver estructura estado resultados:
```sql
SELECT 
    numero_linea,
    tipo_linea,
    codigo_cuenta,
    descripcion_linea,
    nivel_indentacion
FROM con_configuracion_linea_resultado
WHERE company_id = ?
ORDER BY numero_linea;
```

---

## ✅ Checklist de Implementación

- [x] Crear tablas de configuración
- [x] Crear plan de cuentas por empresa
- [x] Implementar multi-empresa
- [x] Agregar configuración de balance y resultados
- [x] Mejorar nomenclatura de columnas
- [x] Documentar relaciones
- [ ] Crear vistas de reportes
- [ ] Implementar auditoria de cambios
- [ ] Agregar validaciones de integridad

---

**Versión:** 2.1  
**Última actualización:** 10 de diciembre de 2025  
**Creado por:** HODSOFT
