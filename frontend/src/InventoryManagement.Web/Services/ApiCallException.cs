namespace InventoryManagement.Web.Services;

public sealed class ApiCallException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
