using Szamlazo.Domain.Enums;

namespace Szamlazo.Application.Dtos;

public sealed record ItemInput(string? Name, decimal? Amount);
public sealed record ExpenseInput(Guid Id, string? Merchant, DateOnly? Date, decimal? Amount, string? Category,
    ExpenseSource? Source, List<ItemInput?>? Items);

public sealed record ItemDto(string Name, decimal Amount);
public sealed record ExpenseDto(Guid Id, string Merchant, DateOnly Date, string Category, ExpenseSource Source,
    decimal Amount, IReadOnlyList<ItemDto> Items);
