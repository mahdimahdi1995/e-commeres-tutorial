using Core.Entities;
using Infrastructure.Data;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(IProductRepository repo) : ControllerBase
{

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetProducts(string? brand = null, string? type = null, string? sort = null)
    {
        return Ok(await repo.GetProductsAsync(brand, type, sort));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await repo.GetProductByIdAsync(id);
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
        repo.AddProduct(product);
        if (await repo.SaveChangesAsync())
        {
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        return BadRequest("Failed to create product.");
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateProduct(int id, Product product)
    {
        if (product.Id != id)
            return BadRequest("Mismatched id");

        // Load the existing doc (avoid Any/Exists; use FirstOrDefaultAsync)
        var existing = await repo.GetProductByIdAsync(id);
        if (existing is null) return NotFound();

        // For Cosmos, avoid changing partition key (PartitionKey)
        if (!string.Equals(existing.PartitionKey, product.PartitionKey, StringComparison.Ordinal))
            return BadRequest("Changing PartitionKey is not supported.");

        repo.UpdateProduct(product);
        if (await repo.SaveChangesAsync())
        {
            return NoContent();
        }
        return BadRequest("Failed to update product.");
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var product = await repo.GetProductByIdAsync(id);
        if (product == null) return NotFound();
        await repo.DeleteProductAsync(id);
        if (await repo.SaveChangesAsync())
        {
            return NoContent();
        }
        return BadRequest("Failed to delete product.");
    }

    [HttpGet("brands")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetBrands()
    {
        var brands = await repo.GetBrandsAsync();
        return Ok(brands);
    }

    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetTypes()
    {
        var types = await repo.GetTypesAsync();
        return Ok(types);
    }
}
