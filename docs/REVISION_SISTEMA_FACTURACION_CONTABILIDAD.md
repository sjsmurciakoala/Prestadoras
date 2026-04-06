# Revision Integral: WS Lectores + App Lectores + Backend/DB

> Objetivo: anotar los puntos mas importantes del flujo de facturacion y su integracion futura con contabilidad.

## 1) Componentes principales (vision general)

- WS Lectores (WCF): `WSappLectores/WS_APC`
  - Servicio `APCService.svc` con endpoints REST/JSON para la app de lectura.
  - Conecta a PostgreSQL (`conexionpostgres`) y MySQL (`conexionmysql`, usado para log historico).
- App Lectores Android: `AppLectoresAPC`
  - Descarga datos del WS, guarda en SQLite local y calcula importes en el dispositivo.
  - Sube lecturas via `/ActualizarLectura`.
- Backend Web (APC): `Prestadoras/apc` + `Prestadoras/SIAD.Services`
  - API ASP.NET que maneja facturacion miscelaneos, pagos y notas.
  - Accede a PostgreSQL via `SiadDbContext`.
- Blazor Client: `Prestadoras/apc.Client`
  - UI para operaciones comerciales y consulta de datos.
- Base de Datos (PostgreSQL):
  - Facturacion: `historicomedicion`, `factura`, `factura_detalle`, `transaccion_abonado`.
  - Contabilidad: `con_partida_hdr` y `con_partida_dtl` (antes `con_poliza*`).

---

## 2) WS Lectores (APCService.svc) – Endpoints y SPs

### Endpoints principales

- `GET /GetRuta/{ruta}/Ciclo/{ciclo}/Anio/{anio}/Mes/{mes}`
  - SP: `sp_medidores_por_ruta_ws`
  - Devuelve medidores + saldos + banderas de servicio (ser1..ser10).

- `GET /GetCiclo/{ruta}`
  - SP: `sp_informacion_ciclo`
  - Devuelve periodo abierto (ciclo/anio/mes) para la ruta.

- `GET /GetCAI/{ruta}`
  - SP: `sp_cai_por_ruta`

- `GET /GetCobrosAdicionales`
  - SP: `sp_cobros_adicionales_ws`

- `GET /Tarifa`
  - SP: `sp_tarifas_ws`

- `GET /TarifaContador`
  - SP: `sp_tarifas_contador_ws`

- `GET /CondicionesLectura`
  - SP: `sp_condicion_lectura_ws`

- `GET /Informativos`
  - SP: `sp_informativo_ws`

- `GET /Configuraciones`
  - SP: `sp_configuracion_ws`

- `POST /ActualizarLectura`
  - SP: `sp_lectura`
  - Subida de lectura + calculo ya hecho en app.

### Puntos clave

- `ActualizarLectura` usa `ToChar1` para truncar campos que van a `char(1)`
  (`condicionlectura`, `categoria`, `tienemedidor`, `ser3`, `ser4`).
- Guarda en `UsuariosDiag.log` y expone `/UltimoError`.
- La app envia los montos calculados (`taservi1..4`, consumo, etc).
  El servidor no recalcula, solo registra.

---

## 3) App Lectores Android (AppLectoresAPC)

### URLs base

Definidas en `Utilidades.java`:

- Base: `http://172.16.0.9:2805/APCService.svc`
- Endpoints:
  `GetRuta`, `GetCiclo`, `GetCobrosAdicionales`, `Tarifa`, `TarifaContador`,
  `GetCAI`, `ActualizarLectura`, `Configuraciones`, `CondicionesLectura`,
  `Informativos`, `Usuarios`.

### SQLite local

- Base local: `BD_APC` (SQLite).
- Tablas clave: `CONFIGURACION`, `Tarifa`, `TarifaContador`, `CobroAdicional`,
  `Medidor`, `Usuario`.

### Calculo local de factura

- El calculo se hace en `Utilidades/GenerarFactura.java`.
- Usa:
  - `Tarifa` / `TarifaContador` (segun condicion).
  - `CobrosAdicionales` (por categoria).
  - `Configuraciones` por IDs fijos (1..6).
- El resultado se guarda en el dispositivo y se envia en `ActualizarLectura`.

### Detalle de calculo (actual)

1) **CAI y numero de factura**
   - Obtiene CAI local (`UtilidadesBD.getCAINuevo`) y arma `NumeroFactura`.

2) **Determinar tarifa base (agua)**
   - Si `TieneMedidor = "N"`:
     - Usa **Tarifa global** (`UtilidadesBD.GetInformacionTarifaBD`):
       - Filtro: `Tipo`, `Categoria`, `Codigo`.
       - `ValorAgua = Tarifa.Valor`.
   - Si `TieneMedidor = "S"`:
     - Usa **TarifaContador** (`UtilidadesBD.GetInformacionTarifaContadorBD`):
       - Filtro: `Tipo = 1` (hardcode), `Categoria`.
       - Calcula por rangos Min/Max:
         - Primera fila aplica `Cuota * consumo + Agua (valor base)`.
         - Siguientes filas aplican `Cuota * diferencia`.
         - `Alquiler` se suma si aplica.
       - Si consumo = 0 => `ValorAgua = BaseAgua`.

3) **Condicion de lectura**
   - `MIN` o `PND`: consumo = 0 (lect_act = lect_ant).
   - `PD`: consumo = promedio (usa `Promedio`).
   - `N`: mantiene lectura.

4) **Cobros adicionales**
   - Se cargan por categoria (SQLite) y se filtran por:
     - `GetAplicarCobroAdicional` → usa `serBase{IdCobro}` en el medidor.
   - Formula:
     - `Valor = (PorcentajeAgua * ValorAgua) + (PorcentajeAlcantarilla * ValorAlcantarilla)`
     - Si el concepto es alcantarillado (IdCobro = 2), se guarda `ValorAlcantarilla`.
   - Se separan en:
     - **Aplica descuento** (S)
     - **No aplica descuento** (N)

5) **Descuento**
   - Usa configuraciones fijas:
     - Agua: Id = 5
     - Alcantarillado: Id = 6
   - Limita descuento por tope.
   - Solo se descuenta Agua y Alcantarillado (si aplica).

6) **Saldos atrasados y recargos**
   - Se suman desde campos en SQLite:
     - Saldos: `saldo_anterior_*`
     - Recargos: `recargo_*`

7) **Total**
   - `ValorCuotaTotal = Agua + CobrosAdicionales + SaldosAtrasados + Recargos`
   - Redondeo a 2 decimales.
   - Si total < 0 → 0.

8) **Se guarda en SQLite**
   - Se actualiza el Medidor local y el CAI.

### Valores que la app envia al WS

En `UtilidadesBD.SendDataToServer`:
- `taservi1` = `ValorAgua`
- `taservi2` = Cobro adicional **Id 2** (Alcantarillado)
- `taservi3` = Cobro adicional **Id 3** (Ambiental)
- `taservi4` = Cobro adicional **Id 4** (ERSAPS)
- `Consumo`, `LecturaActual`, `CondicionLectura`, `DescuentoAplicado`, etc.

### Puntos sensibles

- Dependencia de IDs fijos de configuracion (1..6).
- Si la descarga de configuraciones falla, el calculo puede romperse o quedar incompleto.
- La logica de facturacion vive en el movil, no en el servidor.

---

## 4) Backend Comercial (Prestadoras/apc + SIAD.Services)

### Facturacion por lecturas (WS / BD)

SP `sp_lectura`:
- Actualiza `historicomedicion`.
- Inserta `factura`.
- Inserta `factura_detalle`.
- Inserta `transaccion_abonado`.

### Facturacion miscelaneos

- Servicio: `FacturacionMiscelaneosService.CrearReciboAsync`
- Inserta en `factura` (tipofactura = "R"), `factura_detalle`, `transaccion_abonado`.

### Notas credito/debito

- Servicio: `NotasCreditoDebitoService.RegistrarNotaAsync`
- Inserta `ajuste` + `ajustes_detalle`.
- Inserta `transaccion_abonado` con:
  - Nota credito: `tipotransaccion = "205"`
  - Nota debito: `tipotransaccion = "206"`

### Captacion de pagos

- Servicio: `CaptacionPagosService.RegistrarPagoAsync`
- Actualiza `factura`, `factura_detalle`.
- Inserta `transaccion_abonado` con:
  - Pago: `tipotransaccion = "201"`
  - `tipo_partida = "002"`

### Auxiliar de lectura

- Servicio: `AuxiliarLecturaService`
  - Abre/cierra periodos (`historialmes`).
  - Genera base de `historicomedicion` clonando mes previo o desde clientes.

---

## 5) Contabilidad (estado actual)

- Tablas principales:
  - `con_partida_hdr` (antes `con_poliza`)
  - `con_partida_dtl` (antes `con_poliza_linea`)

- Procedure disponible:
  - `sp_registrar_partida_contable`
    Inserta cabecera y detalle y valida cuadre.

Pendiente: No hay integracion automatica desde facturacion hacia contabilidad.

---

## 6) Puntos criticos (para el diseno contable)

- Codificacion hardcode en transacciones comerciales:
  - `201` (pago), `205/206` (notas), `tipofactura = "R"`,
    `tipo_partida = "01/002"`.
- Calculo de facturacion en movil: el servidor no recalcula montos.
- Mapeos de servicios se movieron a roles (`servicios_roles_ws`) en SPs del WS.

---

## 7) Proximo paso propuesto (fase 1: facturacion)

Para que lo comercial genere contabilidad:

1. Definir evento contable: `FACTURA_EMITIDA`.
2. Crear mapeo comercial -> contable (por rol de servicio):
   - `evento`, `rol_comercial`, `cuenta_contable`, `lado` (Debe/Haber), `diario`, `activo`.
3. Enlazar ese evento en:
   - `sp_lectura` (lecturas WS)
   - `FacturacionMiscelaneosService`
4. Crear partida contable usando `sp_registrar_partida_contable`.

---

## 8) Archivos clave consultados

- WS:
  - `WSappLectores/WS_APC/APCService.svc.cs`
  - `WSappLectores/WS_APC/IAPCService.cs`
  - `WSappLectores/WS_APC/Modelos/MedidorModelo.cs`
- App:
  - `AppLectoresAPC/app/src/main/java/com/example/alberwills/aguaspuertocortes/Utilidades/Utilidades.java`
  - `AppLectoresAPC/FLUJO_DE_DATOS.md`
- Backend:
  - `Prestadoras/SIAD.Services/FacturacionMiscelaneos/FacturacionMiscelaneosService.cs`
  - `Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`
  - `Prestadoras/SIAD.Services/NotasCreditoDebito/NotasCreditoDebitoService.cs`
  - `Prestadoras/SIAD.Services/AuxiliarLectura/AuxiliarLecturaService.cs`
- DB:
  - `Prestadoras/Database/2026-02-16_fix_sp_lectura_roles.sql`
  - `Prestadoras/Database/ddl_v3/PROCEDURE-public.sp_registrar_partida_contable.sql`
