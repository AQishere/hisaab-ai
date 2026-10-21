using Microsoft.AspNetCore.Authorization;
using Hisaab.ERP.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hisaab.ERP.Data;
using Hisaab.ERP.Models;
using Hisaab.ERP.DTOs;

namespace Hisaab.ERP.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.SalesFinance)]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext context;

    public PaymentsController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] ListQuery list)
    {
        IQueryable<Payment> query = context.Payments
            .Include(p => p.Invoice).ThenInclude(i => i!.Customer);
        if (list.Pattern is { } like)
            query = query.Where(p => EF.Functions.ILike(p.Invoice!.InvoiceNumber, like, @"\") ||
                EF.Functions.ILike(p.Invoice.Customer!.CustomerName, like, @"\") || EF.Functions.ILike(p.Reference!, like, @"\") ||
                EF.Functions.ILike(p.PaymentMode, like, @"\"));

        if (!list.Paged)
            return Ok((await query.ToListAsync()).Select(p => MapToDTO(p)).ToList());
        var total = await query.SumAsync(p => (decimal?)p.PaymentAmount) ?? 0;
        var page = await Paging.PageAsync(query.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.PaymentID), list);
        return Ok(Paging.Result(page, p => MapToDTO(p), total));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentDTO>> GetById(int id)
    {
        var payment = await context.Payments
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.PaymentID == id);

        if (payment == null)
            return NotFound();

        return Ok(MapToDTO(payment));
    }

    [HttpPost]
    public async Task<ActionResult<PaymentDTO>> Create(CreatePaymentDTO dto)
    {
        var invoice = await context.Invoices
            .Include(i => i.Payments)
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.InvoiceID == dto.InvoiceID);

        if (invoice == null)
            return BadRequest("Invoice not found");

        if (!BusinessRules.Money(dto.PaymentAmount))
            return BadRequest("Payment amount must be positive and have at most two decimal places");

        var totalPaid = invoice.Payments.Sum(p => p.PaymentAmount);
        if (totalPaid + dto.PaymentAmount > invoice.NetAmount)
            return BadRequest("Payment amount exceeds invoice amount");

        var payment = new Payment
        {
            InvoiceID = dto.InvoiceID,
            PaymentDate = dto.PaymentDate,
            PaymentMode = dto.PaymentMode,
            PaymentAmount = dto.PaymentAmount,
            Reference = dto.Reference,
            CreatedDate = DateTime.Now
        };

        context.Payments.Add(payment);

        totalPaid += dto.PaymentAmount;
        if (totalPaid >= invoice.NetAmount)
        {
            invoice.Status = "Paid";
        }
        else
        {
            invoice.Status = "PartiallyPaid";
        }


        await context.SaveChangesAsync();
        await BusinessRules.RecalculateCustomerBalance(context, invoice.CustomerID);

        var createdPayment = await context.Payments
            .Include(p => p.Invoice)
            .SingleAsync(p => p.PaymentID == payment.PaymentID);

        return CreatedAtAction(nameof(GetById), new { id = payment.PaymentID }, MapToDTO(createdPayment));
    }

    [HttpGet("invoice/{invoiceId}")]
    public async Task<ActionResult<List<PaymentDTO>>> GetByInvoice(int invoiceId)
    {
        var payments = await context.Payments
            .Where(p => p.InvoiceID == invoiceId)
            .Include(p => p.Invoice).ThenInclude(i => i!.Customer)
            .ToListAsync();

        return Ok(payments.Select(p => MapToDTO(p)).ToList());
    }

    private PaymentDTO MapToDTO(Payment p)
    {
        return new PaymentDTO
        {
            PaymentID = p.PaymentID,
            InvoiceID = p.InvoiceID,
            InvoiceNumber = p.Invoice?.InvoiceNumber,
            CustomerName = p.Invoice?.Customer?.CustomerName,
            PaymentDate = p.PaymentDate,
            PaymentMode = p.PaymentMode,
            PaymentAmount = p.PaymentAmount,
            Reference = p.Reference,
            CreatedDate = p.CreatedDate
        };
    }
}
