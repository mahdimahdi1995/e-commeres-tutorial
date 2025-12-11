using Infrastructure.Data;
using Core.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Core.Interfaces;
using API.MiddleWare;
using StackExchange.Redis;
using Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<StoreContext>(options =>
{
    var c = builder.Configuration.GetSection("Cosmos");

    options.UseCosmos(
        c["AccountEndpoint"]!,   // https://localhost:8081
        c["AccountKey"]!,        // emulator default key
        c["DatabaseName"]!,
        cosmos =>
        {
            cosmos.ConnectionMode(ConnectionMode.Gateway);
            cosmos.LimitToEndpoint();

            // Accept self-signed emulator cert (DEV ONLY)
            cosmos.HttpClientFactory(() =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                return new HttpClient(handler);
            });
        });
});

// Register CosmosClient for SDK-based repository
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("Cosmos");
    var endpoint = cfg["AccountEndpoint"]!;
    var key = cfg["AccountKey"]!;
    var clientOptions = new CosmosClientOptions
    {
        ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway,
        LimitToEndpoint = true,
        HttpClientFactory = () =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            return new HttpClient(handler);
        }
    };
    return new CosmosClient(endpoint, key, clientOptions);
});

builder.Services.AddScoped<IProductRepository, ProductsRepository>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(CosmosGenericRepository<>));
builder.Services.AddCors();
builder.Services.AddSingleton<IConnectionMultiplexer>(config =>
{
    var connString = builder.Configuration.GetConnectionString("Redis")
        ?? throw new Exception("Cannot get Redis connection string from configuration.");
    var configuration = ConfigurationOptions.Parse(connString, true);
    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddScoped<ICartService, CartService>();

// builder.Services.AddDbContext<StoreContext>(opt =>
// {
//     opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
// });


var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseMiddleware<ExceptionMiddleWare>();

using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<StoreContext>();
    await ctx.Database.EnsureCreatedAsync();
}

#region minimal-api
// Minimal API endpoints to quickly test the DB connection (Cosmos via EF Core)
// app.MapGet("/products", async (StoreContext db, CancellationToken ct) =>
// {
//     var items = await db.Products.AsNoTracking().ToListAsync(ct);
//     return Results.Ok(items);
// });

// app.MapGet("/products/{id:int}", async (int id, StoreContext db, CancellationToken ct) =>
// {
//     var item = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
//     return item is not null ? Results.Ok(item) : Results.NotFound();
// });

// app.MapPost("/products", async (Product product, StoreContext db, CancellationToken ct) =>
// {
//     if (string.IsNullOrWhiteSpace(product.Brand))
//         return Results.BadRequest("Brand is required (used as partition key).");

//     // With Cosmos provider, keys are client-generated; ensure caller sets a unique Id.
//     if (product.Id == 0)
//         return Results.BadRequest("Please provide a non-zero Id for the product.");

//     db.Products.Add(product);
//     await db.SaveChangesAsync(ct);
//     return Results.Created($"/products/{product.Id}", product);
// });

// app.MapPut("/products/{id:int}", async (int id, Product input, StoreContext db, CancellationToken ct) =>
// {
//     var existing = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
//     if (existing is null) return Results.NotFound();

//     // Cosmos cannot change partition key value for an existing item
//     if (!string.Equals(existing.Brand, input.Brand, StringComparison.Ordinal))
//         return Results.BadRequest("Changing Brand (partition key) is not supported.");

//     existing.Name = input.Name;
//     existing.Description = input.Description;
//     existing.Price = input.Price;
//     existing.PictureUrl = input.PictureUrl;
//     existing.Type = input.Type;
//     existing.QuantityInStock = input.QuantityInStock;

//     await db.SaveChangesAsync(ct);
//     return Results.NoContent();
// });

// app.MapDelete("/products/{id:int}", async (int id, StoreContext db, CancellationToken ct) =>
// {
//     var existing = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
//     if (existing is null) return Results.NotFound();

//     db.Products.Remove(existing);
//     await db.SaveChangesAsync(ct);
//     return Results.NoContent();
// });
#endregion

app.UseCors(x => x
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithOrigins("http://localhost:4200", "https://localhost:4200"));
// Keep attribute-routed controllers too (e.g., WeatherForecast)
app.MapControllers();

try
{
    // Seed data
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StoreContext>();
    await StoreContextSeed.SeedAsync(context);
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    throw;
}

app.Run();
