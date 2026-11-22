using Szamlazo.Domain.Enums;

namespace Szamlazo.Application.Dtos;

public sealed record ItemInput(string? Name, decimal? Amount, decimal? Quantity = null, string? Unit = null, decimal? UnitPrice = null, ExpenseItemKind Kind = ExpenseItemKind.Product);
public sealed record ExpenseInput(Guid Id, string? Merchant, DateOnly? Date, decimal? Amount, string? Category,
    ExpenseSource? Source, List<ItemInput?>? Items, string? PaymentMethod = null, TimeOnly? TransactionTime = null);

public sealed record ItemDto(string Name, decimal Amount, decimal? Quantity = null, string? Unit = null, decimal? UnitPrice = null, ExpenseItemKind Kind = ExpenseItemKind.Product);
public sealed record ExpenseDto(Guid Id, string Merchant, DateOnly Date, string Category, ExpenseSource Source,
    decimal Amount, IReadOnlyList<ItemDto> Items, string? PaymentMethod = null, TimeOnly? TransactionTime = null);
