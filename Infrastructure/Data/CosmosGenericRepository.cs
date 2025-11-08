using System.Linq;
using System.Linq.Expressions;
using Core.Entities;
using Core.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Infrastructure.Data;

/// <summary>
/// Cosmos SDK implementation of IGenericRepository using a single Container per aggregate type.
/// Notes:
/// - Uses Container.GetItemLinqQueryable for basic filtering and ordering via specifications.
/// - Falls back to in-memory projection for TResult specs to keep implementation simple.
/// - Assumes partition key property exists on entities (PartitionKey). For cross-partition queries, EnableCrossPartitionQuery=true.
/// </summary>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    private readonly Container _container;

    public GenericRepository(CosmosClient client)
    {
        // Infer container name from type, default to "Products" for Product
        var database = client.GetDatabase("AppDb"); // same as appsettings DatabaseName
        var containerName = typeof(T) == typeof(Product) ? "Products" : typeof(T).Name + "s";
        _container = database.GetContainer(containerName);
    }

    public void Add(T entity)
    {
        // No-op; SDK write is executed in SaveAllAsync via _pendingAdds
        _pendingAdds.Add(entity);
    }

    public bool Exists(int id)
    {
        // Use a simple point-read attempt with a wildcard partition (not possible). Use query instead.
        var q = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: true)
            .Where(e => e.Id == id)
            .Select(e => e.Id)
            .Take(1)
            .ToList();
        return q.Count > 0;
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        // Without knowing partition key value, use a cross-partition query by id
        var iterator = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
            .Where(e => e.Id == id)
            .ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
                return item;
        }
        return default;
    }

    public async Task<T?> GetEntityWithSpec(ISpecification<T> spec)
    {
        var query = ApplySpecification(spec);
    var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
                return item;
        }
        return default;
    }

    public async Task<TResult?> GetEntityWithSpec<TResult>(ISpecification<T, TResult> spec)
    {
        // Retrieve base entities with filtering/ordering, then project in-memory
        var list = await ListAsync((ISpecification<T>)spec);
        var first = list.FirstOrDefault();
        if (first is null) return default;
        if (spec.Select is null) return (TResult?)(object?)first;
        return spec.Select.Compile().Invoke(first);
    }

    public async Task<IReadOnlyList<T>> ListAllAsync()
    {
        var query = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
            .ToFeedIterator();

        var results = new List<T>();
        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec)
    {
    var query = ApplySpecification(spec).ToFeedIterator();
        var results = new List<T>();
        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }

    public async Task<IReadOnlyList<TResult?>> ListAsync<TResult>(ISpecification<T, TResult> spec)
    {
        // Query for base entities first (filter/order), then project and distinct if needed
        var entities = await ListAsync((ISpecification<T>)spec);
        IEnumerable<TResult?> projected;
        if (spec.Select is null)
        {
            projected = entities.Cast<TResult?>();
        }
        else
        {
            var projector = spec.Select.Compile();
            projected = entities.Select(e => (TResult?)projector(e));
        }
        if (spec.IsDistinct)
        {
            projected = projected.Distinct();
        }
        return projected.ToList();
    }

    public Task Remove(T entity)
    {
        _pendingDeletes.Add(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> SaveAllAsync()
    {
        var any = false;
        // Execute pending adds
        foreach (var e in _pendingAdds)
        {
            // Assume entities carry PartitionKey property if required
            var pk = GetPartitionKey(e);
            await _container.UpsertItemAsync(e, pk);
            any = true;
        }
        _pendingAdds.Clear();

        // Execute pending updates
        foreach (var e in _pendingUpdates)
        {
            var pk = GetPartitionKey(e);
            await _container.UpsertItemAsync(e, pk);
            any = true;
        }
        _pendingUpdates.Clear();

        // Execute pending deletes
        foreach (var e in _pendingDeletes)
        {
            var pk = GetPartitionKey(e);
            try
            {
                await _container.DeleteItemAsync<T>(e.Id.ToString(), pk);
                any = true;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // ignore
            }
        }
        _pendingDeletes.Clear();

        return any;
    }

    public void Update(T entity)
    {
        _pendingUpdates.Add(entity);
    }

    private readonly List<T> _pendingAdds = new();
    private readonly List<T> _pendingUpdates = new();
    private readonly List<T> _pendingDeletes = new();

    private static PartitionKey GetPartitionKey(T entity)
    {
        // If entity exposes PartitionKey property, use it. Otherwise empty
        var prop = typeof(T).GetProperty("PartitionKey");
        if (prop != null && prop.PropertyType == typeof(string))
        {
            var val = (string?)prop.GetValue(entity);
            if (!string.IsNullOrWhiteSpace(val))
            {
                return new PartitionKey(val);
            }
        }
        // Cross-partition upsert requires explicit PK in account with partitioning; this fallback uses none
        return PartitionKey.Null;
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        IQueryable<T> query = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false);
        if (spec.Criteria != null)
        {
            query = query.Where(spec.Criteria);
        }

        // Apply ordering if provided; otherwise a stable default
        if (spec.OrderBy != null)
        {
            query = query.OrderBy(spec.OrderBy);
        }
        else if (spec.OrderByDescending != null)
        {
            query = query.OrderByDescending(spec.OrderByDescending);
        }
        else
        {
            query = query.OrderBy(e => e.Id);
        }

        // Apply paging when requested
        if (spec.IsPagingEnabled)
        {
            query = query.Skip(spec.Skip).Take(spec.Take);
        }

        return query;
    }

    // test method passthrough not supported in SDK repo; emulate via query
    public async Task<IReadOnlyList<T>> GetEntityByBrandsAndTypes()
    {
        if (typeof(T) != typeof(Product))
            return Array.Empty<T>();

        var q = _container.GetItemLinqQueryable<Product>(allowSynchronousQueryExecution: false)
            .Where(p => (p.Brand == "Angular" || p.Brand == "React") && (p.Type == "Boots" || p.Type == "Gloves"))
            .ToFeedIterator();
        var results = new List<Product>();
        while (q.HasMoreResults)
        {
            var page = await q.ReadNextAsync();
            results.AddRange(page);
        }
        return results.Cast<T>().ToList();
    }

    public async Task<int> CountAsync(ISpecification<T> spec)
    {
        // For counts, do NOT apply ordering or paging to avoid emulator aggregate+order bug
        IQueryable<T> query = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false);
        if (spec.Criteria != null)
        {
            query = query.Where(spec.Criteria);
        }
        return await query.CountAsync();
    }
}
