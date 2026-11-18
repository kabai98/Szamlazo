using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szamlazo.Application.Dtos;
using Szamlazo.Application.Services;

namespace Szamlazo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class LedgerController(LedgerService ledger) : ControllerBase
{
    private string AccountId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard(string? month, CancellationToken token) =>
        ledger.GetDashboardAsync(AccountId, month, token);

    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpenses(List<ExpenseInput?> input, CancellationToken token)
    {
        await ledger.SaveExpensesAsync(AccountId, input, token);
        return NoContent();
    }

    [HttpPut("expenses/{id:guid}")]
    public async Task<IActionResult> UpdateExpense(Guid id, ExpenseInput input, CancellationToken token)
    {
        await ledger.UpdateExpenseAsync(AccountId, id, input, token);
        return NoContent();
    }

    [HttpDelete("expenses/{id:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken token)
    {
        await ledger.DeleteExpenseAsync(AccountId, id, token);
        return NoContent();
    }

    [HttpPut("budget")]
    public async Task<IActionResult> SetBudget(BudgetInput input, CancellationToken token)
    {
        await ledger.SetBudgetAsync(AccountId, input, token);
        return NoContent();
    }
}
