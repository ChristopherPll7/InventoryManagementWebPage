using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace InventoryManagement.Web.Models;

public sealed class CategoryForm : IValidatableObject
{
    [Required(ErrorMessage = "Escribe el nombre.")]
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return new ValidationResult("Escribe el nombre.", [nameof(Name)]);
        if (Name?.Trim().Length > 150) yield return new ValidationResult("El nombre admite hasta 150 caracteres.", [nameof(Name)]);
        if (Description?.Trim().Length > 500) yield return new ValidationResult("La descripción admite hasta 500 caracteres.", [nameof(Description)]);
    }

    public CategoryRequest ToRequest() => new(Name.Trim(), Description?.Trim());
}

public sealed class ProductForm : IValidatableObject
{
    [Required(ErrorMessage = "Escribe el nombre.")]
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    [Required(ErrorMessage = "Escribe el SKU.")]
    public string Sku { get; set; } = "";
    [Required(ErrorMessage = "Escribe el precio.")]
    public string PriceText { get; set; } = "";
    [Required(ErrorMessage = "Selecciona una categoría.")]
    public Guid? CategoryId { get; set; }
    public IReadOnlyList<CategoryDto> Categories { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 150)
            yield return new ValidationResult("El nombre debe tener entre 1 y 150 caracteres.", [nameof(Name)]);
        if (Description?.Trim().Length > 1000)
            yield return new ValidationResult("La descripción admite hasta 1000 caracteres.", [nameof(Description)]);
        if (string.IsNullOrWhiteSpace(Sku) || Sku.Trim().Length > 80)
            yield return new ValidationResult("El SKU debe tener entre 1 y 80 caracteres.", [nameof(Sku)]);
        if (!decimal.TryParse(PriceText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var price) || price < 0 || price > 9999999999999999.99m || decimal.Round(price, 2) != price)
            yield return new ValidationResult("Escribe un precio de 0 a 9999999999999999.99, con hasta dos decimales (usa punto).", [nameof(PriceText)]);
        if (!CategoryId.HasValue || CategoryId.Value == Guid.Empty)
            yield return new ValidationResult("Selecciona una categoría válida.", [nameof(CategoryId)]);
    }

    public ProductRequest ToRequest() => new(Name.Trim(), Description?.Trim(), Sku.Trim(),
        decimal.Parse(PriceText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), CategoryId!.Value);
}

public sealed class MovementForm : IValidatableObject
{
    [Required(ErrorMessage = "Selecciona un tipo de movimiento.")]
    public string Type { get; set; } = "Entry";
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser un entero mayor que cero.")]
    public int Quantity { get; set; }
    public string? Reason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Type is not ("Entry" or "Exit")) yield return new ValidationResult("Selecciona Entrada o Salida.", [nameof(Type)]);
        if (Reason?.Trim().Length > 500) yield return new ValidationResult("El motivo admite hasta 500 caracteres.", [nameof(Reason)]);
    }

    public MovementRequest ToRequest(Guid productId) => new(productId, Type, Quantity, Reason?.Trim());
}

public sealed class HistoryFilter
{
    public string? Type { get; set; }
    public string? StartDateText { get; set; }
    public string? EndDateText { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTimeOffset? StartDate { get; private set; }
    public DateTimeOffset? EndDate { get; private set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PaginationLimits.IsInvalid(Page, PageSize)) errors.Add("La página debe estar entre 1 y 1000 y el tamaño entre 1 y 100.");
        if (!string.IsNullOrWhiteSpace(Type) && Type is not ("Entry" or "Exit")) errors.Add("El tipo debe ser Entrada o Salida.");
        StartDate = ParseDate(StartDateText, "fecha inicial", errors);
        EndDate = ParseDate(EndDateText, "fecha final", errors);
        if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
            errors.Add("La fecha final no puede ser anterior a la inicial.");
        return errors;
    }

    private static DateTimeOffset? ParseDate(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Regex.IsMatch(value, @"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            errors.Add($"La {label} debe incluir hora y zona, por ejemplo 2026-09-29T12:00:00-06:00.");
            return null;
        }
        return result;
    }
}

public sealed record InventoryPage(ProductInventoryDto Balance, PagedResult<InventoryMovementDto> History,
    HistoryFilter Filter, MovementForm Movement, bool? IsActive);

public sealed record DeactivateById(Guid Id, string Action, string Resource);

public sealed record ProductListPage(PagedResult<ProductDto> Products, IReadOnlyDictionary<Guid, string> Categories);
public sealed record ProductDetailPage(ProductDto Product, string CategoryName);
public sealed record PagerModel(int Page, long TotalPages, long TotalCount, Func<int, string> Link)
{
    public bool HasNext => Page < TotalPages && Page < PaginationLimits.MaxPage;
}

