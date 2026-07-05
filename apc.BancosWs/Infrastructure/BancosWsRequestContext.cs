namespace apc.BancosWs.Infrastructure;

/// <summary>
/// Estado por request del canal bancario: credencial autenticada (banco+key
/// de la query string) que resuelve el tenant y la cuenta bancaria destino.
/// </summary>
public sealed class BancosWsRequestContext
{
    public bool Autenticado { get; private set; }

    public long CompanyId { get; private set; }

    public string Banco { get; private set; } = string.Empty;

    public long? BancoCuentaId { get; private set; }

    public void Autenticar(long companyId, string banco, long? bancoCuentaId)
    {
        Autenticado = true;
        CompanyId = companyId;
        Banco = banco;
        BancoCuentaId = bancoCuentaId;
    }
}
