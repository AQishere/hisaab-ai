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

        modelBuilder.Entity<InboundReceipt>().Property(x => x.GrossWeight).HasPrecision(15, 3);
        modelBuilder.Entity<InboundReceipt>().Property(x => x.TareWeight).HasPrecision(15, 3);
        modelBuilder.Entity<InboundReceipt>().Property(x => x.NetWeight).HasPrecision(15, 3);

        modelBuilder.Entity<Inventory>().Property(x => x.QuantityAvailable).HasPrecision(15, 3);

        modelBuilder.Entity<ProductionBatch>().Property(x => x.TotalInputQuantity).HasPrecision(15, 3);
        modelBuilder.Entity<ProductionBatch>().Property(x => x.TotalOutputQuantity).HasPrecision(15, 3);
        modelBuilder.Entity<ProductionBatch>().Property(x => x.TotalWasteQuantity).HasPrecision(15, 3);
        modelBuilder.Entity<ProductionBatch>().Property(x => x.YieldPercentage).HasPrecision(5, 2);

        modelBuilder.Entity<BatchMaterial>().Property(x => x.Quantity).HasPrecision(15, 3);

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
    }
}
