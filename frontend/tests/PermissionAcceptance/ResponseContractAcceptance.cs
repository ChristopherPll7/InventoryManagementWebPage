using System.Text.Json;
using InventoryManagement.Web.Models;
using InventoryManagement.Web.Services;

internal static class ResponseContractAcceptance
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
        string Json(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var id = Guid.NewGuid();
        var category = new CategoryDto(id, "Category", null, false, DateTimeOffset.UtcNow, null);
        var product = new ProductDto(id, "Product", null, "SKU", 0m, id, false, 0);
        var balance = new ProductInventoryDto(id, "SKU", "Product", 0);
        var movement = new InventoryMovementDto(id, id, "Entry", 1, null, DateTimeOffset.UtcNow);
        var create = Json(new CreatedId(id));
        var categoryRequest = new CategoryRequest("Category", null);
        var productRequest = new ProductRequest("Product", null, "SKU", 0m, id);
        var operations = new (string Name, int Status, string Body, Func<InventoryApiClient, Task> Call)[] {
            ("categories list", 200, Json(new PagedResult<CategoryDto>([category],1,20,1,1)), api => api.GetCategoriesAsync(1,20,default)),
            ("category detail", 200, Json(category), api => api.GetCategoryAsync(id,default)),
            ("category create", 201, create, api => api.CreateCategoryAsync(categoryRequest,default)),
            ("category update", 200, Json(category), api => api.UpdateCategoryAsync(id,categoryRequest,default)),
            ("category deactivate", 204, "", api => api.DeleteCategoryAsync(id,default)),
            ("products list", 200, Json(new PagedResult<ProductDto>([product],1,20,1,1)), api => api.GetProductsAsync(1,20,default)),
            ("product detail", 200, Json(product), api => api.GetProductAsync(id,default)),
            ("product create", 201, create, api => api.CreateProductAsync(productRequest,default)),
            ("product update", 200, Json(product), api => api.UpdateProductAsync(id,productRequest,default)),
            ("product deactivate", 204, "", api => api.DeleteProductAsync(id,default)),
            ("inventory balance", 200, Json(balance), api => api.GetInventoryAsync(id,default)),
            ("inventory history", 200, Json(new PagedResult<InventoryMovementDto>([movement],1,20,1,1)), api => api.GetMovementsAsync(id,new HistoryFilter(),default)),
            ("movement create", 201, Json(new InventoryMovementResult(id,0)), api => api.RegisterMovementAsync(new MovementRequest(id,"Exit",1,null),default))
        };
        foreach (var operation in operations)
        {
            using var valid = new TransportAcceptance.Probe(operation.Status, new(operation.Body,TimeSpan.Zero), TimeSpan.FromSeconds(2));
            await operation.Call(valid.Api);
            Check(valid.Handler.Calls == 1 && valid.Body.Disposed, operation.Name + " accepts expected status and valid DTO");
            using var invalid = new TransportAcceptance.Probe(operation.Status == 200 ? 201 : 200, new(operation.Body,TimeSpan.Zero), TimeSpan.FromSeconds(2));
            try { await operation.Call(invalid.Api); throw new Exception("Unexpected success"); }
            catch (ApiCallException error) { Check(error.StatusCode == 502 && error.Code == "INVALID_API_RESPONSE" && invalid.Body.Reads == 0 && invalid.Handler.Calls == 1,
                operation.Name + " rejects unexpected 2xx before body, without retry"); }
        }
        foreach (var malformed in new[] { "{}", "null", "{\"id\":\"00000000-0000-0000-0000-000000000000\"}", "{\"id\":null}" })
        {
            using var probe = new TransportAcceptance.Probe(201,new(malformed,TimeSpan.Zero),TimeSpan.FromSeconds(2));
            try { await probe.Api.CreateCategoryAsync(categoryRequest,default); throw new Exception("Unexpected success"); }
            catch(ApiCallException error) { Check(error.Code == "INVALID_API_RESPONSE", "invalid creation body rejected"); }
        }
        foreach (var malformed in new[] {
            "{}", "{\"items\":null,\"page\":1,\"pageSize\":20,\"totalCount\":0,\"totalPages\":0}",
            "{\"items\":[null],\"page\":1,\"pageSize\":20,\"totalCount\":1,\"totalPages\":1}",
            Json(new PagedResult<CategoryDto>([],0,20,0,0)), Json(new PagedResult<CategoryDto>([],1,20,-1,0)) })
        {
            using var probe = new TransportAcceptance.Probe(200,new(malformed,TimeSpan.Zero),TimeSpan.FromSeconds(2));
            try { await probe.Api.GetCategoriesAsync(1,20,default); throw new Exception("Unexpected success"); }
            catch(ApiCallException error) { Check(error.Code == "INVALID_API_RESPONSE", "incomplete or invalid page rejected"); }
        }
        foreach (var body in new[] { Json(new PagedResult<CategoryDto>([],1,20,0,0)), Json(new PagedResult<CategoryDto>([],5,20,3,1)) })
        {
            using var probe = new TransportAcceptance.Probe(200,new(body,TimeSpan.Zero),TimeSpan.FromSeconds(2));
            await probe.Api.GetCategoriesAsync(1,20,default);
            Check(true,"empty and beyond-last-page responses remain valid");
        }
        foreach (var location in new Uri?[] { null, new("/api/categories/"+id,UriKind.Relative), new("https://other.example/resource") })
        {
            using var probe = new TransportAcceptance.Probe(201,new(create,TimeSpan.Zero),TimeSpan.FromSeconds(2));
            probe.Handler.Location = location;
            var result = await probe.Api.CreateCategoryAsync(categoryRequest,default);
            Check(result.Id == id && result.Location == location && probe.Handler.Calls == 1,"Location captured as optional metadata, never followed");
        }
        using (var probe = new TransportAcceptance.Probe(200,new(Json(product).Replace("\"currentStock\":0", "\"extra\":true"),TimeSpan.Zero),TimeSpan.FromSeconds(2)))
        {
            try { await probe.Api.GetProductAsync(id,default); throw new Exception("Unexpected success"); }
            catch(ApiCallException error) { Check(error.Code == "INVALID_API_RESPONSE","missing numeric field cannot silently become zero"); }
        }
        using (var probe = new TransportAcceptance.Probe(200,new(Json(product with { CurrentStock = -1 }),TimeSpan.Zero),TimeSpan.FromSeconds(2)))
        {
            try { await probe.Api.GetProductAsync(id,default); throw new Exception("Unexpected success"); }
            catch(ApiCallException error) { Check(error.Code == "INVALID_API_RESPONSE","negative stock rejected"); }
        }
        Console.WriteLine($"{count} response contract checks passed with simulated HTTP responses.");
    }
}
