# Guía de pruebas E2E — Demo Azure 2026-05-14

**Objetivo**: validar que el motor de facturación V3 da el mismo total en app (offline) y server (online), después del fix del 14-may.

**Esperado**: factura del cliente `LZV2CF1JFW` (Jessel) = **L. 331.11** en ambos motores, sin `SYNC_CONFLICT_TOTAL`.

---

## Lo que ya está desplegado en Azure (NO tocar)

| Recurso | Valor | Estado |
|---|---|---|
| Resource Group | `rg-hodsoft-demo` | ✅ |
| Region | `westus3` | ✅ |
| Portal Blazor | https://hodsoft-apc-demo-31680.azurewebsites.net | ✅ |
| PostgreSQL host | `pg-hodsoft-demo-31680.postgres.database.azure.com` | ✅ |
| BD | `siad_v3` (dump PROD restaurado + fixes 13-may y 14-may) | ✅ |

### Credenciales PostgreSQL Azure

```
Host:     pg-hodsoft-demo-31680.postgres.database.azure.com
Port:     5432
Database: siad_v3
User:     siadadmin
Password: #hodS0demo2026
SSL Mode: Require (Trust Server Certificate)
```

### Apagar / encender BD para ahorrar créditos

```bash
# Apagar (no cobra cómputo, solo storage)
az postgres flexible-server stop -g rg-hodsoft-demo -n pg-hodsoft-demo-31680

# Encender de nuevo
az postgres flexible-server start -g rg-hodsoft-demo -n pg-hodsoft-demo-31680
```

---

## Setup local antes de probar (cada vez que reinicies la PC)

### 1. Arrancar el WS desde Visual Studio

- Abrir `WSappLectores\WS_APC.sln` en VS
- Verificar que `Web.config` tiene la connection string apuntando a Azure PG (ya está en el repo):
  ```xml
  <add name="conexionpostgres" connectionString="SERVER=pg-hodsoft-demo-31680.postgres.database.azure.com;Port=5432;Database=siad_v3;User name=siadadmin;Password=#hodS0demo2026;SSL Mode=Require;Trust Server Certificate=true" providerName="Npgsql2" />
  ```
- **F5** (Iniciar depuración) — abre el WS en `http://localhost:2805/APCService.svc`
- Smoke: en cualquier browser de tu PC abrí `http://localhost:2805/APCService.svc/SaludaServices` → debe responder `{"SaludoResult":"Hola Humano"}`

### 2. Conectar teléfono Android por USB + adb reverse

```powershell
# Si adb no está en PATH:
$env:Path += ";$env:LOCALAPPDATA\Android\Sdk\platform-tools"

# Verificar teléfono conectado (debe listar device)
adb devices

# Hacer que localhost:2805 del teléfono apunte a tu PC
adb reverse tcp:2805 tcp:2805

# Confirmar
adb reverse --list
```

Si reconectas el teléfono o reinicias `adb`, repetí el `adb reverse`.

### 3. Run app desde Android Studio

- Abrir `AppLectoresAPC` en Android Studio
- Verificar que `Utilidades.java:25` apunta a `http://localhost:2805/APCService.svc` (ya está en el repo)
- **Run ▶** (Shift+F10) — VS instala y arranca la app en el teléfono

---

## Flujo de prueba E2E

### 1. Limpiar datos del app (CRÍTICO — sino usa snapshot viejo)

En el teléfono:
- Settings de Android → Apps → buscar **HODSOFT** / **APC** / lo que se llame
- **Almacenamiento → Borrar datos** (o "Clear data")
- Confirmar

Esto borra el SQLite local que tenía las facturas pendientes con el cálculo malo.

### 2. Login en la app

- Abrir la app (debe arrancar como recién instalada)
- Login con tu usuario (el WS valida contra Azure PG)

### 3. Descargar ruta `01001`

- Menú principal → **Descargar ruta**
- Ingresar ruta: `01001`
- (Periodo lo resuelve automático: `2026/2`)
- Debe descargar **5 clientes** sin warnings
- El WS responde el snapshot V3 con las **reglas correctas** (porcentaje 0.60 ya fixed)

### 4. Probar facturación con cliente sin saldo previo

Recomendado: cliente `LZV2CF1JFW` (Jessel, saldo previo 0).

- En la app: abrir el cliente
- Verificar que la app NO te pide lectura (es SIN_MEDICION, `tiene_medidor=false`)
- Tocar **Facturar** o equivalente
- **Esperado**: total = **L. 331.11**
  - AGUA: 199.27
  - ALCANTARILLADO: 119.56 (60% de 199.27)
  - TASA_AMBIENTAL: 5.90
  - TASA_SVA_ERSAPS: 6.38 (2% AGUA + 2% ALCANT)

### 5. Sincronizar al WS

- Ir a "Sincronización" / "Facturas pendientes" en la app
- Sincronizar
- **Esperado**: `Sincronizadas: 1, Errores: 0, Conflictos: 0`
- NO debe aparecer `SYNC_CONFLICT_TOTAL`

### 6. Validar en portal Azure

- Browser → https://hodsoft-apc-demo-31680.azurewebsites.net
- Login admin
- Ir a **Facturación** → ver la factura nueva del cliente `LZV2CF1JFW`
- Total debe ser L. 331.11

### 7. (Opcional) Probar otros clientes

- `090040001` (ALTAMIRANO LARA NIEVES) — saldo previo 0, total esperado 331.11
- `090041003` (MONGE MELGAR) — saldo previo 0, total esperado 331.11
- `090041004` (COMIXMEPOR SERRANO) — saldo previo 0, total esperado 331.11
- ⚠️ NO usar `090041002` — ya tiene factura emitida en 2026/2 (`FACTURA_YA_EMITIDA`)
- ⚠️ NO usar `090041008` — tiene saldo previo L. 331.11, total esperado L. 662.22 (cuota mes + saldo)

---

## Validación SQL directa contra Azure PG

Para confirmar que el motor SP da los totales correctos sin tocar la app:

```sql
-- Conectar a Azure PG con las credenciales arriba

-- Total esperado de cliente Jessel (ID 102789)
SELECT
    subtotal_servicios,
    taservi1 AS agua,
    taservi2 AS alcant,
    taservi3 AS ambiental,
    taservi4 AS ersaps,
    saldos_anteriores,
    total_factura
FROM public.sp_adm_calcular_factura_lectura(
    p_company_id := 2::bigint,
    p_anio := 2026,
    p_mes := 2,
    p_cliente_id := 102789::bigint,
    p_condicion_lectura := 'SM'
);
```

**Resultado esperado**:
```
 subtotal_servicios |   agua   |  alcant  | ambiental | ersaps | saldos_anteriores | total_factura
--------------------+----------+----------+-----------+--------+-------------------+---------------
           331.1086 | 199.2700 | 119.5620 |    5.9000 | 6.3766 |                 0 |      331.1086
```

---

## Si algo falla

| Síntoma | Causa probable | Solución |
|---|---|---|
| App da `Cannot connect to server` | VS no corre o `adb reverse` perdido | F5 en VS + `adb reverse tcp:2805 tcp:2805` |
| App da `SYNC_CONFLICT_TOTAL` con monto distinto a 331.11 | App usa snapshot viejo cacheado | Clear data del app + reinstalar |
| App da `requiere consumo p_consumo` | Cliente está como CON_MEDICION pero el cuadro no tiene tarifa de consumo | Revisar `cliente_maestro.tiene_medidor` y `adm_cliente_servicio.condicion_medicion` deben coincidir |
| Portal Azure error 500 al login | Cold start del F1 Free | Esperar 30-60s y reintentar |
| Portal Azure muestra valores raros | Posible cache navegador | Ctrl+F5 (hard refresh) |

---

## Lo que ya está corregido en Azure PG (14-may)

| Fix | Detalle |
|---|---|
| Regla 29 (ALCANTARILLADO) | `porcentaje 60.00 → 0.60` (formato decimal) |
| SP `sp_adm_resolver_tarifa_cliente_servicio` | Ahora implementa `PORCENTAJE_SERVICIO` (antes devolvía NULL) |
| Cuadro tarifario `APC_AGUA_SM_DOMESTICA_BAJA` regla 5 | `RANGO_CONSUMO → MONTO_FIJO 199.27` (13-may) |
| 24 clientes `tiene_medidor` | Sincronizado a `false` matcheando `condicion=SIN_MEDICION` (13-may) |
| Cam Computer | Eliminada (company_id=3) |

---

## Republish portal si querés probar el fix UI

El portal en Azure todavía tiene el SpinEdit viejo (sin conversión %↔decimal). El nuevo build local SÍ lo tiene. Para subirlo:

```powershell
cd e:\Koala\proyectos\HODSOFT_DEVEXPRESS\Prestadoras
dotnet publish apc\apc.csproj -c Release -r linux-x64 --self-contained false -o publish_azure
Compress-Archive -Path publish_azure\* -DestinationPath publish_azure.zip -Force

# Subir
az webapp deploy -g rg-hodsoft-demo -n hodsoft-apc-demo-31680 --src-path publish_azure.zip --type zip
```

O desde Visual Studio: click derecho `apc` → Publicar → Publicar.

Para validar el fix UI:
- Ir a `/contabilidad/cuadros-tarifarios` → entrar a un cuadro → editar una regla con porcentaje
- El SpinEdit ahora muestra "60.00" en lugar de "0.60"
- Al guardar 60, se guarda como 0.60 en BD
