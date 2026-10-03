using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kratix.ERP.Migrations
{
    /// <inheritdoc />
    public partial class LinkInboundReceiptToPurchaseOrderLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "POLineItemID",
                table: "InboundReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ir SET POLineItemID = singleLine.LineItemID
                FROM InboundReceipts ir
                JOIN (
                    SELECT POID, MIN(LineItemID) AS LineItemID
                    FROM POLineItems GROUP BY POID HAVING COUNT(*) = 1
                ) singleLine ON singleLine.POID = ir.POID;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_InboundReceipts_POLineItemID",
                table: "InboundReceipts",
                column: "POLineItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundReceipts_POLineItems_POLineItemID",
                table: "InboundReceipts",
                column: "POLineItemID",
                principalTable: "POLineItems",
                principalColumn: "LineItemID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InboundReceipts_POLineItems_POLineItemID",
                table: "InboundReceipts");

            migrationBuilder.DropIndex(
                name: "IX_InboundReceipts_POLineItemID",
                table: "InboundReceipts");

            migrationBuilder.DropColumn(
                name: "POLineItemID",
                table: "InboundReceipts");
        }
    }
}
