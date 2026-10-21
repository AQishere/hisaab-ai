using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hisaab.ERP.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: true),
                    ContactPerson = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerID);
                });

            migrationBuilder.CreateTable(
                name: "ProductionBatches",
                columns: table => new
                {
                    BatchID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchNumber = table.Column<string>(type: "text", nullable: false),
                    ProductionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ShiftNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalInputQuantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    TotalOutputQuantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    TotalWasteQuantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    YieldPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    OperatorName = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionBatches", x => x.BatchID);
                    table.CheckConstraint("CK_Batch_Status", "\"Status\" IN ('Planned','InProgress','Completed') AND \"TotalInputQuantity\" >= 0 AND \"TotalOutputQuantity\" >= 0 AND \"TotalWasteQuantity\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductCode = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    KgPerBag = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    HSNCode = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductID);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    SupplierID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierName = table.Column<string>(type: "text", nullable: false),
                    ContactPerson = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    NTN = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    PaymentTerms = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.SupplierID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    WarehouseID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WarehouseName = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Capacity = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.WarehouseID);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    POID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PONumber = table.Column<string>(type: "text", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    PODate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ClosedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.POID);
                    table.CheckConstraint("CK_PO_Status", "\"Status\" IN ('Draft','Sent','Received','Closed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierID");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    SalesOrderID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SONumber = table.Column<string>(type: "text", nullable: false),
                    CustomerID = table.Column<int>(type: "integer", nullable: false),
                    SODate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ClosedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.SalesOrderID);
                    table.CheckConstraint("CK_SO_Status", "\"Status\" IN ('Quotation','Confirmed','PartiallyDispatched','Dispatched','Closed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_SalesOrders_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID");
                    table.ForeignKey(
                        name: "FK_SalesOrders_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    InventoryID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductID = table.Column<int>(type: "integer", nullable: false),
                    WarehouseID = table.Column<int>(type: "integer", nullable: false),
                    BinCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QuantityAvailable = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    StockForm = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Standard"),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.InventoryID);
                    table.CheckConstraint("CK_Inventory_Quantity", "\"QuantityAvailable\" >= 0 AND \"StockForm\" IN ('Standard','Bulk')");
                    table.ForeignKey(
                        name: "FK_Inventories_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID");
                    table.ForeignKey(
                        name: "FK_Inventories_Warehouses_WarehouseID",
                        column: x => x.WarehouseID,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseID");
                });

            migrationBuilder.CreateTable(
                name: "POLineItems",
                columns: table => new
                {
                    LineItemID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    POID = table.Column<int>(type: "integer", nullable: false),
                    ProductID = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POLineItems", x => x.LineItemID);
                    table.CheckConstraint("CK_POLine_Quantities", "\"Quantity\" > 0 AND \"RatePerUnit\" > 0 AND \"TotalAmount\" >= 0 AND \"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"Quantity\" * 1.05");
                    table.ForeignKey(
                        name: "FK_POLineItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID");
                    table.ForeignKey(
                        name: "FK_POLineItems_PurchaseOrders_POID",
                        column: x => x.POID,
                        principalTable: "PurchaseOrders",
                        principalColumn: "POID");
                });

            migrationBuilder.CreateTable(
                name: "Dispatches",
                columns: table => new
                {
                    DispatchID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderID = table.Column<int>(type: "integer", nullable: false),
                    TruckNumber = table.Column<string>(type: "text", nullable: false),
                    DriverName = table.Column<string>(type: "text", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RecordedBy = table.Column<int>(type: "integer", nullable: true)
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
                name: "Invoices",
                columns: table => new
                {
                    InvoiceID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    SalesOrderID = table.Column<int>(type: "integer", nullable: false),
                    CustomerID = table.Column<int>(type: "integer", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceID);
                    table.CheckConstraint("CK_Invoice_Amounts", "\"TotalAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"NetAmount\" = \"TotalAmount\" + \"TaxAmount\" AND \"Status\" IN ('Unpaid','PartiallyPaid','Paid')");
                    table.ForeignKey(
                        name: "FK_Invoices_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID");
                    table.ForeignKey(
                        name: "FK_Invoices_SalesOrders_SalesOrderID",
                        column: x => x.SalesOrderID,
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderID");
                });

            migrationBuilder.CreateTable(
                name: "SOLineItems",
                columns: table => new
                {
                    LineItemID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderID = table.Column<int>(type: "integer", nullable: false),
                    ProductID = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    DispatchedQuantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    DispatchStatus = table.Column<string>(type: "text", nullable: true),
                    TruckNumber = table.Column<string>(type: "text", nullable: true),
                    DriverName = table.Column<string>(type: "text", nullable: true),
                    DispatchDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SOLineItems", x => x.LineItemID);
                    table.CheckConstraint("CK_SOLine_Quantities", "\"Quantity\" > 0 AND \"RatePerUnit\" > 0 AND \"TotalAmount\" >= 0 AND \"DispatchedQuantity\" >= 0 AND \"DispatchedQuantity\" <= \"Quantity\" AND \"DispatchStatus\" IN ('NotDispatched','PartiallyDispatched','Dispatched')");
                    table.ForeignKey(
                        name: "FK_SOLineItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID");
                    table.ForeignKey(
                        name: "FK_SOLineItems_SalesOrders_SalesOrderID",
                        column: x => x.SalesOrderID,
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderID");
                });

            migrationBuilder.CreateTable(
                name: "BatchMaterials",
                columns: table => new
                {
                    BatchMaterialID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchID = table.Column<int>(type: "integer", nullable: false),
                    ProductID = table.Column<int>(type: "integer", nullable: false),
                    MaterialType = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    PackedQuantityKg = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    SourceInventoryID = table.Column<int>(type: "integer", nullable: true),
                    OutputInventoryID = table.Column<int>(type: "integer", nullable: true),
                    KgPerBag = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    IsReversed = table.Column<bool>(type: "boolean", nullable: false),
                    ReversalReason = table.Column<string>(type: "text", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReversedBy = table.Column<int>(type: "integer", nullable: true),
                    SourceWarehouseID = table.Column<int>(type: "integer", nullable: true),
                    SourceLotNumber = table.Column<string>(type: "text", nullable: true),
                    IsQualityApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<int>(type: "integer", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchMaterials", x => x.BatchMaterialID);
                    table.CheckConstraint("CK_Material_Quantities", "\"Quantity\" > 0 AND \"PackedQuantityKg\" >= 0 AND \"PackedQuantityKg\" <= \"Quantity\" AND \"MaterialType\" IN ('Input','Output','Waste')");
                    table.ForeignKey(
                        name: "FK_BatchMaterials_Inventories_OutputInventoryID",
                        column: x => x.OutputInventoryID,
                        principalTable: "Inventories",
                        principalColumn: "InventoryID");
                    table.ForeignKey(
                        name: "FK_BatchMaterials_Inventories_SourceInventoryID",
                        column: x => x.SourceInventoryID,
                        principalTable: "Inventories",
                        principalColumn: "InventoryID");
                    table.ForeignKey(
                        name: "FK_BatchMaterials_ProductionBatches_BatchID",
                        column: x => x.BatchID,
                        principalTable: "ProductionBatches",
                        principalColumn: "BatchID");
                    table.ForeignKey(
                        name: "FK_BatchMaterials_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID");
                    table.ForeignKey(
                        name: "FK_BatchMaterials_Users_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_BatchMaterials_Warehouses_SourceWarehouseID",
                        column: x => x.SourceWarehouseID,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseID");
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    StockMovementID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryID = table.Column<int>(type: "integer", nullable: false),
                    QuantityChange = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MovementType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    RecordedBy = table.Column<int>(type: "integer", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "InboundReceipts",
                columns: table => new
                {
                    ReceiptID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    ReceiptType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Weighed"),
                    ReceiptNumber = table.Column<string>(type: "text", nullable: false),
                    POID = table.Column<int>(type: "integer", nullable: false),
                    POLineItemID = table.Column<int>(type: "integer", nullable: true),
                    TruckNumber = table.Column<string>(type: "text", nullable: true),
                    DriverName = table.Column<string>(type: "text", nullable: true),
                    DriverPhone = table.Column<string>(type: "text", nullable: true),
                    ArrivalDateTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GrossWeight = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    TareWeight = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    NetWeight = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    Moisture = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Impurities = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    GrainType = table.Column<string>(type: "text", nullable: true),
                    QCResult = table.Column<string>(type: "text", nullable: true),
                    WarehouseID = table.Column<int>(type: "integer", nullable: true),
                    BinCode = table.Column<string>(type: "text", nullable: true),
                    LotNumber = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedBy = table.Column<int>(type: "integer", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundReceipts", x => x.ReceiptID);
                    table.CheckConstraint("CK_Receipt_Status", "\"Status\" IN ('GateEntry','Weighed','QCApproved','Rejected','Received','Voided') AND \"ReceiptType\" IN ('Weighed','Counted') AND (\"ReceivedQuantity\" IS NULL OR \"ReceivedQuantity\" > 0)");
                    table.ForeignKey(
                        name: "FK_InboundReceipts_POLineItems_POLineItemID",
                        column: x => x.POLineItemID,
                        principalTable: "POLineItems",
                        principalColumn: "LineItemID");
                    table.ForeignKey(
                        name: "FK_InboundReceipts_PurchaseOrders_POID",
                        column: x => x.POID,
                        principalTable: "PurchaseOrders",
                        principalColumn: "POID");
                    table.ForeignKey(
                        name: "FK_InboundReceipts_Users_ReceivedBy",
                        column: x => x.ReceivedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_InboundReceipts_Warehouses_WarehouseID",
                        column: x => x.WarehouseID,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseID");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    InvoiceLineID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceID = table.Column<int>(type: "integer", nullable: false),
                    ProductID = table.Column<int>(type: "integer", nullable: false),
                    ProductCode = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    KgPerBag = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    IsLegacySnapshot = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "Payments",
                columns: table => new
                {
                    PaymentID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceID = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaymentMode = table.Column<string>(type: "text", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentID);
                    table.CheckConstraint("CK_Payment_Amount", "\"PaymentAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceID",
                        column: x => x.InvoiceID,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceID");
                });

            migrationBuilder.CreateTable(
                name: "DispatchLines",
                columns: table => new
                {
                    DispatchLineID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DispatchID = table.Column<int>(type: "integer", nullable: false),
                    SOLineItemID = table.Column<int>(type: "integer", nullable: false),
                    InventoryID = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchLines", x => x.DispatchLineID);
                    table.CheckConstraint("CK_DispatchLine_Quantity", "\"Quantity\" > 0");
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

            migrationBuilder.CreateTable(
                name: "PackingRuns",
                columns: table => new
                {
                    PackingRunID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchMaterialID = table.Column<int>(type: "integer", nullable: false),
                    BulkInventoryID = table.Column<int>(type: "integer", nullable: false),
                    FinishedInventoryID = table.Column<int>(type: "integer", nullable: false),
                    PackagingInventoryID = table.Column<int>(type: "integer", nullable: false),
                    QuantityKg = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    KgPerBag = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    BagsPacked = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    RecordedBy = table.Column<int>(type: "integer", nullable: true),
                    PackedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingRuns", x => x.PackingRunID);
                    table.CheckConstraint("CK_Packing_Quantities", "\"QuantityKg\" > 0 AND \"KgPerBag\" > 0 AND \"BagsPacked\" > 0 AND \"BagsPacked\" = FLOOR(\"BagsPacked\") AND \"QuantityKg\" = \"KgPerBag\" * \"BagsPacked\"");
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

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_ApprovedBy",
                table: "BatchMaterials",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_BatchID",
                table: "BatchMaterials",
                column: "BatchID");

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_OutputInventoryID",
                table: "BatchMaterials",
                column: "OutputInventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_ProductID",
                table: "BatchMaterials",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_SourceInventoryID",
                table: "BatchMaterials",
                column: "SourceInventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_SourceWarehouseID",
                table: "BatchMaterials",
                column: "SourceWarehouseID");

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
                name: "IX_InboundReceipts_POID",
                table: "InboundReceipts",
                column: "POID");

            migrationBuilder.CreateIndex(
                name: "IX_InboundReceipts_POLineItemID",
                table: "InboundReceipts",
                column: "POLineItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InboundReceipts_ReceiptNumber",
                table: "InboundReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundReceipts_ReceivedBy",
                table: "InboundReceipts",
                column: "ReceivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InboundReceipts_WarehouseID",
                table: "InboundReceipts",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductID_WarehouseID_StockForm_BinCode_LotNumb~",
                table: "Inventories",
                columns: new[] { "ProductID", "WarehouseID", "StockForm", "BinCode", "LotNumber" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_WarehouseID",
                table: "Inventories",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceID",
                table: "InvoiceLines",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_ProductID",
                table: "InvoiceLines",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerID",
                table: "Invoices",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SalesOrderID",
                table: "Invoices",
                column: "SalesOrderID",
                unique: true);

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
                name: "IX_Payments_InvoiceID",
                table: "Payments",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_POLineItems_POID",
                table: "POLineItems",
                column: "POID");

            migrationBuilder.CreateIndex(
                name: "IX_POLineItems_ProductID",
                table: "POLineItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_BatchNumber",
                table: "ProductionBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCode",
                table: "Products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedBy",
                table: "PurchaseOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PONumber",
                table: "PurchaseOrders",
                column: "PONumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierID",
                table: "PurchaseOrders",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CreatedBy",
                table: "SalesOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerID",
                table: "SalesOrders",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_SONumber",
                table: "SalesOrders",
                column: "SONumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SOLineItems_ProductID",
                table: "SOLineItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_SOLineItems_SalesOrderID",
                table: "SOLineItems",
                column: "SalesOrderID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryID_RecordedAt",
                table: "StockMovements",
                columns: new[] { "InventoryID", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierName",
                table: "Suppliers",
                column: "SupplierName");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispatchLines");

            migrationBuilder.DropTable(
                name: "InboundReceipts");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "PackingRuns");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "Dispatches");

            migrationBuilder.DropTable(
                name: "SOLineItems");

            migrationBuilder.DropTable(
                name: "POLineItems");

            migrationBuilder.DropTable(
                name: "BatchMaterials");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "Inventories");

            migrationBuilder.DropTable(
                name: "ProductionBatches");

            migrationBuilder.DropTable(
                name: "SalesOrders");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Users");

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
        }
    }
}
