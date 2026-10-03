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
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext context;

    public PaymentsController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<PaymentDTO>>> GetAll()
    {
        var payments = await context.Payments
            .Include(p => p.Invoice)
            .ToListAsync();

        return Ok(payments.Select(p => MapToDTO(p)).ToList());
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

        invoice.Customer.OutstandingBalance -= dto.PaymentAmount;

        await context.SaveChangesAsync();

        var createdPayment = await context.Payments
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.PaymentID == payment.PaymentID);

        return CreatedAtAction(nameof(GetById), new { id = payment.PaymentID }, MapToDTO(createdPayment));
    }

    [HttpGet("invoice/{invoiceId}")]
    public async Task<ActionResult<List<PaymentDTO>>> GetByInvoice(int invoiceId)
    {
        var payments = await context.Payments
            .Where(p => p.InvoiceID == invoiceId)
            .Include(p => p.Invoice)
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
            PaymentDate = p.PaymentDate,
            PaymentMode = p.PaymentMode,
            PaymentAmount = p.PaymentAmount,
            Reference = p.Reference,
            CreatedDate = p.CreatedDate
        };
    }
}
