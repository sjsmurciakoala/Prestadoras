namespace SIAD.Core.Entities;

// 2026-09-08: quién firma por la unidad de cobranza en el pagaré y el
// compromiso de pago. Vacío deja el rótulo genérico de los documentos.
public partial class con_empresa_configuracion
{
    public string? firmante_cobranza { get; set; }
}
