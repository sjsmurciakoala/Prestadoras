# Campos de ticket/factura en el snapshot offline (contrato para la app de lectores)

**Fecha:** 2026-07-07 · **Rama:** `feat/snapshot-campos-piloto` · **SP:** `sp_adm_generar_snapshot_offline_cliente_lectura`

Construye sobre el PR #12 (tramos CM) y el PR #13 (bloque `mora`). Agrega al
`snapshot_json` los datos que la app de lectores (L9/L10) necesita para imprimir
**offline** el **ticket fiscal** y la **factura**, y que antes no viajaban en el
snapshot. Todo es **multitenant** (A6): se resuelve por `p_company_id`, nunca por un
parámetro que venga del cliente/dispositivo.

## Campos nuevos

### 1. `emisor` — encabezado de la empresa que imprime el ticket

Fuente: `cfg_company` de la empresa (`WHERE company_id = p_company_id`). Es la **misma
fuente y prioridad de nombre** que usa el encabezado de los reportes del portal
(`SIAD.Reports/ReportCompanyHeaderParameters.cs`), para que el ticket offline coincida
con lo que imprime el portal online.

```jsonc
"emisor": {
  "nombre":           "Empresa de Agua y Saneamiento S.A de C.V",  // legal_name → commercial_name → code → "EMPRESA"
  "nombre_comercial": "Aguas de Puerto Cortes",                    // commercial_name (puede ser null)
  "rtn":              "R.T.N-05069999182490",                      // tax_id — RTN del EMISOR
  "direccion":        "Bo. Copen 9 calle este, ...",               // address
  "telefono":         "+504 26271450 / 26271451",                  // phone (puede ser null)
  "email":            "administracion@aguasdepuertocortes.com"     // email (puede ser null)
}
```

- `nombre` nunca es null: cae a `"EMPRESA"` si `legal_name`/`commercial_name`/`code` están vacíos.
- Los demás campos son `null` cuando la columna correspondiente de `cfg_company` está vacía
  (se emiten con `NULLIF(BTRIM(...), '')`).

### 2. `cliente_rtn` — RTN del abonado (comprador) que la factura imprime

Fuente: `cliente_maestro.maestro_cliente_rtn` (ya cargado company-scoped en el SP).

```jsonc
"cliente_rtn": "990020001"   // string | null (null si el abonado no tiene RTN)
```

> Es el RTN fiscal. El documento de identidad (`maestro_cliente_identidad`) es otro campo
> y **no** se expone aquí; si la factura necesita mostrar identidad, pedir un campo aparte.

### 3. `fecha_lectura_anterior` — fecha de la lectura anterior del medidor

Fuente: `historicomedicion.fecha_lect_ant` del período (`ano`/`mes`/`clave`), ahora
**filtrado por `company_id`**. Es el mismo dato que el path online del lector expone como
`FechaLecturaAnterior` (`LectoresMobileService`).

```jsonc
"fecha_lectura_anterior": "2026-04-15"   // date (YYYY-MM-DD) | null
```

- Va **`null`** cuando no hay histórico del período (igual que `lectura_anterior_referencia`
  cae a `0`). La app decide qué imprimir en ese caso (el portal online usa la fecha actual
  como fallback en la lista de rutas, pero el snapshot expone el dato crudo).

## Multitenancy (A6)

- `emisor` sale de `cfg_company` de `p_company_id`.
- `cliente_rtn` sale del `cliente_maestro` ya resuelto company-scoped.
- La carga de `historicomedicion` (que alimenta `fecha_lectura_anterior`,
  `lectura_anterior_referencia` y `promedio_referencia`) **ahora filtra por `company_id`**.
  Antes seleccionaba solo por `ano`/`mes`/`clave`; con claves colisionantes entre empresas
  podía tomar el histórico de otra empresa. En una sola empresa el resultado es idéntico.

## Reglas del contrato

- `contract_version` se mantiene en `OFFLINE_SNAPSHOT_V3_2` (cambio **aditivo**, mismo
  precedente que #12 y #13). La app detecta los campos nuevos por **presencia de la clave**;
  su ausencia significa snapshot viejo (pre-2026-07-07). No se toca el const C#
  `MobileApiDtos.SnapshotContractVersion` ni los consumidores (el `snapshot_json` viaja como
  string opaco dentro de `PackageJson`).
- Campos aditivos: no se cambió nada de `mora`, `saldos_por_servicio`, `servicios`,
  `warnings`, ni el descuento de tercera edad.

## Paridad garantizada (tests)

`SIAD.Tests/SnapshotCamposPilotoTests.cs`:

- `emisor` coincide campo a campo con `cfg_company` de la empresa (nombre con la misma
  prioridad que el portal, RTN, dirección, teléfono, email).
- `cliente_rtn` coincide con `cliente_maestro.maestro_cliente_rtn`.
- `fecha_lectura_anterior` refleja `fecha_lect_ant` de un histórico insertado, y **no** se
  contamina con el histórico de otra empresa con la misma clave (A6).
- Los campos nuevos están presentes y el contrato sigue en `OFFLINE_SNAPSHOT_V3_2`.
