using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kratix.ERP.Migrations
{
    /// <inheritdoc />
    public partial class AllowShortClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SO_Status",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PO_Status",
                table: "PurchaseOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_POLine_Quantities",
                table: "POLineItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Receipt_Status",
                table: "InboundReceipts");

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "SalesOrders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "PurchaseOrders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "InboundReceipts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SO_Status",
                table: "SalesOrders",
                sql: "[Status] IN ('Quotation','Confirmed','PartiallyDispatched','Dispatched','Closed','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PO_Status",
                table: "PurchaseOrders",
                sql: "[Status] IN ('Draft','Sent','Received','Closed','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_POLine_Quantities",
                table: "POLineItems",
                sql: "[Quantity] > 0 AND [RatePerUnit] > 0 AND [TotalAmount] >= 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] * 1.05");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Receipt_Status",
                table: "InboundReceipts",
                sql: "[Status] IN ('GateEntry','Weighed','QCApproved','Rejected','Received','Voided') AND [ReceiptType] IN ('Weighed','Counted') AND ([ReceivedQuantity] IS NULL OR [ReceivedQuantity] > 0)");

            // Pre-ledger lots were dated by their last change, so recently dispatched lots looked newest.
            // Use the original receipt or batch date where one is recorded. Lots refilled after the
            // history upgrade already carry a later date and are left alone.
            migrationBuilder.Sql(@"
                UPDATE i SET ReceivedAt = COALESCE(
                    (SELECT MIN(r.ReceivedDate) FROM InboundReceipts r JOIN POLineItems l ON l.LineItemID = r.POLineItemID
                        WHERE r.Status = 'Received' AND l.ProductID = i.ProductID AND r.WarehouseID = i.WarehouseID AND r.LotNumber = i.LotNumber),
                    (SELECT MIN(b.ProductionDate) FROM ProductionBatches b WHERE b.BatchNumber = i.LotNumber),
                    i.ReceivedAt)
                FROM Inventories i
                JOIN StockMovements o ON o.InventoryID = i.InventoryID AND o.MovementType = 'OpeningBalance'
                WHERE i.ReceivedAt <= o.RecordedAt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM PurchaseOrders WHERE Status = 'Closed') OR EXISTS (SELECT 1 FROM SalesOrders WHERE Status = 'Closed')
                   OR EXISTS (SELECT 1 FROM InboundReceipts WHERE Status = 'Voided') OR EXISTS (SELECT 1 FROM POLineItems WHERE ReceivedQuantity > Quantity)
                    THROW 51007, 'Closed, voided or over-received records exist. Restore a verified backup instead.', 1;
            ");
            migrationBuilder.DropCheckConstraint(
                name: "CK_SO_Status",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PO_Status",
                table: "PurchaseOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_POLine_Quantities",
                table: "POLineItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Receipt_Status",
                table: "InboundReceipts");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "InboundReceipts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SO_Status",
                table: "SalesOrders",
                sql: "[Status] IN ('Quotation','Confirmed','PartiallyDispatched','Dispatched','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PO_Status",
                table: "PurchaseOrders",
                sql: "[Status] IN ('Draft','Sent','Received','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_POLine_Quantities",
                table: "POLineItems",
                sql: "[Quantity] > 0 AND [RatePerUnit] > 0 AND [TotalAmount] >= 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Receipt_Status",
                table: "InboundReceipts",
                sql: "[Status] IN ('GateEntry','Weighed','QCApproved','Rejected','Received') AND [ReceiptType] IN ('Weighed','Counted') AND ([ReceivedQuantity] IS NULL OR [ReceivedQuantity] > 0)");
        }
    }
}
