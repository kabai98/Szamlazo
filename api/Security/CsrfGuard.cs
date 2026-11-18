using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Szamlazo.Api.Dtos;

namespace Szamlazo.Api.Security;

internal sealed class CsrfGuard(IAntiforgery csrf) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
            HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
            return;

        try
        {
            await csrf.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(
                new ErrorDto("A munkamenet lejárt. Frissítsd az oldalt, majd próbáld újra."));
        }
    }
}
