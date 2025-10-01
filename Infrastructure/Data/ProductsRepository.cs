using System;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class ProductsRepository(StoreContext context) : IProductRepository
{
    public void AddProduct(Product product)
    {
        context.Products.Add(product);
    }

    public async Task DeleteProductAsync(int id)
    {
        var product = await context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product != null)
        {
            context.Products.Remove(product);
        }
    }

    public async Task<IReadOnlyList<string>> GetBrandsAsync()
    {
        return await context.Products.AsNoTracking()
            .Select(p => p.Brand)
            .Distinct()
            .ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IReadOnlyList<Product>> GetProductsAsync(string? brand, string? type, string? sort)
    {
        var query = context.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(brand))
        {
            query = query.Where(p => p.Brand == brand);
        }
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(p => p.Type == type);
        }

        query = sort?.ToLower() switch
        {
            "priceasc"  => query.OrderBy(p => p.Price),
            "pricedesc" => query.OrderByDescending(p => p.Price),
            _           => query.OrderBy(p => p.Name) // default sort
        };

        return await query.AsNoTracking().ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetTypesAsync()
    {
        return await context.Products.AsNoTracking()
            .Select(p => p.Type)
            .Distinct()
            .ToListAsync();
    }

    public async Task<bool> ProductExistsAsync(int id)
    {
        // Avoid Any/EXISTS translation in Cosmos emulator
        var any = await context.Products.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.Id)
            .Take(1)
            .ToListAsync();
        return any.Count > 0;
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await context.SaveChangesAsync() > 0;
    }

    public void UpdateProduct(Product product)
    {
        // Attach the incoming detached entity and mark it Modified
        context.Attach(product);
        context.Entry(product).State = EntityState.Modified;
    }
}
