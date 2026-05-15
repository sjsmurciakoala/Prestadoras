namespace SIAD.Core.DTOs.TiposDocumentoFiscal;

public sealed class TipoDocumentoFiscalDto
{
    public short TipoDocumentoFiscalId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EsComprobanteFiscal { get; set; }
    public bool EsDocumentoComplementario { get; set; }
    public bool RequiereFacturaOrigen { get; set; }
    public bool Activo { get; set; }
    public int CaisAsociados { get; set; }
}

public sealed class TipoDocumentoFiscalUpdateDto
{
    public string Descripcion { get; set; } = string.Empty;
    public bool EsComprobanteFiscal { get; set; }
    public bool EsDocumentoComplementario { get; set; }
    public bool RequiereFacturaOrigen { get; set; }
    public bool Activo { get; set; } = true;
}
