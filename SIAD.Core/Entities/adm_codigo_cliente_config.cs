using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Configuración POR EMPRESA del generador del código de cliente (2026-07-16):
/// codigo = prefijo + correlativo con relleno de ceros hasta <see cref="longitud"/>.
/// Solo actúa cuando la clave llega vacía al crear el cliente (directo o desde
/// solicitud); la clave manual sigue permitida. Esquema en
/// Database/2026-07-16_codigo_cliente_automatico.sql; el consumo atómico lo
/// hace fn_adm_siguiente_codigo_cliente.
/// </summary>
public partial class adm_codigo_cliente_config : ICompanyScopedEntity
{
    public long company_id { get; set; }

    public bool activo { get; set; } = true;

    /// <summary>Prefijo fijo del código (SIMAFI usa '09'). Mayúsculas, sin espacios.</summary>
    public string prefijo { get; set; } = string.Empty;

    /// <summary>Largo TOTAL del código (prefijo + correlativo). SIMAFI: 9.</summary>
    public short longitud { get; set; } = 9;

    /// <summary>Próximo correlativo a asignar.</summary>
    public long siguiente { get; set; } = 1;

    public string? updated_by { get; set; }

    public DateTime? updated_at { get; set; }
}
