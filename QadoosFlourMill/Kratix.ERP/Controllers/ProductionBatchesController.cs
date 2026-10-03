using Microsoft.AspNetCore.Authorization;
using Kratix.ERP.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Data;
using Kratix.ERP.Models;
using Kratix.ERP.DTOs;

namespace Kratix.ERP.Controllers;

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
    public async Task<ActionResult<List<ProductionBatchDTO>>> GetAll()
    {
        var batches = await context.ProductionBatches
            .Include(pb => pb.BatchMaterials)
            .ThenInclude(bm => bm.Product)
            .ToListAsync();

        return Ok(batches.Select(b => MapToDTO(b)).ToList());
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
            .FirstOrDefaultAsync(pb => pb.BatchID == batch.BatchID);

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

        if (dto.Quantity <= 0)
            return BadRequest("Quantity must be greater than zero");

        var product = await context.Products.FindAsync(dto.ProductID);
        if (product == null)
            return BadRequest("Product not found");

        var inventory = await context.Inventories
            .FirstOrDefaultAsync(inv =>
                inv.ProductID == dto.ProductID &&
                inv.LotNumber == dto.SourceLotNumber);

        if (inventory == null || inventory.QuantityAvailable < dto.Quantity)
            return BadRequest("Insufficient inventory");

        var material = new BatchMaterial
        {
            BatchID = id,
            ProductID = dto.ProductID,
            MaterialType = "Input",
            Quantity = dto.Quantity,
            SourceWarehouseID = dto.SourceWarehouseID,
            SourceLotNumber = dto.SourceLotNumber,
            CreatedDate = DateTime.Now
        };

        context.BatchMaterials.Add(material);

        inventory.QuantityAvailable -= dto.Quantity;
        inventory.LastUpdatedDate = DateTime.Now;

        batch.TotalInputQuantity += dto.Quantity;
        batch.Status = "InProgress";

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

        if (dto.Quantity <= 0)
            return BadRequest("Quantity must be greater than zero");

        if (dto.MaterialType != "Output" && dto.MaterialType != "Waste")
            return BadRequest("MaterialType must be Output or Waste");

        var product = await context.Products.FindAsync(dto.ProductID);
        if (product == null)
            return BadRequest("Product not found");

        var material = new BatchMaterial
        {
            BatchID = id,
            ProductID = dto.ProductID,
            MaterialType = dto.MaterialType,
            Quantity = dto.Quantity,
            IsQualityApproved = false,
            CreatedDate = DateTime.Now
        };

        context.BatchMaterials.Add(material);

        if (dto.MaterialType == "Output")
        {
            batch.TotalOutputQuantity += dto.Quantity;
        }
        else if (dto.MaterialType == "Waste")
        {
            batch.TotalWasteQuantity += dto.Quantity;
        }

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/finalize")]
    public async Task<ActionResult> FinalizeBatch(int id, FinalizeBatchDTO dto)
    {
        var batch = await context.ProductionBatches
            .Include(pb => pb.BatchMaterials)
            .FirstOrDefaultAsync(pb => pb.BatchID == id);

        if (batch == null)
            return NotFound();

        if (batch.Status == "Completed")
            return BadRequest("Batch is already completed");

        if (batch.TotalInputQuantity == 0)
            return BadRequest("Cannot finalize batch without inputs");

        if (await context.Warehouses.FindAsync(dto.WarehouseID) == null)
            return BadRequest("Choose the warehouse that receives the finished goods");

        batch.YieldPercentage = (batch.TotalOutputQuantity / batch.TotalInputQuantity) * 100;
        batch.Status = "Completed";

        foreach (var output in batch.BatchMaterials.Where(bm => bm.MaterialType == "Output"))
        {
            context.Inventories.Add(new Inventory
            {
                ProductID = output.ProductID,
                WarehouseID = dto.WarehouseID,
                LotNumber = batch.BatchNumber,
                QuantityAvailable = output.Quantity,
                LastUpdatedDate = DateTime.Now
            });
        }

        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GenerateBatchNumber()
    {
        var count = await context.ProductionBatches.CountAsync();
        var year = DateTime.Now.Year;
        return $"MB-{year}-{count + 1:D3}";
    }

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
            Materials = pb.BatchMaterials?.Select(bm => new BatchMaterialDTO
            {
                BatchMaterialID = bm.BatchMaterialID,
                ProductID = bm.ProductID,
                ProductName = bm.Product?.ProductName,
                MaterialType = bm.MaterialType,
                Quantity = bm.Quantity,
                SourceLotNumber = bm.SourceLotNumber,
                IsQualityApproved = bm.IsQualityApproved
            }).ToList(),
            CreatedDate = pb.CreatedDate
        };
    }
}
