namespace SIAD.Core.DTOs.Bancos;

public sealed class BancoConfiguracionDto
{
    public long ConfigId { get; set; }
    public decimal MaxCheque { get; set; }
    public int DiasD1 { get; set; }
    public int DiasD2 { get; set; }
    public int DiasD3 { get; set; }
    public int IChequeNe { get; set; }
    public int ICEgreso { get; set; }
    public int PrxCEgreso { get; set; }
    public int PrxDeposito { get; set; }
    public int PrxNDebito { get; set; }
    public int PrxNCredito { get; set; }
    public decimal PDebBan { get; set; }
    public int MesesH { get; set; }
    public string? StDta { get; set; }
    public bool AlertarNd { get; set; }
    public int MOpeConc { get; set; }
    public bool Consolidado { get; set; }
    public string? CuentaMayor { get; set; }
    public string? DirContab { get; set; }
    public string? DirDtaCont { get; set; }
    public int CcTipo { get; set; }
    public string? CcDescrip { get; set; }
    public bool CcSsw { get; set; }
    public string? CcServer { get; set; }
    public string? CcDb { get; set; }
    public string? CcUser { get; set; }
    public string? CcPwd { get; set; }
    public int CcPrefix { get; set; }
    public int NroCxb { get; set; }
    public int ACtas0 { get; set; }
    public int ACtas1 { get; set; }
    public int ACtas2 { get; set; }
    public int ACtas3 { get; set; }
    public int ACtas4 { get; set; }
    public int ACtas5 { get; set; }
    public int NOpe1 { get; set; }
    public int NOpe2 { get; set; }
    public int NOpe3 { get; set; }
    public int NOpe4 { get; set; }
    public int NOpe5 { get; set; }
    public int NOpe6 { get; set; }
    public int NOpe7 { get; set; }
    public int NOpe8 { get; set; }
    public int NOpe9 { get; set; }
    public int NOpe10 { get; set; }
    public string? CtaAux1 { get; set; }
    public string? CtaAux2 { get; set; }
    public string? CtaAux3 { get; set; }
    public string? CodSucu { get; set; }
}
