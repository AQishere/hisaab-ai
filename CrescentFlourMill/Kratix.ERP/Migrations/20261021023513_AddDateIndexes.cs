using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kratix.ERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDateIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_RecordedAt",
                table: "StockMovements",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_SODate",
                table: "SalesOrders",
                column: "SODate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_ProductionDate",
                table: "ProductionBatches",
                column: "ProductionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentDate",
                table: "Payments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_InboundReceipts_CreatedDate",
                table: "InboundReceipts",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Dispatches_DispatchedAt",
                table: "Dispatches",
                column: "DispatchedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_RecordedAt",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_SODate",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionBatches_ProductionDate",
                table: "ProductionBatches");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InboundReceipts_CreatedDate",
                table: "InboundReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Dispatches_DispatchedAt",
                table: "Dispatches");
        }
    }
}
