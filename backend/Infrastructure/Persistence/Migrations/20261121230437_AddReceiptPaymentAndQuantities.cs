using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Szamlazo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptPaymentAndQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Expenses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "TransactionTime",
                table: "Expenses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "ExpenseItem",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "ExpenseItem",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "ExpenseItem",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TransactionTime",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "ExpenseItem");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "ExpenseItem");
        }
    }
}
