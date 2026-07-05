using System.Text;
using apc.BancosWs.Contrato;
using SIAD.Services.BancosWs;

namespace apc.BancosWs.Infrastructure;

/// <summary>
/// Autenticación del canal: banco+key por query string contra ban_ws_credencial.
/// El WS viejo solo filtraba /api/consulta/* (con bypass si banco ∈ "999");
/// F8 valida TODAS las rutas de negocio (decisión del plan — el banco ya envía
/// key+banco en todas las requests) y elimina el bypass. La respuesta de
/// rechazo replica byte a byte la del filtro viejo (Handle.toString + println).
/// </summary>
public sealed class BancosWsAuthMiddleware
{
    private static readonly string[] RutasProtegidas =
    {
        "/simafi/api/consulta",
        "/simafi/api/pago",
        "/simafi/api/reversion",
    };

    private readonly RequestDelegate _next;

    public BancosWsAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IBancosWsService service, BancosWsRequestContext requestContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var protegida = RutasProtegidas.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase));

        // GET /pago/dummy es un endpoint de prueba sin auth en el WS viejo.
        if (protegida && path.TrimEnd('/').EndsWith("/pago/dummy", StringComparison.OrdinalIgnoreCase))
        {
            protegida = false;
        }

        if (!protegida)
        {
            await _next(context);
            return;
        }

        var banco = context.Request.Query["banco"].ToString();
        var llave = context.Request.Query["key"].ToString();

        var credencial = await service.AutenticarAsync(banco, llave, context.RequestAborted);
        if (credencial is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = ContractXml.ContentTypeFiltro;
            await context.Response.WriteAsync(ContractXml.MensajeFiltroNoAutorizado(), Encoding.ASCII, context.RequestAborted);
            return;
        }

        requestContext.Autenticar(credencial.CompanyId, credencial.Banco, credencial.BancoCuentaId);
        await _next(context);
    }
}
