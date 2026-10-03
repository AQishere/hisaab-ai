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
public class InboundReceiptsController : ControllerBase
{
    private readonly AppDbContext context;

    public InboundReceiptsController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<InboundReceiptDTO>>> GetAll()
    {
        var receipts = await context.InboundReceipts
            .Include(ir => ir.Warehouse)
            .Include(ir => ir.PurchaseOrder)
            .ToListAsync();

        return Ok(receipts.Select(r => MapToDTO(r)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InboundReceiptDTO>> GetById(int id)
    {
        var receipt = await context.InboundReceipts
            .Include(ir => ir.Warehouse)
            .Include(ir => ir.PurchaseOrder)
            .FirstOrDefaultAsync(ir => ir.ReceiptID == id);

        if (receipt == null)
            return NotFound();

        return Ok(MapToDTO(receipt));
    }

    [HttpPost("gate-entry")]
    public async Task<ActionResult<InboundReceiptDTO>> CreateGateEntry(CreateGateEntryDTO dto)
    {
        var po = await context.PurchaseOrders.FindAsync(dto.POID);
        if (po == null)
            return BadRequest("Purchase Order not found");

        var receiptNumber = await GenerateReceiptNumber();

        var receipt = new InboundReceipt
        {
            ReceiptNumber = receiptNumber,
            POID = dto.POID,
            TruckNumber = dto.TruckNumber,
            DriverName = dto.DriverName,
            DriverPhone = dto.DriverPhone,
            ArrivalDateTime = DateTime.Now,
            Status = "GateEntry",
            CreatedDate = DateTime.Now
        };

        context.InboundReceipts.Add(receipt);
        await context.SaveChangesAsync();

        var createdReceipt = await context.InboundReceipts
            .Include(ir => ir.Warehouse)
            .Include(ir => ir.PurchaseOrder)
            .FirstOrDefaultAsync(ir => ir.ReceiptID == receipt.ReceiptID);

        return CreatedAtAction(nameof(GetById), new { id = receipt.ReceiptID }, MapToDTO(createdReceipt));
    }

    [HttpPut("{id}/weighbridge")]
    public async Task<ActionResult> UpdateWeighbridge(int id, UpdateWeighbridgeDTO dto)
    {
        var receipt = await context.InboundReceipts.FindAsync(id);
        if (receipt == null)
            return NotFound();

        receipt.GrossWeight = dto.GrossWeight;
        receipt.TareWeight = dto.TareWeight;
        receipt.NetWeight = dto.GrossWeight - dto.TareWeight;
        receipt.Status = "Weighed";

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/qc")]
    public async Task<ActionResult> UpdateQC(int id, UpdateQCDTO dto)
    {
        var receipt = await context.InboundReceipts.FindAsync(id);
        if (receipt == null)
            return NotFound();

        receipt.Moisture = dto.Moisture;
        receipt.Impurities = dto.Impurities;
        receipt.GrainType = dto.GrainType;
        receipt.QCResult = dto.QCResult;
        receipt.Status = dto.QCResult == "Approved" ? "QCApproved" : "Rejected";

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/grn")]
    public async Task<ActionResult> CreateGRN(int id, CreateGRNDTO dto)
    {
        var receipt = await context.InboundReceipts.FindAsync(id);
        if (receipt == null)
            return NotFound();

        if (receipt.Status != "QCApproved")
            return BadRequest("QC must be approved before creating GRN");

        receipt.WarehouseID = dto.WarehouseID;
        receipt.BinCode = dto.BinCode;
        receipt.LotNumber = dto.LotNumber;
        receipt.ReceivedDate = DateTime.Now;
        receipt.Status = "Received";

        var warehouse = await context.Warehouses.FindAsync(dto.WarehouseID);
        if (warehouse == null)
            return BadRequest("Warehouse not found");

        var po = await context.PurchaseOrders
            .Include(p => p.POLineItems)
            .FirstOrDefaultAsync(p => p.POID == receipt.POID);

        if (po != null && po.POLineItems.Any())
        {
            foreach (var lineItem in po.POLineItems)
            {
                var inventory = await context.Inventories
                    .FirstOrDefaultAsync(inv =>
                        inv.ProductID == lineItem.ProductID &&
                        inv.WarehouseID == dto.WarehouseID &&
                        inv.LotNumber == dto.LotNumber);

                if (inventory != null)
                {
                    inventory.QuantityAvailable += receipt.NetWeight ?? 0;
                    inventory.LastUpdatedDate = DateTime.Now;
                }
                else
                {
                    var newInventory = new Inventory
                    {
                        ProductID = lineItem.ProductID,
                        WarehouseID = dto.WarehouseID,
                        BinCode = dto.BinCode,
                        LotNumber = dto.LotNumber,
                        QuantityAvailable = receipt.NetWeight ?? 0,
                        LastUpdatedDate = DateTime.Now
                    };
                    context.Inventories.Add(newInventory);
                }

                lineItem.ReceivedQuantity = receipt.NetWeight ?? 0;
            }

            po.Status = "Received";
        }

        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GenerateReceiptNumber()
    {
        var count = await context.InboundReceipts.CountAsync();
        var year = DateTime.Now.Year;
        return $"GE-{year}-{count + 1:D3}";
    }

    private InboundReceiptDTO MapToDTO(InboundReceipt r)
    {
        return new InboundReceiptDTO
        {
            ReceiptID = r.ReceiptID,
            ReceiptNumber = r.ReceiptNumber,
            POID = r.POID,
            PONumber = r.PurchaseOrder?.PONumber ?? string.Empty,
            TruckNumber = r.TruckNumber,
            DriverName = r.DriverName,
            DriverPhone = r.DriverPhone,
            ArrivalDateTime = r.ArrivalDateTime,
            GrossWeight = r.GrossWeight,
            TareWeight = r.TareWeight,
            NetWeight = r.NetWeight,
            Moisture = r.Moisture,
            Impurities = r.Impurities,
            GrainType = r.GrainType,
            QCResult = r.QCResult,
            WarehouseID = r.WarehouseID,
            WarehouseName = r.Warehouse?.WarehouseName,
            BinCode = r.BinCode,
            LotNumber = r.LotNumber,
            Status = r.Status,
            ReceivedDate = r.ReceivedDate,
            CreatedDate = r.CreatedDate
        };
    }
}
