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
            .FirstOrDefaultAsync(i => i.InvoiceID == id);

        if (invoice == null)
            return NotFound();

        return Ok(MapToDTO(invoice));
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDTO>> Create(CreateInvoiceDTO dto)
    {
        var order = await context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.SOLineItems)
            .FirstOrDefaultAsync(so => so.SalesOrderID == dto.SalesOrderID);

        if (order == null)
            return BadRequest("Sales Order not found");

        if (order.Status != "Dispatched")
            return BadRequest("Order must be dispatched before invoicing");

        if (await context.Invoices.AnyAsync(i => i.SalesOrderID == dto.SalesOrderID))
            return BadRequest("This sales order has already been invoiced");

        var invoiceNumber = await GenerateInvoiceNumber();
        var dueDate = dto.InvoiceDate.AddDays(dto.DueDays > 0 ? dto.DueDays : 15);

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

        order.Customer.OutstandingBalance += invoice.NetAmount;

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var createdInvoice = await context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.InvoiceID == invoice.InvoiceID);

        return CreatedAtAction(nameof(GetById), new { id = invoice.InvoiceID }, MapToDTO(createdInvoice));
    }

    private async Task<string> GenerateInvoiceNumber()
    {
        var count = await context.Invoices.CountAsync();
        var year = DateTime.Now.Year;
        return $"INV-{year}-{count + 1:D3}";
    }

    private InvoiceDTO MapToDTO(Invoice i)
    {
        var totalPaid = i.Payments?.Sum(p => p.PaymentAmount) ?? 0;
        var balanceRemaining = i.NetAmount - totalPaid;

        return new InvoiceDTO
        {
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
