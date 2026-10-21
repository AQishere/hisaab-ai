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
public class WarehousesController : ControllerBase
{
    private readonly AppDbContext context;

    public WarehousesController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<WarehouseDTO>>> GetAll()
    {
        var warehouses = await context.Warehouses.ToListAsync();
        return Ok(warehouses.Select(w => MapToDTO(w)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WarehouseDTO>> GetById(int id)
    {
        var warehouse = await context.Warehouses.FindAsync(id);
        if (warehouse == null)
            return NotFound();
        return Ok(MapToDTO(warehouse));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult<WarehouseDTO>> Create(CreateWarehouseDTO dto)
    {
        var warehouse = new Warehouse
        {
            WarehouseName = dto.WarehouseName,
            Location = dto.Location,
            Capacity = dto.Capacity,
            CreatedDate = DateTime.Now
        };

        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = warehouse.WarehouseID }, MapToDTO(warehouse));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult> Update(int id, UpdateWarehouseDTO dto)
    {
        var warehouse = await context.Warehouses.FindAsync(id);
        if (warehouse == null)
            return NotFound();

        warehouse.WarehouseName = dto.WarehouseName;
        warehouse.Location = dto.Location;
        warehouse.Capacity = dto.Capacity;
        warehouse.IsActive = dto.IsActive;

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult> Delete(int id)
    {
        var warehouse = await context.Warehouses.FindAsync(id);
        if (warehouse == null)
            return NotFound();

        if (await context.Inventories.AnyAsync(i => i.WarehouseID == id) ||
            await context.InboundReceipts.AnyAsync(r => r.WarehouseID == id) ||
            await context.BatchMaterials.AnyAsync(m => m.SourceWarehouseID == id))
            return BadRequest("This warehouse has stock or history. Deactivate it instead of deleting it.");
        context.Warehouses.Remove(warehouse);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private WarehouseDTO MapToDTO(Warehouse w)
    {
        return new WarehouseDTO
        {
            WarehouseID = w.WarehouseID,
            WarehouseName = w.WarehouseName,
            Location = w.Location,
            Capacity = w.Capacity,
            IsActive = w.IsActive,
            CreatedDate = w.CreatedDate
        };
    }
}
