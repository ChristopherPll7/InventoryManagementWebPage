using System.Text.Json.Serialization;

namespace InventoryManagement.Web.Models;

public sealed record CreatedId([property: JsonRequired] Guid Id) { [JsonIgnore] public Uri? Location { get; init; } }
public sealed record CategoryDto([property: JsonRequired] Guid Id, [property: JsonRequired] string Name, [property: JsonRequired] string? Description, [property: JsonRequired] bool IsActive, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] DateTimeOffset? UpdatedAt);
public sealed record ProductDto([property: JsonRequired] Guid Id, [property: JsonRequired] string Name, [property: JsonRequired] string? Description, [property: JsonRequired] string Sku, [property: JsonRequired] decimal Price, [property: JsonRequired] Guid CategoryId, [property: JsonRequired] bool IsActive, [property: JsonRequired] int CurrentStock);
public sealed record ProductInventoryDto([property: JsonRequired] Guid ProductId, [property: JsonRequired] string Sku, [property: JsonRequired] string Name, [property: JsonRequired] int CurrentStock);
public sealed record InventoryMovementDto([property: JsonRequired] Guid Id, [property: JsonRequired] Guid ProductId, [property: JsonRequired] string Type, [property: JsonRequired] int Quantity, [property: JsonRequired] string? Reason, [property: JsonRequired] DateTimeOffset CreatedAt);
public sealed record InventoryMovementResult([property: JsonRequired] Guid MovementId, [property: JsonRequired] int CurrentStock);
public sealed record ApiError([property: JsonRequired] string Code, [property: JsonRequired] string Message);
public sealed record PagedResult<T>([property: JsonRequired] IReadOnlyList<T> Items, [property: JsonRequired] int Page, [property: JsonRequired] int PageSize, [property: JsonRequired] long TotalCount, [property: JsonRequired] long TotalPages);

public sealed record CategoryRequest(string Name, string? Description);
public sealed record ProductRequest(string Name, string? Description, string Sku, decimal Price, Guid CategoryId);
public sealed record MovementRequest(Guid ProductId, string Type, int Quantity, string? Reason);

