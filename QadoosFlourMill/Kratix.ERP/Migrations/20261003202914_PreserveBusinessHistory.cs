using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kratix.ERP.Migrations
{
    /// <inheritdoc />
    public partial class PreserveBusinessHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Inventories WHERE LEN(BinCode) > 64 OR LEN(LotNumber) > 100 OR QuantityAvailable < 0)
                    THROW 51003, 'Inventory has invalid quantities or overlong lot/bin codes. Reconcile before upgrading.', 1;
                IF EXISTS (SELECT 1 FROM Inventories GROUP BY ProductID, WarehouseID, NULLIF(LTRIM(RTRIM(BinCode)), ''), NULLIF(LTRIM(RTRIM(LotNumber)), '') HAVING COUNT(*) > 1)
                    THROW 51004, 'Duplicate inventory locations need explicit reconciliation before upgrading.', 1;
                IF EXISTS (SELECT 1 FROM Invoices GROUP BY SalesOrderID HAVING COUNT(*) > 1)
                    THROW 51005, 'Multiple invoices for one sales order need reconciliation before upgrading.', 1;
                UPDATE Inventories SET BinCode = NULLIF(LTRIM(RTRIM(BinCode)), ''), LotNumber = NULLIF(LTRIM(RTRIM(LotNumber)), '');
            ");
            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_ProductionBatches_BatchID",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_Products_ProductID",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_Users_ApprovedBy",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_Warehouses_SourceWarehouseID",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundReceipts_PurchaseOrders_POID",
                table: "InboundReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundReceipts_Users_ReceivedBy",
                table: "InboundReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundReceipts_Warehouses_WarehouseID",
                table: "InboundReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_Products_ProductID",
                table: "Inventories");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_Warehouses_WarehouseID",
                table: "Inventories");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_POLineItems_Products_ProductID",
                table: "POLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_POLineItems_PurchaseOrders_POID",
                table: "POLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierID",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Users_CreatedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Customers_CustomerID",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Users_CreatedBy",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SOLineItems_Products_ProductID",
                table: "SOLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SOLineItems_SalesOrders_SalesOrderID",
                table: "SOLineItems");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SalesOrderID",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Inventories_ProductID",
                table: "Inventories");

            migrationBuilder.CreateSequence(
                name: "BatchNumbers");

            migrationBuilder.CreateSequence(
                name: "InvoiceNumbers");

            migrationBuilder.CreateSequence(
                name: "PurchaseOrderNumbers");

            migrationBuilder.CreateSequence(
                name: "ReceiptNumbers");

            migrationBuilder.CreateSequence(
                name: "SalesOrderNumbers");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "LotNumber",
                table: "Inventories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BinCode",
                table: "Inventories",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "Inventories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "StockForm",
                table: "Inventories",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "ReceiptType",
                table: "InboundReceipts",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Weighed");

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQuantity",
                table: "InboundReceipts",
                type: "decimal(15,3)",
                precision: 15,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReversed",
                table: "BatchMaterials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "KgPerBag",
                table: "BatchMaterials",
                type: "decimal(15,3)",
                precision: 15,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputInventoryID",
                table: "BatchMaterials",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "BatchMaterials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAt",
                table: "BatchMaterials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversedBy",
                table: "BatchMaterials",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceInventoryID",
                table: "BatchMaterials",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Dispatches",
                columns: table => new
                {
                    DispatchID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesOrderID = table.Column<int>(type: "int", nullable: false),
                    TruckNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DriverName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispatches", x => x.DispatchID);
                    table.ForeignKey(
                        name: "FK_Dispatches_SalesOrders_SalesOrderID",
                        column: x => x.SalesOrderID,
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderID");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    InvoiceLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KgPerBag = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    IsLegacySnapshot = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.InvoiceLineID);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceID",
                        column: x => x.InvoiceID,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceID");
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID");
                });

            migrationBuilder.CreateTable(
                name: "PackingRuns",
                columns: table => new
                {
                    PackingRunID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchMaterialID = table.Column<int>(type: "int", nullable: false),
                    BulkInventoryID = table.Column<int>(type: "int", nullable: false),
                    FinishedInventoryID = table.Column<int>(type: "int", nullable: false),
                    PackagingInventoryID = table.Column<int>(type: "int", nullable: false),
                    QuantityKg = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    KgPerBag = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    BagsPacked = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    RecordedBy = table.Column<int>(type: "int", nullable: true),
                    PackedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingRuns", x => x.PackingRunID);
                    table.CheckConstraint("CK_Packing_Quantities", "[QuantityKg] > 0 AND [KgPerBag] > 0 AND [BagsPacked] > 0 AND [BagsPacked] = FLOOR([BagsPacked]) AND [QuantityKg] = [KgPerBag] * [BagsPacked]");
                    table.ForeignKey(
                        name: "FK_PackingRuns_BatchMaterials_BatchMaterialID",
                        column: x => x.BatchMaterialID,
                        principalTable: "BatchMaterials",
                        principalColumn: "BatchMaterialID");
                    table.ForeignKey(
                        name: "FK_PackingRuns_Inventories_BulkInventoryID",
                        column: x => x.BulkInventoryID,
                        principalTable: "Inventories",
                        principalColumn: "InventoryID");
                    table.ForeignKey(
                        name: "FK_PackingRuns_Inventories_FinishedInventoryID",
                        column: x => x.FinishedInventoryID,
                        principalTable: "Inventories",
                        principalColumn: "InventoryID");
                    table.ForeignKey(
                        name: "FK_PackingRuns_Inventories_PackagingInventoryID",
                        column: x => x.PackagingInventoryID,
                        principalTable: "Inventories",
                        principalColumn: "InventoryID");
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    StockMovementID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryID = table.Column<int>(type: "int", nullable: false),
                    QuantityChange = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedBy = table.Column<int>(type: "int", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.StockMovementID);
                    table.ForeignKey(
                        name: "FK_StockMovements_Inventories_InventoryID",
                        column: x => x.InventoryID,
                        principalTable: "Inventories",
                        principalColumn: "InventoryID");
                });

            migrationBuilder.CreateTable(
                name: "DispatchLines",
                columns: table => new
                {
                    DispatchLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DispatchID = table.Column<int>(type: "int", nullable: false),
                    SOLineItemID = table.Column<int>(type: "int", nullable: false),
                    InventoryID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchLines", x => x.DispatchLineID);
                    table.CheckConstraint("CK_DispatchLine_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_DispatchLines_Dispatches_DispatchID",
                        column: x => x.DispatchID,
                        principalTable: "Dispatches",
                        principalColumn: "DispatchID");
                    table.ForeignKey(
                        name: "FK_DispatchLines_Inventories_InventoryID",
                        column: x => x.InventoryID,
                        principalTable: "Inventories",
                        principalColumn: "InventoryID");
                    table.ForeignKey(
                        name: "FK_DispatchLines_SOLineItems_SOLineItemID",
                        column: x => x.SOLineItemID,
                        principalTable: "SOLineItems",
                        principalColumn: "LineItemID");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SOLine_Quantities",
                table: "SOLineItems",
                sql: "[Quantity] > 0 AND [RatePerUnit] > 0 AND [TotalAmount] >= 0 AND [DispatchedQuantity] >= 0 AND [DispatchedQuantity] <= [Quantity] AND [DispatchStatus] IN ('NotDispatched','PartiallyDispatched','Dispatched')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SO_Status",
                table: "SalesOrders",
                sql: "[Status] IN ('Quotation','Confirmed','PartiallyDispatched','Dispatched','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PO_Status",
                table: "PurchaseOrders",
                sql: "[Status] IN ('Draft','Sent','Received','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Batch_Status",
                table: "ProductionBatches",
                sql: "[Status] IN ('Planned','InProgress','Completed') AND [TotalInputQuantity] >= 0 AND [TotalOutputQuantity] >= 0 AND [TotalWasteQuantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_POLine_Quantities",
                table: "POLineItems",
                sql: "[Quantity] > 0 AND [RatePerUnit] > 0 AND [TotalAmount] >= 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payment_Amount",
                table: "Payments",
                sql: "[PaymentAmount] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SalesOrderID",
                table: "Invoices",
                column: "SalesOrderID",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoice_Amounts",
                table: "Invoices",
                sql: "[TotalAmount] >= 0 AND [TaxAmount] >= 0 AND [NetAmount] = [TotalAmount] + [TaxAmount] AND [Status] IN ('Unpaid','PartiallyPaid','Paid')");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductID_WarehouseID_StockForm_BinCode_LotNumber",
                table: "Inventories",
                columns: new[] { "ProductID", "WarehouseID", "StockForm", "BinCode", "LotNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_Quantity",
                table: "Inventories",
                sql: "[QuantityAvailable] >= 0 AND [StockForm] IN ('Standard','Bulk')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Receipt_Status",
                table: "InboundReceipts",
                sql: "[Status] IN ('GateEntry','Weighed','QCApproved','Rejected','Received') AND [ReceiptType] IN ('Weighed','Counted') AND ([ReceivedQuantity] IS NULL OR [ReceivedQuantity] > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_OutputInventoryID",
                table: "BatchMaterials",
                column: "OutputInventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_SourceInventoryID",
                table: "BatchMaterials",
                column: "SourceInventoryID");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Material_Quantities",
                table: "BatchMaterials",
                sql: "[Quantity] > 0 AND [PackedQuantityKg] >= 0 AND [PackedQuantityKg] <= [Quantity] AND [MaterialType] IN ('Input','Output','Waste')");

            migrationBuilder.CreateIndex(
                name: "IX_Dispatches_SalesOrderID",
                table: "Dispatches",
                column: "SalesOrderID");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchLines_DispatchID",
                table: "DispatchLines",
                column: "DispatchID");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchLines_InventoryID",
                table: "DispatchLines",
                column: "InventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchLines_SOLineItemID",
                table: "DispatchLines",
                column: "SOLineItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceID",
                table: "InvoiceLines",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_ProductID",
                table: "InvoiceLines",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_PackingRuns_BatchMaterialID",
                table: "PackingRuns",
                column: "BatchMaterialID");

            migrationBuilder.CreateIndex(
                name: "IX_PackingRuns_BulkInventoryID",
                table: "PackingRuns",
                column: "BulkInventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_PackingRuns_FinishedInventoryID",
                table: "PackingRuns",
                column: "FinishedInventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_PackingRuns_PackagingInventoryID",
                table: "PackingRuns",
                column: "PackagingInventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryID_RecordedAt",
                table: "StockMovements",
                columns: new[] { "InventoryID", "RecordedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_Inventories_OutputInventoryID",
                table: "BatchMaterials",
                column: "OutputInventoryID",
                principalTable: "Inventories",
                principalColumn: "InventoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_Inventories_SourceInventoryID",
                table: "BatchMaterials",
                column: "SourceInventoryID",
                principalTable: "Inventories",
                principalColumn: "InventoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_ProductionBatches_BatchID",
                table: "BatchMaterials",
                column: "BatchID",
                principalTable: "ProductionBatches",
                principalColumn: "BatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_Products_ProductID",
                table: "BatchMaterials",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_Users_ApprovedBy",
                table: "BatchMaterials",
                column: "ApprovedBy",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_Warehouses_SourceWarehouseID",
                table: "BatchMaterials",
                column: "SourceWarehouseID",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundReceipts_PurchaseOrders_POID",
                table: "InboundReceipts",
                column: "POID",
                principalTable: "PurchaseOrders",
                principalColumn: "POID");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundReceipts_Users_ReceivedBy",
                table: "InboundReceipts",
                column: "ReceivedBy",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundReceipts_Warehouses_WarehouseID",
                table: "InboundReceipts",
                column: "WarehouseID",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_Products_ProductID",
                table: "Inventories",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_Warehouses_WarehouseID",
                table: "Inventories",
                column: "WarehouseID",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceID",
                table: "Payments",
                column: "InvoiceID",
                principalTable: "Invoices",
                principalColumn: "InvoiceID");

            migrationBuilder.AddForeignKey(
                name: "FK_POLineItems_Products_ProductID",
                table: "POLineItems",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_POLineItems_PurchaseOrders_POID",
                table: "POLineItems",
                column: "POID",
                principalTable: "PurchaseOrders",
                principalColumn: "POID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierID",
                table: "PurchaseOrders",
                column: "SupplierID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Users_CreatedBy",
                table: "PurchaseOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Customers_CustomerID",
                table: "SalesOrders",
                column: "CustomerID",
                principalTable: "Customers",
                principalColumn: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Users_CreatedBy",
                table: "SalesOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_SOLineItems_Products_ProductID",
                table: "SOLineItems",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_SOLineItems_SalesOrders_SalesOrderID",
                table: "SOLineItems",
                column: "SalesOrderID",
                principalTable: "SalesOrders",
                principalColumn: "SalesOrderID");

            // Establish a cutover balance, never invent old movement events.
            migrationBuilder.Sql(@"
                UPDATE Inventories SET ReceivedAt = LastUpdatedDate;
                UPDATE r SET ReceivedQuantity = NetWeight FROM InboundReceipts r WHERE Status = 'Received';
                UPDATE bm SET KgPerBag = p.KgPerBag FROM BatchMaterials bm JOIN Products p ON p.ProductID = bm.ProductID WHERE p.Unit = 'bag';
                UPDATE bm SET SourceInventoryID = matches.InventoryID
                FROM BatchMaterials bm CROSS APPLY (
                    SELECT MIN(i.InventoryID) InventoryID FROM Inventories i WHERE i.ProductID = bm.ProductID AND i.WarehouseID = bm.SourceWarehouseID
                        AND (i.LotNumber = bm.SourceLotNumber OR (i.LotNumber IS NULL AND bm.SourceLotNumber IS NULL)) HAVING COUNT(*) = 1
                ) matches WHERE bm.MaterialType = 'Input';
                INSERT INTO StockMovements (InventoryID, QuantityChange, Unit, MovementType, Reference, Reason, RecordedAt)
                    SELECT i.InventoryID, i.QuantityAvailable, p.Unit, 'OpeningBalance', 'HISTORY-UPGRADE',
                        'Recorded pre-ledger balance; prior movement history is unavailable.', GETDATE()
                    FROM Inventories i JOIN Products p ON p.ProductID = i.ProductID;
                INSERT INTO InvoiceLines (InvoiceID, ProductID, ProductCode, ProductName, Unit, KgPerBag, Quantity, RatePerUnit, TotalAmount, IsLegacySnapshot)
                    SELECT inv.InvoiceID, l.ProductID, p.ProductCode, p.ProductName, p.Unit, p.KgPerBag, l.DispatchedQuantity, l.RatePerUnit, l.TotalAmount, 1
                    FROM Invoices inv JOIN SOLineItems l ON l.SalesOrderID = inv.SalesOrderID JOIN Products p ON p.ProductID = l.ProductID;
                UPDATE b SET TotalInputQuantity = x.InputQty, TotalOutputQuantity = x.OutputQty, TotalWasteQuantity = x.WasteQty
                FROM ProductionBatches b CROSS APPLY (SELECT
                    COALESCE(SUM(CASE WHEN MaterialType = 'Input' THEN Quantity ELSE 0 END), 0) InputQty,
                    COALESCE(SUM(CASE WHEN MaterialType = 'Output' THEN Quantity ELSE 0 END), 0) OutputQty,
                    COALESCE(SUM(CASE WHEN MaterialType = 'Waste' THEN Quantity ELSE 0 END), 0) WasteQty
                    FROM BatchMaterials WHERE BatchID = b.BatchID AND IsReversed = 0) x;
                UPDATE Customers SET OutstandingBalance =
                    COALESCE((SELECT SUM(NetAmount) FROM Invoices WHERE CustomerID = Customers.CustomerID), 0) -
                    COALESCE((SELECT SUM(p.PaymentAmount) FROM Payments p JOIN Invoices i ON i.InvoiceID = p.InvoiceID WHERE i.CustomerID = Customers.CustomerID), 0);
            ");

            migrationBuilder.Sql(@"
                DECLARE @next bigint = (SELECT COALESCE(MAX(TRY_CONVERT(bigint, RIGHT(PONumber, CHARINDEX('-', REVERSE(PONumber) + '-') - 1))), 0) + 1 FROM PurchaseOrders);
                EXEC('ALTER SEQUENCE dbo.PurchaseOrderNumbers RESTART WITH ' + @next);
            ");

            migrationBuilder.Sql(@"
                DECLARE @next bigint = (SELECT COALESCE(MAX(TRY_CONVERT(bigint, RIGHT(SONumber, CHARINDEX('-', REVERSE(SONumber) + '-') - 1))), 0) + 1 FROM SalesOrders);
                EXEC('ALTER SEQUENCE dbo.SalesOrderNumbers RESTART WITH ' + @next);
            ");

            migrationBuilder.Sql(@"
                DECLARE @next bigint = (SELECT COALESCE(MAX(TRY_CONVERT(bigint, RIGHT(BatchNumber, CHARINDEX('-', REVERSE(BatchNumber) + '-') - 1))), 0) + 1 FROM ProductionBatches);
                EXEC('ALTER SEQUENCE dbo.BatchNumbers RESTART WITH ' + @next);
            ");

            migrationBuilder.Sql(@"
                DECLARE @next bigint = (SELECT COALESCE(MAX(TRY_CONVERT(bigint, RIGHT(ReceiptNumber, CHARINDEX('-', REVERSE(ReceiptNumber) + '-') - 1))), 0) + 1 FROM InboundReceipts);
                EXEC('ALTER SEQUENCE dbo.ReceiptNumbers RESTART WITH ' + @next);
            ");

            migrationBuilder.Sql(@"
                DECLARE @next bigint = (SELECT COALESCE(MAX(TRY_CONVERT(bigint, RIGHT(InvoiceNumber, CHARINDEX('-', REVERSE(InvoiceNumber) + '-') - 1))), 0) + 1 FROM Invoices);
                EXEC('ALTER SEQUENCE dbo.InvoiceNumbers RESTART WITH ' + @next);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM StockMovements) OR EXISTS (SELECT 1 FROM Dispatches) OR EXISTS (SELECT 1 FROM PackingRuns) OR EXISTS (SELECT 1 FROM InvoiceLines)
                    THROW 51006, 'History exists. A rollback would erase it; restore a verified backup instead.', 1;
            ");
            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_Inventories_OutputInventoryID",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_Inventories_SourceInventoryID",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_ProductionBatches_BatchID",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_Products_ProductID",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_Users_ApprovedBy",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchMaterials_Warehouses_SourceWarehouseID",
                table: "BatchMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundReceipts_PurchaseOrders_POID",
                table: "InboundReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundReceipts_Users_ReceivedBy",
                table: "InboundReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundReceipts_Warehouses_WarehouseID",
                table: "InboundReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_Products_ProductID",
                table: "Inventories");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_Warehouses_WarehouseID",
                table: "Inventories");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_POLineItems_Products_ProductID",
                table: "POLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_POLineItems_PurchaseOrders_POID",
                table: "POLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierID",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Users_CreatedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Customers_CustomerID",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Users_CreatedBy",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SOLineItems_Products_ProductID",
                table: "SOLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SOLineItems_SalesOrders_SalesOrderID",
                table: "SOLineItems");

            migrationBuilder.DropTable(
                name: "DispatchLines");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "PackingRuns");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "Dispatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SOLine_Quantities",
                table: "SOLineItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SO_Status",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PO_Status",
                table: "PurchaseOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Batch_Status",
                table: "ProductionBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_POLine_Quantities",
                table: "POLineItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payment_Amount",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SalesOrderID",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoice_Amounts",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Inventories_ProductID_WarehouseID_StockForm_BinCode_LotNumber",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_Quantity",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Receipt_Status",
                table: "InboundReceipts");

            migrationBuilder.DropIndex(
                name: "IX_BatchMaterials_OutputInventoryID",
                table: "BatchMaterials");

            migrationBuilder.DropIndex(
                name: "IX_BatchMaterials_SourceInventoryID",
                table: "BatchMaterials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Material_Quantities",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "StockForm",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "ReceiptType",
                table: "InboundReceipts");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "InboundReceipts");

            migrationBuilder.DropColumn(
                name: "IsReversed",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "KgPerBag",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "OutputInventoryID",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "ReversedBy",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "SourceInventoryID",
                table: "BatchMaterials");

            migrationBuilder.DropSequence(
                name: "BatchNumbers");

            migrationBuilder.DropSequence(
                name: "InvoiceNumbers");

            migrationBuilder.DropSequence(
                name: "PurchaseOrderNumbers");

            migrationBuilder.DropSequence(
                name: "ReceiptNumbers");

            migrationBuilder.DropSequence(
                name: "SalesOrderNumbers");

            migrationBuilder.AlterColumn<string>(
                name: "LotNumber",
                table: "Inventories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BinCode",
                table: "Inventories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SalesOrderID",
                table: "Invoices",
                column: "SalesOrderID");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductID",
                table: "Inventories",
                column: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_ProductionBatches_BatchID",
                table: "BatchMaterials",
                column: "BatchID",
                principalTable: "ProductionBatches",
                principalColumn: "BatchID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_Products_ProductID",
                table: "BatchMaterials",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_Users_ApprovedBy",
                table: "BatchMaterials",
                column: "ApprovedBy",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BatchMaterials_Warehouses_SourceWarehouseID",
                table: "BatchMaterials",
                column: "SourceWarehouseID",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundReceipts_PurchaseOrders_POID",
                table: "InboundReceipts",
                column: "POID",
                principalTable: "PurchaseOrders",
                principalColumn: "POID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundReceipts_Users_ReceivedBy",
                table: "InboundReceipts",
                column: "ReceivedBy",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundReceipts_Warehouses_WarehouseID",
                table: "InboundReceipts",
                column: "WarehouseID",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_Products_ProductID",
                table: "Inventories",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_Warehouses_WarehouseID",
                table: "Inventories",
                column: "WarehouseID",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceID",
                table: "Payments",
                column: "InvoiceID",
                principalTable: "Invoices",
                principalColumn: "InvoiceID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_POLineItems_Products_ProductID",
                table: "POLineItems",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_POLineItems_PurchaseOrders_POID",
                table: "POLineItems",
                column: "POID",
                principalTable: "PurchaseOrders",
                principalColumn: "POID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierID",
                table: "PurchaseOrders",
                column: "SupplierID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Users_CreatedBy",
                table: "PurchaseOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Customers_CustomerID",
                table: "SalesOrders",
                column: "CustomerID",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Users_CreatedBy",
                table: "SalesOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SOLineItems_Products_ProductID",
                table: "SOLineItems",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SOLineItems_SalesOrders_SalesOrderID",
                table: "SOLineItems",
                column: "SalesOrderID",
                principalTable: "SalesOrders",
                principalColumn: "SalesOrderID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
