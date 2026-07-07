# Bloque `mora` del snapshot offline (contrato para la app de lectores)

**Fecha:** 2026-07-06 · **Rama:** `feat/mora-en-snapshot` · **SP:** `sp_adm_generar_snapshot_offline_cliente_lectura`

Construye sobre el PR #12 (tramos CM embebidos). Este cambio agrega el bloque
top-level `mora` a `snapshot_json`, para que la app de lectores (L8/L10) reproduzca
**offline** el recargo por mora que hoy calcula el motor online.

## Por qué

La app calcula la factura offline. La configuración de mora vive por empresa en
`cfg_recargo_mora` (server) y **no llegaba** al snapshot → sin ella, si una empresa
activa la mora, la app **sub-factura a los morosos**. Ninguna empresa puede activar
mora con la app nueva hasta que este dato viaje en el snapshot.

## Cálculo online que se reproduce (`sp_adm_calcular_factura_lectura`, campo `recargos`)

```
saldo_anterior = saldo_actual del cliente (sp_obtener_cliente_saldo)
si saldo_anterior > 0:
    tasa = cfg_recargo_mora.tasa_mensual  si la empresa la tiene ACTIVA, si no 0
    recargos = ROUND(saldo_anterior * tasa, 4)
si saldo_anterior <= 0:
    recargos = 0
```

`dias_gracia` **no** interviene en el cálculo (reservado, uso futuro).

## Forma del bloque

`snapshot_json.mora` — **siempre presente** (multitenant A6: resuelto por empresa,
nunca por parámetro del cliente):

```jsonc
"mora": {
  "activo":       true,        // bool  — cfg_recargo_mora.activo de la empresa
  "tasa_mensual": 0.001667,    // numeric(9,6) — fracción mensual configurada
  "dias_gracia":  0,           // int   — informativo, el motor aún NO lo usa
  "base":         662.22,      // numeric(18,2) — saldo sobre el que aplica (= snapshot_json.saldo_anterior_total)
  "recargo":      1.1039       // numeric(18,4) — AUTORITATIVO: lo que factura el motor
}
```

## Cómo lo consume la app

- **Recargo a facturar:** usar `mora.recargo` (autoritativo, ya calculado por el server
  con la misma fórmula que el motor online).
- **Recalcular/validar** (opcional): `recargo = (activo && base > 0) ? ROUND(base * tasa_mensual, 4) : 0`.
  Debe dar exactamente `mora.recargo`.
- **`activo:false`** ⇒ no se aplica mora (recargo 0), aunque `tasa_mensual` traiga el
  valor configurado. La app **debe** gatear por `activo`.

## Reglas del contrato

- El bloque va **SIEMPRE**, incluso con la mora inactiva (`activo:false`, `recargo:0`).
  Así la app distingue **"no aplica"** (bloque con `activo:false`) de **"no vino el dato"**
  (bloque **ausente** = snapshot viejo, pre-2026-07-06).
- `contract_version` se mantiene en `OFFLINE_SNAPSHOT_V3_2` (cambio **aditivo**): la
  presencia del bloque es la señal.
- `base` == `snapshot_json.saldo_anterior_total` (mismo valor; se repite en el bloque
  para que sea autocontenido).

## Paridad garantizada (test)

`SIAD.Tests/SnapshotMoraTests.cs` verifica, para un cliente con saldo > 0 y mora activa,
que `mora.recargo` == `recargos` de `sp_adm_calcular_factura_lectura` (y que la fórmula
`base * tasa_mensual` da lo mismo), y que con mora inactiva el bloque queda
`activo:false`, `recargo:0` — igual que el motor.
