using System;
using System.Text.Json;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class StoreContextSeed
{
    public static async Task SeedAsync(StoreContext context)
    {
        // Avoid EXISTS translation (not implemented in emulator). Read 1 id instead.
        var anyIds = await context.Products.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .Take(1)
            .ToListAsync();

        if (anyIds.Count == 0)
        {
            var productsData = await File.ReadAllTextAsync("../Infrastructure/Data/SeedData/products.json");
            var products = JsonSerializer.Deserialize<List<Product>>(productsData);
            if (products == null) return;

            // Ensure unique Ids and PartitionKey for Cosmos
            var nextId = 1;
            foreach (var p in products)
            {
                // Assign unique Ids when not provided in seed data
                if (p.Id == 0)
                {
                    p.Id = nextId++;
                }

                // Default PartitionKey to Brand if present, otherwise "product"
                if (string.IsNullOrWhiteSpace(p.PartitionKey))
                {
                    p.PartitionKey = string.IsNullOrWhiteSpace(p.PartitionKey) ? "product" : p.PartitionKey;
                }
            }

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }
    }
}
