using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Hisaab.ERP.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hisaab.ERP.Data;
using Hisaab.ERP.Models;
using Hisaab.ERP.DTOs;

namespace Hisaab.ERP.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.Floor)]
public class ProductionBatchesController : ControllerBase
{
    private readonly AppDbContext context;

    public ProductionBatchesController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] ListQuery list)
    {
        IQueryable<ProductionBatch> query = context.ProductionBatches
            .Include(pb => pb.BatchMaterials)
            .ThenInclude(bm => bm.Product);
        if (!string.IsNullOrWhiteSpace(list.Status))
            query = query.Where(pb => pb.Status == list.Status);
        if (list.Pattern is { } like)
            query = query.Where(pb => EF.Functions.ILike(pb.BatchNumber, like, @"\") || EF.Functions.ILike(pb.OperatorName!, like, @"\") ||
                EF.Functions.ILike(pb.Status, like, @"\"));

        if (!list.Paged)
            return Ok((await query.ToListAsync()).Select(b => MapToDTO(b)).ToList());
        var page = await Paging.PageAsync(query.OrderByDescending(pb => pb.ProductionDate).ThenByDescending(pb => pb.BatchID), list);
        return Ok(Paging.Result(page, b => MapToDTO(b)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductionBatchDTO>> GetById(int id)
    {
        var batch = await context.ProductionBatches
            .Include(pb => pb.BatchMaterials)
            .ThenInclude(bm => bm.Product)
            .FirstOrDefaultAsync(pb => pb.BatchID == id);

        if (batch == null)
            return NotFound();

        return Ok(MapToDTO(batch));
    }

    [HttpPost]
    public async Task<ActionResult<ProductionBatchDTO>> Create(CreateProductionBatchDTO dto)
    {
        if (dto.ShiftNumber < 1 || dto.ShiftNumber > 3) return BadRequest("Choose shift 1, 2, or 3.");
        var batchNumber = await GenerateBatchNumber();

        var batch = new ProductionBatch
        {
            BatchNumber = batchNumber,
            ProductionDate = dto.ProductionDate,
            ShiftNumber = dto.ShiftNumber,
            Status = "Planned",
            TotalInputQuantity = 0,
            TotalOutputQuantity = 0,
            TotalWasteQuantity = 0,
            YieldPercentage = 0,
            OperatorName = dto.OperatorName,
            CreatedDate = DateTime.Now
        };

        context.ProductionBatches.Add(batch);
        await context.SaveChangesAsync();

        var createdBatch = await context.ProductionBatches
            .Include(pb => pb.BatchMaterials)
            .ThenInclude(bm => bm.Product)
            .SingleAsync(pb => pb.BatchID == batch.BatchID);

        return CreatedAtAction(nameof(GetById), new { id = batch.BatchID }, MapToDTO(createdBatch));
    }

    [HttpPost("{id}/add-input")]
    public async Task<ActionResult> AddInput(int id, AddBatchInputDTO dto)
    {
        var batch = await context.ProductionBatches.FindAsync(id);
        if (batch == null)
            return NotFound();

        if (batch.Status == "Completed")
            return BadRequest("Batch is already completed");

        if (!BusinessRules.Quantity(dto.Quantity))
            return BadRequest("Quantity must be positive with at most three decimal places");

        var product = await context.Products.FindAsync(dto.ProductID);
        if (product == null)
            return BadRequest("Product not found");

        if (product.Category != "Raw Material" || product.Unit != "kg")
            return BadRequest("Batch input must be raw material measured in kg");

        var inventory = await context.Inventories
            .FirstOrDefaultAsync(inv =>
                inv.InventoryID == dto.InventoryID &&
                inv.ProductID == dto.ProductID &&
                inv.WarehouseID == dto.SourceWarehouseID &&
                inv.LotNumber == dto.SourceLotNumber);

        if (inventory == null || inventory.QuantityAvailable < dto.Quantity)
            return BadRequest("Insufficient inventory");

        var material = new BatchMaterial
        {
            BatchID = id,
            ProductID = dto.ProductID,
            MaterialType = "Input",
            Quantity = dto.Quantity,
            SourceInventoryID = inventory.InventoryID,
            SourceWarehouseID = dto.SourceWarehouseID,
            SourceLotNumber = dto.SourceLotNumber,
            CreatedDate = DateTime.Now
        };

        context.BatchMaterials.Add(material);

        StockLedger.Change(context, inventory, -dto.Quantity, "kg", "ProductionInput", batch.BatchNumber, User);

        batch.Status = "InProgress";
        await context.SaveChangesAsync();
        await RecalculateTotals(batch);

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/add-output")]
    public async Task<ActionResult> AddOutput(int id, AddBatchOutputDTO dto)
    {
        var batch = await context.ProductionBatches.FindAsync(id);
        if (batch == null)
            return NotFound();

        if (batch.Status == "Completed")
            return BadRequest("Batch is already completed");

        if (!BusinessRules.Quantity(dto.Quantity))
            return BadRequest("Quantity must be positive with at most three decimal places");

        if (dto.MaterialType != "Output" && dto.MaterialType != "Waste")
            return BadRequest("MaterialType must be Output or Waste");

        var product = await context.Products.FindAsync(dto.ProductID);
        if (product == null)
            return BadRequest("Product not found");

        if (dto.MaterialType == "Output" && product.Category != "Finished Good" && product.Category != "By-product")
            return BadRequest("Output must be a finished good or by-product");

        if (dto.MaterialType == "Waste" && product.Category != "Waste")
            return BadRequest("Waste must use a waste product");

        if (dto.MaterialType == "Output" && (product.Unit != "kg" && product.Unit != "bag" || product.Unit == "bag" && product.KgPerBag is null or <= 0))
            return BadRequest("Choose a kg product or a bag product with a valid bag size.");
        var material = new BatchMaterial
        {
            BatchID = id,
            ProductID = dto.ProductID,
            KgPerBag = product.Unit == "bag" ? product.KgPerBag : null,
            MaterialType = dto.MaterialType,
            Quantity = dto.Quantity,
            IsQualityApproved = false,
            CreatedDate = DateTime.Now
        };

        context.BatchMaterials.Add(material);

        await context.SaveChangesAsync();
        await RecalculateTotals(batch);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/finalize")]
    public async Task<ActionResult> FinalizeBatch(int id, FinalizeBatchDTO dto)
    {
        var batch = await context.ProductionBatches
            .Include(pb => pb.BatchMaterials)
            .ThenInclude(bm => bm.Product)
            .FirstOrDefaultAsync(pb => pb.BatchID == id);

        if (batch == null)
            return NotFound();

        if (batch.Status == "Completed")
            return BadRequest("Batch is already completed");

        await RecalculateTotals(batch);
        if (batch.TotalInputQuantity == 0)
            return BadRequest("Cannot finalize batch without inputs");

        if (batch.TotalOutputQuantity <= 0)
            return BadRequest("Cannot finalize batch without outputs");

        if (batch.TotalOutputQuantity + batch.TotalWasteQuantity != batch.TotalInputQuantity)
            return BadRequest("Output and waste must equal the input quantity before finalizing");

        if (batch.BatchMaterials.Any(bm => !bm.IsReversed && bm.MaterialType == "Output" && !bm.IsQualityApproved))
            return BadRequest("All outputs must be quality approved before finalizing");

        if (!await context.Warehouses.AnyAsync(w => w.WarehouseID == dto.WarehouseID && w.IsActive))
            return BadRequest("Choose the warehouse that receives the finished goods");

        var outputs = batch.BatchMaterials.Where(bm => !bm.IsReversed && bm.MaterialType == "Output").ToList();
        foreach (var output in outputs)
        {
            if (output.Product?.Unit == "bag")
            {
                if (output.KgPerBag is null or <= 0)
                    return BadRequest($"Set the bag size for {output.Product.ProductName} before completing the batch");
            }
            else if (output.Product?.Unit != "kg")
                return BadRequest("Production outputs must be measured in kg or packed in bags");
        }

        batch.YieldPercentage = (batch.TotalOutputQuantity / batch.TotalInputQuantity) * 100;
        batch.Status = "Completed";

        foreach (var output in outputs)
        {
            var form = output.Product!.Unit == "bag" ? "Bulk" : "Standard";
            var inventory = await StockLedger.GetOrCreate(context, output.ProductID, dto.WarehouseID, null, batch.BatchNumber, form);
            output.OutputInventory = inventory;
            StockLedger.Change(context, inventory, output.Quantity, "kg", "ProductionOutput", batch.BatchNumber, User);
        }

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/pack-output/{materialId}")]
    public async Task<ActionResult> PackOutput(int id, int materialId, PackBatchOutputDTO dto)
    {
        var batch = await context.ProductionBatches.FindAsync(id);
        if (batch == null)
            return NotFound();

        if (batch.Status != "Completed")
            return BadRequest("Complete the batch before packing its outputs");

        var output = await context.BatchMaterials
            .Include(bm => bm.Product)
            .FirstOrDefaultAsync(bm => bm.BatchID == id && bm.BatchMaterialID == materialId && !bm.IsReversed && bm.MaterialType == "Output");
        if (output == null)
            return NotFound();

        if (output.IsReversed || !output.IsQualityApproved || output.Product?.Unit != "bag" || output.KgPerBag is null or <= 0)
            return BadRequest("Output must be approved and have a recorded bag size.");
        if (!BusinessRules.Quantity(dto.QuantityKg) || dto.QuantityKg > output.Quantity - output.PackedQuantityKg)
            return BadRequest("Packing quantity must be positive, have at most three decimals, and not exceed unpacked output.");
        var kgPerBag = output.KgPerBag.Value;
        if (dto.QuantityKg % kgPerBag != 0)
            return BadRequest($"Packing quantity must be a whole number of {kgPerBag} kg bags.");
        if (!await context.Warehouses.AnyAsync(w => w.WarehouseID == dto.WarehouseID && w.IsActive))
            return BadRequest("Choose an active destination warehouse.");
        var bulk = await context.Inventories.FindAsync(output.OutputInventoryID);
        if (bulk == null || bulk.StockForm != "Bulk" || bulk.QuantityAvailable < dto.QuantityKg)
            return BadRequest("Locate this legacy bulk output before packing, or check the available bulk quantity.");
        var bagsPacked = dto.QuantityKg / kgPerBag;
        var packaging = await context.Inventories.Include(i => i.Product).FirstOrDefaultAsync(i => i.InventoryID == dto.PackagingInventoryID);
        if (packaging?.Product?.Category != "Packaging" || packaging.Product.Unit != "piece" || packaging.QuantityAvailable < bagsPacked)
            return BadRequest("Choose a packaging lot with enough empty bags (one piece per bag).");
        var inventory = await StockLedger.GetOrCreate(context, output.ProductID, dto.WarehouseID, null, batch.BatchNumber);
        var packing = new PackingRun { BatchMaterialID = materialId, BulkInventory = bulk, FinishedInventory = inventory,
            PackagingInventory = packaging, QuantityKg = dto.QuantityKg, KgPerBag = kgPerBag, BagsPacked = bagsPacked,
            RecordedBy = StockLedger.UserId(User) };
        context.PackingRuns.Add(packing);
        await context.SaveChangesAsync();
        var reference = $"PACK-{packing.PackingRunID}";
        StockLedger.Change(context, bulk, -dto.QuantityKg, "kg", "PackingBulk", reference, User);
        StockLedger.Change(context, packaging, -bagsPacked, "piece", "PackingSupply", reference, User);
        StockLedger.Change(context, inventory, bagsPacked, "bag", "PackingOutput", reference, User);

        output.PackedQuantityKg += dto.QuantityKg;
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/approve-output/{materialId}")]
    public async Task<ActionResult> ApproveOutput(int id, int materialId)
    {
        var batch = await context.ProductionBatches.FindAsync(id);
        if (batch == null)
            return NotFound();

        if (batch.Status == "Completed")
            return BadRequest("Completed batches cannot be changed");

        var output = await context.BatchMaterials.FirstOrDefaultAsync(bm =>
            bm.BatchID == id && bm.BatchMaterialID == materialId && bm.MaterialType == "Output");
        if (output == null)
            return NotFound();

        output.IsQualityApproved = true;
        output.ApprovalDate = DateTime.Now;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            output.ApprovedBy = userId;

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/reverse-material/{materialId}")]
    public async Task<ActionResult> ReverseMaterial(int id, int materialId, ReverseMaterialDTO dto)
    {
        var batch = await context.ProductionBatches.FindAsync(id);
        if (batch == null) return NotFound();
        if (batch.Status == "Completed") return BadRequest("Completed batches cannot be changed.");
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest("A correction reason is required.");
        var material = await context.BatchMaterials.FirstOrDefaultAsync(m => m.BatchID == id && m.BatchMaterialID == materialId);
        if (material == null) return NotFound();
        if (material.IsReversed) return BadRequest("This material has already been reversed.");
        if (material.MaterialType == "Input")
        {
            var inventoryId = material.SourceInventoryID ?? dto.InventoryID;
            var inventory = inventoryId.HasValue ? await context.Inventories.FindAsync(inventoryId.Value) : null;
            if (inventory == null || inventory.ProductID != material.ProductID || inventory.WarehouseID != material.SourceWarehouseID || inventory.LotNumber != material.SourceLotNumber)
                return BadRequest("Choose the original warehouse and lot for this legacy input reversal.");
            StockLedger.Change(context, inventory, material.Quantity, "kg", "ProductionReversal", batch.BatchNumber, User, dto.Reason.Trim());
        }
        material.IsReversed = true;
        material.ReversalReason = dto.Reason.Trim();
        material.ReversedAt = DateTime.Now;
        material.ReversedBy = StockLedger.UserId(User);
        await context.SaveChangesAsync();
        await RecalculateTotals(batch);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/locate-output/{materialId}")]
    public async Task<ActionResult> LocateLegacyOutput(int id, int materialId, FinalizeBatchDTO dto)
    {
        var material = await context.BatchMaterials.Include(m => m.ProductionBatch).Include(m => m.Product)
            .FirstOrDefaultAsync(m => m.BatchID == id && m.BatchMaterialID == materialId);
        if (material == null) return NotFound();
        if (material.ProductionBatch!.Status != "Completed" || material.MaterialType != "Output" || material.IsReversed ||
            material.Product!.Unit != "bag" || material.OutputInventoryID.HasValue || material.Quantity <= material.PackedQuantityKg)
            return BadRequest("Only unlocated, unpacked legacy outputs can be assigned a warehouse.");
        if (!await context.Warehouses.AnyAsync(w => w.WarehouseID == dto.WarehouseID && w.IsActive)) return BadRequest("Choose an active warehouse.");
        if (material.KgPerBag is null or <= 0) return BadRequest("This legacy output needs an explicit bag-size data correction before packing.");
        var inventory = await StockLedger.GetOrCreate(context, material.ProductID, dto.WarehouseID, null, material.ProductionBatch.BatchNumber, "Bulk");
        material.OutputInventory = inventory;
        StockLedger.Change(context, inventory, material.Quantity - material.PackedQuantityKg, "kg", "LegacyBulkOpening", material.ProductionBatch.BatchNumber, User, "Operator assigned the location of pre-ledger unpacked output.");
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/packing-runs")]
    public async Task<ActionResult> PackingHistory(int id) => Ok(await context.PackingRuns
        .Where(p => p.BatchMaterial.BatchID == id).OrderByDescending(p => p.PackedAt)
        .Select(p => new { p.PackingRunID, p.BatchMaterialID, p.QuantityKg, p.KgPerBag, p.BagsPacked, p.PackedAt,
            ProductName = p.BatchMaterial.Product!.ProductName,
            SourceWarehouse = p.BulkInventory.Warehouse!.WarehouseName, DestinationWarehouse = p.FinishedInventory.Warehouse!.WarehouseName,
            PackagingLot = p.PackagingInventory.LotNumber }).ToListAsync());

    private async Task RecalculateTotals(ProductionBatch batch)
    {
        var materials = await context.BatchMaterials.Where(m => m.BatchID == batch.BatchID && !m.IsReversed).ToListAsync();
        batch.TotalInputQuantity = materials.Where(m => m.MaterialType == "Input").Sum(m => m.Quantity);
        batch.TotalOutputQuantity = materials.Where(m => m.MaterialType == "Output").Sum(m => m.Quantity);
        batch.TotalWasteQuantity = materials.Where(m => m.MaterialType == "Waste").Sum(m => m.Quantity);
    }

    private Task<string> GenerateBatchNumber() => BusinessRules.NextNumber(context, "MB");

    private ProductionBatchDTO MapToDTO(ProductionBatch pb)
    {
        return new ProductionBatchDTO
        {
            BatchID = pb.BatchID,
            BatchNumber = pb.BatchNumber,
            ProductionDate = pb.ProductionDate,
            ShiftNumber = pb.ShiftNumber,
            Status = pb.Status,
            TotalInputQuantity = pb.TotalInputQuantity,
            TotalOutputQuantity = pb.TotalOutputQuantity,
            TotalWasteQuantity = pb.TotalWasteQuantity,
            YieldPercentage = pb.YieldPercentage,
            OperatorName = pb.OperatorName,
            Materials = pb.BatchMaterials.Select(bm => new BatchMaterialDTO
            {
                BatchMaterialID = bm.BatchMaterialID,
                ProductID = bm.ProductID,
                ProductName = bm.Product?.ProductName,
                MaterialType = bm.MaterialType,
                Quantity = bm.Quantity,
                PackedQuantityKg = bm.PackedQuantityKg,
                KgPerBag = bm.KgPerBag, SourceInventoryID = bm.SourceInventoryID, OutputInventoryID = bm.OutputInventoryID,
                SourceWarehouseID = bm.SourceWarehouseID, IsReversed = bm.IsReversed, ReversalReason = bm.ReversalReason,
                SourceLotNumber = bm.SourceLotNumber,
                IsQualityApproved = bm.IsQualityApproved
            }).ToList(),
            CreatedDate = pb.CreatedDate
        };
    }
}
