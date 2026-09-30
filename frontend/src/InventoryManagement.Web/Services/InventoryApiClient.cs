using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryManagement.Web.Models;

namespace InventoryManagement.Web.Services;

public sealed class InventoryApiClient(HttpClient http, TokenSessionService session, ILogger<InventoryApiClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<PagedResult<CategoryDto>> GetCategoriesAsync(int page, int pageSize, CancellationToken ct) =>
        SendAsync<PagedResult<CategoryDto>>(HttpMethod.Get, $"api/categories?page={page}&pageSize={pageSize}", null, ct);

    public Task<CategoryDto> GetCategoryAsync(Guid id, CancellationToken ct) =>
        SendAsync<CategoryDto>(HttpMethod.Get, $"api/categories/{id}", null, ct);

    public Task<CreatedId> CreateCategoryAsync(CategoryRequest request, CancellationToken ct) =>
        SendAsync<CreatedId>(HttpMethod.Post, "api/categories", request, ct);

    public Task<CategoryDto> UpdateCategoryAsync(Guid id, CategoryRequest request, CancellationToken ct) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{id}", request, ct);

    public Task DeleteCategoryAsync(Guid id, CancellationToken ct) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/categories/{id}", ct);

    public Task<PagedResult<ProductDto>> GetProductsAsync(int page, int pageSize, CancellationToken ct) =>
        SendAsync<PagedResult<ProductDto>>(HttpMethod.Get, $"api/products?page={page}&pageSize={pageSize}", null, ct);

    public Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct) =>
        SendAsync<ProductDto>(HttpMethod.Get, $"api/products/{id}", null, ct);

    public Task<CreatedId> CreateProductAsync(ProductRequest request, CancellationToken ct) =>
        SendAsync<CreatedId>(HttpMethod.Post, "api/products", request, ct);

    public Task<ProductDto> UpdateProductAsync(Guid id, ProductRequest request, CancellationToken ct) =>
        SendAsync<ProductDto>(HttpMethod.Put, $"api/products/{id}", request, ct);

    public Task DeleteProductAsync(Guid id, CancellationToken ct) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/products/{id}", ct);

    public Task<ProductInventoryDto> GetInventoryAsync(Guid productId, CancellationToken ct) =>
        SendAsync<ProductInventoryDto>(HttpMethod.Get, $"api/inventory/products/{productId}", null, ct);

    public Task<InventoryMovementResult> RegisterMovementAsync(MovementRequest request, CancellationToken ct) =>
        SendAsync<InventoryMovementResult>(HttpMethod.Post, "api/inventory/movements", request, ct);

    public Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(Guid productId, HistoryFilter filter, CancellationToken ct)
    {
        var query = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}" };
        if (!string.IsNullOrWhiteSpace(filter.Type)) query.Add($"type={Uri.EscapeDataString(filter.Type)}");
        if (filter.StartDate.HasValue) query.Add($"startDate={Uri.EscapeDataString(filter.StartDate.Value.ToString("O", CultureInfo.InvariantCulture))}");
        if (filter.EndDate.HasValue) query.Add($"endDate={Uri.EscapeDataString(filter.EndDate.Value.ToString("O", CultureInfo.InvariantCulture))}");
        return SendAsync<PagedResult<InventoryMovementDto>>(HttpMethod.Get,
            $"api/inventory/products/{productId}/movements?{string.Join("&", query)}", null, ct);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetActiveCategoriesAsync(CancellationToken ct)
    {
        var categories = await GetAllCategoriesAsync(ct);
        return categories.Where(category => category.IsActive).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllCategoriesAsync(CancellationToken ct)
    {
        var categories = new List<CategoryDto>();
        for (var page = 1; page <= 10000; page++)
        {
            var result = await GetCategoriesAsync(page, 100, ct);
            categories.AddRange(result.Items);
            if (page >= result.TotalPages) break;
        }
        return categories;
    }

    private Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct) where T : class =>
        ExecuteAsync(method, path, body, async (response, token) =>
        {
            var result = await response.Content.ReadFromJsonAsync<T>(Json, token)
                ?? throw new JsonException("The API returned an empty response.");
            ApiResponseValidator.Validate(result);
            if (result is CreatedId created)
            {
                Uri? location = null;
                if (response.Headers.TryGetValues("Location", out var values))
                    Uri.TryCreate(values.FirstOrDefault(), UriKind.RelativeOrAbsolute, out location);
                return (T)(object)(created with { Location = location });
            }
            return result;
        }, ct);

    private async Task SendNoContentAsync(HttpMethod method, string path, CancellationToken ct) =>
        await ExecuteAsync<object?>(method, path, null, (response, _) =>
        {
            if (response.StatusCode != HttpStatusCode.NoContent)
                throw new ApiCallException(502, "INVALID_API_RESPONSE", "La API no confirmó la desactivación.");
            return Task.FromResult<object?>(null);
        }, ct);

    private async Task<T> ExecuteAsync<T>(HttpMethod method, string path, object? body,
        Func<HttpResponseMessage, CancellationToken, Task<T>> readResponse, CancellationToken ct)
    {
        var accessToken = await session.GetAccessTokenAsync(ct);
        // HttpClient.Timeout stops at the headers with ResponseHeadersRead. Keep
        // the same budget alive until the success/error body has been consumed.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (http.Timeout != Timeout.InfiniteTimeSpan) deadline.CancelAfter(http.Timeout);
        var token = deadline.Token;
        try
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (body is not null) request.Content = JsonContent.Create(body, options: Json);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            token.ThrowIfCancellationRequested();
            if ((int)response.StatusCode >= 500)
            {
                // Log metadata only: upstream bodies can contain secrets or stack traces.
                logger.LogError("API returned HTTP {StatusCode} for {Method} {Path}", (int)response.StatusCode, method, path);
                throw new ApiCallException((int)response.StatusCode, "INTERNAL_ERROR", "La API no pudo completar la operación. Inténtalo más tarde.");
            }
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, token);
                token.ThrowIfCancellationRequested();
                throw error;
            }
            var expected = method == HttpMethod.Post ? HttpStatusCode.Created
                : method == HttpMethod.Delete ? HttpStatusCode.NoContent : HttpStatusCode.OK;
            if (response.StatusCode != expected)
            {
                logger.LogError("Unexpected API status {Actual}; expected {Expected} for {Method} {Path}",
                    (int)response.StatusCode, (int)expected, method, path);
                throw new ApiCallException(502, "INVALID_API_RESPONSE", "La API devolvió un estado inesperado. No se pudo confirmar el resultado.");
            }
            var result = await readResponse(response, token);
            token.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning(exception, "API transport failed for {Method} {Path}", method, path);
            throw new ApiCallException(0, "API_UNAVAILABLE", "No fue posible completar la comunicación con la API. Comprueba su disponibilidad.");
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(exception, "API timed out for {Method} {Path}", method, path);
            throw new ApiCallException(0, "API_UNAVAILABLE", "La API tardó demasiado en responder. Inténtalo otra vez.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            logger.LogError(exception, "Invalid response from API for {Method} {Path}", method, path);
            throw new ApiCallException(502, "INVALID_API_RESPONSE", "La API devolvió una respuesta que no se pudo interpretar.");
        }
    }

    private static async Task<ApiCallException> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(Json, ct);
            if (error is not null && !string.IsNullOrWhiteSpace(error.Code) && !string.IsNullOrWhiteSpace(error.Message))
                return new ApiCallException((int)response.StatusCode, error.Code, error.Message);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException) { }
        return new ApiCallException((int)response.StatusCode, "HTTP_ERROR", "La API no pudo completar la operación.");
    }
}

