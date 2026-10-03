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
        var supplier = await context.Suppliers.FindAsync(dto.SupplierID);
        if (supplier == null)
            return BadRequest("Supplier not found");

        var poNumber = await GeneratePONumber();

        var order = new PurchaseOrder
        {
            PONumber = poNumber,
            SupplierID = dto.SupplierID,
            PODate = dto.PODate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Status = "Draft",
            CreatedDate = DateTime.Now
        };

        foreach (var item in dto.LineItems)
        {
            var product = await context.Products.FindAsync(item.ProductID);
            if (product == null)
                return BadRequest($"Product {item.ProductID} not found");

            var lineItem = new POLineItem
            {
                ProductID = item.ProductID,
                Quantity = item.Quantity,
                RatePerUnit = item.RatePerUnit,
                TotalAmount = item.Quantity * item.RatePerUnit,
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

        order.ExpectedDeliveryDate = dto.ExpectedDeliveryDate;
        order.Status = dto.Status;

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

        context.PurchaseOrders.Remove(order);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GeneratePONumber()
    {
        var count = await context.PurchaseOrders.CountAsync();
        var year = DateTime.Now.Year;
        return $"PO-{year}-{count + 1:D3}";
    }

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
