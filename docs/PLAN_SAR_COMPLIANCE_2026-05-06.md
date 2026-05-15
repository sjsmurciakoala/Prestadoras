# Plan SAR-Compliance — modelo NC/ND V3 + datos fiscales obligatorios

**Fecha:** 2026-05-05 (anticipado al Sprint 2 día 2 originalmente programado para 2026-05-06).
**Versión:** v4 — actualizada 2026-05-09 con cierre Sprint 2 al 100%.
**Estado:** **Sprint 2 cerrado al 100%**. CAI completo, snapshot V3_2 con saldos dinámicos, mono-sucursal aplicada (eliminado adm_establecimiento). **Sprint 3 (NC/ND + motor unificado + reglas) arranca 2026-05-12.**
**Base legal:** SAR Honduras, Acuerdo 481-2017 + reformas 609-2017, 725-2018, 817-2018.

---

## ✅ Estado de implementación al 2026-05-09 (Sprint 2 cerrado)

### Aplicado en PROD

- ✅ Tabla `cfg_tipo_documento_fiscal` (10 tipos SAR)
- ✅ Tabla `cfg_motivo_anulacion` (7 motivos)
- ✅ Tabla `cfg_estado_documento_fiscal` (4 estados)
- ✅ Tabla `cfg_cai_estado` (5 estados CAI lookup numérico: DISPONIBLE, EN_USO, VIGENTE, VENCIDA, ANULADA) [09-may]
- ✅ ~~Tabla `adm_establecimiento`~~ **ELIMINADA 09-may**: el sistema es multi-empresa pero **mono-sucursal**. El "establecimiento" SAR es solo el código `EEE` en `adm_cai_facturacion.establecimiento_codigo` (texto libre 3 dígitos autorizado por SAR). Ver `Database/ddl_v3/20260508_drop_adm_establecimiento.sql`.
- ✅ ALTER `adm_cai_facturacion` con `establecimiento_codigo` (texto), `tipo_documento_fiscal_id`, `fecha_limite_emision`, `leyenda_rango`, `punto_emision` (PPP), `correlativo_actual`, `estado_id` (lookup) + backfill. `establecimiento_id` removida 09-may.
- ✅ ALTER `factura` con 9 campos fiscales + backfill snapshot
- ✅ `fn_adm_validar_cai_emitible(company_id, cai_id)`
- ✅ Multi-tenancy: `company_id NOT NULL` en `factura`, `factura_detalle`, `transaccion_abonado`, `historicomedicion`, `maestro_medidor`.
- ✅ Bug fix `sp_obtener_cliente_saldo` (`WHERE estado='A'`)
- ✅ Overload `sp_obtener_cliente_saldo(p_company_id, ...)` y `sp_obtener_cliente_saldo_servicio_detalle(p_company_id, ...)` multi-empresa [09-may]. Las firmas viejas quedan vivas con [DEPRECATED].
- ✅ `sp_lectura_v3` con `company_id` en 4 INSERTs
- ✅ `sp_adm_calcular_factura_lectura` con filtro `company_id` en 2 reads de historicomedicion
- ✅ `sp_adm_generar_snapshot_offline_cliente_lectura` v2 [09-may]: contract_version `OFFLINE_SNAPSHOT_V3_2`, incluye `saldo_anterior_total` y `saldos_por_servicio` (jsonb dinámico). Smoke validado contra cliente real (102820, saldo L. 331.11 con desglose AGUA 199.27 + ALCANT 119.56 + AMBIENTAL 5.90 + SVA 6.38).
- ✅ Entidades EF de 5 tablas alteradas + 4 entidades nuevas (sin adm_establecimiento) + `SiadDbContext.SarCompliance.cs`
- ✅ 13 INSERTs vivos vía EF patcheados con `company_id` en 6 services
- ✅ **CAI offline UI completa** [08-may]: combo Estado, badges clickables por estado, ColumnChooser DevExpress, search Enter, filtros, prefijo `EEE-PPP-TD-NNNNNNNN` preview, leyenda fiscal SAR de 3 líneas autogenerada, anular/reactivar rápido, **Vencida read-only** (UI + service defensivo).
- ✅ Reglas efectivas de estado CAI (defensa profunda en SELECT y write): ANULADA gana sobre fecha; vigencia_hasta < hoy fuerza VENCIDA; GuardarCai bloquea editar Vencida salvo extensión de fecha.
- ✅ ~~Vista `/establecimientos`~~ **ELIMINADA 09-may** (decisión mono-sucursal)
- ✅ ~~Página `/contabilidad/reglas-integracion`~~ **ELIMINADA 09-may** (decisión 2026-05-05, era para lectura móvil legacy ya retirada)

### App Android (APK debug compilado, distribuir a 5 lectores)

- ✅ `VersionBD` 11→12 con auto-migración SQLite
- ✅ Tabla `MedidorSaldoServicio(IdMedidor, ServicioCodigo, ServicioNombre, SaldoAnterior)` PK compuesta
- ✅ `DescargarOfflineSnapshotV3ABD` puebla la tabla al guardar cada snapshot V3_2
- ✅ `GetCobrosAtrasadosPorMedidor` lee dinámico (N items reales del cliente, no 12 hardcoded)
- ✅ Estructura SAR ya presente en `ImprimirFactura.java` y `ImpresoraUniversal.java`: RTN emisor, FACTURA# `EEE-PPP-TD-NNNNNNNN`, RTN cliente, CAI 32-hex, Rango de enumeración, Fecha límite emisión, Original/Copia
- ⚠️ Bug menor pendiente Sprint 3: `CampoCAI_FechaEmision` se setea con `new Date()` (fecha de descarga) en lugar de `vigencia_hasta` del snapshot. Fix de ~10 min.

### Pendiente Sprint 3 (días 1-3 del 16-may al 18-may)

- ⏳ Tablas `adm_nota_credito` + `adm_nota_credito_detalle` (§2.3-2.4)
- ⏳ Tablas `adm_nota_debito` + `adm_nota_debito_detalle` (§2.5)
- ⏳ SP `sp_adm_emitir_nota_credito` (§3.1)
- ⏳ SP `sp_adm_emitir_nota_debito` (§3.2)
- ⏳ Vista Blazor para emitir NC desde portal (§7)
- ⏳ Modificación `sp_lectura_v3` con snapshot completo del establecimiento (rtn_emisor, razon_social_emisor, direccion_emisor) + llamada a `fn_adm_validar_cai_emitible` antes de tomar correlativo

### Pendiente operativo del usuario (no bloquea código)

- 🔧 Actualizar `cfg_company.tax_id` con RTN real (14 dígitos) de cada empresa antes de emitir factura legal
- 🔧 Actualizar `adm_establecimiento` con dirección fiscal real, código de establecimiento SAR y datos de contacto (vía `/establecimientos`)
- 🔧 Reclasificar `adm_cai_facturacion.tipo_documento_fiscal_id` si la empresa emite tipos distintos a Factura (default = 1)

---

## Premisas (correcciones del feedback v1)

1. **Sistema multi-empresa, multi-establecimiento** desde el diseño — no se asume una sola empresa ni un solo establecimiento. Cada empresa puede tener N establecimientos, cada uno con sus datos fiscales propios.
2. **Cada empresa puede emitir cualquier tipo de documento fiscal** (Factura, Recibo, NC, ND, etc.) — no se restringe a Factura.
3. **Un CAI ampara un único tipo de documento** (regla SAR). Si una empresa emite Factura y NC, necesita 2 CAIs distintos. No hay "bloque separado por tipo dentro del mismo CAI".
4. **`cfg_company` ya tiene los datos fiscales de la empresa** (`tax_id`, `legal_name`, `address`, `phone`, `email`). NO se altera.
5. **Datos del establecimiento** (que pueden diferir de la empresa madre) se modelan en nueva tabla `adm_establecimiento`.
6. **PROD actual es solo de pruebas** → backfill simple, no hay datos en vivo a preservar.

## Alcance

Cubre los **5 gaps rojos bloqueantes** + 1 gap amarillo cercano del cuadro del [PLAN_ENTREGA_2026-05-25.md](PLAN_ENTREGA_2026-05-25.md):

| Gap | Resuelve |
|-----|----------|
| #1 | RTN emisor en `adm_cai_facturacion` y `factura` |
| #2 | Leyendas CAI (rango autorizado, fecha límite emisión) persistidas |
| #3 | Modelo NC/ND V3 (tablas nuevas) |
| #4 | NC/ND con referencia a `factura_origen_id` |
| #5 | NC/ND con motivo estructurado (`motivo_anulacion_id`) |
| #10 | `tipo_documento_fiscal` explícito en cada documento |

**Fuera de alcance** (queda para Sprint 3 o post-25):
- Refactor del motor de facturación (`sp_adm_facturar` unificado).
- Recargo mora + descuento tercera edad.
- Tabla auditoría fiscal y `tipo_contribuyente` en `cfg_company` (post-25).

---

## 1. Cambios a tablas existentes (ALTER)

### 1.1 `cfg_company` — sin cambios

`cfg_company` ya contiene los datos fiscales de la empresa: `tax_id` (RTN), `legal_name`, `commercial_name`, `address`, `phone`, `email`, `code`. **NO se altera.**

Lo que falta es el concepto de **establecimiento fiscal** (cuando una empresa tiene varios), que se introduce como tabla nueva en §2.0.

### 1.2 `adm_cai_facturacion` — apuntar a establecimiento + tipo de documento

```sql
ALTER TABLE public.adm_cai_facturacion
    ADD COLUMN IF NOT EXISTS establecimiento_id bigint NOT NULL,  -- FK a adm_establecimiento
    ADD COLUMN IF NOT EXISTS tipo_documento_fiscal_id smallint NOT NULL,  -- el CAI ampara UN tipo
    ADD COLUMN IF NOT EXISTS fecha_limite_emision date NOT NULL,
    ADD COLUMN IF NOT EXISTS leyenda_rango varchar(200),  -- "CAI autorizado del rango_desde al rango_hasta"
    ADD CONSTRAINT fk_adm_cai_facturacion_establecimiento
        FOREIGN KEY (company_id, establecimiento_id)
        REFERENCES public.adm_establecimiento (company_id, establecimiento_id),
    ADD CONSTRAINT fk_adm_cai_facturacion_tipo_doc
        FOREIGN KEY (tipo_documento_fiscal_id)
        REFERENCES public.cfg_tipo_documento_fiscal (tipo_documento_fiscal_id),
    ADD CONSTRAINT ck_adm_cai_facturacion_fecha_limite
        CHECK (fecha_limite_emision >= vigencia_desde);
```

**Reglas SAR aplicadas:**
- Un CAI ampara **un único tipo de documento** (Factura, Recibo, NC, ND, etc.). Si la empresa emite varios tipos, registra varios CAIs.
- El CAI pertenece a un **establecimiento fiscal específico** (no a la empresa abstracta). Los datos del emisor se leen del establecimiento al emitir.
- `fecha_limite_emision`: después de esta fecha no se puede emitir más con ese CAI (regla SAR).

### 1.3 `factura` — datos fiscales del documento emitido

```sql
ALTER TABLE public.factura
    -- snapshot del establecimiento (no recalculado)
    ADD COLUMN IF NOT EXISTS establecimiento_id bigint,
    ADD COLUMN IF NOT EXISTS rtn_emisor varchar(20),
    ADD COLUMN IF NOT EXISTS razon_social_emisor varchar(200),
    ADD COLUMN IF NOT EXISTS direccion_emisor varchar(300),

    -- tipo de documento y referencia al origen
    ADD COLUMN IF NOT EXISTS tipo_documento_fiscal_id smallint NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS factura_origen_id bigint,  -- NULL para facturas; obligatorio en NC/ND si se reusa esta tabla
    ADD COLUMN IF NOT EXISTS motivo_anulacion_id smallint,

    -- snapshot CAI
    ADD COLUMN IF NOT EXISTS leyenda_cai_rango varchar(200),
    ADD COLUMN IF NOT EXISTS fecha_limite_cai date,

    ADD CONSTRAINT fk_factura_tipo_doc
        FOREIGN KEY (tipo_documento_fiscal_id)
        REFERENCES public.cfg_tipo_documento_fiscal (tipo_documento_fiscal_id),
    ADD CONSTRAINT fk_factura_motivo_anulacion
        FOREIGN KEY (motivo_anulacion_id)
        REFERENCES public.cfg_motivo_anulacion (motivo_anulacion_id);
```

**Datos congelados en el documento** (snapshot, no recalculado): si mañana cambia el RTN de la empresa o cierra el establecimiento, las facturas históricas siguen mostrando los datos correctos al momento de emisión. Eso es lo que SAR exige.

**Nota:** las NC/ND viven en tablas separadas (`adm_nota_credito`, `adm_nota_debito`). Las columnas `factura_origen_id` y `motivo_anulacion_id` en `factura` quedan como espacio para casos futuros (factura que sustituye a otra) pero el flujo principal de NC/ND es por las tablas dedicadas.

---

## 2. Tablas nuevas

### 2.0 `adm_establecimiento` — establecimientos fiscales por empresa

```sql
CREATE TABLE IF NOT EXISTS public.adm_establecimiento (
    establecimiento_id bigint GENERATED BY DEFAULT AS IDENTITY,
    company_id bigint NOT NULL,

    -- identificación SAR
    codigo varchar(20) NOT NULL,            -- código del establecimiento ante SAR
    nombre varchar(200) NOT NULL,           -- nombre del establecimiento (ej. "Sucursal Centro")

    -- datos fiscales (pueden diferir del emisor madre cfg_company)
    rtn_emisor varchar(20) NOT NULL,        -- RTN del establecimiento (puede ser igual al de la empresa)
    razon_social_emisor varchar(200) NOT NULL,
    direccion_emisor varchar(300) NOT NULL,
    telefono varchar(50),
    correo varchar(150),

    -- estado
    status_id smallint NOT NULL DEFAULT 1,  -- 1=activo, 0=inactivo
    es_principal boolean NOT NULL DEFAULT false,  -- el principal por empresa

    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(100) NOT NULL DEFAULT current_user,
    updated_at timestamptz,
    updated_by varchar(100),

    CONSTRAINT pk_adm_establecimiento PRIMARY KEY (establecimiento_id),
    CONSTRAINT uq_adm_establecimiento_company_id UNIQUE (company_id, establecimiento_id),  -- composite para FKs
    CONSTRAINT fk_adm_establecimiento_company FOREIGN KEY (company_id) REFERENCES public.cfg_company (company_id) ON DELETE CASCADE,
    CONSTRAINT uq_adm_establecimiento_codigo UNIQUE (company_id, codigo),
    CONSTRAINT ck_adm_establecimiento_rtn_format CHECK (rtn_emisor ~ '^[0-9]{14}$')
);

CREATE INDEX IF NOT EXISTS ix_adm_establecimiento_company
    ON public.adm_establecimiento (company_id, status_id);

-- Cada empresa debe tener exactamente un principal activo
CREATE UNIQUE INDEX IF NOT EXISTS uq_adm_establecimiento_principal_por_company
    ON public.adm_establecimiento (company_id)
    WHERE es_principal = true AND status_id = 1;
```

**Decisiones:**
- **Multi-empresa, multi-establecimiento desde el día 0**. Cualquier empresa que use el sistema puede declarar N establecimientos.
- Si una empresa tiene un solo establecimiento, lo registra con `es_principal = true`.
- El RTN del establecimiento puede ser igual o distinto al de la empresa madre (ambos los exige SAR según el caso).
- Los CAIs y los documentos se asocian al establecimiento, NO a `cfg_company` directamente.

### 2.1 `cfg_tipo_documento_fiscal` — catálogo SAR

```sql
CREATE TABLE IF NOT EXISTS public.cfg_tipo_documento_fiscal (
    tipo_documento_fiscal_id smallint PRIMARY KEY,
    codigo varchar(10) NOT NULL UNIQUE,
    descripcion varchar(80) NOT NULL,
    es_comprobante_fiscal boolean NOT NULL,    -- true: Factura/Ticket/Boleta. false: NC/ND/Guía/Retención
    es_documento_complementario boolean NOT NULL,
    requiere_factura_origen boolean NOT NULL,  -- true para NC/ND
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_tipo_documento_fiscal (tipo_documento_fiscal_id, codigo, descripcion, es_comprobante_fiscal, es_documento_complementario, requiere_factura_origen) VALUES
    (1, 'FAC',  'Factura',                        true,  false, false),
    (2, 'FACP', 'Factura prevalorada',            true,  false, false),
    (3, 'TIK',  'Ticket',                         true,  false, false),
    (4, 'BOL',  'Boleta de compra',               true,  false, false),
    (5, 'HON',  'Recibo por honorarios',          true,  false, false),
    (6, 'NC',   'Nota de crédito',                false, true,  true),
    (7, 'ND',   'Nota de débito',                 false, true,  true),
    (8, 'GR',   'Guía de remisión',               false, true,  false),
    (9, 'CRT',  'Comprobante de retención',       false, true,  false),
    (10, 'REC', 'Recibo de servicio público',     true,  false, false)
ON CONFLICT (tipo_documento_fiscal_id) DO NOTHING;
```

**Decisión:** APC emitirá inicialmente código `1` (Factura) para lecturas y `10` (Recibo de servicio público) si se confirma con SAR que aplica. NC y ND como documentos complementarios al anular o ajustar.

### 2.2 `cfg_motivo_anulacion` — catálogo de motivos NC

```sql
CREATE TABLE IF NOT EXISTS public.cfg_motivo_anulacion (
    motivo_anulacion_id smallint PRIMARY KEY,
    codigo varchar(20) NOT NULL UNIQUE,
    descripcion varchar(150) NOT NULL,
    aplica_factura boolean NOT NULL DEFAULT true,
    aplica_recibo boolean NOT NULL DEFAULT true,
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_motivo_anulacion (motivo_anulacion_id, codigo, descripcion) VALUES
    (1, 'ERROR_LECTURA',     'Error en lectura del medidor'),
    (2, 'ERROR_TARIFA',      'Error en aplicación de tarifa'),
    (3, 'ERROR_DATOS',       'Error en datos del cliente'),
    (4, 'DUPLICADA',         'Factura emitida en duplicado'),
    (5, 'EXONERACION',       'Aplicación posterior de exoneración'),
    (6, 'CORRECCION_PERIODO','Corrección de período'),
    (7, 'OTRO',              'Otro motivo (debe detallarse en observaciones)')
ON CONFLICT (motivo_anulacion_id) DO NOTHING;
```

### 2.3 `adm_nota_credito` — modelo NC V3

```sql
CREATE TABLE IF NOT EXISTS public.adm_nota_credito (
    nota_credito_id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    company_id bigint NOT NULL,
    establecimiento_id bigint NOT NULL,

    -- documento fiscal
    tipo_documento_fiscal_id smallint NOT NULL DEFAULT 6,  -- NC
    numero_documento varchar(80) NOT NULL,
    cai_id bigint NOT NULL,                     -- CAI específico tipo NC (un CAI ampara un solo tipo)
    correlativo bigint NOT NULL,
    fecha_emision timestamptz NOT NULL DEFAULT now(),
    fecha_limite_cai date NOT NULL,
    leyenda_cai_rango varchar(200),

    -- emisor (snapshot del establecimiento al momento de emitir)
    rtn_emisor varchar(20) NOT NULL,
    razon_social_emisor varchar(200) NOT NULL,
    direccion_emisor varchar(300),

    -- receptor (cliente)
    cliente_id bigint NOT NULL,
    rtn_receptor varchar(20),
    razon_social_receptor varchar(200) NOT NULL,
    direccion_receptor varchar(300),

    -- referencia al documento original (OBLIGATORIO SAR)
    factura_origen_id bigint NOT NULL,
    factura_origen_numero varchar(80) NOT NULL,
    factura_origen_fecha date NOT NULL,
    factura_origen_cai varchar(100) NOT NULL,

    -- motivo y detalles
    motivo_anulacion_id smallint NOT NULL,
    motivo_detalle varchar(500),

    -- valores
    monto_disminuir numeric(18,4) NOT NULL,
    isv_disminuir numeric(18,4) NOT NULL DEFAULT 0,
    total_nota numeric(18,4) NOT NULL,

    -- estados (lookup)
    estado_id smallint NOT NULL DEFAULT 1,  -- 1=EMITIDA, 2=APLICADA, 3=ANULADA

    -- auditoría
    usuario_emisor varchar(100) NOT NULL,
    usuario_aprobador varchar(100),
    fecha_aprobacion timestamptz,

    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz,

    CONSTRAINT fk_adm_nota_credito_company FOREIGN KEY (company_id) REFERENCES public.cfg_company (company_id) ON DELETE CASCADE,
    CONSTRAINT fk_adm_nota_credito_establecimiento FOREIGN KEY (company_id, establecimiento_id) REFERENCES public.adm_establecimiento (company_id, establecimiento_id),
    CONSTRAINT fk_adm_nota_credito_cai FOREIGN KEY (company_id, cai_id) REFERENCES public.adm_cai_facturacion (company_id, cai_id),
    CONSTRAINT fk_adm_nota_credito_motivo FOREIGN KEY (motivo_anulacion_id) REFERENCES public.cfg_motivo_anulacion (motivo_anulacion_id),
    CONSTRAINT fk_adm_nota_credito_tipo_doc FOREIGN KEY (tipo_documento_fiscal_id) REFERENCES public.cfg_tipo_documento_fiscal (tipo_documento_fiscal_id),
    CONSTRAINT ck_adm_nota_credito_montos CHECK (monto_disminuir >= 0 AND total_nota >= 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_adm_nota_credito_company_numero
    ON public.adm_nota_credito (company_id, numero_documento);

CREATE INDEX IF NOT EXISTS ix_adm_nota_credito_factura_origen
    ON public.adm_nota_credito (company_id, factura_origen_id);
```

### 2.4 `adm_nota_credito_detalle` — líneas de la NC

```sql
CREATE TABLE IF NOT EXISTS public.adm_nota_credito_detalle (
    nota_credito_detalle_id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    nota_credito_id bigint NOT NULL,
    servicio_id bigint NOT NULL,
    descripcion varchar(300) NOT NULL,
    cantidad numeric(18,4) NOT NULL DEFAULT 1,
    monto_unitario numeric(18,4) NOT NULL,
    monto_total numeric(18,4) NOT NULL,
    isv_monto numeric(18,4) NOT NULL DEFAULT 0,
    cuenta_contable_codigo varchar(20),  -- para asiento contable

    CONSTRAINT fk_adm_nota_credito_detalle_nc FOREIGN KEY (nota_credito_id) REFERENCES public.adm_nota_credito (nota_credito_id) ON DELETE CASCADE,
    CONSTRAINT fk_adm_nota_credito_detalle_servicio FOREIGN KEY (servicio_id) REFERENCES public.adm_servicio (servicio_id),
    CONSTRAINT ck_adm_nota_credito_detalle_montos CHECK (cantidad > 0 AND monto_unitario >= 0 AND monto_total >= 0)
);
```

### 2.5 `adm_nota_debito` y `adm_nota_debito_detalle`

**Estructura idéntica a NC** salvo:
- `tipo_documento_fiscal_id` default `7` (ND).
- `motivo_anulacion_id` reemplazado por `motivo_aumento_id` (catálogo aparte: cobro extemporáneo, recargo por mora, ajuste contable).
- Campo `monto_aumentar` en lugar de `monto_disminuir`.

DDL completo se replica como §2.3-2.4 con esos cambios.

---

## 3. SPs nuevos

### 3.1 `sp_adm_emitir_nota_credito`

```
INPUT:
  p_company_id bigint
  p_factura_origen_id bigint
  p_motivo_anulacion_id smallint
  p_motivo_detalle varchar
  p_monto_disminuir numeric (NULL = total de la factura origen)
  p_lineas jsonb (NULL = mismas líneas de la factura origen)
  p_usuario_emisor varchar
  p_cai_id bigint (CAI específico para NC)
  p_cai_bloque_id bigint
OUTPUT:
  nota_credito_id, numero_documento, correlativo

LÓGICA:
  1. Validar que la factura origen existe, está EMITIDA, no ANULADA.
  2. Tomar correlativo del bloque CAI tipo NC (no del bloque de facturas).
  3. Snapshot de RTN/razón social emisor desde cfg_company.
  4. Snapshot de receptor desde cliente.
  5. Si p_lineas NULL, copiar de factura origen.
  6. INSERT en adm_nota_credito + adm_nota_credito_detalle.
  7. Marcar factura origen con motivo + nota_credito_id (sin tocar el doc original).
  8. Generar partida contable inversa (futuro: vía botón generar partidas).
  9. Devolver número y correlativo.
```

### 3.2 `sp_adm_emitir_nota_debito` — análogo

---

## 4. Modificaciones a SPs existentes

### 4.1 `sp_lectura_v3`

Hoy escribe `factura` con datos del cliente. Debe ahora:

1. **Snapshot de emisor** desde `adm_establecimiento` (por `cai_id` → `establecimiento_id`) y persistir en `factura.rtn_emisor`, `factura.razon_social_emisor`, `factura.direccion_emisor`, `factura.establecimiento_id`.
2. **Snapshot de leyenda CAI** y `fecha_limite_cai` desde `adm_cai_facturacion`.
3. **`tipo_documento_fiscal_id`** desde el CAI (NO se asume Factura — depende del CAI configurado).
4. **NO permitir emisión** si `adm_cai_facturacion.fecha_limite_emision < current_date` (CAI vencido → SAR prohíbe).

### 4.2 `sp_adm_calcular_factura_lectura`

No cambia — sigue calculando importes. Pero la respuesta debe incluir `tipo_documento_fiscal_id` para que el llamador sepa con qué emitir.

### 4.3 Nueva validación CAI

```sql
CREATE OR REPLACE FUNCTION public.fn_adm_validar_cai_emitible(
    p_company_id bigint,
    p_cai_id bigint
) RETURNS boolean LANGUAGE sql STABLE AS $$
    SELECT EXISTS (
        SELECT 1 FROM public.adm_cai_facturacion
        WHERE company_id = p_company_id
          AND cai_id = p_cai_id
          AND status_id = 1
          AND vigencia_desde <= current_date
          AND (vigencia_hasta IS NULL OR vigencia_hasta >= current_date)
          AND fecha_limite_emision >= current_date
    );
$$;
```

Llamado desde `sp_lectura_v3` y de `sp_adm_emitir_nota_credito` antes de tomar correlativo.

---

## 5. Lookup de estados (estados numéricos del Sprint 2)

Coordina con la migración de estados:

```sql
CREATE TABLE IF NOT EXISTS public.cfg_estado_documento_fiscal (
    estado_id smallint PRIMARY KEY,
    codigo varchar(20) NOT NULL UNIQUE,
    descripcion varchar(80) NOT NULL,
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_estado_documento_fiscal VALUES
    (1, 'EMITIDA',  'Documento emitido', true),
    (2, 'APLICADA', 'Documento aplicado a saldo cliente', true),
    (3, 'ANULADA',  'Documento anulado por NC', true),
    (4, 'PENDIENTE','Pendiente de sincronización', true)
ON CONFLICT (estado_id) DO NOTHING;
```

`adm_nota_credito.estado_id` y `factura.estado_id` (cuando se haga la migración) usan este lookup.

---

## 6. Plan de migración

```
Orden de aplicación (script único, idempotente):

 1. CREATE TABLE cfg_tipo_documento_fiscal + seeds (10 tipos SAR).
 2. CREATE TABLE cfg_motivo_anulacion + seeds (7 motivos).
 3. CREATE TABLE cfg_estado_documento_fiscal + seeds (4 estados).
 4. CREATE TABLE adm_establecimiento (multi-empresa, multi-establecimiento).
    4.1. Seed inicial: para cada cfg_company existente, crear un
         adm_establecimiento principal con datos copiados desde cfg_company
         (tax_id -> rtn_emisor, legal_name -> razon_social_emisor,
         address -> direccion_emisor, etc.) y es_principal=true.
 5. ALTER adm_cai_facturacion (establecimiento_id, tipo_documento_fiscal_id,
    fecha_limite_emision, leyenda_rango).
    5.1. Backfill: cada CAI existente -> establecimiento principal de su company.
         tipo_documento_fiscal_id por defecto 1 (Factura) — operador puede
         reclasificar despues.
         fecha_limite_emision = vigencia_desde + 1 año (default SAR; se puede
         editar manualmente despues por CAI).
 6. ALTER factura (establecimiento_id, rtn_emisor, razon_social_emisor,
    direccion_emisor, tipo_documento_fiscal_id, factura_origen_id,
    motivo_anulacion_id, leyenda_cai_rango, fecha_limite_cai).
    6.1. Backfill: para facturas existentes, snapshot desde adm_cai_facturacion
         (que ya quedo enriquecido en paso 5).
 7. CREATE TABLE adm_nota_credito + adm_nota_credito_detalle.
 8. CREATE TABLE adm_nota_debito + adm_nota_debito_detalle.
 9. CREATE FUNCTION fn_adm_validar_cai_emitible.
10. CREATE FUNCTION sp_adm_emitir_nota_credito.
11. CREATE FUNCTION sp_adm_emitir_nota_debito.
12. ALTER sp_lectura_v3 con snapshot establecimiento + validación CAI vencido.
```

**PROD actual es solo de pruebas** → backfill simple, no se preservan datos en vivo. Si algún CAI no queda con datos coherentes después del backfill, se borra y se recrea desde la UI nueva.

---

## 7. Impacto en C# y app

### Backend (apc + SIAD.Services)
- Nuevas entidades EF: `adm_nota_credito`, `adm_nota_credito_detalle`, `adm_nota_debito`, `adm_nota_debito_detalle`, `cfg_tipo_documento_fiscal`, `cfg_motivo_anulacion`, `cfg_estado_documento_fiscal`.
- Nuevo controller: `NotaCreditoController`, `NotaDebitoController`.
- Nuevo service: `NotaCreditoService` con método `EmitirAsync(facturaOrigenId, motivo, ...)`.
- Modificar `LecturaService` o `FacturacionService` para validar CAI emitible antes de llamar `sp_lectura_v3`.

### Portal (apc.Client)
- Nueva vista `Pages/Facturacion/NotasCreditoDebito/EmitirNotaCredito.razor` (o reutilizar la existente si hay legacy).
- Botón "Emitir nota de crédito" en `ClienteDetail.razor` y/o en lista de facturas.
- Combo de motivos (lookup `cfg_motivo_anulacion`).
- Confirmación con doble paso (motivo obligatorio + observaciones).

### App Android
- Sin cambios funcionales en Sprint 2. La app sigue solo emitiendo facturas (no NC desde campo).
- Cambio en factura impresa: agregar leyendas SAR completas (RTN emisor, rango CAI, fecha límite).

---

## 8. Riesgos

1. **CAI vigentes sin `fecha_limite_emision` en BD**: el ALTER agrega columna NOT NULL. Backfill: `vigencia_desde + 1 año`. PROD es de pruebas → se puede recrear si algo queda mal.

2. **Numeración global de documentos**: hoy `numero_factura` es único por `(company_id, numero_factura)` solo dentro de la tabla `factura`. Con NC/ND en tablas separadas, hay que decidir si el `numero_documento` debe ser único globalmente entre las 3 tablas o si cada tipo de documento tiene su propia secuencia. **Decisión propuesta:** cada CAI ya tiene su propio rango → la unicidad la garantiza el correlativo dentro del CAI, no el número global.

3. **Multi-CAI por empresa por tipo**: una empresa con varios establecimientos y varios tipos de documento puede tener 4+ CAIs activos al mismo tiempo. La UI de configuración debe soportar listar/filtrar/seleccionar el CAI correcto al emitir.

4. **`adm_establecimiento.es_principal`**: el unique parcial garantiza un solo principal activo por empresa, pero no garantiza que exista uno. Validar en la lógica de seeds.

---

## 9. Decisiones del usuario (2026-05-05)

1. **Tipo de documento a emitir**: el sistema debe permitir que cada empresa emita **cualquier** tipo de documento fiscal (Factura, Recibo, NC, ND, Boleta, etc.), no se restringe a Factura. Cada empresa configura sus CAIs por tipo.

2. **Establecimientos por empresa**: cada empresa puede tener N establecimientos. APC tendrá uno solo, pero el modelo es multi-establecimiento desde el día 0 (corrección de feedback v1).

3. **CAI por tipo de documento**: SAR exige que cada CAI ampare un tipo de documento específico. NO se mezclan tipos en un mismo CAI. Si una empresa emite Factura + NC, registra 2 CAIs.

4. **Datos del emisor**: `cfg_company` ya tiene los datos de la empresa (`tax_id`, `legal_name`, etc.) — NO se altera. Los datos del establecimiento (que pueden coincidir con la empresa madre o no) viven en `adm_establecimiento`.

5. **Aprobación de NC**: un solo usuario con permiso emite directamente. NO hay doble paso.

6. **Backfill en PROD**: PROD es solo de pruebas. Backfill simple, sin preocupaciones por datos en vivo.

---

## 10. Próximos pasos al aprobar este plan

1. Crear migration `Database/ddl_v3/20260507_sar_compliance_nc_nd.sql` con todo el DDL.
2. Aplicar en DEV + smoke test.
3. Crear entidades EF reverse-engineer (skill `hodsoft-postgres-ef-scaffold`).
4. Implementar `NotaCreditoService` + controller (skill `hodsoft-siad-backend`).
5. Vista Blazor (skill `hodsoft-blazor-devexpress-ui`).
6. Modificar `sp_lectura_v3` con snapshot.
7. Test de emisión NC contra factura emitida.
8. Aplicar en PROD con cargas previas (RTN + CAI fechas).

**Tiempo estimado:** 4-5 días dentro del Sprint 2 días 8-9 + Sprint 3 días 1-3.
