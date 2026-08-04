# Ensayo general E2E — portal + app lectores + app órdenes + WS bancos

Estado al 2026-08-04. Objetivo: los 4 frentes trabajando JUNTOS antes del
cutover. Mitad ya verificada automáticamente (abajo, con evidencia); la otra
mitad necesita teléfonos y se corre con este checklist.

## Parte A — YA VERIFICADO automáticamente (2026-08-04, local vs copia09)

### WS bancario (apc.BancosWs, puerto 8087) — ciclo COMPLETO en vivo ✔
1. Consulta `GET /simafi/api/consulta/servicios/{clave}?banco=OCC&key=…` →
   XML del contrato con facturas + cuotas de convenio (cliente 090802931,
   totalMora 10,837.89). ✔
2. Pago por el total exacto → "Pago exitoso"; `adm_pago` creado, saldo a 0. ✔
3. Idempotencia: la MISMA referencia repetida NO duplicó el pago. ✔
4. Reversión (lleva `banco+clave+referencia`) → "Reversion exitosa"; pago a
   estado 4 (reversado) y saldo del cliente restaurado al centavo. ✔
   Credencial local de prueba: banco `OCC` (solo local, no viaja al repo).

### App de lectores — lado servidor (apc.MobileApi, puerto 8088) ✔
1. Login `POST /api/lectores/login {codigo, clave, dispositivo}` → token (24 h).
   Lector de prueba local: `ENSAYOE2E` (bcrypt real vía pgcrypto). ✔
2. `GET /api/ciclos/{ruta}` → FIFO del ciclo abierto: devolvió ciclo 19 /
   2026-07. ✔
3. `GET /api/rutas/{ruta}/snapshot/ciclo/…` → 200 con el shape correcto.
   Salió vacío porque esa ruta ya está facturada en julio — el snapshot con
   clientes sale al ABRIR EL CICLO DE AGOSTO (paso B.1). ✔ contrato
4. La suite del repo (413 tests) cubre emisión V3, mora en snapshot, sync de
   capturas y golden files del WS — todo verde.

### Portal
Suite completa verde; el humo manual de pantallas es la lista que ya tiene el
usuario (clientes, caja, notas, cobranza, tarifario, reportería).

## Parte B — CHECKLIST con teléfonos (requiere al usuario)

### B.1 Preparación (portal)
- [ ] Abrir el ciclo comercial de AGOSTO (apertura integral) para tener rutas
      con pendientes.
- [ ] Confirmar IP LAN del PC (ipconfig) — los APK `lanApc` apuntan a ella.
- [ ] Portal corriendo (VS, https://localhost:5002), MobileApi (8088) y
      BancosWs (8087) arriba (`dotnet run` en cada proyecto).

### B.2 App de LECTORES (Flutter, APK lanApc)
- [ ] Login con un lector real (credencial del catálogo de lectores).
- [ ] Descargar ciclo/ruta → el snapshot trae clientes, mora y datos de ticket.
- [ ] Capturar 2-3 lecturas (una con foto, una con condición especial).
- [ ] Sincronizar → en el portal: facturas emitidas en Facturas App
      (`/mi-app/facturas`), con estado LEGIBLE.
- [ ] Imprimir ticket de una de ellas (informe Factura ticket).
- [ ] Cobrar UNA en caja → el saldo del cliente baja; volver a consultar en la
      app → ya no aparece pendiente.
- ⚠️ Pendientes del repo de la app ANTES del APK definitivo: merge de la rama
      `fix/mora-offline-en-total` + recompilar APK piloto.

### B.3 App de ÓRDENES DE TRABAJO (legacy, backend 8086)
El backend 8086 corre en el servidor (no en este PC) — este tramo se prueba
contra 0.9 DESPUÉS de su ventana, o donde viva el 8086 hoy.
- [ ] En el portal: crear orden de trabajo y ASIGNARLA al empleado del
      teléfono (regla del contrato: baja solo P + empleado=usuario).
- [ ] En el teléfono: descargar → aparece la orden.
- [ ] Atenderla con foto + GPS → en el portal: estado Atendida, foto y
      coordenadas visibles en el detalle.
- [ ] Cortes masivos: generar lote chico → la orden de corte baja al teléfono
      → cobrar al cliente en caja → la orden se cancela sola.

### B.4 Cruce final (todo junto)
- [ ] Un mismo cliente: emitido por la APP → visible en PORTAL → cobrado por
      el WS DEL BANCO (consulta+pago) → recibo/estado de cuenta correctos →
      reversa del banco → saldo restaurado → re-cobrado en CAJA → partida
      contable posteada (`/contabilidad/partidas`).

## Datos de prueba creados por el ensayo automático (solo local)
- Lector `ENSAYOE2E` (adm_lector_credencial 26) — borrar si molesta.
- Pago E2E-ENSAYO-20260804-01 del cliente 090802931: quedó REVERSADO
  (histórico legítimo de prueba; su saldo volvió a 10,837.89).
