using System.Linq;
using System.Linq.Expressions;
using Core.Entities;

namespace Core.Specifications;

public class ProductSpecification : BaseSpecification<Product>
{
    public ProductSpecification(ProductSpecParams specParams)
        : base(CreatePredicate(specParams))
    {
        ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);
        switch (specParams.Sort)
        {
            case "priceAsc":
                AddOrderBy(x => x.Price);
                break;
            case "priceDesc":
                AddOrderByDescending(x => x.Price);
                break;
            default:
                AddOrderBy(x => x.Name);
                break;
        }
    }

    // Cleaner, Cosmos-friendly predicate: capture local arrays/constants and avoid LOWER/UPPER on document fields.
    private static Expression<Func<Product, bool>> CreatePredicate(ProductSpecParams specParams)
    {
        // Prepare brand/type filters (trim + distinct). Canonicalize type casing to match seeded data.
        var brands = specParams.Brands
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b.Trim())
            .Distinct()
            .ToArray();

        static string CanonicalType(string s)
            => string.IsNullOrWhiteSpace(s)
                ? s
                : char.ToUpperInvariant(s.Trim()[0]) + s.Trim().Substring(1).ToLowerInvariant();

        var types = specParams.Types
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(CanonicalType)
            .Distinct()
            .ToArray();

        // Prepare search (no LOWER on fields). Try raw and TitleCase variants.
        var hasSearch = !string.IsNullOrWhiteSpace(specParams.Search);
        var search = hasSearch ? specParams.Search!.Trim() : string.Empty;
        string? searchTitle = null;
        if (hasSearch)
        {
            searchTitle = char.ToUpperInvariant(search[0]) + (search.Length > 1 ? search.Substring(1).ToLowerInvariant() : string.Empty);
            if (string.Equals(search, searchTitle, StringComparison.Ordinal))
            {
                // avoid redundant variant if already properly cased
                searchTitle = null;
            }
        }

        return p =>
            (brands.Length == 0 || brands.Contains(p.Brand)) &&
            (types.Length == 0 || types.Contains(p.Type)) &&
            (!hasSearch || p.Name.Contains(search) || (searchTitle != null && p.Name.Contains(searchTitle)));
    }
}

#region backup
// using System.Linq.Expressions;
// using Core.Entities;

// namespace Core.Specifications;

// public class ProductSpecification : BaseSpecification<Product>
// {
//     public ProductSpecification(ProductSpecParams productParams)
//         : base(BuildCriteria(productParams))
//     {
//         switch (productParams.Sort)
//         {
//             case "priceAsc":
//                 AddOrderBy(x => x.Price);
//                 break;
//             case "priceDesc":
//                 AddOrderByDescending(x => x.Price);
//                 break;
//             default:
//                 AddOrderBy(x => x.Name);
//                 break;
//         }
//     }

//     public static Expression<Func<Product, bool>> BuildCriteria(ProductSpecParams specParams)
//     {
//         var p = Expression.Parameter(typeof(Product), "p");
//         var brandProp = Expression.Property(p, nameof(Product.Brand));
//         var typeProp = Expression.Property(p, nameof(Product.Type));

//             // Prefer a translator-friendly shape for Cosmos LINQ: brands.Contains(p.Brand) && types.Contains(p.Type)
//             // Build brands.Contains(p.Brand)
//             Expression? brandExpr = null;
//             if (specParams.Brands.Length > 0)
//             {
//                 var brandsConst = Expression.Constant(specParams.Brands);
//                 var containsMethod = typeof(Enumerable)
//                     .GetMethods()
//                     .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
//                     .MakeGenericMethod(typeof(string));
//                 brandExpr = Expression.Call(containsMethod, brandsConst, brandProp);
//             }

//         Expression? typeExpr = null;
//         if (specParams.Types.Length > 0)
//         {
//                 // Build types.Contains(p.Type)
//                 var typesConst = Expression.Constant(specParams.Types);
//                 var containsMethod = typeof(Enumerable)
//                     .GetMethods()
//                     .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
//                     .MakeGenericMethod(typeof(string));
//                 typeExpr = Expression.Call(containsMethod, typesConst, typeProp);
//         }

//         Expression body = (brandExpr, typeExpr) switch
//         {
//             (null, null) => Expression.Constant(true),
//             (null, not null) => typeExpr!,
//             (not null, null) => brandExpr!,
//             _ => Expression.AndAlso(brandExpr!, typeExpr!)
//         };

//         return Expression.Lambda<Func<Product, bool>>(body, p);
//     }
// }

#endregion