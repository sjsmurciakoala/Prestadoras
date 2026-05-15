# Plan de trabajo: modelo tarifario regulatorio escalable

## 1. Reset del enfoque

Este documento redefine el trabajo desde la base correcta:

1. Primero se define el modelo objetivo con base normativa.
2. Despues se analiza el sistema actual.
3. El mapeo desde lo actual hacia el modelo nuevo es un trabajo posterior.

La base normativa usada aqui es:

- `manual_contabilidad_regulatoria.pdf.docx`
- `PLAN DE ARBITRIOS 2026.pdf`

Este documento ya no toma como punto de partida:

- `public.servicios`
- `public.tarifas`
- `public.tarifas_contador`
- letras `A/B/C/D/M/U`
- campos legacy como `tipo`, `codigo`, `descricat`

Eso se analizara despues, en un documento de migracion o mapeo.

---

## 2. Lo que si nos dicen las fuentes regulatorias

### 2.1 Manual regulatorio ERSAPS

El manual contable/regulatorio deja clara una clasificacion minima obligatoria para ingresos y cuentas por cobrar:

- `Servicio de Agua Potable`
- `Servicio de Alcantarillado`
- `Servicios Colaterales Regulados`

Para `Servicio de Agua Potable` el manual desagrega por:

- `Con Medicion`
- `Sin Medicion`

y dentro de cada una por:

- `Domestico`
- `Comercial`
- `Industrial`
- `Gubernamental`

Para `Servicio de Alcantarillado` el manual desagrega por:

- `Domestico`
- `Comercial`
- `Industrial`
- `Gubernamental`

Ademas el manual separa expresamente:

- `Tasa por Servicios SVA (ERSAPS)`

Conclusion regulatoria:

- el eje principal no es la letra,
- el eje principal no es el campo `tipo`,
- y tampoco una tarifa aislada por cliente,
- sino la combinacion de `concepto facturable`, `categoria regulatoria`, `condicion de medicion` y `segmento tarifario` cuando aplique.

### 2.2 Plan de Arbitrios 2026

El Plan de Arbitrios aporta reglas de negocio complementarias para el cobro:

- el cobro de agua potable y alcantarillado, asi como servicios afines como conexiones, instalaciones, modificaciones, traslados, corte y otros, debe ejecutarse conforme a tarifas aprobadas;
- la `Tasa Ambiental` se define como un porcentaje sobre la tarifa del servicio de agua potable;
- existen cargos y servicios eventuales o afines al servicio principal;
- existen beneficios o ajustes legales, por ejemplo descuentos especiales regulados para ciertos abonados.

Conclusion operativa:

- no todo lo que sale en factura es el servicio principal,
- hay conceptos base,
- hay conceptos colaterales,
- hay recargos regulatorios,
- y hay beneficios o ajustes legales.

---

## 3. Que sabemos que esta mal hoy

Con lo ya visto del sistema actual, estos son los problemas estructurales:

### 3.1 Se mezclan dimensiones distintas en una sola tabla o clave

Hoy se mezclan en el mismo espacio conceptos como:

- categoria regulatoria,
- condicion de medicion,
- variante tarifaria,
- rango,
- tipo,
- letra,
- monto.

Eso hace que el modelo no sea claro ni auditable.

### 3.2 Las letras cargan significado que no deberian cargar

Letras como:

- `A`
- `B`
- `C`
- `D`
- `M`
- `U`

terminan usandose para expresar cosas distintas:

- subnivel comercial,
- tramo,
- plan,
- o simplemente una clave heredada.

Eso no es escalable ni regulatoriamente limpio.

### 3.3 Se confunde el servicio facturable con la regla tarifaria

Una cosa es:

- que conceptos pueden salir en factura

y otra distinta es:

- como se calcula cada concepto.

El sistema actual no lo separa con suficiente claridad.

### 3.4 Se mezcla lo recurrente con lo eventual

No es lo mismo:

- `Agua Potable`
- `Alcantarillado`

que:

- `Conexion`
- `Corte y reconexion`
- `Traslado`
- `Modificacion`

ni que:

- `Tasa Ambiental`
- `Tasa SVA ERSAPS`

Todos pueden terminar en la factura, pero no deben modelarse igual.

### 3.5 Se mezclan tarifa base, recargos y beneficios

No debe vivir en el mismo nivel logico:

- la tarifa base del servicio,
- un recargo regulatorio porcentual,
- y un descuento legal o beneficio.

Si se mezclan, despues no se puede mantener ni auditar bien.

---

## 4. Nomenclatura del modelo nuevo

El modelo final usara `adm_` en nombres de tabla.

Reglas de nombre:

- `snake_case`
- singular
- nombres cortos
- sin letras como eje de negocio
- sin tablas separadas por "con medidor" y "sin medidor"

Convencion especifica:

- las tablas del modulo usaran prefijo `adm_`
- las columnas no usaran ese prefijo
- las PK conservaran nombre funcional, por ejemplo `servicio_id`
- las FK conservaran nombre funcional, por ejemplo `tipo_servicio_id`

Esto significa que el modelo nuevo no tendra como diseno final:

- `servicios`
- `tarifas`
- `tarifas_contador`

Esas tablas solo se revisaran despues para migracion.

---

## 5. Tablas base del motor tarifario

### 5.1 `adm_servicio`

Catalogo maestro general de servicios o conceptos cobrables.

Esta tabla debe entenderse con criterio ERP:

- una sola tabla maestra de servicios;
- no solo para agua y alcantarillado;
- tambien para tasas y conceptos colaterales;
- sin necesidad de crear un catalogo separado por cada naturaleza.

Responsabilidad:

- definir todo lo que puede terminar en una linea de factura;
- servir como catalogo unico para motor tarifario, facturacion, app y reportes;
- permitir clasificar internamente el tipo de servicio.

Ejemplos iniciales:

- `Agua Potable`
- `Alcantarillado`
- `Conexion`
- `Corte y Reconexion`
- `Traslado`
- `Modificacion`
- `Agua Potable por Cisterna`
- `Tasa Ambiental`
- `Tasa SVA ERSAPS`
- `Otros Servicios Colaterales Regulados`

### 5.2 `adm_tipo_servicio`

Catalogo que clasifica la naturaleza del servicio dentro del maestro general.

Responsabilidad:

- distinguir que servicios son recurrentes;
- distinguir que servicios son eventuales;
- distinguir que servicios son cargos regulatorios derivados.

Catalogo base inicial:

- `1 = Servicio Base Recurrente`
- `2 = Servicio Colateral o Eventual`
- `3 = Cargo Regulatorio Derivado`

### 5.3 `adm_categoria_regulatoria`

Catalogo maestro del tipo de abonado o usuario.

Responsabilidad:

- clasificar al cliente bajo el esquema regulatorio;
- servir como criterio de seleccion tarifaria;
- servir como criterio de reporte regulatorio.

Catalogo base:

- `Domestico`
- `Comercial`
- `Industrial`
- `Gubernamental`

### 5.4 `adm_segmento_tarifario`

Catalogo maestro del segmento tarifario dentro de una categoria regulatoria.

Responsabilidad:

- capturar subniveles o segmentos reales de tarifa sin contaminar `adm_categoria_regulatoria`;
- soportar casos como `Domestica Baja/Media/Alta`, `Comercial Pequena/Mediana/Grande`;
- soportar casos especiales de negocio como `Preventiva`, `ENP` o `Tulian`;
- permitir que el cuadro tarifario resuelva tarifas APC sin volver a depender de letras legacy.

Catalogo base inicial:

- `DOMESTICA_BAJA`
- `DOMESTICA_MEDIA`
- `DOMESTICA_ALTA`
- `DOMESTICA_TULIAN`
- `PREVENTIVA_DOMESTICA`
- `COMERCIAL_PEQUENA`
- `COMERCIAL_MEDIANA`
- `COMERCIAL_GRANDE`
- `PREVENTIVA_COMERCIAL`
- `INDUSTRIAL_PEQUENA`
- `INDUSTRIAL_MEDIANA`
- `INDUSTRIAL_GRANDE`
- `INDUSTRIAL_ENP`
- `GUBERNAMENTAL_UNICA`
- `PUBLICA_UNICA`

### 5.5 `adm_condicion_medicion`

Catalogo maestro de la condicion de medicion.

Responsabilidad:

- indicar si el concepto se calcula con base en lectura;
- evitar separar la logica en tablas distintas;
- permitir conceptos donde la medicion no aplica.

Catalogo base:

- `1 = Con Medicion`
- `2 = Sin Medicion`
- `3 = No Aplica`

### 5.6 `adm_tipo_regla_tarifaria`

Catalogo maestro del mecanismo de calculo.

Responsabilidad:

- decir como se calcula un concepto;
- permitir un solo motor con distintos tipos de regla.

Catalogo base:

- `1 = Monto Fijo`
- `2 = Rango de Consumo`
- `3 = Porcentaje sobre otro concepto`
- `4 = Regla Especial`

### 5.7 `adm_tipo_ajuste_tarifario`

Catalogo maestro de ajustes posteriores al calculo base.

Responsabilidad:

- separar ajuste de tarifa base;
- soportar descuentos, topes, exoneraciones y beneficios.

Catalogo base inicial:

- `1 = Descuento`
- `2 = Tope`
- `3 = Exoneracion`
- `4 = Recargo`
- `5 = Beneficio Especial`

### 5.8 `adm_cuadro_tarifario`

Encabezado de la tarifa vigente.

Responsabilidad:

- representar la combinacion aprobada para un concepto;
- guardar vigencia y estado;
- amarrar concepto, categoria y condicion de medicion cuando aplique.

La seleccion tarifaria debe partir de:

- `adm_servicio`
- `adm_categoria_regulatoria`
- `adm_condicion_medicion`
- `adm_segmento_tarifario` cuando aplique
- `vigencia`

### 5.9 `adm_regla_tarifaria`

Detalle operativo del cuadro tarifario.

Responsabilidad:

- ejecutar el calculo del monto;
- manejar tarifa fija;
- manejar rangos de consumo;
- manejar porcentajes derivados;
- soportar reglas especiales.

### 5.10 `adm_ajuste_tarifario`

Detalle de ajustes que se aplican despues del calculo base.

Responsabilidad:

- aplicar descuento legal;
- aplicar tope;
- aplicar exoneracion;
- aplicar beneficio regulado;
- aplicar recargo posterior si corresponde.

### 5.11 `adm_cliente_servicio`

Relacion recurrente entre cliente y servicio regulado.

Responsabilidad:

- guardar los servicios permanentes del cliente;
- guardar el contexto regulatorio para resolver tarifa;
- no guardar letras, ni monto manual, ni tramo.

Debe usarse para:

- `Agua Potable`
- `Alcantarillado`

No todos los registros de `adm_servicio` deben vivir aqui.

Solo deben vivir aqui los que sean:

- recurrentes;
- asignables al cliente;
- base para la facturacion periodica.

### 5.12 Convenciones fisicas obligatorias

Todas las tablas nuevas del motor tarifario deben nacer con estas reglas:

#### Multiempresa

- todas las tablas del motor tarifario llevaran `company_id bigint not null`;
- `company_id` referenciara `public.cfg_company(company_id)`;
- las entidades nuevas deben implementar `ICompanyScopedEntity`;
- el modelo debe respetar el filtro tenant automatico de `SiadDbContext.Tenancy.cs`;
- las relaciones entre tablas hijas y maestras deben ser `tenant-safe`, es decir, amarradas por `company_id` y el id del padre.

#### Auditoria

Campos obligatorios:

- `created_at timestamptz not null default now()`
- `created_by varchar(100) not null default current_user`
- `updated_at timestamptz null`
- `updated_by varchar(100) null`

#### Estado

Convencion base:

- `status_id smallint not null default 1`
- `1 = activo`
- `0 = inactivo`

Se recomienda check constraint:

- `status_id in (0, 1)`

#### Llaves primarias

Cada tabla tendra PK propia:

- `<tabla>_id bigint generated always as identity primary key`

#### Uniques

Regla base:

- si la tabla tiene `codigo`, debe existir `unique (company_id, codigo)`
- si la tabla es de detalle ordenado, debe existir `unique (company_id, <padre_id>, orden)`

### 5.13 Diseno fisico inicial por tabla

#### `adm_tipo_servicio`

Columnas:

- `tipo_servicio_id bigint`
- `company_id bigint`
- `codigo varchar(30)`
- `nombre varchar(100)`
- `descripcion varchar(300) null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (tipo_servicio_id)`
- `unique (company_id, codigo)`
- `unique (company_id, nombre)`

#### `adm_servicio`

Columnas:

- `servicio_id bigint`
- `company_id bigint`
- `tipo_servicio_id bigint`
- `codigo varchar(30)`
- `nombre varchar(150)`
- `descripcion varchar(300) null`
- `es_asignable_cliente boolean not null default false`
- `usa_condicion_medicion boolean not null default false`
- `facturable_app boolean not null default false`
- `app_orden int not null default 0`
- `permite_evento boolean not null default false`
- `genera_por_regla boolean not null default false`
- `cont_account_id bigint null`
- `orden_visual int not null default 0`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (servicio_id)`
- `unique (company_id, codigo)`
- `unique (company_id, nombre)`
- `index (company_id, facturable_app, app_orden)`
- `fk (company_id, tipo_servicio_id) -> adm_tipo_servicio`
- `fk (company_id, cont_account_id) -> con_plan_cuentas` si se decide amarre contable desde esta tabla

Nota:

- `usa_condicion_medicion` no significa que siempre exista medidor;
- significa que el servicio participa en la logica `con medicion / sin medicion / no aplica`.
- `facturable_app` y `app_orden` si vale la pena heredarlos porque hoy tienen uso real en WS, UI y administracion del catalogo.
- `es_servicio_base` no debe heredarse como booleano; en el modelo nuevo esa intencion queda mejor expresada por `tipo_servicio_id`, `es_asignable_cliente` y la propia logica del cuadro tarifario.

#### `adm_categoria_regulatoria`

Columnas:

- `categoria_regulatoria_id bigint`
- `company_id bigint`
- `codigo varchar(30)`
- `nombre varchar(100)`
- `descripcion varchar(300) null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (categoria_regulatoria_id)`
- `unique (company_id, codigo)`
- `unique (company_id, nombre)`

#### `adm_segmento_tarifario`

Columnas:

- `segmento_tarifario_id bigint`
- `company_id bigint`
- `categoria_regulatoria_id bigint null`
- `codigo varchar(40)`
- `nombre varchar(120)`
- `descripcion varchar(300) null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (segmento_tarifario_id)`
- `unique (company_id, codigo)`
- `index (company_id, categoria_regulatoria_id)`
- `fk (company_id, categoria_regulatoria_id) -> adm_categoria_regulatoria`

Nota:

- `adm_segmento_tarifario` existe porque con APC no basta con `categoria_regulatoria`;
- se necesita una capa adicional para subniveles y segmentos como `Baja`, `Media`, `Alta`, `Pequena`, `Mediana`, `Grande`, `Tulian`, `Preventiva` y `ENP`;
- esta tabla reemplaza el uso semantico de letras como `A/B/C/D/M/U`.

#### `adm_condicion_medicion`

Columnas:

- `condicion_medicion_id bigint`
- `company_id bigint`
- `codigo varchar(30)`
- `nombre varchar(100)`
- `descripcion varchar(300) null`
- `requiere_lectura boolean not null default false`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (condicion_medicion_id)`
- `unique (company_id, codigo)`
- `unique (company_id, nombre)`

#### `adm_tipo_regla_tarifaria`

Columnas:

- `tipo_regla_tarifaria_id bigint`
- `company_id bigint`
- `codigo varchar(30)`
- `nombre varchar(100)`
- `descripcion varchar(300) null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (tipo_regla_tarifaria_id)`
- `unique (company_id, codigo)`
- `unique (company_id, nombre)`

#### `adm_tipo_ajuste_tarifario`

Columnas:

- `tipo_ajuste_tarifario_id bigint`
- `company_id bigint`
- `codigo varchar(30)`
- `nombre varchar(100)`
- `descripcion varchar(300) null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (tipo_ajuste_tarifario_id)`
- `unique (company_id, codigo)`
- `unique (company_id, nombre)`

#### `adm_cuadro_tarifario`

Columnas:

- `cuadro_tarifario_id bigint`
- `company_id bigint`
- `servicio_id bigint`
- `categoria_regulatoria_id bigint null`
- `condicion_medicion_id bigint null`
- `segmento_tarifario_id bigint null`
- `codigo varchar(40)`
- `nombre varchar(150)`
- `descripcion varchar(300) null`
- `vigencia_desde date not null`
- `vigencia_hasta date null`
- `prioridad int not null default 1`
- `referencia_normativa varchar(150) null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (cuadro_tarifario_id)`
- `unique (company_id, codigo)`
- `index (company_id, servicio_id)`
- `index (company_id, vigencia_desde, vigencia_hasta)`
- `fk (company_id, servicio_id) -> adm_servicio`
- `fk (company_id, categoria_regulatoria_id) -> adm_categoria_regulatoria`
- `fk (company_id, condicion_medicion_id) -> adm_condicion_medicion`
- `fk (company_id, segmento_tarifario_id) -> adm_segmento_tarifario`

Nota:

- aqui vive la vigencia del plan;
- si una tarifa aplica a todos los abonados sin distinguir categoria, condicion o segmento, esos campos pueden ser null.

#### `adm_regla_tarifaria`

Columnas:

- `regla_tarifaria_id bigint`
- `company_id bigint`
- `cuadro_tarifario_id bigint`
- `tipo_regla_tarifaria_id bigint`
- `orden int not null`
- `consumo_minimo numeric(18,4) null`
- `consumo_maximo numeric(18,4) null`
- `monto_fijo numeric(18,4) null`
- `monto_unitario numeric(18,6) null`
- `porcentaje numeric(18,6) null`
- `servicio_referencia_id bigint null`
- `parametros jsonb null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (regla_tarifaria_id)`
- `unique (company_id, cuadro_tarifario_id, orden)`
- `index (company_id, tipo_regla_tarifaria_id)`
- `fk (company_id, cuadro_tarifario_id) -> adm_cuadro_tarifario`
- `fk (company_id, tipo_regla_tarifaria_id) -> adm_tipo_regla_tarifaria`
- `fk (company_id, servicio_referencia_id) -> adm_servicio`

Checks recomendados:

- si el tipo es `Rango de Consumo`, debe existir al menos `consumo_minimo`
- si el tipo es `Monto Fijo`, debe existir `monto_fijo`
- si el tipo es `Porcentaje sobre otro concepto`, deben existir `porcentaje` y `servicio_referencia_id`

#### `adm_ajuste_tarifario`

Columnas:

- `ajuste_tarifario_id bigint`
- `company_id bigint`
- `cuadro_tarifario_id bigint`
- `tipo_ajuste_tarifario_id bigint`
- `orden int not null`
- `servicio_referencia_id bigint null`
- `monto_fijo numeric(18,4) null`
- `porcentaje numeric(18,6) null`
- `tope_maximo numeric(18,4) null`
- `condicion_codigo varchar(50) null`
- `parametros jsonb null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (ajuste_tarifario_id)`
- `unique (company_id, cuadro_tarifario_id, orden)`
- `fk (company_id, cuadro_tarifario_id) -> adm_cuadro_tarifario`
- `fk (company_id, tipo_ajuste_tarifario_id) -> adm_tipo_ajuste_tarifario`
- `fk (company_id, servicio_referencia_id) -> adm_servicio`

#### `adm_cliente_servicio`

Columnas:

- `cliente_servicio_id bigint`
- `company_id bigint`
- `cliente_id bigint`
- `servicio_id bigint`
- `categoria_regulatoria_id bigint null`
- `condicion_medicion_id bigint null`
- `segmento_tarifario_id bigint null`
- `medidor_id bigint null`
- `cuadro_tarifario_id bigint null`
- `fecha_alta date not null`
- `fecha_baja date null`
- `status_id smallint`
- `created_at`
- `created_by`
- `updated_at`
- `updated_by`

Indices y reglas:

- `pk (cliente_servicio_id)`
- `index (company_id, cliente_id)`
- `index (company_id, servicio_id)`
- `unique (company_id, cliente_id, servicio_id)` filtrado a activos si se desea una sola relacion activa por servicio
- `fk (company_id, servicio_id) -> adm_servicio`
- `fk (company_id, categoria_regulatoria_id) -> adm_categoria_regulatoria`
- `fk (company_id, condicion_medicion_id) -> adm_condicion_medicion`
- `fk (company_id, segmento_tarifario_id) -> adm_segmento_tarifario`
- `fk (company_id, cuadro_tarifario_id) -> adm_cuadro_tarifario`

Nota:

- `cuadro_tarifario_id` aqui debe entenderse como override opcional;
- la resolucion normal del motor puede hacerse por servicio + categoria + condicion + segmento + vigencia.

### 5.14 Semilla funcional inicial

#### Semilla de `adm_tipo_servicio`

| codigo | nombre | descripcion |
|---|---|---|
| `BASE` | Servicio Base Recurrente | Servicio permanente que puede vivir en `adm_cliente_servicio` y participar en facturacion periodica. |
| `EVENTUAL` | Servicio Colateral o Eventual | Servicio que se cobra por ocurrencia o gestion puntual. |
| `DERIVADO` | Cargo Regulatorio Derivado | Concepto que nace por regla a partir de otro servicio base. |

#### Semilla inicial de `adm_servicio`

| codigo | nombre | tipo_servicio | es_asignable_cliente | usa_condicion_medicion | facturable_app | app_orden | permite_evento | genera_por_regla | comentario funcional |
|---|---|---|---:|---:|---:|---:|---:|---:|---|
| `AGUA_POTABLE` | Agua Potable | `BASE` | 1 | 1 | 1 | 10 | 0 | 0 | Servicio base principal. Puede facturarse con medicion o sin medicion. |
| `ALCANTARILLADO` | Alcantarillado | `BASE` | 1 | 0 | 1 | 20 | 0 | 0 | Servicio base recurrente. Normalmente usa `No Aplica` en condicion de medicion. |
| `CONEXION` | Conexion | `EVENTUAL` | 0 | 0 | 0 | 0 | 1 | 0 | Cobro por evento. |
| `INSTALACION` | Instalacion | `EVENTUAL` | 0 | 0 | 0 | 0 | 1 | 0 | Cobro por evento. |
| `MODIFICACION` | Modificacion | `EVENTUAL` | 0 | 0 | 0 | 0 | 1 | 0 | Cobro por evento. |
| `TRASLADO` | Traslado | `EVENTUAL` | 0 | 0 | 0 | 0 | 1 | 0 | Cobro por evento. |
| `CORTE_RECONEXION` | Corte y Reconexion | `EVENTUAL` | 0 | 0 | 0 | 0 | 1 | 0 | Cobro colateral regulado. |
| `AGUA_CISTERNA` | Agua Potable por Cisterna | `EVENTUAL` | 0 | 0 | 0 | 0 | 1 | 0 | Servicio colateral regulado. |
| `OTROS_COLATERALES` | Otros Servicios Colaterales Regulados | `EVENTUAL` | 0 | 0 | 0 | 0 | 1 | 0 | Cajon regulatorio para conceptos colaterales aprobados. |
| `TASA_AMBIENTAL` | Tasa Ambiental | `DERIVADO` | 0 | 1 | 0 | 0 | 0 | 1 | Se genera por regla como cargo derivado de agua potable segun cuadro tarifario vigente. |
| `TASA_SVA_ERSAPS` | Tasa SVA ERSAPS | `DERIVADO` | 0 | 0 | 0 | 0 | 0 | 1 | Se genera por regla como concepto regulatorio separado. |

#### Criterio operativo ya cerrado

- solo `AGUA_POTABLE` y `ALCANTARILLADO` entran inicialmente a `adm_cliente_servicio`;
- los servicios `EVENTUAL` no viven como relacion permanente del cliente;
- los servicios `DERIVADO` no se asignan manualmente al cliente, se generan por regla;
- si luego aparece otro cobro permanente regulado, se evaluara si entra como `BASE`.

#### Semilla de `adm_categoria_regulatoria`

| codigo | nombre | descripcion |
|---|---|---|
| `DOMESTICO` | Domestico | Usuario o abonado de uso residencial. |
| `COMERCIAL` | Comercial | Usuario o abonado de actividad comercial o de servicios. |
| `INDUSTRIAL` | Industrial | Usuario o abonado de actividad industrial. |
| `GUBERNAMENTAL` | Gubernamental | Usuario o abonado del sector publico o gubernamental. |

#### Semilla de `adm_segmento_tarifario`

| categoria_regulatoria | codigo | nombre | descripcion |
|---|---|---|---|
| `DOMESTICO` | `DOMESTICA_BAJA` | Domestica Baja | Segmento tarifario domestico sin medicion. |
| `DOMESTICO` | `DOMESTICA_MEDIA` | Domestica Media | Segmento tarifario domestico sin medicion. |
| `DOMESTICO` | `DOMESTICA_ALTA` | Domestica Alta | Segmento tarifario domestico sin medicion. |
| `DOMESTICO` | `DOMESTICA_TULIAN` | Domestica Tulian | Segmento tarifario domestico especial. |
| `DOMESTICO` | `PREVENTIVA_DOMESTICA` | Preventiva Domestica | Segmento tarifario domestico preventivo. |
| `COMERCIAL` | `COMERCIAL_PEQUENA` | Comercial Pequena | Segmento tarifario comercial sin medicion. |
| `COMERCIAL` | `COMERCIAL_MEDIANA` | Comercial Mediana | Segmento tarifario comercial sin medicion. |
| `COMERCIAL` | `COMERCIAL_GRANDE` | Comercial Grande | Segmento tarifario comercial sin medicion. |
| `COMERCIAL` | `PREVENTIVA_COMERCIAL` | Preventiva Comercial | Segmento tarifario comercial preventivo. |
| `INDUSTRIAL` | `INDUSTRIAL_PEQUENA` | Industrial Pequena | Segmento tarifario industrial sin medicion. |
| `INDUSTRIAL` | `INDUSTRIAL_MEDIANA` | Industrial Mediana | Segmento tarifario industrial sin medicion. |
| `INDUSTRIAL` | `INDUSTRIAL_GRANDE` | Industrial Grande | Segmento tarifario industrial sin medicion. |
| `INDUSTRIAL` | `INDUSTRIAL_ENP` | Industrial ENP | Segmento tarifario industrial especial para terminales portuarias. |
| `GUBERNAMENTAL` | `GUBERNAMENTAL_UNICA` | Publica Unica | Segmento tarifario gubernamental sin medicion. |
| `GUBERNAMENTAL` | `PUBLICA_UNICA` | Publica Unica Especial | Segmento tarifario gubernamental especial usado por APC en tarifas sin medicion. |

#### Semilla de `adm_condicion_medicion`

| codigo | nombre | requiere_lectura | descripcion |
|---|---|---:|---|
| `CON_MEDICION` | Con Medicion | 1 | El monto depende de lectura o consumo facturable. |
| `SIN_MEDICION` | Sin Medicion | 0 | El monto no depende de lectura y se resuelve por regla fija o especial. |
| `NO_APLICA` | No Aplica | 0 | La condicion de medicion no forma parte del calculo del concepto. |

#### Regla operativa de `adm_condicion_medicion`

- `AGUA_POTABLE` puede usar `CON_MEDICION` o `SIN_MEDICION`;
- `ALCANTARILLADO` inicialmente usara `NO_APLICA`, salvo que una ordenanza futura obligue otro comportamiento;
- `TASA_AMBIENTAL` usara la misma condicion de medicion resuelta para agua potable;
- `TASA_SVA_ERSAPS` y servicios eventuales usaran `NO_APLICA` mientras no exista una regla distinta confirmada.

#### Semilla de `adm_tipo_regla_tarifaria`

| codigo | nombre | descripcion |
|---|---|---|
| `MONTO_FIJO` | Monto Fijo | La tarifa se resuelve con un valor fijo por periodo o por evento. |
| `RANGO_CONSUMO` | Rango de Consumo | La tarifa se resuelve por tramos o bloques de consumo. |
| `PORCENTAJE_SERVICIO` | Porcentaje sobre otro servicio | La tarifa se calcula como porcentaje de otro servicio base o derivado. |
| `REGLA_ESPECIAL` | Regla Especial | La tarifa requiere formula o parametros especiales fuera de los patrones anteriores. |

#### Regla operativa de `adm_tipo_regla_tarifaria`

- `AGUA_POTABLE` con medicion normalmente usara `RANGO_CONSUMO`;
- `AGUA_POTABLE` sin medicion normalmente usara `MONTO_FIJO` o `REGLA_ESPECIAL`;
- `ALCANTARILLADO` inicialmente usara `MONTO_FIJO` o `REGLA_ESPECIAL`;
- `TASA_AMBIENTAL` en el seed APC usara `RANGO_CONSUMO` para medido y `MONTO_FIJO` para no medido;
- `TASA_SVA_ERSAPS` se dejara listo para `PORCENTAJE_SERVICIO` o `REGLA_ESPECIAL`, pendiente de monto confirmado;
- `CONEXION`, `CORTE_RECONEXION`, `TRASLADO`, `MODIFICACION`, `AGUA_CISTERNA` normalmente usaran `MONTO_FIJO`.

#### Semilla de `adm_tipo_ajuste_tarifario`

| codigo | nombre | descripcion |
|---|---|---|
| `DESCUENTO` | Descuento | Rebaja porcentual o fija sobre el resultado base. |
| `TOPE` | Tope | Limite maximo de aplicacion del ajuste o beneficio. |
| `EXONERACION` | Exoneracion | Exclusion total o parcial del cobro bajo una condicion normativa. |
| `RECARGO` | Recargo | Incremento adicional posterior al calculo base. |
| `BENEFICIO_ESPECIAL` | Beneficio Especial | Ajuste regulado que no debe mezclarse con la tarifa base. |

#### Regla operativa de `adm_tipo_ajuste_tarifario`

- inicialmente el caso mas claro es `BENEFICIO_ESPECIAL` + `TOPE` para adulto mayor;
- no se sembraran todavia ajustes no confirmados por norma;
- los ajustes se modelaran separados de la tarifa base aunque se reflejen en la misma factura.

#### Criterio de mapeo APC para seed inicial

- `tarifas.tipo = 1` se mapeara a `AGUA_POTABLE` con `SIN_MEDICION`;
- `tarifas.tipo = 2` se mapeara a `ALCANTARILLADO` con `NO_APLICA`;
- `tarifas.tipo = 3` se mapeara a `TASA_AMBIENTAL` con `SIN_MEDICION`;
- `tarifas.tipo = 4` corresponde a `TASA_SVA_ERSAPS`, pero no se sembrara hasta confirmar el monto;
- `tarifas.tipo = 5` corresponde a gestion legal legacy y queda fuera del seed inicial del motor;
- `tarifas_contador.tipo = 1` se mapeara a `AGUA_POTABLE` con `CON_MEDICION`;
- `tarifas_contador.cuota3` se mapeara a `TASA_AMBIENTAL` con `CON_MEDICION`;
- el mapeo APC a categoria/segmento inicial se resolvera asi:
  - `1` -> `DOMESTICO`
  - `2` -> `COMERCIAL`
  - `3` -> `INDUSTRIAL`
  - `4` -> `GUBERNAMENTAL`
  - `5` -> `GUBERNAMENTAL` + `PUBLICA_UNICA`
  - `6` -> `INDUSTRIAL` + `INDUSTRIAL_ENP`
  - `7` -> `COMERCIAL` + `PREVENTIVA_COMERCIAL`
  - `8` -> `DOMESTICO` + `PREVENTIVA_DOMESTICA`

---

## 6. Como funcionaria el motor tarifario

### 6.1 Entrada

El motor recibe:

- cliente;
- fecha de facturacion;
- servicios recurrentes activos del cliente;
- lectura y consumo cuando aplique;
- eventos del periodo cuando existan.

### 6.2 Resolucion del servicio base

Para cada servicio recurrente:

1. identifica el `adm_servicio`;
2. toma la `adm_categoria_regulatoria` del cliente;
3. toma la `adm_condicion_medicion`;
4. busca el `adm_cuadro_tarifario` vigente;
5. ejecuta la `adm_regla_tarifaria`.

### 6.3 Casos de calculo

- `Agua Potable con Medicion`
  usa rango de consumo o regla especial.

- `Agua Potable sin Medicion`
  usa monto fijo o regla especial.

- `Alcantarillado`
  usa monto fijo o regla especial segun el cuadro tarifario.

### 6.4 Servicios derivados o cargos derivados

Despues del servicio base, el motor puede generar:

- `Tasa Ambiental`
- `Tasa SVA ERSAPS`
- otros conceptos derivados regulados

Estos no tienen que vivir como servicio recurrente manual del cliente si la regla los genera automaticamente.

### 6.5 Servicios eventuales

El catalogo `adm_servicio` tambien puede contener servicios eventuales:

- conexion
- corte
- reconexion
- traslado
- modificacion
- cisterna

Estos conceptos entran a la factura como lineas por evento, no como servicio base recurrente.

### 6.6 Ajustes

Al final, el motor revisa `adm_ajuste_tarifario`.

Ejemplo claro:

- descuento de adulto mayor sobre la factura de agua, con limite regulado.

### 6.7 Salida

El motor devuelve lineas de factura con trazabilidad:

- concepto cobrado;
- cuadro tarifario aplicado;
- regla usada;
- ajuste aplicado si hubo;
- indicador de si la linea es base, derivada o por evento.

---

## 7. Decisiones de diseno para que sea escalable

### 7.1 No usar letras en la logica

No se modelara con:

- `A`
- `B`
- `C`
- `D`
- `M`
- `U`

Si existen en historico, se trataran despues como dato de migracion.

### 7.2 Usar enteros para catalogos y estados

Convencion:

- `1 = Activo`
- `0 = Inactivo`

### 7.3 Un solo maestro y un solo motor

No existiran:

- dos catalogos maestros de servicios;
- dos motores separados por tabla.

La diferencia entre medido y no medido la resolveran:

- `adm_condicion_medicion`
- `adm_tipo_regla_tarifaria`
- `adm_regla_tarifaria`

La diferencia entre servicio base, eventual y derivado la resolvera:

- `adm_tipo_servicio`

### 7.4 Separar siempre tres capas

- tarifa base
- concepto derivado o por evento
- ajuste posterior

### 7.5 No contaminar el nucleo regulatorio

Si luego el negocio necesita:

- preventiva
- ENP
- multifamiliar
- plan especial

eso debe resolverse como capa adicional del negocio, no dentro del nucleo regulatorio minimo.

---

## 8. Lo que no vamos a decidir todavia

Todavia no se define en este documento:

- como mapear `public.servicios` al modelo nuevo;
- como migrar `public.tarifas`;
- como migrar `public.tarifas_contador`;
- como interpretar `tipo`, `codigo`, letras o variantes actuales;
- como adaptar la UI actual;
- como postear contablemente cada concepto.

Todo eso va despues.

El objetivo aqui es fijar primero el modelo correcto.

---

## 9. Proximos pasos

### Fase 1. Cerrar el modelo normativo

Confirmar y congelar:

- catalogo maestro `adm_servicio`;
- catalogo `adm_tipo_servicio`;
- catalogo de categorias regulatorias;
- catalogo de condicion de medicion;
- tipos de regla tarifaria;
- tipos de ajuste tarifario.

### Fase 2. Disenar el modelo fisico

Definir:

- nombres finales de tablas;
- columnas;
- relaciones;
- constraints;
- reglas de vigencia;
- reglas de generacion de lineas de factura.

### Fase 3. Mapear el sistema actual

Recien despues:

- `servicios`
- `tarifas`
- `tarifas_contador`
- app lectora
- WS
- facturacion actual

se comparan contra este modelo nuevo.

---

## 10. Decision central ya cerrada

La tarifa nueva se manejara asi:

- primero por `adm_servicio`,
- clasificado por `adm_tipo_servicio`,
- luego por `categoria regulatoria`,
- luego por `condicion de medicion` cuando aplique,
- luego por `segmento tarifario` cuando aplique,
- con `vigencia`,
- con una `regla tarifaria` explicita,
- y con `ajustes` separados de la tarifa base.

Ese sera el nucleo escalable del nuevo modelo.

---

## 11. Estado actual del prototipo

Hasta este punto ya quedaron preparados estos artefactos:

- `20260416_adm_motor_tarifario_core.sql`
  - crea tablas nuevas del motor tarifario y siembra catalogos base.
- `20260416_adm_motor_tarifario_apc_seed.sql`
  - siembra cuadros y reglas APC para:
    - agua potable sin medicion,
    - alcantarillado,
    - tasa ambiental sin medicion,
    - agua potable con medicion,
    - tasa ambiental con medicion.
- `20260417_adm_motor_tarifario_ersaps_seed.sql`
  - siembra cuadros y reglas derivadas para `TASA_SVA_ERSAPS`
    usando `configuracioncobroadicional.csv` como fuente de compatibilidad
    con la configuracion actual del app.
- `20260417_add_sp_adm_resolver_tarifa_cliente_servicio.sql`
  - crea un resolver de prueba para validar un `adm_cliente_servicio` en una fecha dada
    y devolver el cuadro resuelto, las reglas aplicadas y el monto calculado por regla.

### 11.1 Objetivo del siguiente paso

El siguiente paso ya no es sembrar mas catalogos.

Ahora corresponde:

- insertar registros reales o de prueba en `adm_cliente_servicio`;
- ejecutar `sp_adm_resolver_tarifa_cliente_servicio`;
- validar casos base:
  - agua domestico con medicion,
  - agua domestico sin medicion,
  - alcantarillado,
  - tasa ambiental.

### 11.2 Lo que sigue despues de validar

Solo despues de validar el resolver se recomienda avanzar con:

- `adm_ajuste_tarifario` para adulto mayor / jubilado;
- resolver derivado para `TASA_SVA_ERSAPS` y `TASA_AMBIENTAL`
  a partir de los servicios base ya resueltos;
- integracion del motor nuevo con el flujo de facturacion.

### 11.3 Criterio cerrado para cobros adicionales legacy

Con la revision de `configuracioncobroadicional.csv` y del flujo actual del app
queda cerrado este criterio:

- `IdConcepto = 2` representa `ALCANTARILLADO`;
- `IdConcepto = 3` representa `TASA_AMBIENTAL`;
- `IdConcepto = 4` representa `TASA_SVA_ERSAPS`.

Para el motor nuevo:

- `ALCANTARILLADO` se mantiene con cuadros APC explicitos;
- `TASA_AMBIENTAL` se mantiene con cuadros APC explicitos;
- `TASA_SVA_ERSAPS` se modela como `PORCENTAJE_SERVICIO`
  derivado de `AGUA_POTABLE` y `ALCANTARILLADO`,
  usando la configuracion legacy como fuente inicial.
