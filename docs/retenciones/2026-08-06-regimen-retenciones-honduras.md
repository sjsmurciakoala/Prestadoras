# Régimen de retenciones en Honduras — referencia fiscal y contable

**Fecha:** 2026-08-06 · **Ámbito:** pagos a proveedores y retenciones en clientes · **Uso:** documento de referencia para el diseño del módulo de retenciones (ver el plan hermano `2026-08-06-plan-retenciones-proveedores-estado-y-mejoras.md`).

> ### Cómo leer los marcadores de confianza
> - **[OFICIAL]** — confirmado en SAR / SEFIN / ley o acuerdo.
> - **[SECUNDARIO]** — fuente contable/profesional hondureña, no verificada contra el texto legal.
> - **[PRÁCTICA]** — práctica contable común / criterio general, no es un dato normativo literal.
>
> **Advertencia:** varios PDFs oficiales (Ley ISR consolidada, Acuerdo 215-2010, Acuerdo 481-2017, tabla progresiva) están escaneados y no fueron legibles al redactar esto. Todo porcentaje marcado [SECUNDARIO] — en especial el **% del ISV que retiene el gran contribuyente** — debe confirmarse en la SAR / DET Live **antes de codificarlo** en el sistema. Este documento no sustituye la asesoría del contador.

---

## 1. Marco general y agentes de retención

Una **retención en la fuente** es el mecanismo por el cual quien **paga** (agente de retención) descuenta del pago un porcentaje de impuesto y lo **entera directamente al fisco** en nombre de quien **cobra** (sujeto retenido). Existen dos grandes familias en Honduras:

- **Retención de ISR** (Impuesto Sobre la Renta) — sobre la *renta* del que cobra.
- **Retención de ISV** (Impuesto Sobre Ventas) — sobre el *impuesto sobre ventas* de ciertos servicios.

Quién es **agente de retención** depende del concepto: para el 12.5% lo es cualquiera que pague honorarios/servicios; para el 1% lo son los grandes contribuyentes (> L.15M de ventas anuales); para el ISV lo son los grandes contribuyentes designados. El Estado (administración central y descentralizada) siempre retiene y entera en la Tesorería General de la República.

La retención puede ser **anticipo** (crédito a favor del retenido, aplicable contra su impuesto anual/mensual) o **impuesto definitivo** (caso de no residentes).

---

## 2. Retención de ISR a proveedores

### 2.1 Retención del 12.5% (honorarios y servicios) — [OFICIAL]

| Concepto | Detalle |
|---|---|
| **Porcentaje** | **12.5%** |
| **Sobre qué** | Honorarios profesionales, dietas, comisiones, gratificaciones, bonificaciones y remuneraciones por servicios técnicos. Personas naturales y jurídicas. |
| **Agente de retención** | Cualquier persona natural o jurídica que efectúe el pago o crédito. |
| **Base** | El monto del honorario/servicio, **sin ISV** [PRÁCTICA] (el ISR grava la renta, no el ISV). |
| **Carácter** | **Anticipo** del ISR del proveedor (crédito a su favor). |
| **Excepción** | Pagos bajo contrato de trabajo del ejercicio cuando esos honorarios, como única fuente, no superan el tramo exento de la tabla progresiva (antes se citaba el fijo de L.90,000; el FAQ actual del SAR remite a la tabla progresiva vigente). |
| **Plazo de entero** | Primeros **10 días** del mes siguiente a la retención. |
| **Base legal** | Artículo 50 de la Ley del ISR (FAQ SAR). |

### 2.2 Retención del 1% a proveedores — [OFICIAL]

| Concepto | Detalle |
|---|---|
| **Porcentaje** | **1%** en concepto de ISR. |
| **Agente de retención** | Personas jurídicas y comerciantes individuales con **ventas anuales > L.15,000,000**. |
| **A quién se retiene** | Proveedores de bienes y servicios **que NO estén sujetos al sistema de pagos a cuenta** (los que ya pagan a cuenta quedan exceptuados). |
| **Base** | Valor de la compra **sin ISV** [PRÁCTICA]. |
| **Carácter** | **Anticipo** del ISR del proveedor. |
| **Base legal** | Acuerdo **DEI-217-2010**. |

### 2.3 Retenciones a no residentes / pagos al exterior (Art. 5) — [OFICIAL]

Son **impuesto definitivo** (no anticipo). Base = monto bruto pagado o acreditado al no residente.

| Concepto de pago al exterior | Tasa |
|---|---:|
| Rentas de bienes muebles e inmuebles (con excepciones) | **25%** |
| Regalías de minas, canteras y recursos naturales | **25%** |
| Sueldos, salarios, comisiones y remuneración por servicios | **25%** |
| Regalías por patentes, marcas, derechos de autor, secretos industriales | **25%** |
| Renta por espectáculos públicos | **25%** |
| Películas, videotape, TV por cable y derechos similares | **25%** |
| Utilidades de empresas extranjeras (sucursales, subsidiarias, agencias) | **10%** |
| Dividendos y distribución de utilidades | **10%** |
| Intereses comerciales, bonos, títulos valores | **10%** |
| Operación de naves aéreas, marítimas y terrestres | **10%** |
| Telecomunicaciones y servicios de informática | **10%** |
| Primas de seguros y fianzas | **10%** |
| Cualquier otra renta no especificada | **10%** |

> **Alquileres a personas naturales:** una fuente secundaria menciona **10%** de retención de ISR [SECUNDARIO], no verificado en fuente oficial — confirmar con el contador.

---

## 3. Retención del Impuesto Sobre Ventas (ISV)

### 3.1 Tasas de ISV vigentes

- **15%** — tasa general [OFICIAL]. Base = precio antes de ISV (base × 1.15).
- **18%** — tasa selectiva sobre bebidas alcohólicas, cigarrillos/tabaco, bebidas gaseosas y boletos aéreos de clase ejecutiva/primera [SECUNDARIO en la lista exacta].
- Existe una canasta de bienes/servicios exentos o exonerados (alimentos básicos, medicinas, educación…).

### 3.2 Agentes de retención del ISV — grandes contribuyentes (Acuerdo 215-2010)

| Concepto | Detalle |
|---|---|
| **Quién retiene** | Sujetos categorizados como **grandes contribuyentes**, designados agentes retenedores del ISV. |
| **Sobre qué servicios** | (1) Transporte de carga; (2) limpieza, aseo y fumigación; (3) impresión o serigrafía; (4) investigación y seguridad; (5) alquiler de locales comerciales, maquinaria o equipo. |
| **Porcentaje retenido** | **NO CONFIRMADO oficialmente.** Guías secundarias citan **75% del ISV** (= **11.25%** del subtotal gravado al 15%) cuando el gran contribuyente compra a un proveedor que **no** es gran contribuyente [SECUNDARIO]. El PDF oficial del 215-2010 no fue legible → **parametrizable, validar en SAR**. |
| **Declaración informativa** | Reportar mensualmente los sujetos retenidos, dentro de los **15 días** del mes siguiente [OFICIAL, vía resumen del acuerdo]. |

### 3.3 Otros agentes de retención de ISV

- **Tarjetas de crédito/débito:** emisores, operadores y concesionarios son agentes retenedores del ISV sobre las ventas de sus afiliados. Porcentaje exacto **no confirmado** [SECUNDARIO].
- **Proveedores del Estado:** el ISV se retiene en cada orden de pago o documento equivalente y se entera en la Tesorería General de la República. Mecánica exacta a confirmar.

---

## 4. Constancia / Comprobante de Retención (CRT)

### 4.1 Naturaleza fiscal

- El **Comprobante de Retención** está regulado en el Régimen de Facturación (**Acuerdo 481-2017** y reformas) como **Documento Fiscal Complementario**.
- El agente de retención está **obligado a entregar** al obligado tributario un Comprobante de Retención por la suma retenida.
- **¿Requiere CAI?** Hay discrepancia de fuentes: una fuente legal lo clasifica entre los documentos complementarios que **requieren autorización (CAI)** con **correlativo de 16 dígitos** (formato NNN-NNN-NN-NNNNNNNN: establecimiento–punto de emisión–tipo–correlativo); otra lo lista entre los que "no requieren trámite ante la SAR". **Interpretación recomendada [PRÁCTICA]:** tratarlo como documento complementario **con CAI y correlativo de 16 dígitos**, y **confirmar con la SAR**.
- **Datos obligatorios [OFICIAL, del 215-2010]:** nombre/razón social, domicilio y **RTN del agente retenedor y del sujeto retenido**, **monto de la retención**, impuesto retenido y **fecha** en que se practicó.

### 4.2 Declaración y pago de lo retenido

| Impuesto retenido | Periodicidad | Plazo | Formulario (DET Live) |
|---|---|---|---|
| ISR en la fuente | Mensual | Primeros **10 días** del mes siguiente | Declaración de Selectivo, Específicos y **Retenciones** (histórico "**350 / SER**") [nombre OFICIAL; número SECUNDARIO — confirmar] |
| ISV (incluye ISV retenido y crédito fiscal) | Mensual | Según calendario ISV | Declaración mensual del ISV ("**352**") [SECUNDARIO — confirmar] |

> Los códigos de formulario legados llevan prefijo "DEI-" (antigua Dirección Ejecutiva de Ingresos, hoy SAR) y han ido migrando a **DET Live**. **Parametrizar** los números de formulario en el sistema, no hardcodearlos. Recargo por incumplimiento citado: **5% mensual** [SECUNDARIO].

---

## 5. Lado cliente — retenciones que a NOSOTROS nos practican

Cuando un **cliente que es agente de retención** nos paga, puede retenernos:
- **ISR 12.5%** (si facturamos honorarios/servicios técnicos) o **ISR 1%** (si el cliente es agente del 1% y somos proveedor de bienes/servicios no sujeto a pagos a cuenta).
- **ISV** si prestamos uno de los 5 servicios del Acuerdo 215-2010 a un gran contribuyente.

**Documentación y efecto:**
- El cliente debe entregarnos el **Comprobante de Retención**; ese comprobante es el **soporte del crédito** ante el SAR.
- **ISR retenido a favor:** anticipo / pago a cuenta del ISR, acreditable contra el ISR del período en la declaración anual (**activo / crédito fiscal**). En pagos a no residentes es definitivo, no crédito.
- **ISV retenido a favor:** pago anticipado del ISV, se acredita/rebaja del ISV a pagar en la declaración mensual.

---

## 6. Tratamiento contable (asientos)

> [PRÁCTICA] Reflejan la práctica contable estándar en Honduras; no son texto normativo. Cifras de ejemplo en Lempiras.

### 6.1 Lado del AGENTE que retiene (nosotros pagamos al proveedor)

**Ejemplo A — Honorario profesional con retención de ISR 12.5% (sin retención de ISV).**
Honorario L.10,000 + ISV 15% L.1,500 = factura L.11,500. Retención ISR = 12.5% × 10,000 = **L.1,250**. Neto a pagar = **L.10,250**.

| Cuenta | Debe | Haber |
|---|---:|---:|
| Gasto por servicios profesionales / honorarios | 10,000 | |
| ISV crédito fiscal (acreditable) | 1,500 | |
| Retenciones por pagar – ISR 12.5% (pasivo) | | 1,250 |
| Bancos | | 10,250 |

Al enterar la retención al SAR (dentro de los 10 días del mes siguiente):

| Cuenta | Debe | Haber |
|---|---:|---:|
| Retenciones por pagar – ISR 12.5% | 1,250 | |
| Bancos | | 1,250 |

**Ejemplo B — Servicio del Acuerdo 215-2010 (p. ej. seguridad) con retención de ISV + ISR 1%.**
Servicio L.10,000 + ISV 15% L.1,500 = L.11,500. ISV retenido 75% = **L.1,125** *(% [SECUNDARIO], parametrizable)*; ISR 1% = **L.100**. Neto = 11,500 − 1,125 − 100 = **L.10,275**.

| Cuenta | Debe | Haber |
|---|---:|---:|
| Gasto por servicio | 10,000 | |
| ISV crédito fiscal (acreditable) | 1,500 | |
| ISV retenido por pagar (pasivo) | | 1,125 |
| Retenciones por pagar – ISR 1% (pasivo) | | 100 |
| Bancos | | 10,275 |

> **Clave:** aunque retengamos parte del ISV, se registra el **100% del ISV como crédito fiscal acreditable**. La diferencia es que una parte la pagamos al proveedor y la otra la enteramos directo al fisco vía la retención. "ISV retenido por pagar" y "Retenciones ISR por pagar" son **pasivos** que se saldan al enterar al SAR.

### 6.2 Lado del que SUFRE la retención (nosotros cobramos a un cliente que retiene)

**Ejemplo C — Facturamos un servicio 215-2010 a un gran contribuyente que nos retiene ISV 75% e ISR 1%.**
Servicio L.10,000 + ISV 15% L.1,500 = L.11,500. Nos retienen ISV L.1,125 e ISR L.100. Recibimos neto **L.10,275**.

| Cuenta | Debe | Haber |
|---|---:|---:|
| Bancos | 10,275 | |
| ISR pagado por anticipado / Retenciones ISR a favor (activo) | 100 | |
| ISV retenido a favor / anticipo ISV (activo) | 1,125 | |
| Ingreso por servicios | | 10,000 |
| ISV débito fiscal (ISV por pagar) | | 1,500 |

> **Relación con el crédito fiscal del ISV:** en la declaración mensual, ISV a pagar = **ISV débito fiscal (ventas) − ISV crédito fiscal (compras) − ISV que nos retuvieron**. El "ISV retenido a favor" opera como anticipo del ISV. El "ISR retenido a favor" se acumula y se acredita contra el ISR anual.

---

## 7. Qué parametrizar en un ERP + datos a confirmar

**Parametrizar como tablas configurables (no hardcode):**
- % de ISR: 12.5, 1 (y la tabla de no residentes 25/10 por concepto).
- % de ISV retenido (verificar) y tasas de ISV (15 / 18).
- Umbral de agente del 1% (L.15M) y plazos (10 y 15 días).
- **Base ISR = valor sin ISV** vs. **base ISV retenido = monto del ISV** (distinguirlas).
- Cuentas separadas: pasivos "Retenciones ISR por pagar" e "ISV retenido por pagar"; activos "ISR retenido a favor" e "ISV retenido a favor".
- Numeración del Comprobante de Retención (CAI + correlativo 16 dígitos) y números de formulario DET Live.
- Config **por tercero**: proveedores exentos / sujetos a pagos a cuenta (no se les retiene el 1%), residente vs. no residente.

**Datos que NO se pudieron confirmar en fuente oficial (validar con SAR / contador antes de codificar):**
1. **% exacto del ISV que retiene el gran contribuyente** (215-2010); el 75% / 11.25% es [SECUNDARIO].
2. % de retención de ISV en tarjetas de crédito/débito y a proveedores del Estado (confirmado el "quién", no el "cuánto").
3. Números de formulario vigentes en DET Live (350 / 352 son referencias legadas).
4. Lista exacta de bienes al 18% y la exclusión del ISV de la base del 12.5%.
5. Retención del 10% sobre alquileres y el recargo del 5% mensual (solo fuente secundaria).

---

## 8. Fuentes

**Oficiales (SAR / SEFIN / Estado):**
- SAR — Retención 12.5% del ISR: https://www.sar.gob.hn/helpie_faq/quienes-estan-sujetos-a-la-retencion-del-12-del-isr/
- SAR — Tasa a no residentes (Art. 5): https://www.sar.gob.hn/helpie_faq/cual-es-la-tasa-que-se-aplica-por-concepto-de-impuesto-sobre-la-renta-a-las-personas-naturales-y-juridicas-no-domiciliadas-y-o-no-residentes-que-obtienen-ingresos-de-fuente-hondurena/
- SAR — Agente de retención del 1% (Acuerdo DEI-217-2010): https://www.sar.gob.hn/helpie_faq/quienes-estan-obligados-a-ser-agente-de-retencion-del-1-a-los-proveedores/
- SAR — Acuerdo DEI-215-2010 (retención/entero de ISV en servicios): https://www.sar.gob.hn/download/acuerdo-dei-215-2010-no-32355-del-02-de-noviembre-de-2010-procedimiento-de-retencion-y-entero-de-impuesto-sobre-ventas-en-la-prestacion-de-servicios-que-brindan-las-personas-naturales-o-juridicas/
- SAR — DET Live (Selectivo, Específicos y Retenciones): http://detlive.sar.gob.hn/?q=Ayuda-SER
- SAR — ISR (portal): https://www.sar.gob.hn/isr/
- SEFIN — Ley del ISR (texto consolidado, PDF): https://www.sefin.gob.hn/download_file.php?download_file=%2Fwp-content%2Fuploads%2F2018%2F06%2FTexto_Consolidado_Ley_Impuesto_sobre_la_Renta_25JUNIO2018.pdf
- TSC — Acuerdo 189-2014 Régimen de Facturación: https://www.tsc.gob.hn/web/leyes/ACUERDO_189-2014_REGIMEN_DE_FACTURACION.pdf

**Secundarias (contables/legales):**
- Consortium Legal — Documentos fiscales (Acuerdo 481-2017, Comprobante de Retención): https://consortiumlegal.com/2022/09/06/consideraciones-importantes-respecto-a-los-documentos-fiscales-en-honduras/
- KODDIX — Guía de retenciones ISR/ISV: https://www.koddix.com/blog/retenciones-isr-isv-honduras
- KODDIX — Cómo facturar con CAI: https://www.koddix.com/blog/como-facturar-con-cai-en-honduras
- ivacalculator — Tasas ISV 15%/18%: https://ivacalculator.com/honduras/

> Portales de referencia adicionales aportados por el usuario: TSC (biblioteca de leyes), SEFIN, ARSA, COHPUCPH, AMP Puerto Cortés (regulación/códigos), Aduanas (AAH), elcontador.hn.
