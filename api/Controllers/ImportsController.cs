using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Szamlazo.Api.Dtos;
using Szamlazo.Application.Contracts;
using Szamlazo.Application.Dtos;

namespace Szamlazo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/import")]
public sealed class ImportsController(IDocumentReader reader) : ControllerBase
{
    public const int MaxBytes = 12 * 1024 * 1024;

    [HttpPost]
    [EnableRateLimiting("import")]
    public async Task<ActionResult<ImportPreview>> Preview(CancellationToken token)
    {
        if (!Request.HasFormContentType)
            return BadRequest(new ErrorDto("Válassz egy feltölthető fájlt."));

        try
        {
            var form = await Request.ReadFormAsync(token);
            var file = form.Files.GetFile("file");
            if (file is null || form.Files.Count != 1 || file.Length is <= 0 or > MaxBytes)
                return BadRequest(new ErrorDto("Egy legfeljebb 12 MB-os fájlt tölthetsz fel."));

            await using var content = file.OpenReadStream();
            return await reader.ReadAsync(file.FileName, content, token);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ErrorDto(ex.Message));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return BadRequest(new ErrorDto("A feldolgozás túl sokáig tartott. Próbálj kisebb vagy élesebb fájlt feltölteni."));
        }
    }
}
