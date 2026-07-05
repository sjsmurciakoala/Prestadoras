using SIAD.Core.DTOs.MobileApi;

namespace apc.MobileApi.Infrastructure;

/// <summary>
/// Estado por request de la API móvil: la sesión autenticada (token bearer) que
/// resuelve el tenant (company_id) y la ruta del lector. company_id NUNCA viene
/// de un parámetro del cliente (A6).
/// </summary>
public sealed class MobileApiRequestContext
{
    public bool Autenticado { get; private set; }

    public LectorSesionContexto? Sesion { get; private set; }

    public long CompanyId => Sesion?.CompanyId ?? 0;

    public void Autenticar(LectorSesionContexto sesion)
    {
        Sesion = sesion;
        Autenticado = true;
    }
}
