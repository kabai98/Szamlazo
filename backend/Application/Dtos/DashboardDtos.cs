namespace Szamlazo.Application.Dtos;

public sealed record BudgetInput(string? Month, decimal? Amount);
public sealed record DailySpending(int Day, decimal Amount);
public sealed record WeekdaySpending(int Index, decimal Average);
public sealed record CategorySpending(string Name, decimal Amount);
public sealed record DashboardDto(string Month, decimal Total, decimal Previous, decimal? Budget,
    IReadOnlyList<DailySpending> Days, IReadOnlyList<WeekdaySpending> Weekdays,
    IReadOnlyList<CategorySpending> Categories, IReadOnlyList<ExpenseDto> Expenses);
