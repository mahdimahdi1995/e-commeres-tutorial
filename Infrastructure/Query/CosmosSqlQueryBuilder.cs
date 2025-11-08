// using System.Text;

// namespace Infrastructure.Query;

// /// <summary>
// /// Represents a built Cosmos SQL query string with its bound parameters.
// /// </summary>
// public sealed class CosmosSqlQuery
// {
//     public string QueryText { get; }
//     public IReadOnlyList<(string Name, object Value)> Parameters { get; }

//     public CosmosSqlQuery(string queryText, IReadOnlyList<(string Name, object Value)> parameters)
//     {
//         QueryText = queryText;
//         Parameters = parameters;
//     }

//     public override string ToString() => QueryText;
// }

// /// <summary>
// /// Lightweight builder for Cosmos DB SQL queries (Gateway/SQL API compatible).
// /// Generates parameterized WHERE and ORDER BY clauses and avoids problematic shapes
// /// like LOWER() on document fields. Use IN for multi-value filters.
// /// </summary>
// public sealed class CosmosSqlQueryBuilder
// {
//     private readonly List<string> _where = new();
//     private readonly List<string> _orderBy = new();
//     private readonly List<(string Name, object Value)> _parameters = new();

//     private string NextParamName() => $"@p{_parameters.Count}";

//     /// <summary>
//     /// Adds an equality filter: c["Property"] = @pN
//     /// </summary>
//     public CosmosSqlQueryBuilder AddEqualsFilter(string property, object? value)
//     {
//         if (value is null) return this;
//         var name = NextParamName();
//         _parameters.Add((name, value));
//         _where.Add($"c[\"{property}\"] = {name}");
//         return this;
//     }

//     /// <summary>
//     /// Adds a multi-value filter using IN: c["Property"] IN (@p0, @p1, ...)
//     /// Values are trimmed and deduplicated; empties are ignored.
//     /// </summary>
//     public CosmosSqlQueryBuilder AddInFilter(string property, IEnumerable<string>? values)
//     {
//         var list = values?
//             .Where(v => !string.IsNullOrWhiteSpace(v))
//             .Select(v => v.Trim())
//             .Distinct()
//             .ToList();

//         if (list is null || list.Count == 0) return this;

//         var names = new List<string>(list.Count);
//         foreach (var v in list)
//         {
//             var name = NextParamName();
//             _parameters.Add((name, v));
//             names.Add(name);
//         }

//         _where.Add($"c[\"{property}\"] IN ({string.Join(", ", names)})");
//         return this;
//     }

//     /// <summary>
//     /// Adds ORDER BY c["Property"] ASC/DESC. Multiple calls append multiple keys.
//     /// </summary>
//     public CosmosSqlQueryBuilder AddOrderBy(string property, bool ascending = true)
//     {
//         _orderBy.Add($"c[\"{property}\"] {(ascending ? "ASC" : "DESC")}");
//         return this;
//     }

//     /// <summary>
//     /// Builds the final SELECT statement.
//     /// </summary>
//     public CosmosSqlQuery Build(int? limit = null, int? offset = null)
//     {
//         var sb = new StringBuilder("SELECT c FROM c");

//         if (_where.Count > 0)
//         {
//             sb.Append(" WHERE ").Append(string.Join(" AND ", _where));
//         }

//         if (_orderBy.Count > 0)
//         {
//             sb.Append(" ORDER BY ").Append(string.Join(", ", _orderBy));
//         }

//         if (limit.HasValue)
//         {
//             sb.Append(" OFFSET ").Append(offset.GetValueOrDefault(0)).Append(" LIMIT ").Append(limit.Value);
//         }

//         return new CosmosSqlQuery(sb.ToString(), _parameters);
//     }
// }

// /*
// Example usage:

// // Build a query equivalent to:
// // SELECT c FROM c
// // WHERE c["Brand"] IN (@p0, @p1) AND c["Type"] IN (@p2, @p3)
// // ORDER BY c["Name"] ASC

// var builder = new CosmosSqlQueryBuilder()
//     .AddInFilter("Brand", new[] { "Angular", "React" })
//     .AddInFilter("Type", new[] { "Boots", "Gloves" })
//     .AddOrderBy("Name", ascending: true);

// CosmosSqlQuery q = builder.Build();
// // q.QueryText -> the SQL string
// // q.Parameters -> list of (name, value) to add with your Cosmos SDK QueryDefinition

// // With Microsoft.Azure.Cosmos SDK (pseudo-code):
// // var qd = new QueryDefinition(q.QueryText);
// // foreach (var (name, value) in q.Parameters)
// //     qd.WithParameter(name, value);
// // var iterator = container.GetItemQueryIterator<Product>(qd);
// // while (iterator.HasMoreResults) { var page = await iterator.ReadNextAsync(); }
// */
