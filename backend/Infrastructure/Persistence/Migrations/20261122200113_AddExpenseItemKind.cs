using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Szamlazo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseItemKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ExpenseItem",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.Sql("UPDATE ExpenseItem SET Kind = 1 WHERE AmountMinor < 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ExpenseItem");
        }
    }
}
