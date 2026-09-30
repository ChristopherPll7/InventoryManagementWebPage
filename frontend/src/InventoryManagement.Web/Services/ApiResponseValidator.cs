using System.Text.Json;
using InventoryManagement.Web.Models;

namespace InventoryManagement.Web.Services;

internal static class ApiResponseValidator
{
    public static void Validate(object? value)
    {
        var valid = value switch
        {
            CreatedId item => item.Id != Guid.Empty,
            CategoryDto item => item.Id != Guid.Empty && Text(item.Name) && item.CreatedAt != default,
            ProductDto item => item.Id != Guid.Empty && item.CategoryId != Guid.Empty && Text(item.Name) && Text(item.Sku)
                && item.Price is >= 0 and <= 9999999999999999.99m && decimal.Round(item.Price, 2) == item.Price && item.CurrentStock >= 0,
            ProductInventoryDto item => item.ProductId != Guid.Empty && Text(item.Name) && Text(item.Sku) && item.CurrentStock >= 0,
            InventoryMovementDto item => item.Id != Guid.Empty && item.ProductId != Guid.Empty && item.Type is "Entry" or "Exit"
                && item.Quantity > 0 && item.CreatedAt != default,
            InventoryMovementResult item => item.MovementId != Guid.Empty && item.CurrentStock >= 0,
            PagedResult<CategoryDto> page => Page(page),
            PagedResult<ProductDto> page => Page(page),
            PagedResult<InventoryMovementDto> page => Page(page),
            _ => false
        };
        if (!valid) throw new JsonException("The API response does not satisfy the required contract.");
    }

    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool Page<T>(PagedResult<T> page)
    {
        if (page.Items is null || page.Page is < 1 or > 10000 || page.PageSize is < 1 or > 100
            || page.TotalCount < 0 || page.TotalPages < 0 || page.Items.Count > page.PageSize) return false;
        // Offset paging may return no items beyond the last page, and counts can
        // change concurrently. Do not infer an exact snapshot from the totals.
        foreach (var item in page.Items) Validate(item);
        return true;
    }
}
