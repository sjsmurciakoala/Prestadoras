# SIAD.Tests

Tests automatizados de integración para el motor V3 / SAR compliance (Sprint 3 día 11 — entrega 25-may).

## Qué cubre

| Archivo | Test | Cubre |
|---|---|---|
| `LecturaV3Tests.cs` | `SP_lectura_v3_existe_y_tiene_parametro_uuid_idempotencia` | Firma del SP con `p_lectura_uuid` |
| | `SP_lectura_v3_devuelve_idempotente_para_uuid_ya_registrado` | UUID replay → mismo resultado |
| | `SP_lectura_v3_lanza_FACTURA_YA_EMITIDA_en_doble_emision` | Doble emisión bloqueada |
| `NotaCreditoTests.cs` | `Tablas_y_SPs_NC_ND_V3_existen` | Modelo NC/ND completo |
| | `SP_emitir_nota_credito_rechaza_factura_inexistente` | `FACTURA_NO_EXISTE` |
| | `SP_emitir_nota_credito_rechaza_factura_ya_anulada` | `FACTURA_YA_ANULADA` |
| `AnulacionTests.cs` | `NC_total_marca_factura_origen_como_estado_N` | Anulación E2E con rollback |
| `RecargoMoraTests.cs` | 3 tests | `cfg_recargo_mora`, tasa en \[0,1\], SP cliente invoca la tabla |
| `TerceraEdadTests.cs` | 2 tests | Ajuste TERCERA_EDAD con tope L300 y 25% |
| `CaiCorrelativoTests.cs` | 3 tests | Avance correlativo con `GREATEST`, validación CAI, lookup `cfg_estado_cai` |
| `SarComplianceTests.cs` | 8 asserts | Catálogos SAR, `factura.company_id NOT NULL`, mono-sucursal, `fn_adm_validar_cai_emitible` |

## Diseño

- **Cada test abre conexión + `BEGIN TRANSACTION`** y al final hace `ROLLBACK`. No persiste nada en la BD.
- **Sin env var `SIAD_TEST_DB`** los tests se marcan como **Skipped** (no fallan) — el proyecto compila en CI sin BD.
- Tests escritos para que el setup mínimo sea "una BD `siad_v3` con los scripts `Database/ddl_v3/*` aplicados". No requieren seeds específicos del demo.

## Cómo correrlos

```powershell
# Apuntar a una BD de prueba (NO PROD APC, NO Azure demo si está en uso)
$env:SIAD_TEST_DB = "Host=localhost;Port=5432;Database=siad_v3_test;Username=postgres;Password=root;Timeout=10"
$env:SIAD_TEST_COMPANY_ID = "2"   # opcional, default 2

dotnet test SIAD.Tests/SIAD.Tests.csproj --logger "console;verbosity=normal"
```

Sin la env var:

```powershell
dotnet test SIAD.Tests/SIAD.Tests.csproj
# → todos los tests Skipped con "Falta env var SIAD_TEST_DB"
```

## Restricciones

- **Tests son read-mostly + rollback**. No deben dejar basura ni en CAIs ni en `factura`.
- Si la BD demo está en uso por otra prueba E2E, evita correr `AnulacionTests` (puede tomar bloqueo breve sobre la última factura activa antes del rollback).
- La transacción rollback **no revierte side effects de SPs que usan `dblink` o autonomous transactions**. Ninguno de los SPs probados lo hace hoy, pero si alguno migra a `pg_background` hay que aislar la BD de tests.

## Lo que NO cubre (post-25)

- Performance / carga.
- App Android (Java) — la migración cubrirá esto.
- Reportería DevExpress.
- Validación de las migraciones EF de Identity.
