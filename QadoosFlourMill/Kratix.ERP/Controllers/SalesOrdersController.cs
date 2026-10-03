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
[Authorize(Policy = Policies.SalesFinance)]
public class SalesOrdersController : ControllerBase
{
    private readonly AppDbContext context;

    public SalesOrdersController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<SalesOrderDTO>>> GetAll()
    {
        var orders = await context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.SOLineItems)
            .ThenInclude(li => li.Product)
            .ToListAsync();

        return Ok(orders.Select(o => MapToDTO(o)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SalesOrderDTO>> GetById(int id)
    {
        var order = await context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.SOLineItems)
            .ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(so => so.SalesOrderID == id);

        if (order == null)
            return NotFound();

        return Ok(MapToDTO(order));
    }

    [HttpPost]
    public async Task<ActionResult<SalesOrderDTO>> Create(CreateSalesOrderDTO dto)
    {
        var customer = await context.Customers.FindAsync(dto.CustomerID);
        if (customer == null)
            return BadRequest("Customer not found");

        var soNumber = await GenerateSONumber();

        var order = new SalesOrder
        {
            SONumber = soNumber,
            CustomerID = dto.CustomerID,
            SODate = dto.SODate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Status = "Quotation",
            CreatedDate = DateTime.Now
        };

        decimal totalAmount = 0;
        foreach (var item in dto.LineItems)
        {
            var product = await context.Products.FindAsync(item.ProductID);
            if (product == null)
                return BadRequest($"Product {item.ProductID} not found");

            var lineItem = new SOLineItem
            {
                ProductID = item.ProductID,
                Quantity = item.Quantity,
                RatePerUnit = item.RatePerUnit,
                TotalAmount = item.Quantity * item.RatePerUnit,
                DispatchedQuantity = 0,
                DispatchStatus = "NotDispatched"
            };

            order.SOLineItems.Add(lineItem);
            totalAmount += lineItem.TotalAmount;
        }

        order.TotalAmount = totalAmount;
        context.SalesOrders.Add(order);
        await context.SaveChangesAsync();

        var createdOrder = await context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.SOLineItems)
            .ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(so => so.SalesOrderID == order.SalesOrderID);

        return CreatedAtAction(nameof(GetById), new { id = order.SalesOrderID }, MapToDTO(createdOrder));
    }

    [HttpPut("{id}/dispatch/{lineItemId}")]
    public async Task<ActionResult> DispatchLineItem(int id, int lineItemId, DispatchSOLineItemDTO dto)
    {
        var order = await context.SalesOrders
            .Include(so => so.SOLineItems)
            .FirstOrDefaultAsync(so => so.SalesOrderID == id);
        if (order == null)
            return NotFound();

        var lineItem = order.SOLineItems.FirstOrDefault(li => li.LineItemID == lineItemId);
        if (lineItem == null)
            return NotFound();

        if (dto.DispatchedQuantity <= 0)
            return BadRequest("Dispatch quantity must be greater than zero");

        if (dto.DispatchedQuantity > lineItem.Quantity - lineItem.DispatchedQuantity)
            return BadRequest("Cannot dispatch more than ordered quantity");

        lineItem.DispatchedQuantity += dto.DispatchedQuantity;
        lineItem.TruckNumber = dto.TruckNumber;
        lineItem.DriverName = dto.DriverName;
        lineItem.DispatchDate = DateTime.Now;

        if (lineItem.DispatchedQuantity >= lineItem.Quantity)
            lineItem.DispatchStatus = "Dispatched";
        else
            lineItem.DispatchStatus = "PartiallyDispatched";

        var allDispatched = order.SOLineItems.All(li => li.DispatchStatus == "Dispatched");
        if (allDispatched)
            order.Status = "Dispatched";
        else if (order.SOLineItems.Any(li => li.DispatchStatus != "NotDispatched"))
            order.Status = "PartiallyDispatched";

        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GenerateSONumber()
    {
        var count = await context.SalesOrders.CountAsync();
        var year = DateTime.Now.Year;
        return $"SO-{year}-{count + 1:D3}";
    }

    private SalesOrderDTO MapToDTO(SalesOrder so)
    {
        return new SalesOrderDTO
        {
            SalesOrderID = so.SalesOrderID,
            SONumber = so.SONumber,
            CustomerID = so.CustomerID,
            CustomerName = so.Customer?.CustomerName,
            SODate = so.SODate,
            ExpectedDeliveryDate = so.ExpectedDeliveryDate,
            Status = so.Status,
            TotalAmount = so.TotalAmount,
            LineItems = so.SOLineItems?.Select(li => new SOLineItemDTO
            {
                LineItemID = li.LineItemID,
                ProductID = li.ProductID,
                ProductName = li.Product?.ProductName,
                Quantity = li.Quantity,
                RatePerUnit = li.RatePerUnit,
                TotalAmount = li.TotalAmount,
                DispatchedQuantity = li.DispatchedQuantity,
                DispatchStatus = li.DispatchStatus,
                TruckNumber = li.TruckNumber,
                DriverName = li.DriverName,
                DispatchDate = li.DispatchDate
            }).ToList(),
            CreatedDate = so.CreatedDate
        };
    }
}
