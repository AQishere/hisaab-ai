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
public class CustomersController : ControllerBase
{
    private readonly AppDbContext context;

    public CustomersController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CustomerDTO>>> GetAll()
    {
        var customers = await context.Customers.ToListAsync();
        return Ok(customers.Select(c => MapToDTO(c)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDTO>> GetById(int id)
    {
        var customer = await context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();
        return Ok(MapToDTO(customer));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDTO>> Create(CreateCustomerDTO dto)
    {
        var customer = new Customer
        {
            CustomerName = dto.CustomerName,
            Type = dto.Type,
            ContactPerson = dto.ContactPerson,
            PhoneNumber = dto.PhoneNumber,
            Address = dto.Address,
            CreditLimit = dto.CreditLimit,
            OutstandingBalance = 0,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.CustomerID }, MapToDTO(customer));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, UpdateCustomerDTO dto)
    {
        var customer = await context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();

        customer.CustomerName = dto.CustomerName;
        customer.Type = dto.Type;
        customer.ContactPerson = dto.ContactPerson;
        customer.PhoneNumber = dto.PhoneNumber;
        customer.Address = dto.Address;
        customer.CreditLimit = dto.CreditLimit;
        customer.IsActive = dto.IsActive;

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var customer = await context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();

        if (await context.SalesOrders.AnyAsync(o => o.CustomerID == id) || await context.Invoices.AnyAsync(i => i.CustomerID == id))
            return BadRequest("This customer has transactions. Deactivate it instead of deleting it.");
        context.Customers.Remove(customer);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private CustomerDTO MapToDTO(Customer c)
    {
        return new CustomerDTO
        {
            CustomerID = c.CustomerID,
            CustomerName = c.CustomerName,
            Type = c.Type,
            ContactPerson = c.ContactPerson,
            PhoneNumber = c.PhoneNumber,
            Address = c.Address,
            CreditLimit = c.CreditLimit,
            OutstandingBalance = c.OutstandingBalance,
            IsActive = c.IsActive,
            CreatedDate = c.CreatedDate
        };
    }
}
