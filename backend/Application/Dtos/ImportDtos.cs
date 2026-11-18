using Szamlazo.Domain.Enums;

namespace Szamlazo.Application.Dtos;

public sealed record ImportRow(string? Merchant, DateOnly? Date, decimal? Amount, string Category,
    ExpenseSource Source, List<ItemInput> Items);
public sealed record ImportPreview(List<ImportRow> Rows, List<string> Warnings, string? Text = null);
