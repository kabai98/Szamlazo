using Microsoft.AspNetCore.Diagnostics;
using Szamlazo.Api.Dtos;
using Szamlazo.Application.Services;

namespace Szamlazo.Api.Errors;

internal sealed class LedgerExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        if (exception is not LedgerRequestException problem)
            return false;

        context.Response.StatusCode = problem.IsMissing ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ErrorDto(problem.Message), token);
        return true;
    }
}
