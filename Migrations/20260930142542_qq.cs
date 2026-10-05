using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanAPI.Migrations
{
    /// <inheritdoc />
    public partial class qq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Beneficiaries_BeneficiaryId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BeneficiaryId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BeneficiaryId",
                table: "Transactions");

            migrationBuilder.AddColumn<Guid>(
                name: "SenderId",
                table: "Beneficiaries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderId",
                table: "Beneficiaries");

            migrationBuilder.AddColumn<Guid>(
                name: "BeneficiaryId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BeneficiaryId",
                table: "Transactions",
                column: "BeneficiaryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Beneficiaries_BeneficiaryId",
                table: "Transactions",
                column: "BeneficiaryId",
                principalTable: "Beneficiaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
