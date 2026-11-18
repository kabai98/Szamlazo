namespace Szamlazo.Api.Dtos;

public sealed record CredentialsDto(string? Email, string? Password);
public sealed record SessionDto(string? Email, string? Csrf, IEnumerable<string> Categories);
public sealed record ErrorDto(string Message);
