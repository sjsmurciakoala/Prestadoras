# F8 — Contrato del WS bancario SIMAFI (CONGELADO)

**Fecha de extracción:** 2026-07-04
**Fuentes (solo lectura, `E:\Koala\proyectos\SIMAFI_WS`):**

- Código Java (fuente de verdad): `Web Service\RS_SIMAFI\simafi\src\java\com\siafi\serv\` →
  `SrvAutorizacion.java`, `SrvConsulta.java`, `SrvPago.java`, `SrvReversion.java`, `Heartbeat.java`
  + `dao/Control.java`, `dom/*.java`, `filtro/*.java`, `ApplicationConfig.java`.
- Variante desplegada: el log de producción 2019 (`rs_simafi/LOG_SIMAFI.log`) casa con la copia
  `simafi-test/` (la consulta de servicios filtra además por `abonado`); `simafi/` es igual en
  forma HTTP/XML. La carpeta `trash/` es una versión anterior.
- Colecciones Postman: `SIMAFI.postman_collection.json` (v1) y `SIMAFIob.postman_collection.json`
  (v2.1) — requests reales, **sin respuestas guardadas** (`response: []`).
- Logs reales 2015/2019 (`rs_simafi/*.log`): tráfico del banco `002` (referencias tipo
  `915813005409000`, sucursal `207`, cajero `2138`) — confirman formatos de fecha/hora/monto y el
  XML de `<mensaje>`.

> D8: este contrato está **CONGELADO**. La certificación bancaria ya existe y el banco consume el
> WS en producción. F8 es una migración con respuestas byte-compatibles, no una certificación nueva.

## 0. Verificación contra producción SIMAFI (2026-07-04, solo lectura autorizada)

Consultas `SELECT`/`SHOW` contra MySQL `172.16.0.3/bdsimafi` (autorizado por el usuario):

- **El canal está VIVO**: `pagos_bancos` registra pagos de HOY de los bancos `001` (Occidente),
  `002` (Davivienda), `015` (Banpaís), `028` (Ficohsa), `051` (Comixven) y `777` (Tigo Money);
  también hay reversiones recientes (`001` el 2026-06-30). `tipofa='O'` (otros) también se usa
  (2,690 filas). **SIMAFI comercial sigue facturando** (filas de `pagovariostemp` con período
  2026/06 creadas hoy) — condición dura para el cutover (§ plan de cutover).
- **Granularidad de `pagovariostemp`** (tipofa='S'): una fila por CONCEPTO de la factura vigente
  del abonado (1 recibo, 1 período por clave), incluyendo líneas **negativas y cero**:
  `01 Saldo Anterior` (−778.67), `02 Agua Potable`, `03 Alcantarillado Sanitario`,
  `04 Fondo Fuentes de Agua/Fondo Ambiental`, `05 Tasa ERSAPS`, `12 Convenio de Pago`,
  `16 Pagos` (−272.41), `17 Descuentos o Rebajas` (0.00), `112-03 Reconexion`, `15 Creditos`.
  `totalMora` = `SUM(valor)` = total neto a pagar.
- **Estados** de `pagovariostemp.estado`: pendiente = `''` (cadena vacía — la cabecera de la
  consulta emite `<estado></estado>` vacío), `'A'` aplicado, `'R'` reversado.
- **`recolector`** (credenciales): llaves reales de 26–42 caracteres, siempre mayúsculas (no
  todas vienen de `genkey`; hay llaves manuales). `vigencia` es DATE y hay bancos activos con
  vigencia vencida desde 2015 → confirma que la vigencia NUNCA se valida. `idBancoWS` con llave:
  001, 002, 005, 006, 015, 022, 028, 045, 050, 051, 070, 555, 777.
- **`pagos_bancos`**: `referencia varchar(15)`, `fechap date`, `horap time`, `montop decimal(12,2)`,
  `banco char(3)`, `sucursal/agencia char(3)`, `cajero char(10)`.
- Charset de la BD vieja: latin1; el WS emitía UTF-8 en el XML.

---

## 1. Convenciones generales

| Aspecto | Contrato |
|---|---|
| Base path | `/simafi/api` (context root `simafi` + `@ApplicationPath("api")`) |
| Autenticación | `?key={llave}&banco={idBanco}` por **query string** en todas las requests del banco |
| Transporte | HTTP plano (el WS viejo corre sobre GlassFish 3 sin TLS propio) |
| Content-Type respuestas XML | `application/xml` (JAXB; charset UTF-8) |
| Declaración XML | `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` (marshaller JAXB por defecto) |
| Códigos HTTP | Solo `200` (éxito) y `400` (todo error de negocio/validación/servidor) |
| Fechas request | `fechaRegistro`/`fechaEfectiva`: `yyyy-MM-dd`; `horaRegistro`: `HH:mm:ss` (confirmado en log 2019) |
| Montos | `monto` request: decimal con punto (`222.42`); `valor`/`totalmora` respuesta: 2 decimales |
| Trailing slash | Las rutas registradas terminan en `/` (`/servicios/{clave}/`, `/pago/servicios/`); JAX-RS acepta con y sin — el nuevo router debe aceptar ambas |

### 1.1 Error genérico `<mensaje>` (Handle)

Toda condición no-exitosa responde HTTP 400 con este cuerpo (elementos y valores exactos):

```xml
<mensaje>
    <error>false|true</error>
    <estado>400</estado>
    <mensaje>TEXTO EXACTO</mensaje>
</mensaje>
```

- `error=false` → rechazo de negocio (no existe, sin pendientes, vencidas, no autorizado…).
- `error=true` → error de servidor/validación dura (`Problema con el Web servidor`, campos vacíos).
- ⚠️ Orden de elementos: ver §5 Ambigüedades (JAXB sin `propOrder`; el `toString()` del filtro de
  auth imprime `estado, error, mensaje`, el marshaller JAXB puede emitir otro orden).

## 2. Endpoints

### 2.1 `GET /simafi/api/heartbeat`

- Sin auth. Respuesta `200`, cuerpo literal `ok ok` (texto plano). El banco probablemente lo monitorea.

### 2.2 Autorización — `SrvAutorizacion` (respuestas de TEXTO, no XML, salvo el Handle)

| Ruta | Query | 200 | 400 |
|---|---|---|---|
| `GET /auth/` | `key`, `banco` | **cuerpo vacío** | Handle XML `No autorizado` (`error=false`) |
| `GET /auth/vigencia/` | `banco` | texto `Vigencia:<valor>` o `Vigencia: permanente` | texto `No existe registro de banco` |
| `GET /auth/genkey/` | `banco`, `vigencia` (opcional) | texto `Llave actualizada` | texto `No se puede actualizar llave` |

Semántica de `genkey` en el WS viejo (tabla MySQL `recolector(idBancoWS, llave, vigencia)`):

- `UPDATE recolector SET llave = replace(password(<millis>),'*',''), vigencia = ? WHERE idBancoWS = ?`
  → la llave es el hash `PASSWORD()` de MySQL sin el `*`: **40 hex uppercase**. Si el banco no
  existe (0 filas afectadas) → 400.
- **La llave nueva NO se devuelve en la respuesta** — se comunica fuera de línea. `vigencia` se
  guarda tal cual (NULL si vacía).
- ⚠️ `validarLlave` compara solo `idBancoWS + llave`; **la vigencia jamás se valida** (solo se
  reporta en `/auth/vigencia`). Replicar: la expiración NO bloquea.

### 2.3 Autenticación por request (filtro)

En el WS viejo el filtro servlet aplica **solo a `/api/consulta/*`** y hace un loopback a `/auth`:

- Falla → HTTP 400, cuerpo = `Handle.toString()` **sin declaración XML** (orden `estado, error, mensaje`):
  `<mensaje><estado>400</estado><error>false</error><mensaje>No autorizado</mensaje></mensaje>` + salto de línea.
- ⚠️ Bypass del WS viejo: sin `banco`, o `banco` ∈ {"9","99","999"} → **no valida la llave**
  (`"999".contains(banco)`). Pago y reversión **no validan llave en absoluto**.
- **Decisión F8 (más estricta, acordada en el plan):** validar `banco`+`key` contra
  `ban_ws_credencial` en TODAS las rutas de negocio (consulta, pago, reversión) y usar la
  credencial para resolver el tenant. El banco ya envía `key`+`banco` en todas las requests
  (Postman + logs), así que no rompe al cliente real. El bypass "999" NO se replica.

### 2.4 Consulta — `GET /consulta/servicios/{clave}/?abonado=` y `GET /consulta/otros/{clave}`

`clave` = clave del abonado que dicta el cliente (SIMAFI 9 dígitos, ej. `090802740`).
`abonado` (query, opcional, default `""`) — la variante desplegada lo usa como filtro adicional.

**200 — hay facturas pendientes:**

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<factura>
    <cabecera>
        <clave>090802740</clave>
        <comentario>...</comentario>            <!-- omitido si NULL -->
        <direccion>COL. EJEMPLO</direccion>     <!-- solo servicios; omitido si NULL -->
        <estado>P</estado>
        <fechaVence>N</fechaVence>              <!-- 'S' = vencida, 'N' = no -->
        <nombreAbonado>NOMBRE DEL ABONADO</nombreAbonado>
        <totalMora>222.42</totalMora>           <!-- SUMA de todos los valores pendientes -->
    </cabecera>
    <detalle>
        <codigoConcepto>001</codigoConcepto>
        <concepto>DESCRIPCION LINEA</concepto>
        <id>12345</id>
        <valor>44.48</valor>
    </detalle>
    <!-- un <detalle> por fila pendiente -->
</factura>
```

- La cabecera sale de la PRIMERA fila (con su `detalle` en null ⇒ omitido); campos `ano`, `mes`,
  `expediente` nunca se llenan en servicios ⇒ omitidos.
- `totalMora` en SIMAFI = `SUM(valor)` de las filas pendientes de la clave (a pesar del nombre, es
  el **total a pagar**, no solo mora).

**400 (`error=false`):** en este orden de evaluación —

1. Sin filas → `No existe registro`
2. Primera fila con `estado='A'` (ya aplicado) → `No hay pagos pendientes`
3. Primera fila con `vence < hoy` (`fechaVence='S'`) → `Las facturas estan vencidas`

**400 (`error=true`):** excepción → `Problema con el Web servidor`.

### 2.5 Pago — `POST /pago/servicios/?abonado=` y `POST /pago/otros/`

Request (`Content-Type: application/xml`; el orden de elementos de entrada es libre para JAXB):

```xml
<pago>
    <banco>002</banco>
    <cajero>2138</cajero>
    <clave>090802740</clave>
    <fechaEfectiva>2019-06-07</fechaEfectiva>
    <fechaRegistro>2019-06-07</fechaRegistro>
    <horaRegistro>13:00:55</horaRegistro>
    <monto>222.42</monto>
    <referencia>915813005409000</referencia>
    <sucursal>207</sucursal>
</pago>
```

(`expediente` existe en el modelo pero no se usa en servicios/otros.)

**Validaciones en orden (todas → 400):**

| Condición | mensaje | error |
|---|---|---|
| `fechaRegistro` vacía | `Fecha de registro no puede estar vacia` | true |
| `referencia` vacía | `Referencia no puede estar vacia` | true |
| `banco` vacío | `Codigo de banco puede estar vacio` (sic) | true |
| `clave` null | `Clave no puede estar vacio` | true |
| consulta sin filas | `No existe registro.` (con punto) | true |
| primera fila `estado='A'` | `No hay pagos pendientes.` (con punto) | false |
| **solo servicios:** `monto != totalMora` (igualdad exacta) | `Total a pagar no coincide con el monto.` | false |
| falla interna al pagar | `No se puede pagar, revisar log de Servidor` | true |
| excepción | `Problema con el Web servidor` | true |

**200 — éxito:**

```xml
<mensaje><error>false</error><estado>200</estado><mensaje>Pago exitoso</mensaje></mensaje>
```

Semántica del pago viejo (servicios): marca TODAS las filas pendientes de la clave con
`estado='A'` + `referencia` + `cajero`, inserta bitácora en `pagos_bancos`
(`idreg='P', clave, fechap, horap, referencia, banco, agencia/sucursal, cajero, recibo, montop`)
y ejecuta `sp_posteo(recibo, banco, cajero, referencia)`.

⚠️ Idempotencia del WS viejo: solo rechaza si la `referencia` existe con `estado='R'`
(reversada) — el replay de un pago ya aplicado devuelve 400 `No hay pagos pendientes.`.
**Decisión F8 (spec del plan, más fuerte):** mismo `referencia`+`banco` ya aplicado ⇒ repetir la
**misma respuesta 200 `Pago exitoso` sin duplicar nada** (test obligatorio). Referencia ya
reversada ⇒ se mantiene el rechazo.

### 2.6 Reversión — `POST /reversion/servicios/` y `POST /reversion/otros/`

Request: `<reversion>` con los mismos hijos que `<pago>`.

**Validaciones en orden (→ 400):** las 4 de campos vacíos (idénticas al pago, `clave` con
`isEmpty`), luego búsqueda **por `referencia`**:

| Condición | mensaje | error |
|---|---|---|
| referencia inexistente | `No existe numero de referencia, para reversar` | false |
| estado ≠ 'A' — servicios | `Numero de referencia ya fue reversado` | false |
| estado ≠ 'A' — otros | `No existe numero de referencia, para reversar` | false |
| falla interna | `No se puede reversar` | true |
| excepción | `Problema con el Web servidor` | true |

**200 — éxito:** `<mensaje>` con `estado=200`, `error=false`, `mensaje=Reversion exitosa`.

Semántica vieja: filas de la referencia → `estado='R'`, `referencia=''`; bitácora `pagos_bancos`
con `idreg='R'`; `sp_reversion(referencia, cajero, banco)`.

### 2.7 `GET /pago/dummy`

Endpoint de prueba que devuelve un `<pago>` fijo (banco 001, cajero JOSEFO…). Se replica por
completitud; no lo usa el banco.

## 3. Mapeo al modelo SIAD (implementación F8)

| Concepto SIMAFI | SIAD |
|---|---|
| `clave` del abonado | `cliente_maestro.maestro_cliente_clave` (== `factura.clientecodigo`) |
| Facturas pendientes | `factura` con `estado IN ('A','B')`, saldo = `SUM(factura_detalle.montovalor_saldo ?? montovalor)`; orden FIFO `fechaemision ASC, numrecibo ASC` |
| `totalMora` | suma de saldos pendientes de todas las facturas (la mora SIAD ya viene facturada como detalle/recargo) |
| `<detalle>` | ver pregunta abierta §5.3 (por factura vs por línea de detalle) |
| Pago | aplicación FIFO reusando el flujo F4 de abonos (derrame `montovalor_saldo`, `transaccion_abonado` 202, estado B/C) + `ban_kardex` DEP en la cuenta del banco + comprobante módulo BANCOS vía `sp_con_generar_comprobante_config` (Debe cuenta de `ban_cuenta`/`BANCO_DEFAULT`, Haber CxC analítica); sin período abierto ⇒ `con_partida_pendiente` (`encolar_sin_periodo`) |
| Idempotencia / bitácora | tabla nueva `ban_ws_pago` (equivalente de `pagos_bancos`): `company_id, banco, referencia, clave, monto, fechas, sucursal, cajero, ban_kardex_id, estado A/R` + AK `(company_id, banco, referencia)` |
| Credenciales | tabla nueva `ban_ws_credencial` (equivalente de `recolector`): `company_id, banco, llave, vigencia, banco_cuenta_id, activo` — resuelve el tenant y la cuenta bancaria destino |
| Reversión | por `referencia` sobre `ban_ws_pago`: restituye saldos de detalles, anula transacciones 202, contramovimiento kardex, `sp_con_revertir_comprobante_config` |

## 4. Divergencias deliberadas vs el WS viejo (aprobadas por el plan)

1. **Auth en todas las rutas** (viejo: solo consulta; bypass "999"). El banco no percibe cambio.
2. **Idempotencia real por referencia** (viejo: replay → 400 `No hay pagos pendientes.`).
3. Los errores 200-con-texto de SQLException del viejo (p. ej. `validarVigencia` devolvía 200 con
   el mensaje de la excepción) no se replican: error interno ⇒ 400 `Problema con el Web servidor`.

## 5. Ambigüedades — RESUELTAS (decisiones del 2026-07-04)

### 5.1 Orden de elementos XML en respuestas JAXB → alfabético + ajustable

Las clases Java no declaran `@XmlType(propOrder)` ni `@XmlAccessorOrder`: el orden que emite el
JAXB RI de GlassFish 3 con orden *UNDEFINED* no es reconstruible con certeza desde el código y no
hay respuestas capturadas. **Decisión (usuario):** implementar con orden **alfabético** de
propiedades (hipótesis JAXB estándar) usando `[XmlElement(Order)]` explícito en los DTOs .NET
(trivial de ajustar), y **capturar UNA consulta real del WS vivo durante la preparación del
cutover** para confirmar/ajustar los golden files antes del corte (queda en el plan de cutover).
Orden resultante: `<factura>`: cabecera, detalle…; `<cabecera>`: ano?, clave, comentario?,
direccion?, estado, expediente?, fechaVence, mes?, nombreAbonado, totalMora; `<detalle>`:
codigoConcepto, concepto, id, valor; `<mensaje>`: error, estado, mensaje. (`?` = omitido si NULL.)
Excepción: el rechazo de auth del filtro usa el orden literal `estado, error, mensaje` sin
declaración XML (es un `toString()` manual, verificado en el log).

### 5.2 Regla "Las facturas estan vencidas" → SE REPLICA

**Decisión (usuario): replicar el bloqueo.** Si la factura pendiente más antigua (FIFO) tiene
`fechavence < hoy`, la consulta responde 400 `Las facturas estan vencidas` (`error=false`), igual
que el WS viejo. `fechavence` NULL no bloquea (equivale al ELSE 'N' de SIMAFI). El pago no evalúa
la regla (igual que el viejo: el pago solo exige monto == total).

### 5.3 Granularidad de `<detalle>` → por concepto (verificado en producción SIMAFI)

Verificado contra `pagovariostemp` (§0): una fila por CONCEPTO de la factura vigente, con líneas
negativas (Saldo Anterior, Pagos) y cero. Mapeo SIAD: **una fila `<detalle>` por línea de
`factura_detalle` con saldo pendiente ≠ 0, de todas las facturas pendientes en orden FIFO**
(`codigoConcepto` = `factura_detalle.codigo` o, si vacío, el código del servicio
(`tiposervicio`); `concepto` = descripción de la línea; `valor` = saldo pendiente de la línea;
`id` = id de la línea). En SIAD no hay línea "Saldo Anterior" porque cada factura pendiente
aparece con sus propias líneas — el total neto (`totalMora`) es equivalente.

### 5.4 Filtro `abonado` de la variante desplegada

La versión desplegada filtra `clave + abonado`. Si el banco no envía `abonado` (default `""`), en
SIMAFI igual devolvía filas (el campo estaba vacío en la tabla temp — verificado en §0: columna
`abonado` vacía). En SIAD el parámetro se acepta y se ignora.
