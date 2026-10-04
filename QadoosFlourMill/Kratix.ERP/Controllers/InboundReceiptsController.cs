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
            .Include(ir => ir.POLineItem).ThenInclude(li => li!.Product)
            .ToListAsync();

        return Ok(receipts.Select(r => MapToDTO(r)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InboundReceiptDTO>> GetById(int id)
    {
        var receipt = await context.InboundReceipts
            .Include(ir => ir.Warehouse)
            .Include(ir => ir.PurchaseOrder)
            .Include(ir => ir.POLineItem).ThenInclude(li => li!.Product)
            .FirstOrDefaultAsync(ir => ir.ReceiptID == id);

        if (receipt == null)
            return NotFound();

        return Ok(MapToDTO(receipt));
    }

    [HttpPost("gate-entry")]
    public async Task<ActionResult<InboundReceiptDTO>> CreateGateEntry(CreateGateEntryDTO dto)
    {
        var po = await context.PurchaseOrders
            .Include(p => p.POLineItems)
            .ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(p => p.POID == dto.POID);
        if (po == null)
            return BadRequest("Purchase Order not found");

        if (po.Status != "Sent")
            return BadRequest("Purchase Order must be sent before gate entry");

        var lineItem = po.POLineItems.FirstOrDefault(li => li.LineItemID == dto.POLineItemID);
        if (lineItem == null)
            return BadRequest("Choose a line item from this Purchase Order");

        if (lineItem.Product?.Category != "Raw Material" || lineItem.Product.Unit != "kg")
            return BadRequest("Weighed receipts require a raw material ordered in kg");

        if (lineItem.ReceivedQuantity >= lineItem.Quantity)
            return BadRequest("This Purchase Order line has already been fully received");

        var receiptNumber = await GenerateReceiptNumber();

        var receipt = new InboundReceipt
        {
            ReceiptNumber = receiptNumber,
            POID = dto.POID,
            POLineItemID = dto.POLineItemID,
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
            .Include(ir => ir.POLineItem).ThenInclude(li => li!.Product)
            .SingleAsync(ir => ir.ReceiptID == receipt.ReceiptID);

        return CreatedAtAction(nameof(GetById), new { id = receipt.ReceiptID }, MapToDTO(createdReceipt));
    }

    [HttpPut("{id}/weighbridge")]
    public async Task<ActionResult> UpdateWeighbridge(int id, UpdateWeighbridgeDTO dto)
    {
        var receipt = await context.InboundReceipts.FindAsync(id);
        if (receipt == null)
            return NotFound();

        if (receipt.Status != "GateEntry")
            return BadRequest("Only a gate entry can be weighed");

        if (dto.TareWeight < 0 || decimal.Round(dto.TareWeight, 3) != dto.TareWeight || !BusinessRules.Quantity(dto.GrossWeight) || dto.GrossWeight <= dto.TareWeight)
            return BadRequest("Gross weight must be positive and greater than tare weight");

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

        if (receipt.Status != "Weighed")
            return BadRequest("Only a weighed receipt awaiting inspection can be quality checked");

        if (receipt.NetWeight is null or <= 0)
            return BadRequest("Receipt must have a positive net weight before quality inspection");

        if (dto.Moisture < 0 || dto.Moisture > 100 || dto.Impurities < 0 || dto.Impurities > 100)
            return BadRequest("Quality percentages must be between 0 and 100");

        if (dto.QCResult != "Approved" && dto.QCResult != "Rejected")
            return BadRequest("QC result must be Approved or Rejected");

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

        if (receipt.NetWeight is null or <= 0)
            return BadRequest("Receipt must have a positive net weight");

        if (string.IsNullOrWhiteSpace(dto.LotNumber) || dto.LotNumber.Length > 100 || dto.BinCode?.Length > 64)
            return BadRequest("Lot number is required");

        var warehouse = await context.Warehouses.FindAsync(dto.WarehouseID);
        if (warehouse == null || !warehouse.IsActive)
            return BadRequest("Warehouse not found");

        var po = await context.PurchaseOrders
            .Include(p => p.POLineItems)
            .FirstOrDefaultAsync(p => p.POID == receipt.POID);

        if (po == null)
            return BadRequest("Purchase Order not found");

        if (po.Status != "Sent")
            return BadRequest("Purchase Order must still be sent to receive stock");

        var lineItem = receipt.POLineItemID.HasValue
            ? po.POLineItems.FirstOrDefault(li => li.LineItemID == receipt.POLineItemID)
            : po.POLineItems.Count == 1 ? po.POLineItems.Single() : null;
        if (lineItem == null)
            return BadRequest("Receipt must identify one Purchase Order line");

        var receivedWeight = receipt.NetWeight.Value;
        var receivable = BusinessRules.MaxReceivable(lineItem.Quantity) - lineItem.ReceivedQuantity;
        if (receivedWeight > receivable)
            return BadRequest($"Received weight exceeds the Purchase Order quantity plus the {BusinessRules.OverReceiptTolerance:P0} weighbridge tolerance ({Math.Max(receivable, 0):0.###} kg can still be accepted). Void this receipt and raise a new order for the excess.");

        var inventory = await StockLedger.GetOrCreate(context, lineItem.ProductID, dto.WarehouseID, dto.BinCode, dto.LotNumber);
        StockLedger.Change(context, inventory, receivedWeight, "kg", "Receipt", receipt.ReceiptNumber, User);

        lineItem.ReceivedQuantity += receivedWeight;
        po.Status = po.POLineItems.All(li => li.ReceivedQuantity >= li.Quantity) ? "Received" : "Sent";
        receipt.POLineItemID = lineItem.LineItemID;
        receipt.WarehouseID = dto.WarehouseID;
        receipt.BinCode = inventory.BinCode;
        receipt.LotNumber = inventory.LotNumber;
        receipt.ReceivedDate = DateTime.Now;
        receipt.ReceivedQuantity = receivedWeight;
        receipt.ReceivedBy = StockLedger.UserId(User);
        receipt.Status = "Received";

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/void")]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult> Void(int id, ReasonDTO dto)
    {
        var receipt = await context.InboundReceipts.FindAsync(id);
        if (receipt == null)
            return NotFound();

        if (receipt.Status is not ("GateEntry" or "Weighed" or "QCApproved"))
            return BadRequest("Only a receipt that has not been received into stock can be voided");

        var reason = BusinessRules.Reason(dto.Reason);
        if (reason == null)
            return BadRequest("Give a reason of up to 500 characters");

        receipt.Status = "Voided";
        receipt.VoidReason = reason;
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("count-receipt")]
    public async Task<ActionResult> ReceivePackaging(CountReceiptDTO dto)
    {
        if (!BusinessRules.Quantity(dto.Quantity) || dto.Quantity != decimal.Truncate(dto.Quantity))
            return BadRequest("Packaging quantity must be a positive whole number of pieces.");
        if (string.IsNullOrWhiteSpace(dto.LotNumber) || dto.LotNumber.Length > 100 || dto.BinCode?.Length > 64)
            return BadRequest("Provide a lot number (up to 100 characters) and a bin of at most 64 characters.");
        var order = await context.PurchaseOrders.Include(o => o.POLineItems).ThenInclude(l => l.Product).FirstOrDefaultAsync(o => o.POID == dto.POID);
        if (order == null || order.Status != "Sent") return BadRequest("Choose a sent purchase order.");
        var line = order.POLineItems.FirstOrDefault(l => l.LineItemID == dto.POLineItemID);
        if (line?.Product?.Category != "Packaging" || line.Product.Unit != "piece") return BadRequest("Choose a packaging line ordered in pieces.");
        if (dto.Quantity > line.Quantity - line.ReceivedQuantity) return BadRequest("Quantity exceeds the remaining order quantity.");
        if (!await context.Warehouses.AnyAsync(w => w.WarehouseID == dto.WarehouseID && w.IsActive)) return BadRequest("Choose an active warehouse.");
        var receipt = new InboundReceipt
        {
            ReceiptNumber = await GenerateReceiptNumber(), POID = dto.POID, POLineItemID = dto.POLineItemID,
            ReceiptType = "Counted", ReceivedQuantity = dto.Quantity, Status = "Received",
            WarehouseID = dto.WarehouseID, BinCode = string.IsNullOrWhiteSpace(dto.BinCode) ? null : dto.BinCode.Trim(), LotNumber = dto.LotNumber.Trim(),
            ArrivalDateTime = DateTime.Now, ReceivedDate = DateTime.Now, ReceivedBy = StockLedger.UserId(User),
            TruckNumber = dto.TruckNumber, DriverName = dto.DriverName
        };
        context.InboundReceipts.Add(receipt);
        var inventory = await StockLedger.GetOrCreate(context, line.ProductID, dto.WarehouseID, dto.BinCode, dto.LotNumber);
        StockLedger.Change(context, inventory, dto.Quantity, "piece", "Receipt", receipt.ReceiptNumber, User);
        line.ReceivedQuantity += dto.Quantity;
        order.Status = order.POLineItems.All(l => l.ReceivedQuantity >= l.Quantity) ? "Received" : "Sent";
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = receipt.ReceiptID }, new { receipt.ReceiptID, receipt.ReceiptNumber });
    }

    private Task<string> GenerateReceiptNumber() => BusinessRules.NextNumber(context, "GE");

    private InboundReceiptDTO MapToDTO(InboundReceipt r)
    {
        return new InboundReceiptDTO
        {
            ReceiptID = r.ReceiptID,
            ReceiptType = r.ReceiptType, ReceivedQuantity = r.ReceivedQuantity,
            ReceiptNumber = r.ReceiptNumber,
            POID = r.POID,
            POLineItemID = r.POLineItemID,
            ProductName = r.POLineItem?.Product?.ProductName,
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
            VoidReason = r.VoidReason,
            ReceivedDate = r.ReceivedDate,
            CreatedDate = r.CreatedDate
        };
    }
}
