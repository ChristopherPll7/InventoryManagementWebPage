namespace InventoryManagement.Web.Models;

public static class PaginationLimits
{
    public const int MaxPage = 1000;
    public static bool IsInvalid(int page, int pageSize) => page is < 1 or > MaxPage || pageSize is < 1 or > 100;
}
