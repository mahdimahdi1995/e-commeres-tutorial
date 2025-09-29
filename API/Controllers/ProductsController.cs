using Core.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly StoreContext context;

    public ProductsController(StoreContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
        return await context.Products.AsNoTracking().ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        return product;
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        // if the product has no id, then assign a new random id
        if (product.Id == 0)
        {
            var rand = new Random();
            product.Id = rand.Next(1, int.MaxValue);
        }
        // Ensure PartitionKey is set; default to Brand if provided, otherwise a static value
        if (string.IsNullOrWhiteSpace(product.PartitionKey))
        {
            product.PartitionKey = string.IsNullOrWhiteSpace(product.Brand) ? "product" : product.Brand;
        }
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product;
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateProduct(int id, Product product)
    {
        if (product.Id != id)
            return BadRequest("Mismatched id");

        // Load the existing doc (avoid Any/Exists; use FirstOrDefaultAsync)
        var existing = await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (existing is null) return NotFound();

        // For Cosmos, avoid changing partition key (PartitionKey)
        if (!string.Equals(existing.PartitionKey, product.PartitionKey, StringComparison.Ordinal))
            return BadRequest("Changing PartitionKey is not supported.");

        context.Entry(product).State = EntityState.Modified;
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var product = await context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        context.Products.Remove(product);
        await context.SaveChangesAsync();
        return NoContent();
    }

    // Removed ProductExists to avoid translation to EXISTS which is not supported by emulator vNext
}
