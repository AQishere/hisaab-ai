using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Models;

namespace Kratix.ERP.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Users
    public DbSet<User> Users { get; set; }

    // Master Data
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }

    // Purchasing
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<POLineItem> POLineItems { get; set; }

    // Receiving & QC
    public DbSet<InboundReceipt> InboundReceipts { get; set; }

    // Inventory
    public DbSet<Inventory> Inventories { get; set; }

    // Production
    public DbSet<ProductionBatch> ProductionBatches { get; set; }
    public DbSet<BatchMaterial> BatchMaterials { get; set; }

    // Sales
    public DbSet<SalesOrder> SalesOrders { get; set; }
    public DbSet<SOLineItem> SOLineItems { get; set; }

    // Invoicing & Payments
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<Dispatch> Dispatches { get; set; }
    public DbSet<DispatchLine> DispatchLines { get; set; }
    public DbSet<PackingRun> PackingRuns { get; set; }
    public DbSet<InvoiceLine> InvoiceLines { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure primary keys explicitly (for non-standard naming)
        modelBuilder.Entity<POLineItem>().HasKey(x => x.LineItemID);
        modelBuilder.Entity<SOLineItem>().HasKey(x => x.LineItemID);
        modelBuilder.Entity<ProductionBatch>().HasKey(x => x.BatchID);
        modelBuilder.Entity<BatchMaterial>().HasKey(x => x.BatchMaterialID);

        // Configure unique constraints
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(s => s.SupplierName);
        modelBuilder.Entity<Product>().HasIndex(p => p.ProductCode).IsUnique();
        modelBuilder.Entity<PurchaseOrder>().HasIndex(po => po.PONumber).IsUnique();
        modelBuilder.Entity<InboundReceipt>().HasIndex(ir => ir.ReceiptNumber).IsUnique();
        modelBuilder.Entity<ProductionBatch>().HasIndex(pb => pb.BatchNumber).IsUnique();
        modelBuilder.Entity<SalesOrder>().HasIndex(so => so.SONumber).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(i => i.InvoiceNumber).IsUnique();

        // Configure decimal precision
        modelBuilder.Entity<POLineItem>().Property(x => x.Quantity).HasPrecision(15, 3);
        modelBuilder.Entity<POLineItem>().Property(x => x.RatePerUnit).HasPrecision(15, 2);
        modelBuilder.Entity<POLineItem>().Property(x => x.TotalAmount).HasPrecision(15, 2);
        modelBuilder.Entity<POLineItem>().Property(x => x.ReceivedQuantity).HasPrecision(15, 3);

        modelBuilder.Entity<InboundReceipt>().Property(x => x.GrossWeight).HasPrecision(15, 3);
        modelBuilder.Entity<InboundReceipt>().Property(x => x.TareWeight).HasPrecision(15, 3);
        modelBuilder.Entity<InboundReceipt>().Property(x => x.NetWeight).HasPrecision(15, 3);
        modelBuilder.Entity<InboundReceipt>().Property(x => x.Moisture).HasPrecision(5, 2);
        modelBuilder.Entity<InboundReceipt>().Property(x => x.Impurities).HasPrecision(5, 2);

        modelBuilder.Entity<Inventory>().Property(x => x.QuantityAvailable).HasPrecision(15, 3);

        modelBuilder.Entity<ProductionBatch>().Property(x => x.TotalInputQuantity).HasPrecision(15, 3);
        modelBuilder.Entity<ProductionBatch>().Property(x => x.TotalOutputQuantity).HasPrecision(15, 3);
        modelBuilder.Entity<ProductionBatch>().Property(x => x.TotalWasteQuantity).HasPrecision(15, 3);
        modelBuilder.Entity<ProductionBatch>().Property(x => x.YieldPercentage).HasPrecision(5, 2);

        modelBuilder.Entity<BatchMaterial>().Property(x => x.Quantity).HasPrecision(15, 3);
        modelBuilder.Entity<BatchMaterial>().Property(x => x.PackedQuantityKg).HasPrecision(15, 3);
        modelBuilder.Entity<Product>().Property(x => x.KgPerBag).HasPrecision(15, 3);

        modelBuilder.Entity<SOLineItem>().Property(x => x.Quantity).HasPrecision(15, 3);
        modelBuilder.Entity<SOLineItem>().Property(x => x.RatePerUnit).HasPrecision(15, 2);
        modelBuilder.Entity<SOLineItem>().Property(x => x.TotalAmount).HasPrecision(15, 2);
        modelBuilder.Entity<SOLineItem>().Property(x => x.DispatchedQuantity).HasPrecision(15, 3);

        modelBuilder.Entity<SalesOrder>().Property(x => x.TotalAmount).HasPrecision(15, 2);

        modelBuilder.Entity<Invoice>().Property(x => x.TotalQuantity).HasPrecision(15, 3);
        modelBuilder.Entity<Invoice>().Property(x => x.TotalAmount).HasPrecision(15, 2);
        modelBuilder.Entity<Invoice>().Property(x => x.TaxAmount).HasPrecision(15, 2);
        modelBuilder.Entity<Invoice>().Property(x => x.NetAmount).HasPrecision(15, 2);

        modelBuilder.Entity<Payment>().Property(x => x.PaymentAmount).HasPrecision(15, 2);

        modelBuilder.Entity<Customer>().Property(x => x.CreditLimit).HasPrecision(15, 2);
        modelBuilder.Entity<Customer>().Property(x => x.OutstandingBalance).HasPrecision(15, 2);

        modelBuilder.Entity<Warehouse>().Property(x => x.Capacity).HasPrecision(15, 2);

        // Foreign Key Configurations
        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(po => po.Supplier)
            .WithMany(s => s.PurchaseOrders)
            .HasForeignKey(po => po.SupplierID);

        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(po => po.CreatedByUser)
            .WithMany()
            .HasForeignKey(po => po.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<POLineItem>()
            .HasOne(pli => pli.PurchaseOrder)
            .WithMany(po => po.POLineItems)
            .HasForeignKey(pli => pli.POID);

        modelBuilder.Entity<POLineItem>()
            .HasOne(pli => pli.Product)
            .WithMany(p => p.POLineItems)
            .HasForeignKey(pli => pli.ProductID);

        modelBuilder.Entity<InboundReceipt>()
            .HasOne(ir => ir.PurchaseOrder)
            .WithMany(po => po.InboundReceipts)
            .HasForeignKey(ir => ir.POID);

        modelBuilder.Entity<InboundReceipt>()
            .HasOne(ir => ir.POLineItem)
            .WithMany()
            .HasForeignKey(ir => ir.POLineItemID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<InboundReceipt>()
            .HasOne(ir => ir.Warehouse)
            .WithMany(w => w.InboundReceipts)
            .HasForeignKey(ir => ir.WarehouseID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InboundReceipt>()
            .HasOne(ir => ir.ReceivedByUser)
            .WithMany()
            .HasForeignKey(ir => ir.ReceivedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Inventory>()
            .HasOne(i => i.Product)
            .WithMany(p => p.Inventories)
            .HasForeignKey(i => i.ProductID);

        modelBuilder.Entity<Inventory>()
            .HasOne(i => i.Warehouse)
            .WithMany(w => w.Inventories)
            .HasForeignKey(i => i.WarehouseID);

        modelBuilder.Entity<ProductionBatch>()
            .HasMany(pb => pb.BatchMaterials)
            .WithOne(bm => bm.ProductionBatch)
            .HasForeignKey(bm => bm.BatchID);

        modelBuilder.Entity<BatchMaterial>()
            .HasOne(bm => bm.Product)
            .WithMany(p => p.BatchMaterials)
            .HasForeignKey(bm => bm.ProductID);

        modelBuilder.Entity<BatchMaterial>()
            .HasOne(bm => bm.SourceWarehouse)
            .WithMany(w => w.BatchMaterials)
            .HasForeignKey(bm => bm.SourceWarehouseID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<BatchMaterial>()
            .HasOne(bm => bm.ApprovedByUser)
            .WithMany()
            .HasForeignKey(bm => bm.ApprovedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SalesOrder>()
            .HasOne(so => so.Customer)
            .WithMany(c => c.SalesOrders)
            .HasForeignKey(so => so.CustomerID);

        modelBuilder.Entity<SalesOrder>()
            .HasOne(so => so.CreatedByUser)
            .WithMany()
            .HasForeignKey(so => so.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SOLineItem>()
            .HasOne(sli => sli.SalesOrder)
            .WithMany(so => so.SOLineItems)
            .HasForeignKey(sli => sli.SalesOrderID);

        modelBuilder.Entity<SOLineItem>()
            .HasOne(sli => sli.Product)
            .WithMany(p => p.SOLineItems)
            .HasForeignKey(sli => sli.ProductID);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.SalesOrder)
            .WithMany(so => so.Invoices)
            .HasForeignKey(i => i.SalesOrderID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Customer)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.CustomerID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceID);

        // Business history must never disappear when master data is removed.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;

        modelBuilder.Entity<Warehouse>().Property(x => x.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<Inventory>().Property(x => x.StockForm).HasMaxLength(16).HasDefaultValue("Standard");
        modelBuilder.Entity<Inventory>().Property(x => x.BinCode).HasMaxLength(64);
        modelBuilder.Entity<Inventory>().Property(x => x.LotNumber).HasMaxLength(100);
        modelBuilder.Entity<Inventory>().HasIndex(x => new { x.ProductID, x.WarehouseID, x.StockForm, x.BinCode, x.LotNumber }).IsUnique().AreNullsDistinct(false);
        modelBuilder.Entity<Inventory>().ToTable(t => t.HasCheckConstraint("CK_Inventory_Quantity", "\"QuantityAvailable\" >= 0 AND \"StockForm\" IN ('Standard','Bulk')"));
        modelBuilder.Entity<Invoice>().HasIndex(x => x.SalesOrderID).IsUnique();
        modelBuilder.Entity<Invoice>().ToTable(t => t.HasCheckConstraint("CK_Invoice_Amounts", "\"TotalAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"NetAmount\" = \"TotalAmount\" + \"TaxAmount\" AND \"Status\" IN ('Unpaid','PartiallyPaid','Paid')"));
        modelBuilder.Entity<Payment>().ToTable(t => t.HasCheckConstraint("CK_Payment_Amount", "\"PaymentAmount\" > 0"));
        modelBuilder.Entity<POLineItem>().ToTable(t => t.HasCheckConstraint("CK_POLine_Quantities", "\"Quantity\" > 0 AND \"RatePerUnit\" > 0 AND \"TotalAmount\" >= 0 AND \"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"Quantity\" * 1.05"));
        modelBuilder.Entity<SOLineItem>().ToTable(t => t.HasCheckConstraint("CK_SOLine_Quantities", "\"Quantity\" > 0 AND \"RatePerUnit\" > 0 AND \"TotalAmount\" >= 0 AND \"DispatchedQuantity\" >= 0 AND \"DispatchedQuantity\" <= \"Quantity\" AND \"DispatchStatus\" IN ('NotDispatched','PartiallyDispatched','Dispatched')"));
        modelBuilder.Entity<PurchaseOrder>().ToTable(t => t.HasCheckConstraint("CK_PO_Status", "\"Status\" IN ('Draft','Sent','Received','Closed','Cancelled')"));
        modelBuilder.Entity<SalesOrder>().ToTable(t => t.HasCheckConstraint("CK_SO_Status", "\"Status\" IN ('Quotation','Confirmed','PartiallyDispatched','Dispatched','Closed','Cancelled')"));
        modelBuilder.Entity<ProductionBatch>().ToTable(t => t.HasCheckConstraint("CK_Batch_Status", "\"Status\" IN ('Planned','InProgress','Completed') AND \"TotalInputQuantity\" >= 0 AND \"TotalOutputQuantity\" >= 0 AND \"TotalWasteQuantity\" >= 0"));
        modelBuilder.Entity<BatchMaterial>().Property(x => x.KgPerBag).HasPrecision(15, 3);
        modelBuilder.Entity<BatchMaterial>().HasOne(x => x.SourceInventory).WithMany().HasForeignKey(x => x.SourceInventoryID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<BatchMaterial>().HasOne(x => x.OutputInventory).WithMany().HasForeignKey(x => x.OutputInventoryID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<BatchMaterial>().ToTable(t => t.HasCheckConstraint("CK_Material_Quantities", "\"Quantity\" > 0 AND \"PackedQuantityKg\" >= 0 AND \"PackedQuantityKg\" <= \"Quantity\" AND \"MaterialType\" IN ('Input','Output','Waste')"));
        modelBuilder.Entity<InboundReceipt>().Property(x => x.ReceivedQuantity).HasPrecision(15, 3);
        modelBuilder.Entity<InboundReceipt>().Property(x => x.ReceiptType).HasMaxLength(16).HasDefaultValue("Weighed");
        modelBuilder.Entity<InboundReceipt>().ToTable(t => t.HasCheckConstraint("CK_Receipt_Status", "\"Status\" IN ('GateEntry','Weighed','QCApproved','Rejected','Received','Voided') AND \"ReceiptType\" IN ('Weighed','Counted') AND (\"ReceivedQuantity\" IS NULL OR \"ReceivedQuantity\" > 0)"));

        modelBuilder.Entity<InboundReceipt>().Property(x => x.VoidReason).HasMaxLength(500);
        modelBuilder.Entity<PurchaseOrder>().Property(x => x.ClosedReason).HasMaxLength(500);
        modelBuilder.Entity<SalesOrder>().Property(x => x.ClosedReason).HasMaxLength(500);

        modelBuilder.Entity<StockMovement>().Property(x => x.QuantityChange).HasPrecision(15, 3);
        modelBuilder.Entity<StockMovement>().Property(x => x.Unit).HasMaxLength(16);
        modelBuilder.Entity<StockMovement>().Property(x => x.MovementType).HasMaxLength(32);
        modelBuilder.Entity<StockMovement>().Property(x => x.Reference).HasMaxLength(100);
        modelBuilder.Entity<StockMovement>().HasIndex(x => new { x.InventoryID, x.RecordedAt });
        modelBuilder.Entity<StockMovement>().HasOne(x => x.Inventory).WithMany().HasForeignKey(x => x.InventoryID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Dispatch>().HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<DispatchLine>().HasOne(x => x.Dispatch).WithMany(x => x.Lines).HasForeignKey(x => x.DispatchID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<DispatchLine>().HasOne(x => x.SOLineItem).WithMany().HasForeignKey(x => x.SOLineItemID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<DispatchLine>().HasOne(x => x.Inventory).WithMany().HasForeignKey(x => x.InventoryID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<DispatchLine>().Property(x => x.Quantity).HasPrecision(15, 3);
        modelBuilder.Entity<DispatchLine>().ToTable(t => t.HasCheckConstraint("CK_DispatchLine_Quantity", "\"Quantity\" > 0"));
        modelBuilder.Entity<PackingRun>().HasOne(x => x.BatchMaterial).WithMany().HasForeignKey(x => x.BatchMaterialID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<PackingRun>().HasOne(x => x.BulkInventory).WithMany().HasForeignKey(x => x.BulkInventoryID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<PackingRun>().HasOne(x => x.FinishedInventory).WithMany().HasForeignKey(x => x.FinishedInventoryID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<PackingRun>().HasOne(x => x.PackagingInventory).WithMany().HasForeignKey(x => x.PackagingInventoryID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<PackingRun>().Property(x => x.QuantityKg).HasPrecision(15, 3);
        modelBuilder.Entity<PackingRun>().Property(x => x.KgPerBag).HasPrecision(15, 3);
        modelBuilder.Entity<PackingRun>().Property(x => x.BagsPacked).HasPrecision(15, 3);
        modelBuilder.Entity<PackingRun>().ToTable(t => t.HasCheckConstraint("CK_Packing_Quantities", "\"QuantityKg\" > 0 AND \"KgPerBag\" > 0 AND \"BagsPacked\" > 0 AND \"BagsPacked\" = FLOOR(\"BagsPacked\") AND \"QuantityKg\" = \"KgPerBag\" * \"BagsPacked\""));
        modelBuilder.Entity<InvoiceLine>().HasOne(x => x.Invoice).WithMany(x => x.Lines).HasForeignKey(x => x.InvoiceID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<InvoiceLine>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductID).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.Quantity).HasPrecision(15, 3);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.KgPerBag).HasPrecision(15, 3);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.RatePerUnit).HasPrecision(15, 2);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.TotalAmount).HasPrecision(15, 2);
        // Dates the dashboard and reports filter by ("this month", "last 30 days", overdue): without these every such
        // query reads the whole table, which grows by thousands of rows a year.
        modelBuilder.Entity<SalesOrder>().HasIndex(x => x.SODate);
        modelBuilder.Entity<Invoice>().HasIndex(x => x.InvoiceDate);
        modelBuilder.Entity<Invoice>().HasIndex(x => x.DueDate);
        modelBuilder.Entity<Payment>().HasIndex(x => x.PaymentDate);
        modelBuilder.Entity<ProductionBatch>().HasIndex(x => x.ProductionDate);
        modelBuilder.Entity<StockMovement>().HasIndex(x => x.RecordedAt);
        modelBuilder.Entity<InboundReceipt>().HasIndex(x => x.CreatedDate);
        modelBuilder.Entity<Dispatch>().HasIndex(x => x.DispatchedAt);
        foreach (var sequence in new[] { "PurchaseOrderNumbers", "SalesOrderNumbers", "ReceiptNumbers", "BatchNumbers", "InvoiceNumbers" })
            modelBuilder.HasSequence<long>(sequence);
    }
}
