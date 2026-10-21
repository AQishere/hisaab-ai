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
        if (!new[] { "Raw Material", "Finished Good", "By-product", "Packaging", "Waste" }.Contains(dto.Category) ||
            !new[] { "kg", "bag", "piece" }.Contains(dto.Unit) ||
            ((dto.Category == "Raw Material" || dto.Category == "Waste") && dto.Unit != "kg") ||
            (dto.Category == "Packaging" && dto.Unit != "piece") ||
            ((dto.Category == "Finished Good" || dto.Category == "By-product") && dto.Unit == "piece") ||
            (dto.Unit == "bag" && (!dto.KgPerBag.HasValue || !BusinessRules.Quantity(dto.KgPerBag.Value))))
            return BadRequest("Use kg for raw material/waste, pieces for packaging, and kg or bags with a positive bag size for finished goods.");

        var product = new Product
        {
            ProductCode = dto.ProductCode,
            ProductName = dto.ProductName,
            Category = dto.Category,
            Unit = dto.Unit,
            KgPerBag = dto.Unit == "bag" ? dto.KgPerBag : null,
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

        if (!new[] { "Raw Material", "Finished Good", "By-product", "Packaging", "Waste" }.Contains(dto.Category) ||
            !new[] { "kg", "bag", "piece" }.Contains(dto.Unit) ||
            ((dto.Category == "Raw Material" || dto.Category == "Waste") && dto.Unit != "kg") ||
            (dto.Category == "Packaging" && dto.Unit != "piece") ||
            ((dto.Category == "Finished Good" || dto.Category == "By-product") && dto.Unit == "piece") ||
            (dto.Unit == "bag" && (!dto.KgPerBag.HasValue || !BusinessRules.Quantity(dto.KgPerBag.Value))))
            return BadRequest("Use kg for raw material/waste, pieces for packaging, and kg or bags with a positive bag size for finished goods.");

        if ((product.Unit != dto.Unit || product.Category != dto.Category || product.KgPerBag != (dto.Unit == "bag" ? dto.KgPerBag : null)) && await IsUsed(id))
            return BadRequest("This product has transactions. Create a separate product for a different unit, category, or bag size.");
        product.ProductCode = dto.ProductCode;
        product.ProductName = dto.ProductName;
        product.Category = dto.Category;
        product.Unit = dto.Unit;
        product.KgPerBag = dto.Unit == "bag" ? dto.KgPerBag : null;
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

        if (await IsUsed(id)) return BadRequest("Products used in stock or transactions cannot be deleted.");
        context.Products.Remove(product);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> IsUsed(int id) =>
        await context.Inventories.AnyAsync(x => x.ProductID == id) ||
        await context.POLineItems.AnyAsync(x => x.ProductID == id) ||
        await context.SOLineItems.AnyAsync(x => x.ProductID == id) ||
        await context.BatchMaterials.AnyAsync(x => x.ProductID == id) ||
        await context.InvoiceLines.AnyAsync(x => x.ProductID == id);

    private ProductDTO MapToDTO(Product p)
    {
        return new ProductDTO
        {
            ProductID = p.ProductID,
            ProductCode = p.ProductCode,
            ProductName = p.ProductName,
            Category = p.Category,
            Unit = p.Unit,
            KgPerBag = p.KgPerBag,
            HSNCode = p.HSNCode,
            CreatedDate = p.CreatedDate
        };
    }
}
