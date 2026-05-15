# Plan de pruebas y mejoras — 2026-04-27

Fecha de cierre de la jornada anterior: **2026-04-26 ~22:00**.
Estado: **flujo V3 E2E validado en PROD** con 2 facturas creadas vía app real (correlativos 101 y 102), sin conflictos. Ambiente preparado para pruebas multi-usuario el 2026-04-27.

---

## 1. Estado de PROD al iniciar el día

| Pieza | Valor |
|---|---|
| BD | `172.16.0.9 / siad_v3` |
| WS | `http://172.16.0.9:2805/APCService.svc` |
| Periodo abierto | 2026 / mes 2 / ciclo `01` |
| Clientes activos | **25** (todos con `adm_cliente_servicio` V3 configurado), distribuidos 5 por ruta |
| Lectores activos | **5**, cada uno con SU ruta propia |
| `historicomedicion` periodo 2026/2 | 25 filas (5 por ruta, todas pendientes de lectura) |
| Bloques CAI por ruta | 1 bloque de 100 correlativos por ruta (ver tabla 2.1) |
| Facturas previas en este periodo | 0 |

**Asignación de rutas y clientes (uno por lector — sin solapamiento):**

| Lector | Ruta | Clientes (5) | Bloque CAI |
|---|---|---|---|
| jmurcia  | `01001` | 102789, 102813, 102814, 102815, 102816 | correlativos 100..199 (próx **103**) |
| KGARCIA  | `01002` | 102817, 102818, 102819, 102820, 102821 | 200..299 (próx 200) |
| LECTOR1  | `01003` | 102822, 102823, 102824, 102825, 102826 | 300..399 (próx 300) |
| LECTOR2  | `01004` | 102827, 102828, 102829, 102830, 102831 | 400..499 (próx 400) |
| LECTOR3  | `01005` | 102832, 102833, 102834, 102835, 102836 | 500..599 (próx 500) |

**Regla de ruta vigente**: ruta = `lpad(ciclo,2,'0') || lpad(libreta,3,'0')`. 21 ciclos × 999 libretas. Ej. ciclo 1 + libreta 1 = `01001`. Backend la aplica al guardar cliente desde el portal.

---

## 2. Guion de prueba para los lectores (mañana en la mañana)

### 2.1 Pruebas felices (objetivo: cada lector emite 5 facturas sin conflicto)

**Cada lector tiene su propia ruta con 5 medidores** (ver tabla en sección 1). No hay solapamiento entre lectores: cada uno solo ve sus clientes al descargar.

Cada lector logueado con su usuario hace lo siguiente:

1. **Login** con su usuario (mayúsculas) y contraseña:

| Usuario | Password |
|---|---|
| `jmurcia` | `jmurcia` |
| `KGARCIA` | `apc123` |
| `LECTOR1` | `lector1` |
| `LECTOR2` | `lector2` |
| `LECTOR3` | `lector3` |

2. **Descargar información** → debe bajar **5 medidores** (los suyos, según la ruta asignada en sección 1).
3. Para cada uno de los 5 medidores: ingresar lectura (cualquier número entero, condición `N`).
4. Cada factura local debe mostrar **L 331.11** (cargos del periodo, sin saldo previo porque los clientes están limpios).
5. **Subir información** → debe sincronizar con `Sincronizadas: 5, Errores: 0, Conflictos: 0`.
6. Cada lector consume correlativos consecutivos de **su propio bloque** (rangos en sección 1).

**Resultado esperado al final del día:** 25 facturas creadas, 1 por cliente, sin conflictos entre lectores.

**Pre-vuelo para cada celular:**

- **APK instalado:** debe ser el del 2026-04-26 19:13 (`AppLectoresAPC/app/build/outputs/apk/debug/app-debug.apk`). Si tienen uno anterior, reinstalar.
- **Red:** el celular debe estar en la red corporativa `172.16.0.x` (WiFi o VPN). Probar desde Chrome del celular: `http://172.16.0.9:2805/APCService.svc/GetCiclo/01001` debe devolver JSON.
- **Distribución del APK a otros lectores:** vía WhatsApp/email/Drive del archivo `app-debug.apk` (~10 MB). Habilitar "Orígenes desconocidos" en cada celular antes de instalar.

### 2.2 Pruebas de borde — 1 lector designado

Un lector designado (recomendado `KGARCIA`) prueba los siguientes casos:

| Caso | Acción | Resultado esperado |
|---|---|---|
| **Doble emisión** | Hacer una lectura, sincronizar OK; intentar una segunda lectura para el MISMO cliente en el mismo periodo | Server rechaza con HTTP 409 código `FACTURA_YA_EMITIDA`. App marca `Conflicto`. **NO se duplica factura.** |
| **Reintento por red** | Hacer lectura sin red. Sincronizar (debe quedar `PENDING_SYNC`). Reconectar y volver a sincronizar | Server responde `IDEMPOTENTE`, no crea factura nueva. |
| **Bloqueo de re-descarga** | Tener una lectura en `Conflicto` o `Error` y darle a "Descargar información" | App muestra mensaje "Descarga bloqueada: hay X lecturas con conflicto/error pendientes…" sin tocar SQLite. |
| **Sync exitoso visible en portal** | Sincronizar 1 lectura. Luego abrir portal → Captación de Pagos → buscar el cliente | La factura aparece con su número y total correctos. |

---

## 3. Trabajo del día (en orden de prioridad)

### 3.1 Bloqueantes para que las pruebas tengan sentido fiscal real

#### A) Bug `sp_obtener_cliente_saldo` — falta filtrar por `estado='A'`

- **Síntoma:** transacciones anuladas (estado=`N`) siguen contando como saldo previo del cliente porque el SP toma la última fila sin filtrar.
- **Fix (~5 min):** modificar el SP para incluir `WHERE ta.estado='A'`. Verificar antes que ningún otro consumidor lo use con expectativa contraria (`grep` en `Prestadoras/SIAD.Services` y en `WSappLectores`).
- **Impacto si no se hace:** una vez que un cliente facture y luego anulen su factura, el motor seguirá contándole saldo previo en la siguiente.

#### B) Snapshot V3 debe incluir saldo previo del cliente

- **Síntoma:** el `sp_adm_generar_snapshot_offline_cliente_lectura` no incluye `saldo_anterior_total`. El app calcula solo cargos del periodo (sin saldo previo) y emite L 331.11. El server suma saldo previo y exige L XYZ. Diferencia → `SYNC_CONFLICT_TOTAL`.
- **Hoy** los clientes están "limpios" (sin deuda) y por eso funciona, pero no es realista.
- **Fix (cross-componente):**
  1. Agregar campo `saldo_anterior_total` (numeric) al jsonb de `snapshot_json` que devuelve el SP.
  2. App: leer `saldo_anterior_total` del snapshot, sumarlo al total local antes de imprimir y enviar al WS.
  3. Recompilar APK + reinstalar en dispositivos.
  4. Validar con un cliente que tenga deuda previa real.
- **Estimado:** 1-2 horas.

### 3.2 Mejoras UI/UX acordadas

#### C) Vista `usuarioapc` (mantenimiento de lectores)

- Agregar combo **Ciclo** (catálogo `ciclos`) y combo **Ruta** (filtrado por ciclo, igual que `ClienteForm.razor`).
- **Validación:** `ruta` debe empezar con `lpad(codciclo, 2, '0')`. Si no, error.
- **Sin inputs libres** para estos campos.

#### D) `ClienteForm.razor` — combo Ruta con template código + descripción

- Hoy el combo muestra solo el `Display` (código - descripción concatenado). Cambiar a un template DevExpress que muestre **código en negrita** arriba y **descripción** debajo.
- Usar el MCP de DevExpress para buscar el patrón correcto (`ItemTemplate` de `DxComboBox`).

#### E) `ClientesList.razor` — agregar columnas Ciclo y Ruta

- Hoy la lista de clientes no muestra a qué ciclo/ruta pertenecen. Agregar 2 columnas nuevas con sort y filtro.
- La ruta se extrae del indicativo: `split_part(maestro_cliente_indicativo_ruta, '-', 3)`.

### 3.3 Consolidar deuda técnica acumulada

- **Migrar estados a numérico** (lookup tables). Decisión tomada el 2026-04-26. Ver `feedback_estados_numericos.md`. Es un PR grande, programar para esta semana.
- **Eliminar más residuos `V3` literales** en strings visibles (Toast IDs, query params son OK). Buscar con `grep V3` en `apc.Client/Pages` después de los cambios anteriores.
- **Vista UI en app de "Lecturas con problema"** con botones explícitos "Reintentar mismo UUID" y "Marcar para reproceso". Hoy las lecturas en `Conflicto` se ven con badge en la lista normal pero no hay vista dedicada con acciones.
- **Portal — Conflictos y reprocesos:** agregar acciones reales (Aceptar valor servidor / Anular y reprocesar / Forzar valor app). Hoy solo "marcar revisado".

---

## 4. Cómo retomar al iniciar el día

1. Validar que PROD sigue como se dejó:
   ```bash
   curl http://172.16.0.9:2805/APCService.svc/GetCiclo/01001
   # debe devolver {"GetCicloResult":{"Anio":2026,"Ciclo":1,"Mes":2}}
   ```
2. Compartir credenciales con los lectores (jmurcia/jmurcia, KGARCIA/apc123, LECTOR1/lector1, LECTOR2/lector2, LECTOR3/lector3).
3. Asegurarse que **el APK instalado en cada celular es el del 2026-04-26 19:13** (`AppLectoresAPC/app/build/outputs/apk/debug/app-debug.apk`). Si no, reinstalar.
4. Empezar pruebas felices (sección 2.1).
5. Mientras los lectores prueban, atacar fix `sp_obtener_cliente_saldo` (sección 3.1.A) y el snapshot con saldo previo (3.1.B).

---

## 5. Limpieza recomendada antes de cerrar la jornada

Al terminar el día con todos los lectores facturando 5 veces cada uno (~25 facturas en total), el bloque CAI llegará a correlativo ~127. Conservar las facturas como evidencia de las pruebas.

Si se quiere reset para volver a probar otro día:
- Anular todas las facturas activas del periodo 2026/2 ciclo 01.
- Resetear `correlativo_actual` del bloque a 103 (próximo libre).
- Limpiar saldos en `transaccion_abonado` (ver bug en 3.1.A).
- Reabrir el periodo (nuevo mes en `historialmes`).

---

## 6. Pendientes que NO se atacan mañana (siguiente sprint)

- Migración masiva de **estados string → numérico** en BD, backend, app, portal. Es trabajo de varias horas, programar PR aparte.
- Anular factura 1 (`000-01-000000000002` L 331.11 estado N) y factura 2 (`000-01-000000000004` L 662.22 estado N) ya están en estado anulado, no requieren acción.
- Tests automatizados del flujo V3 en backend (ninguno hoy).
- Onboarding de un segundo dispositivo móvil para probar emisión paralela y detectar colisiones de correlativo entre lectores.
