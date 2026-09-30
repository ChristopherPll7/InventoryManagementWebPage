namespace InventoryManagement.Web.Models;

public sealed class DirectAccessForm
{
    public string? Id { get; set; }
    public string? Operation { get; set; }
    public bool Category { get; set; }
    public string BasePath => Category ? "/categories" : "/products";
    public string Title => Category ? "Categorías" : "Productos";
    public IEnumerable<(string Value, string Permission, string Label, string Prefix, string Suffix)> Operations => Category
        ? [("edit", "categories.write", "Editar categoría", "/categories/", "/edit"),
           ("deactivate", "categories.write", "Desactivar categoría", "/categories/", "/deactivate")]
        : [("edit", "products.write", "Editar producto", "/products/", "/edit"),
           ("deactivate", "products.write", "Desactivar producto", "/products/", "/deactivate"),
           ("inventory-read", "inventory.read", "Consultar inventario", "/inventory/products/", ""),
           ("inventory-write", "inventory.write", "Registrar movimiento", "/inventory/products/", "/movements")];
}
