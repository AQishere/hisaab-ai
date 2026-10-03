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
public class InvoicesController : ControllerBase
{
    private readonly AppDbContext context;

    public InvoicesController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<InvoiceDTO>>> GetAll()
    {
        var invoices = await context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Payments)
            .Include(i => i.Lines)
            .ToListAsync();

        return Ok(invoices.Select(i => MapToDTO(i)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InvoiceDTO>> GetById(int id)
    {
        var invoice = await context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Payments)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.InvoiceID == id);

        if (invoice == null)
            return NotFound();

        return Ok(MapToDTO(invoice));
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDTO>> Create(CreateInvoiceDTO dto)
    {
        if (!BusinessRules.Money(dto.TaxAmount, true) || dto.DueDays < 0 || dto.DueDays > 3650)
            return BadRequest("Tax must be nonnegative with at most two decimals; due days must be between 0 and 3650.");
        var order = await context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.SOLineItems).ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(so => so.SalesOrderID == dto.SalesOrderID);

        if (order == null)
            return BadRequest("Sales Order not found");

        if (order.Status != "Dispatched")
            return BadRequest("Order must be dispatched before invoicing");

        if (await context.Invoices.AnyAsync(i => i.SalesOrderID == dto.SalesOrderID))
            return BadRequest("This sales order has already been invoiced");

        var invoiceNumber = await GenerateInvoiceNumber();
        var dueDate = dto.InvoiceDate.AddDays(dto.DueDays);

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            SalesOrderID = dto.SalesOrderID,
            CustomerID = order.CustomerID,
            InvoiceDate = dto.InvoiceDate,
            TotalQuantity = order.SOLineItems.Sum(li => li.DispatchedQuantity),
            TotalAmount = order.TotalAmount,
            TaxAmount = dto.TaxAmount,
            NetAmount = order.TotalAmount + dto.TaxAmount,
            Status = "Unpaid",
            DueDate = dueDate,
            CreatedDate = DateTime.Now
        };

        foreach (var line in order.SOLineItems)
            invoice.Lines.Add(new InvoiceLine
            {
                ProductID = line.ProductID, ProductCode = line.Product!.ProductCode,
                ProductName = line.Product.ProductName, Unit = line.Product.Unit, KgPerBag = line.Product.KgPerBag,
                Quantity = line.DispatchedQuantity, RatePerUnit = line.RatePerUnit, TotalAmount = line.TotalAmount
            });

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        await BusinessRules.RecalculateCustomerBalance(context, invoice.CustomerID);

        var createdInvoice = await context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Payments)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.InvoiceID == invoice.InvoiceID);

        return CreatedAtAction(nameof(GetById), new { id = invoice.InvoiceID }, MapToDTO(createdInvoice));
    }

    private Task<string> GenerateInvoiceNumber() => BusinessRules.NextNumber(context, "INV");

    private InvoiceDTO MapToDTO(Invoice i)
    {
        var totalPaid = i.Payments?.Sum(p => p.PaymentAmount) ?? 0;
        var balanceRemaining = i.NetAmount - totalPaid;

        return new InvoiceDTO
        {
            Lines = i.Lines.Select(l => new InvoiceLineDTO { InvoiceLineID = l.InvoiceLineID, ProductCode = l.ProductCode, ProductName = l.ProductName, Unit = l.Unit, Quantity = l.Quantity, RatePerUnit = l.RatePerUnit, TotalAmount = l.TotalAmount, KgPerBag = l.KgPerBag, IsLegacySnapshot = l.IsLegacySnapshot }).ToList(),
            InvoiceID = i.InvoiceID,
            InvoiceNumber = i.InvoiceNumber,
            SalesOrderID = i.SalesOrderID,
            SONumber = i.SalesOrder?.SONumber,
            CustomerID = i.CustomerID,
            CustomerName = i.Customer?.CustomerName,
            InvoiceDate = i.InvoiceDate,
            TotalQuantity = i.TotalQuantity,
            TotalAmount = i.TotalAmount,
            TaxAmount = i.TaxAmount,
            NetAmount = i.NetAmount,
            Status = i.Status,
            DueDate = i.DueDate,
            BalanceRemaining = balanceRemaining,
            CreatedDate = i.CreatedDate
        };
    }
}
