using InventoryManagement.Web.Models;
using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventoryManagement.Web.Controllers;

public abstract class UiController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionArguments.Values.Any(value => value is Guid id && id == Guid.Empty))
            context.Result = Failure(new ApiCallException(400, "INVALID_REQUEST", "Indica un UUID distinto de cero."), (this is CategoriesController ? "/categories" : "/products"));
        base.OnActionExecuting(context);
    }
    protected async Task<IActionResult> OpenById(DirectAccessForm form, bool category, PermissionService permissions)
    {
        form.Category = category;
        var operation = form.Operations.FirstOrDefault(item => item.Value == form.Operation);
        if (operation.Value is null) return BadRequest();
        if (!await permissions.HasAsync(operation.Permission))
            return Failure(new ApiCallException(403, "FORBIDDEN", "Permission denied."), form.BasePath);
        if (!Guid.TryParse(form.Id, out var id) || id == Guid.Empty)
            ModelState.AddModelError(nameof(form.Id), "Indica un UUID válido y distinto de cero.");
        if (!ModelState.IsValid) return View("~/Views/Shared/DirectAccess.cshtml", form);
        return Redirect(operation.Prefix + id + operation.Suffix);
    }

    protected IActionResult Failure(ApiCallException exception, string returnUrl)
    {
        if (exception.StatusCode == 401)
            return RedirectToAction("Login", "Home", new { returnUrl });

        var message = exception.Code switch
        {
            "FORBIDDEN" => "Tu usuario no tiene permiso para realizar esta operación.",
            "PRODUCT_NOT_FOUND" or "CATEGORY_NOT_FOUND" => "El registro ya no está disponible.",
            "API_UNAVAILABLE" => exception.Message,
            "INVALID_REQUEST" => "Revisa los datos enviados. Los identificadores deben ser UUID válidos y distintos de cero.",
            "CATEGORY_IN_USE" => "La categoría contiene productos activos. Desactívalos antes de desactivar la categoría.",
            "PRODUCT_INACTIVE" => "El producto está inactivo. Su stock e historial siguen disponibles, pero no admite nuevos movimientos.",
            _ => "No pudimos completar la operación. Revisa el detalle o inténtalo de nuevo."
        };
        Response.StatusCode = exception.StatusCode is >= 400 and <= 599 ? exception.StatusCode : 503;
        return View("~/Views/Shared/ApiError.cshtml", new ErrorPage(message, exception.Code, returnUrl));
    }

    protected void AddFormError(ApiCallException exception)
    {
        var (field, message) = exception.Code switch
        {
            "DUPLICATE_CATEGORY_NAME" or "INVALID_CATEGORY_NAME" or "INVALID_PRODUCT_NAME" => ("Name", "Ese nombre ya existe o no es válido."),
            "DUPLICATE_PRODUCT_SKU" or "INVALID_PRODUCT_SKU" => ("Sku", "Ese SKU ya existe o no es válido."),
            "INVALID_PRODUCT_PRICE" => ("PriceText", "El precio no es válido."),
            "INVALID_CATEGORY_DESCRIPTION" or "INVALID_PRODUCT_DESCRIPTION" => ("Description", "La descripción supera la longitud permitida."),
            "INVALID_CATEGORY_ID" or "CATEGORY_INACTIVE" or "CATEGORY_NOT_FOUND" => ("CategoryId", "Selecciona una categoría activa y disponible."),
            "INVALID_INVENTORY_QUANTITY" or "INSUFFICIENT_STOCK" or "INVENTORY_STOCK_OVERFLOW" => ("Quantity", exception.Code == "INSUFFICIENT_STOCK" ? "No hay suficientes unidades disponibles." : "La cantidad no es válida para el stock actual."),
            "INVALID_INVENTORY_TYPE" => ("Type", "Selecciona Entrada o Salida."),
            "INVALID_INVENTORY_REASON" => ("Reason", "El motivo no es válido."),
            _ => ("", $"{exception.Code}: {exception.Message}")
        };
        ModelState.AddModelError(field, message);
    }

    protected bool InvalidPagination(int page, int pageSize) => Models.PaginationLimits.IsInvalid(page, pageSize);

    protected string? SafeReturnUrl()
    {
        var target = Request.Query["returnUrl"].ToString();
        return Url.IsLocalUrl(target) ? target : null;
    }

    protected IActionResult QueryFailure(Models.HistoryFilter filter, bool inventory = false)
    {
        Response.StatusCode = 400;
        ViewData["InventoryFilters"] = inventory;
        return View("~/Views/Shared/QueryError.cshtml", filter);
    }
}

public sealed record ErrorPage(string Message, string Code, string ReturnUrl);
