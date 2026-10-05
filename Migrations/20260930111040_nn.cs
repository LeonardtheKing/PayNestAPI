using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanAPI.Migrations
{
    /// <inheritdoc />
    public partial class nn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiverBankName",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RecipientCode",
                table: "Beneficiaries");

            migrationBuilder.AddColumn<Guid>(
                name: "BeneficiaryId",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<string>(
                name: "ReceiverBankName",
                table: "Transactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientCode",
                table: "Beneficiaries",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
