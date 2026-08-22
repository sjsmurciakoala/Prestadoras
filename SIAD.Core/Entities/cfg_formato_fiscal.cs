using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Formato de un código fiscal que se transcribe del proveedor (No. de factura SAR, CAI).
/// Una fila por campo y por empresa. Ver Database/2026-08-22_cfg_formato_fiscal.sql.
/// </summary>
public partial class cfg_formato_fiscal : ICompanyScopedEntity
{
    public int id { get; set; }

    public long company_id { get; set; }

    /// <summary>Identificador del campo: NUMERO_SAR, CAI, ... Es la clave que pide la pantalla.</summary>
    public string codigo { get; set; } = null!;

    /// <summary>Etiqueta visible del campo, tal como aparece en la vista que lo captura.</summary>
    public string nombre { get; set; } = null!;

    /// <summary>Máscara de captura: '#' dígito, 'X' letra o dígito, 'H' hexadecimal, resto literal.</summary>
    public string mascara { get; set; } = null!;

    /// <summary>Expresión regular de validación. NULL = se deriva de la máscara.</summary>
    public string? patron { get; set; }

    /// <summary>1 = no valida, 2 = advierte y deja guardar, 3 = bloquea el guardado.</summary>
    public short modo_validacion { get; set; }

    public bool obligatorio { get; set; }

    /// <summary>true = el valor se guarda sin separadores y se muestra con la máscara.</summary>
    public bool normalizar { get; set; }

    public bool mayusculas { get; set; }

    public bool activo { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }
}
