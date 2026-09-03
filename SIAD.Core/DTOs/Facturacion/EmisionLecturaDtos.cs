namespace SIAD.Core.DTOs.Facturacion;

/// <summary>
/// Emisión de una factura de lectura desde el portal.
///
/// Es el mismo acto que hace el lector en campo —capturar la lectura de un medidor y emitir su
/// factura— pero desde el escritorio: sirve para refacturar tras anular, para el abonado que se
/// leyó en papel y para el que el teléfono no alcanzó.
///
/// El folio NO lo elige quien captura: lo entrega el servidor desde el bloque CAI del portal
/// (<see cref="BloqueCaiPortalDto"/>), que es un rango distinto del que reparten los teléfonos.
/// </summary>
public class EmitirFacturaLecturaRequest
{
    /// <summary>Clave del abonado (<c>cliente_maestro.maestro_cliente_clave</c>).</summary>
    public string Clave { get; set; } = string.Empty;

    public int Anio { get; set; }

    public int Mes { get; set; }

    /// <summary>Lectura del medidor. Nula en los casos sin medición, donde manda la condición.</summary>
    public decimal? LecturaActual { get; set; }

    /// <summary>Código de <c>adm_condicion_lectura_tipo</c>. <c>N</c> = lectura normal.</summary>
    public string CondicionLectura { get; set; } = "N";

    public DateTime? FechaLectura { get; set; }

    /// <summary>Promedio a usar cuando la condición lo exige (medidor dañado, casa cerrada…).</summary>
    public decimal? LecturaPromedio { get; set; }

    public string? Contador { get; set; }

    public string? Observacion { get; set; }

    /// <summary>Foto del medidor en base64, opcional. Ver la nota de evidencia en el servicio.</summary>
    public string? ImagenBase64 { get; set; }
}

/// <summary>Estado del bloque de folios del portal, para avisar antes de que se agote.</summary>
public class BloqueCaiPortalDto
{
    public long CaiBloqueId { get; set; }
    public long CaiId { get; set; }
    public string? CodigoCai { get; set; }
    public string? PrefijoDocumento { get; set; }
    public long CorrelativoDesde { get; set; }
    public long CorrelativoHasta { get; set; }
    public long CorrelativoActual { get; set; }
    public long CorrelativoSiguiente { get; set; }
    public DateTime? FechaExpiracion { get; set; }
    public string? EstadoCodigo { get; set; }

    /// <summary>Folios que quedan en el bloque antes de tener que reservar otro.</summary>
    public long Disponibles => Math.Max(0, CorrelativoHasta - CorrelativoActual);
}

/// <summary>Resultado de la emisión. Los errores de negocio viajan aquí, no como excepción.</summary>
public class EmitirFacturaLecturaResultado
{
    public bool Success { get; set; }

    /// <summary>Código estable del contrato: <c>OK</c>, <c>FACTURA_YA_EMITIDA</c>, etc.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Mensaje { get; set; } = string.Empty;

    public long FacturaId { get; set; }
    public string? NumeroFactura { get; set; }
    public long? CorrelativoCai { get; set; }
    public long? IdCai { get; set; }

    public string? ClienteClave { get; set; }
    public string? ClienteNombre { get; set; }

    public decimal Consumo { get; set; }
    public decimal Subtotal { get; set; }
    public decimal SubtotalAjustes { get; set; }
    public decimal SaldosAnteriores { get; set; }
    public decimal Recargos { get; set; }
    public decimal Total { get; set; }

    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Lo que se facturaría con esa lectura, ANTES de emitir. Sale del mismo
/// <c>sp_adm_calcular_factura_lectura</c> que usa la emisión, así que lo que muestra la pantalla
/// es lo que va a salir en el papel — no una estimación aparte.
/// </summary>
public class PreviewFacturaLecturaDto
{
    public bool Encontrado { get; set; }
    public string? Mensaje { get; set; }

    public long ClienteId { get; set; }
    public string? ClienteClave { get; set; }
    public string? ClienteNombre { get; set; }
    public string? Contador { get; set; }
    public string? Ciclo { get; set; }
    public string? Ruta { get; set; }
    public bool TieneMedidor { get; set; }

    /// <summary>Factura viva del período, si la hay: hasta anularla no se puede volver a facturar.</summary>
    public string? FacturaVigente { get; set; }

    public string? CondicionLecturaAplicada { get; set; }
    public decimal LecturaAnterior { get; set; }
    public decimal LecturaActualEfectiva { get; set; }
    public decimal ConsumoFacturable { get; set; }

    public decimal SubtotalServicios { get; set; }
    public decimal SubtotalAjustes { get; set; }
    public decimal SaldosAnteriores { get; set; }
    public decimal Recargos { get; set; }
    public decimal TotalFactura { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public List<string> Warnings { get; set; } = [];
}
