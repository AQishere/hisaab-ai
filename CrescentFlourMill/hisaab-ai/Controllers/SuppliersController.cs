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
[Authorize(Policy = Policies.Purchasing)]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext context;

    public SuppliersController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<SupplierDTO>>> GetAll()
    {
        var suppliers = await context.Suppliers.ToListAsync();
        return Ok(suppliers.Select(s => MapToDTO(s)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplierDTO>> GetById(int id)
    {
        var supplier = await context.Suppliers.FindAsync(id);
        if (supplier == null)
            return NotFound();
        return Ok(MapToDTO(supplier));
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDTO>> Create(CreateSupplierDTO dto)
    {
        var supplier = new Supplier
        {
            SupplierName = dto.SupplierName,
            ContactPerson = dto.ContactPerson,
            PhoneNumber = dto.PhoneNumber,
            NTN = dto.NTN,
            Address = dto.Address,
            PaymentTerms = dto.PaymentTerms,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = supplier.SupplierID }, MapToDTO(supplier));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, UpdateSupplierDTO dto)
    {
        var supplier = await context.Suppliers.FindAsync(id);
        if (supplier == null)
            return NotFound();

        supplier.SupplierName = dto.SupplierName;
        supplier.ContactPerson = dto.ContactPerson;
        supplier.PhoneNumber = dto.PhoneNumber;
        supplier.NTN = dto.NTN;
        supplier.Address = dto.Address;
        supplier.PaymentTerms = dto.PaymentTerms;
        supplier.IsActive = dto.IsActive;

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var supplier = await context.Suppliers.FindAsync(id);
        if (supplier == null)
            return NotFound();

        if (await context.PurchaseOrders.AnyAsync(o => o.SupplierID == id))
            return BadRequest("This supplier has purchase orders. Deactivate it instead of deleting it.");
        context.Suppliers.Remove(supplier);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private SupplierDTO MapToDTO(Supplier s)
    {
        return new SupplierDTO
        {
            SupplierID = s.SupplierID,
            SupplierName = s.SupplierName,
            ContactPerson = s.ContactPerson,
            PhoneNumber = s.PhoneNumber,
            NTN = s.NTN,
            Address = s.Address,
            PaymentTerms = s.PaymentTerms,
            IsActive = s.IsActive,
            CreatedDate = s.CreatedDate
        };
    }
}
