using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class GenericRepository<T>(StoreContext context) : IGenericRepository<T> where T : BaseEntity
{

    public void Add(T entity)
    {
        context.Set<T>().Add(entity);
    }

    public bool Exists(int id)
    {
        return context.Set<T>().Any(e => e.Id == id);
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        // Avoid FindAsync which always tracks; use a no-tracking query to prevent conflicts
        return await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<TResult?> GetEntityWithSpec<TResult>(ISpecification<T, TResult> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<T?> GetEntityWithSpec(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<T>> ListAllAsync()
    {
        // No-tracking for read-only lists to reduce tracking conflicts and memory
        return await context.Set<T>().AsNoTracking().ToListAsync();
    }

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).ToListAsync();
    }

    public async Task<IReadOnlyList<TResult>> ListAsync<TResult>(ISpecification<T, TResult> spec)
    {
        return await ApplySpecification(spec).ToListAsync();
    }

    public Task Remove(T entity)
    {
        // Avoid loading/tracking another instance. Attach the given stub if needed, then remove.
        var entry = context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            context.Set<T>().Attach(entity);
        }
        context.Set<T>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> SaveAllAsync()
    {
        return await context.SaveChangesAsync() > 0;
    }


    public void Update(T entity)
    {
        // If another instance with same key is already tracked, detach it to avoid conflicts
        var local = context.Set<T>().Local.FirstOrDefault(e => e.Id == entity.Id);
        if (local is not null && !ReferenceEquals(local, entity))
        {
            context.Entry(local).State = EntityState.Detached;
        }

        context.Set<T>().Attach(entity);
        context.Entry(entity).State = EntityState.Modified;
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        return SpecificationEvaluator<T>.GetQuery(context.Set<T>().AsQueryable(), spec);
    }

    private IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> spec)
    {
        return SpecificationEvaluator<T>.GetQuery<T, TResult>(context.Set<T>().AsQueryable(), spec);
    }

}
