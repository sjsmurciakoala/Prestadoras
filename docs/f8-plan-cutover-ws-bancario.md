# F8 — Plan de cutover del WS bancario (SIMAFI → apc.BancosWs)

**Estado:** DOCUMENTO — NO se ejecuta hasta agendar la ventana (decisión de deploy:
F1–F8 van en UN solo paquete; el cutover del banco es una ventana APARTE y posterior).
**Contrato:** [f8-contrato-ws-bancario.md](f8-contrato-ws-bancario.md) (D8: congelado).
**Regla de oro:** en todo momento los pagos deben caer en **UN solo sistema de registro**.

---

## 0. Precondición dura (verificada el 2026-07-04)

**SIMAFI comercial sigue vivo y facturando** (período 2026/06, ~64k líneas pendientes,
6 recolectores pagando HOY por el WS: 001 Occidente, 002 Davivienda, 015 Banpaís,
028 Ficohsa, 051 Comixven, 777 Tigo Money — además 045, 555 y otros con llave activa).

> El cutover del WS bancario **solo puede ocurrir cuando TODA la cartera esté
> facturando en SIAD**. Si el corte se hace antes, los abonados aún facturados en
> SIMAFI no aparecerían en la consulta del WS nuevo ("No existe registro") y el
> canal de recaudación bancaria quedaría roto para ellos. No hay fallback dual
> (por diseño: un solo sistema de registro).

Además, ANTES de la ventana:

- [ ] Scripts F1–F8 aplicados en producción y portal publicado (ventana de deploy F1–F8).
- [ ] `con_integracion_config.activo_bancos = true` + asiento módulo `BANCOS`
      (diario + tipo) + matriz CXC/BANCO_DEFAULT completa (pantalla Integración).
- [ ] Tipo `DEP` en `ban_tipos_transacciones` de la empresa.
- [ ] `ban_cuenta` por recolector definida con el área contable (`cont_account_id` puesto).
- [ ] Clientes SIAD con la MISMA clave que dictan al banco (las claves SIMAFI de
      9 dígitos = `cliente_maestro.maestro_cliente_clave`, verificado).

## 1. Verificación del contrato con captura real (pendiente §5.1 del contrato)

Durante la preparación (sin tocar nada del canal):

1. Capturar 1 respuesta real del WS viejo: `GET http://<ws-viejo>/simafi/api/consulta/servicios/<clave>/?key=<key>&banco=<banco>`
   (solo lectura) con `curl -s -D headers.txt -o body.xml`.
2. Diff contra la respuesta del WS nuevo para un cliente equivalente en SIAD:
   **orden de elementos**, declaración XML, Content-Type y encoding.
3. Si el orden difiere de la hipótesis alfabética: ajustar `ContractXml` + golden
   files de `SIAD.Tests/GoldenFiles/BancosWs/` (un solo lugar) y re-correr la suite.
4. **Shadow testing** (recomendado): apuntar una réplica del tráfico de CONSULTA
   (solo GET) al WS nuevo durante unos días y comparar respuestas. Nunca duplicar
   pagos/reversiones.

## 2. Migración de llaves

En la ventana (las llaves NUNCA pasan por el repo):

```sql
-- MySQL bdsimafi (origen):
SELECT idBancoWS, descripcion, llave, vigencia
FROM recolector WHERE idBancoWS IS NOT NULL AND llave IS NOT NULL;

-- PostgreSQL siad_v3 (destino): plantilla del final de
-- Database/ddl_v3/20260704_ci_fase8_ws_bancario.sql (INSERT ... ON CONFLICT),
-- asignando banco_cuenta_id por recolector con contabilidad.
```

Verificar: `GET /simafi/api/auth/?key=<llave>&banco=<banco>` → 200 por cada banco activo.

## 3. Ventana de corte

**Coordinación previa:** fecha/hora acordada con los bancos (o con operaciones si el
repunte es transparente por DNS/proxy). Ventana sugerida: madrugada de día hábil bajo
(el canal 777/Tigo opera 24/7 — no hay hora sin tráfico, elegir mínimo histórico).

Secuencia:

1. **T-1h — Congelar reversiones cruzadas:** avisar a los bancos que desde el corte
   NO envíen reversiones de pagos anteriores al corte por el WS: una reversión de un
   pago pre-corte (registrado en SIMAFI) NO existe en SIAD y responderá
   `No existe numero de referencia, para reversar`; se maneja MANUALMENTE
   (nota de crédito/ajuste en el sistema que registró el pago).
2. **T-15m — Snapshot:** backup de `siad_v3` + dump de `pagovariostemp`/`pagos_bancos`
   de SIMAFI (evidencia del estado pre-corte y base para conciliar).
3. **T0 — Repunte:** cambiar el destino del host/puerto que consume el banco
   (IP/DNS o regla del proxy/NAT) del GlassFish viejo → `apc.BancosWs`
   (publicado con `./publish-onprem.ps1 -Solo bancosws`; el binding debe servir
   `/simafi/api/...` idéntico — el path es parte del contrato).
   **El WS viejo NO se apaga:** se deja corriendo sin tráfico (rollback en segundos)
   pero idealmente detrás de una regla que solo permita tráfico interno.
4. **T+5m — Humo:** `GET /simafi/api/heartbeat` → `ok ok`; `GET /auth/` con cada
   llave migrada → 200; consulta de un cliente conocido → XML correcto.

## 4. Monitoreo intensivo (primeras horas / primer día)

- Log de `apc.BancosWs` (stdout/EventLog IIS): errores por request.
- Primeras transacciones reales, POR CADA banco que pague:
  ```sql
  SELECT * FROM ban_ws_pago ORDER BY pago_id DESC LIMIT 20;             -- canal
  SELECT * FROM ban_kardex ORDER BY ban_kardex_id DESC LIMIT 20;        -- banco
  SELECT h.* FROM con_partida_hdr h WHERE h.module='BANCOS'
   AND h.document_type='PGB' ORDER BY h.poliza_id DESC LIMIT 20;        -- GL
  SELECT * FROM con_partida_pendiente WHERE module='BANCOS'
   AND status_id=1;                                                     -- cola (debe tender a 0)
  ```
- Cuadre del día 1: total de `ban_ws_pago` del día == suma de kardex DEP del canal
  == Debe de las pólizas PGB del día; facturas pagadas en estado C.
- Verificación de saldos F6: `SELECT * FROM fn_con_verificar_saldo_cuenta(<company>);` → 0 divergencias.
- El banco confirma de su lado el corte de recaudación del día (conciliación T+1).

## 5. Rollback

Disparadores: consultas/pagos fallando de forma sistemática, doble registro, o el
banco reporta errores de parseo (contrato).

1. Repuntar IP/DNS/proxy de vuelta al WS viejo (GlassFish sigue vivo) — segundos.
2. **Pagos que alcanzaron a caer en SIAD durante la ventana:** NO se borran.
   Se reconcilian manualmente: o se replican en SIMAFI (registro operativo del
   canal restaurado) o se dejan en SIAD y se excluyen del corte SIMAFI del día —
   decidir UNO de los dos con contabilidad ANTES de la ventana y documentar cada
   referencia en la bitácora (`ban_ws_pago`).
3. Congelar reversiones cruzadas también durante el rollback (mismas reglas del §3.1).
4. Post-mortem antes de reintentar.

## 6. Post-corte (día 2 en adelante)

- Mantener el WS viejo instalado ≥1 mes (solo lectura de emergencia / evidencia).
- `sp_con_procesar_partida_pendiente` para la cola BANCOS si hubo pagos sin período
  (o el reproceso de F7 al abrir el período).
- Conciliación semanal `ban_ws_pago` vs estado de cuenta bancario por recolector.
- Retiro definitivo del WS viejo y del acceso MySQL del canal: decisión aparte,
  después de ≥1 mes estable (regla espejo de F7).

## 7. Divergencias funcionales a socializar con operaciones (no rompen al banco)

1. Auth en TODAS las rutas (el viejo solo validaba consulta; bypass "999" eliminado).
2. Replay de un pago ya aplicado → `Pago exitoso` (el viejo devolvía
   `No hay pagos pendientes.`); cero duplicación garantizada por referencia.
3. Re-pago de una referencia reversada → rechazado SIEMPRE (en el viejo el chequeo
   estaba roto — referencia se borraba al reversar).
4. La regla "Las facturas estan vencidas" se evalúa con el vencimiento de la
   factura VIGENTE del abonado (equivalente al comportamiento SIMAFI donde la
   factura del mes arrastra el saldo anterior).
