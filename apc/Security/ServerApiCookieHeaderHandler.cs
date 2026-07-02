using Microsoft.Extensions.Primitives;

namespace apc.Security;

public sealed class ServerApiCookieHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServerApiCookieHeaderHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        request.Headers.Remove("Cookie");
        if (httpContext?.Request.Headers.TryGetValue("Cookie", out var cookies) == true &&
            !StringValues.IsNullOrEmpty(cookies))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookies.AsEnumerable());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
