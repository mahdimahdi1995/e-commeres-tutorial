using System.Linq;
using System.Linq.Expressions;
using Core.Entities;

namespace Core.Specifications;

public class ProductSpecificationOld : BaseSpecification<Product>
{
    public ProductSpecificationOld(ProductSpecParams specParams)
        : base(BuildCriteria(specParams))
    {
        switch (specParams.Sort?.ToLower())
        {
            case "priceasc":
                AddOrderBy(p => p.Price);
                break;
            case "pricedesc":
                AddOrderByDescending(p => p.Price);
                break;
            default:
                AddOrderBy(p => p.Name); // default sort
                break;
        }
    }

    private static Expression<Func<Product, bool>> BuildCriteria(ProductSpecParams specParams)
    {
        // Build the filter expression for products which matches the brands and types in specParams
        var p = Expression.Parameter(typeof(Product), "p");
        var brandProperty = Expression.Property(p, nameof(Product.Brand));
        var typeProperty = Expression.Property(p, nameof(Product.Type));
        Expression? brandExpr = null;
        Expression? typeExpr = null;
        // Build brand expression if brands are specified
        var brandList = specParams.Brands?
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b.Trim())
            .Distinct()
            .ToList();
        if (brandList != null && brandList.Count > 0)
        {
            // start with first equality
            brandExpr = Expression.Equal(brandProperty, Expression.Constant(brandList[0]));
            for (int i = 1; i < brandList.Count; i++)
            {
                var eq = Expression.Equal(brandProperty, Expression.Constant(brandList[i]));
                brandExpr = Expression.OrElse(brandExpr, eq);
            }
        }
        // Build type expression if types are specified
        var typeList = specParams.Types?
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct()
            .ToList();
        if (typeList != null && typeList.Count > 0)
        {
            // start with first equality
            typeExpr = Expression.Equal(typeProperty, Expression.Constant(typeList[0]));
            for (int i = 1; i < typeList.Count; i++)
            {
                var eq = Expression.Equal(typeProperty, Expression.Constant(typeList[i]));
                typeExpr = Expression.OrElse(typeExpr, eq);
            }
        }
        // Combine brand and type expressions with AND
        Expression body = (brandExpr, typeExpr) switch
        {
            (null, null)       => Expression.Constant(true),
            (null, not null)   => typeExpr!,
            (not null, null)   => brandExpr!,
            _                  => Expression.AndAlso(brandExpr!, typeExpr!)
        };
        return Expression.Lambda<Func<Product, bool>>(body, p);
    }
}
