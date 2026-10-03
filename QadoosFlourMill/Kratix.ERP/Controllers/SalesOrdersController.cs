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
        var (lines, error) = await BuildLines(dto.LineItems);
        if (error != null)
            return BadRequest(error);
        var customer = await context.Customers.FindAsync(dto.CustomerID);
        if (customer == null || !customer.IsActive)
            return BadRequest("Customer not found");

        var soNumber = await GenerateSONumber();

        var order = new SalesOrder
        {
            SONumber = soNumber,
            CustomerID = dto.CustomerID,
            SODate = dto.SODate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Status = "Quotation",
            CreatedBy = StockLedger.UserId(User),
            CreatedDate = DateTime.Now
        };

        foreach (var line in lines!)
            order.SOLineItems.Add(line);
        order.TotalAmount = lines!.Sum(l => l.TotalAmount);
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

        if (!BusinessRules.Quantity(dto.DispatchedQuantity))
            return BadRequest("Dispatch quantity must be greater than zero");

        if (dto.DispatchedQuantity > lineItem.Quantity - lineItem.DispatchedQuantity)
            return BadRequest("Cannot dispatch more than ordered quantity");

        if (order.Status is "Cancelled" or "Closed" or "Dispatched") return BadRequest("This order cannot be dispatched.");
        if (string.IsNullOrWhiteSpace(dto.TruckNumber) || string.IsNullOrWhiteSpace(dto.DriverName)) return BadRequest("Truck and driver are required.");
        var product = await context.Products.FindAsync(lineItem.ProductID);
        if (product!.Unit == "bag" && dto.DispatchedQuantity != decimal.Truncate(dto.DispatchedQuantity)) return BadRequest("Dispatch a whole number of bags.");
        var stockLots = await context.Inventories
            .Where(inv => inv.ProductID == lineItem.ProductID && inv.StockForm == "Standard" && inv.QuantityAvailable > 0)
            .OrderBy(inv => inv.ReceivedAt)
            .ThenBy(inv => inv.InventoryID)
            .ToListAsync();
        if (stockLots.Sum(inv => inv.QuantityAvailable) < dto.DispatchedQuantity)
            return BadRequest("Insufficient finished-goods stock to dispatch this quantity");

        var dispatch = new Dispatch { SalesOrderID = id, TruckNumber = dto.TruckNumber.Trim(), DriverName = dto.DriverName.Trim(), RecordedBy = StockLedger.UserId(User) };
        context.Dispatches.Add(dispatch);
        await context.SaveChangesAsync();
        var remainingToDispatch = dto.DispatchedQuantity;
        foreach (var stockLot in stockLots)
        {
            var used = Math.Min(stockLot.QuantityAvailable, remainingToDispatch);
            StockLedger.Change(context, stockLot, -used, product.Unit, "Dispatch", $"DISP-{dispatch.DispatchID}", User);
            dispatch.Lines.Add(new DispatchLine { SOLineItemID = lineItemId, Inventory = stockLot, Quantity = used, Unit = product.Unit });
            remainingToDispatch -= used;
            if (remainingToDispatch == 0)
                break;
        }

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

    // Editable until the first truck leaves; after that the order is history.
    [HttpPut("{id}/details")]
    public async Task<ActionResult<SalesOrderDTO>> UpdateDetails(int id, CreateSalesOrderDTO dto)
    {
        var order = await context.SalesOrders.Include(so => so.SOLineItems).FirstOrDefaultAsync(so => so.SalesOrderID == id);
        if (order == null)
            return NotFound();

        if (order.Status is not ("Quotation" or "Confirmed") || order.SOLineItems.Any(li => li.DispatchedQuantity > 0) ||
            await context.Dispatches.AnyAsync(d => d.SalesOrderID == id))
            return BadRequest("Only orders with nothing dispatched can be edited.");

        var (lines, error) = await BuildLines(dto.LineItems);
        if (error != null)
            return BadRequest(error);

        var customer = await context.Customers.FindAsync(dto.CustomerID);
        if (customer == null || !customer.IsActive)
            return BadRequest("Customer not found");

        order.CustomerID = dto.CustomerID;
        order.SODate = dto.SODate;
        order.ExpectedDeliveryDate = dto.ExpectedDeliveryDate;
        context.SOLineItems.RemoveRange(order.SOLineItems);
        foreach (var line in lines!)
            order.SOLineItems.Add(line);
        order.TotalAmount = lines!.Sum(l => l.TotalAmount);
        await context.SaveChangesAsync();

        var updated = await context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.SOLineItems).ThenInclude(li => li.Product)
            .FirstAsync(so => so.SalesOrderID == id);
        return Ok(MapToDTO(updated));
    }

    [HttpPut("{id}/cancel")]
    public async Task<ActionResult> Cancel(int id, ReasonDTO dto)
    {
        var order = await context.SalesOrders.Include(so => so.SOLineItems).FirstOrDefaultAsync(so => so.SalesOrderID == id);
        if (order == null)
            return NotFound();

        if (order.Status is not ("Quotation" or "Confirmed") || order.SOLineItems.Any(li => li.DispatchedQuantity > 0))
            return BadRequest("Only an order with nothing dispatched can be cancelled; close it short instead.");

        var reason = BusinessRules.Reason(dto.Reason);
        if (reason == null)
            return BadRequest("Give a reason of up to 500 characters");

        order.Status = "Cancelled";
        order.ClosedReason = reason;
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/close")]
    public async Task<ActionResult> CloseShort(int id, ReasonDTO dto)
    {
        var order = await context.SalesOrders.FindAsync(id);
        if (order == null)
            return NotFound();

        if (order.Status != "PartiallyDispatched")
            return BadRequest("Only a partly dispatched order can be closed short.");

        var reason = BusinessRules.Reason(dto.Reason);
        if (reason == null)
            return BadRequest("Give a reason of up to 500 characters");

        order.Status = "Closed";
        order.ClosedReason = reason;
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/dispatches")]
    public async Task<ActionResult> DispatchHistory(int id) => Ok(await context.Dispatches.Where(d => d.SalesOrderID == id)
        .OrderByDescending(d => d.DispatchedAt).Select(d => new { d.DispatchID, d.TruckNumber, d.DriverName, d.DispatchedAt,
            Lines = d.Lines.Select(l => new { l.DispatchLineID, l.SOLineItemID, l.Quantity, l.Unit,
                ProductName = l.Inventory.Product!.ProductName, l.Inventory.LotNumber, Warehouse = l.Inventory.Warehouse!.WarehouseName }) }).ToListAsync());

    private Task<string> GenerateSONumber() => BusinessRules.NextNumber(context, "SO");

    private async Task<(List<SOLineItem>? Lines, string? Error)> BuildLines(List<CreateSOLineItemDTO>? items)
    {
        if (items == null || items.Count == 0 || items.Any(i => !BusinessRules.Quantity(i.Quantity) || !BusinessRules.Money(i.RatePerUnit) || !BusinessRules.Money(BusinessRules.Amount(i.Quantity, i.RatePerUnit), true)))
            return (null, "Add at least one line with positive quantities (up to 3 decimals) and rates (up to 2 decimals).");

        var lines = new List<SOLineItem>();
        foreach (var item in items)
        {
            var product = await context.Products.FindAsync(item.ProductID);
            if (product == null)
                return (null, $"Product {item.ProductID} not found");

            if (product.Category != "Finished Good" && product.Category != "By-product")
                return (null, "Only finished goods and by-products can be sold.");
            if (product.Unit == "bag" && item.Quantity != decimal.Truncate(item.Quantity))
                return (null, "Order a whole number of bags.");
            lines.Add(new SOLineItem
            {
                ProductID = item.ProductID,
                Quantity = item.Quantity,
                RatePerUnit = item.RatePerUnit,
                TotalAmount = BusinessRules.Amount(item.Quantity, item.RatePerUnit),
                DispatchedQuantity = 0,
                DispatchStatus = "NotDispatched"
            });
        }
        if (!BusinessRules.Money(lines.Sum(l => l.TotalAmount), true))
            return (null, "Order total is too large.");
        return (lines, null);
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
            ClosedReason = so.ClosedReason,
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
