using Core.Entities;

namespace Core.Specifications;

public class ProductSpecification : BaseSpecification<Product>
{
    public ProductSpecification(string? brand, string? type, string? sort)
        : base(p => (string.IsNullOrEmpty(brand) || p.Brand.ToLower() == brand!.ToLower()) &&
                    (string.IsNullOrEmpty(type) || p.Type.ToLower() == type!.ToLower()))
    {
        switch (sort?.ToLower())
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

}
