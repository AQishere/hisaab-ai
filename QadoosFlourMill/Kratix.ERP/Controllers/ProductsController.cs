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
public class ProductsController : ControllerBase
{
    private readonly AppDbContext context;

    public ProductsController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductDTO>>> GetAll()
    {
        var products = await context.Products.ToListAsync();
        return Ok(products.Select(p => MapToDTO(p)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDTO>> GetById(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product == null)
            return NotFound();
        return Ok(MapToDTO(product));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult<ProductDTO>> Create(CreateProductDTO dto)
    {
        var product = new Product
        {
            ProductCode = dto.ProductCode,
            ProductName = dto.ProductName,
            Category = dto.Category,
            Unit = dto.Unit,
            HSNCode = dto.HSNCode,
            CreatedDate = DateTime.Now
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.ProductID }, MapToDTO(product));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult> Update(int id, UpdateProductDTO dto)
    {
        var product = await context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        product.ProductCode = dto.ProductCode;
        product.ProductName = dto.ProductName;
        product.Category = dto.Category;
        product.Unit = dto.Unit;
        product.HSNCode = dto.HSNCode;

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Purchasing)]
    public async Task<ActionResult> Delete(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        context.Products.Remove(product);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private ProductDTO MapToDTO(Product p)
    {
        return new ProductDTO
        {
            ProductID = p.ProductID,
            ProductCode = p.ProductCode,
            ProductName = p.ProductName,
            Category = p.Category,
            Unit = p.Unit,
            HSNCode = p.HSNCode,
            CreatedDate = p.CreatedDate
        };
    }
}
