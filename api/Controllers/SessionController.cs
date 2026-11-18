using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Szamlazo.Api.Dtos;
using Szamlazo.Domain.Enums;
using Szamlazo.Infrastructure.Identity;

namespace Szamlazo.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SessionController(UserManager<Account> users, SignInManager<Account> signIn) : ControllerBase
{
    [HttpGet("session")]
    public ActionResult<SessionDto> Session([FromServices] IAntiforgery csrf)
    {
        Response.Headers.CacheControl = "no-store";
        return new SessionDto(User.Identity?.Name, csrf.GetAndStoreTokens(HttpContext).RequestToken,
            SpendingCategories.Labels);
    }

    [HttpPost("register")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Register(CredentialsDto input)
    {
        var email = ValidEmail(input.Email);
        if (email is null || input.Password is not { Length: >= 10 and <= 128 })
            return BadRequest(new ErrorDto("Adj meg érvényes e-mail-címet és 10–128 karakteres jelszót."));

        var account = new Account { Email = email, UserName = email };
        var created = await users.CreateAsync(account, input.Password);
        if (!created.Succeeded)
            return BadRequest(new ErrorDto("A fiók nem hozható létre ezzel az e-mail-címmel."));

        await signIn.SignInAsync(account, isPersistent: true);
        return NoContent();
    }

    [HttpPost("login")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Login(CredentialsDto input)
    {
        var email = ValidEmail(input.Email);
        if (email is null || input.Password is not { Length: >= 10 and <= 128 })
            return Unauthorized(new ErrorDto("Az e-mail-cím vagy a jelszó hibás."));

        var account = await users.FindByEmailAsync(email);
        if (account is not null && (await signIn.PasswordSignInAsync(account, input.Password, true, true)).Succeeded)
            return NoContent();

        return Unauthorized(new ErrorDto("A belépés sikertelen. Ellenőrizd az adataidat, vagy próbáld meg később."));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    private static string? ValidEmail(string? email)
    {
        email = email?.Trim();
        return email is { Length: > 0 and <= 254 } && new EmailAddressAttribute().IsValid(email) ? email : null;
    }
}
