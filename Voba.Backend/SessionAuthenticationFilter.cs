using Voba.Interfaces;
using Voba.Models;

namespace Voba.Backend;

public sealed class SessionAuthenticationFilter(IBackendAuthService auth) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var authorization = http.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();
        var token = authorization[7..].Trim();
        if (token.Length == 0 || token.Contains(' '))
            return Results.Unauthorized();
        BackendPrincipal? principal = await auth.AuthenticateAsync(token,
            http.RequestAborted);
        if (principal is null)
            return Results.Unauthorized();
        http.Items[typeof(BackendPrincipal)] = principal;
        return await next(context);
    }
}
