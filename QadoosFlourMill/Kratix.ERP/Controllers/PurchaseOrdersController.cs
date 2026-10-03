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
public class PurchaseOrdersController : ControllerBase
{
    private readonly AppDbContext context;

    public PurchaseOrdersController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<PurchaseOrderDTO>>> GetAll()
    {
        var orders = await context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.POLineItems)
            .ThenInclude(li => li.Product)
            .ToListAsync();

        return Ok(orders.Select(po => MapToDTO(po)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseOrderDTO>> GetById(int id)
    {
        var order = await context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.POLineItems)
            .ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(po => po.POID == id);

        if (order == null)
            return NotFound();

        return Ok(MapToDTO(order));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult<PurchaseOrderDTO>> Create(CreatePurchaseOrderDTO dto)
    {
        if (dto.LineItems == null || dto.LineItems.Count == 0 || dto.LineItems.Any(i => !BusinessRules.Quantity(i.Quantity) || !BusinessRules.Money(i.RatePerUnit) || !BusinessRules.Money(BusinessRules.Amount(i.Quantity, i.RatePerUnit), true)))
            return BadRequest("Add at least one line with positive quantities (up to 3 decimals) and rates (up to 2 decimals).");
        var supplier = await context.Suppliers.FindAsync(dto.SupplierID);
        if (supplier == null || !supplier.IsActive)
            return BadRequest("Supplier not found");

        var poNumber = await GeneratePONumber();

        var order = new PurchaseOrder
        {
            PONumber = poNumber,
            SupplierID = dto.SupplierID,
            PODate = dto.PODate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Status = "Draft",
            CreatedBy = StockLedger.UserId(User),
            CreatedDate = DateTime.Now
        };

        foreach (var item in dto.LineItems)
        {
            var product = await context.Products.FindAsync(item.ProductID);
            if (product == null)
                return BadRequest($"Product {item.ProductID} not found");

            if (!((product.Category == "Raw Material" && product.Unit == "kg") || (product.Category == "Packaging" && product.Unit == "piece")))
                return BadRequest("Purchase raw materials in kg or packaging in pieces.");
            if (product.Unit == "piece" && item.Quantity != decimal.Truncate(item.Quantity))
                return BadRequest("Packaging quantities must be whole pieces.");
            var lineItem = new POLineItem
            {
                ProductID = item.ProductID,
                Quantity = item.Quantity,
                RatePerUnit = item.RatePerUnit,
                TotalAmount = BusinessRules.Amount(item.Quantity, item.RatePerUnit),
                ReceivedQuantity = 0
            };

            order.POLineItems.Add(lineItem);
        }

        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();

        var createdOrder = await context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.POLineItems)
            .ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(po => po.POID == order.POID);

        return CreatedAtAction(nameof(GetById), new { id = order.POID }, MapToDTO(createdOrder));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult> Update(int id, UpdatePurchaseOrderDTO dto)
    {
        var order = await context.PurchaseOrders.FindAsync(id);
        if (order == null)
            return NotFound();

        var allowed = order.Status == dto.Status ||
            (order.Status == "Draft" && (dto.Status == "Sent" || dto.Status == "Cancelled")) ||
            (order.Status == "Sent" && dto.Status == "Cancelled");
        if (!allowed || order.Status is "Received" or "Closed" or "Cancelled")
            return BadRequest("Only draft orders can be sent; received, closed and cancelled orders cannot be changed.");
        if (dto.Status == "Sent" && !await context.POLineItems.AnyAsync(i => i.POID == id))
            return BadRequest("Add order lines before sending the order.");
        if (dto.Status == "Cancelled" && await context.InboundReceipts.AnyAsync(r => r.POID == id && r.Status != "Rejected" && r.Status != "Voided"))
            return BadRequest("This order has receipts. Void open receipts, or close the order short if stock was received.");
        order.ExpectedDeliveryDate = dto.ExpectedDeliveryDate;
        order.Status = dto.Status;

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/close")]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult> CloseShort(int id, ReasonDTO dto)
    {
        var order = await context.PurchaseOrders.FindAsync(id);
        if (order == null)
            return NotFound();

        if (order.Status != "Sent")
            return BadRequest("Only a sent order can be closed short.");

        var reason = BusinessRules.Reason(dto.Reason);
        if (reason == null)
            return BadRequest("Give a reason of up to 500 characters");

        var receipts = await context.InboundReceipts.Where(r => r.POID == id).Select(r => r.Status).ToListAsync();
        if (receipts.Any(s => s is "GateEntry" or "Weighed" or "QCApproved"))
            return BadRequest("Finish or void the open receipts before closing this order.");
        if (!receipts.Contains("Received"))
            return BadRequest("Nothing has been received on this order; cancel it instead.");

        order.Status = "Closed";
        order.ClosedReason = reason;
        order.LastModifiedDate = DateTime.Now;
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult> Delete(int id)
    {
        var order = await context.PurchaseOrders.FindAsync(id);
        if (order == null)
            return NotFound();

        if (order.Status != "Draft" || await context.InboundReceipts.AnyAsync(r => r.POID == id))
            return BadRequest("Only unused draft orders can be deleted.");
        var lines = await context.POLineItems.Where(i => i.POID == id).ToListAsync();
        context.POLineItems.RemoveRange(lines);
        context.PurchaseOrders.Remove(order);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private Task<string> GeneratePONumber() => BusinessRules.NextNumber(context, "PO");

    private PurchaseOrderDTO MapToDTO(PurchaseOrder po)
    {
        return new PurchaseOrderDTO
        {
            POID = po.POID,
            PONumber = po.PONumber,
            SupplierID = po.SupplierID,
            SupplierName = po.Supplier?.SupplierName,
            PODate = po.PODate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            Status = po.Status,
            ClosedReason = po.ClosedReason,
            TotalAmount = po.POLineItems?.Sum(li => li.TotalAmount) ?? 0,
            LineItems = po.POLineItems?.Select(li => new POLineItemDTO
            {
                LineItemID = li.LineItemID,
                ProductID = li.ProductID,
                ProductName = li.Product?.ProductName,
                Quantity = li.Quantity,
                Unit = li.Product?.Unit,
                RatePerUnit = li.RatePerUnit,
                TotalAmount = li.TotalAmount,
                ReceivedQuantity = li.ReceivedQuantity
            }).ToList(),
            CreatedDate = po.CreatedDate
        };
    }
}
