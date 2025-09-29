using System.Dynamic;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class StoreContext(DbContextOptions options) : DbContext(options)
{
    public string ProductsContainerName { get; } = "Products";
    public DbSet<Product> Products => Set<Product>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultContainer(ProductsContainerName);

        modelBuilder.Entity<Product>(eb =>
        {
            eb.ToContainer(ProductsContainerName);

            // Choose a partition key you commonly filter by.
            // Here we use Brand (you can switch to Type or add a dedicated PartitionKey property).
            eb.HasPartitionKey(p => p.PartitionKey);

            // Cosmos adds a discriminator by default; this removes it for clean JSON.
            eb.HasNoDiscriminator();

            // Optional: Cosmos stores numbers as JSON numbers (double).
            // Converting decimal->double avoids precision/serialization quirks in older providers.
            eb.Property(p => p.Price).HasConversion<double>();

            // Create a simple index (logical in EF; Cosmos indexes by default, so this is mostly for LINQ clarity)
            eb.HasKey(p => p.Id);
        });
    }
}
