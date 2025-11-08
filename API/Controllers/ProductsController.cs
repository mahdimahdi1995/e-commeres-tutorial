using Core.Entities;
using Infrastructure.Data;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Specifications;
using API.RequestHelpers;

namespace API.Controllers;

public class ProductsController(IGenericRepository<Product> repo) : BaseApiController
{

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetProducts(
        [FromQuery] ProductSpecParams specParams)
    {
        var spec = new ProductSpecification(specParams);
        
        return await CreatePagedResult(repo, spec, specParams.PageIndex, specParams.PageSize);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await repo.GetByIdAsync(id);
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
        repo.Add(product);
        if (await repo.SaveAllAsync())
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
        var existing = await repo.GetByIdAsync(id);
        if (existing is null) return NotFound();

        // For Cosmos, avoid changing partition key (PartitionKey)
        if (!string.Equals(existing.PartitionKey, product.PartitionKey, StringComparison.Ordinal))
            return BadRequest("Changing PartitionKey is not supported.");

        repo.Update(product);
        if (await repo.SaveAllAsync())
        {
            return NoContent();
        }
        return BadRequest("Failed to update product.");
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var product = await repo.GetByIdAsync(id);
        if (product == null) return NotFound();
        await repo.Remove(product);
        if (await repo.SaveAllAsync())
        {
            return NoContent();
        }
        return BadRequest("Failed to delete product.");
    }

    [HttpGet("brands")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetBrands()
    {
        var spec = new BrandListSpecification();
        var brands = await repo.ListAsync(spec);
        return Ok(brands);
    }

    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetTypes()
    {
        var spec = new TypeListSpecification();
        var types = await repo.ListAsync(spec);
        return Ok(types);
    }
}
