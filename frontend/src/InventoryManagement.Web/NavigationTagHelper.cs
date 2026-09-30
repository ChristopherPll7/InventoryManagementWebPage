using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.WebUtilities;

namespace InventoryManagement.Web;

[HtmlTargetElement("a", Attributes = "preserve-return")]
[HtmlTargetElement("a", Attributes = "return-back")]
[HtmlTargetElement("form", Attributes = "preserve-return")]
public sealed class NavigationTagHelper(IUrlHelperFactory urls) : TagHelper
{
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var request = ViewContext.HttpContext.Request;
        var helper = urls.GetUrlHelper(ViewContext);
        var returnUrl = request.Query["returnUrl"].ToString();
        if (output.Attributes.ContainsName("return-back"))
        {
            output.Attributes.RemoveAll("return-back");
            if (helper.IsLocalUrl(returnUrl)) output.Attributes.SetAttribute("href", returnUrl);
            return;
        }
        output.Attributes.RemoveAll("preserve-return");
        var attribute = output.TagName == "form" ? "action" : "href";
        var target = output.Attributes[attribute]?.Value?.ToString();
        if (!helper.IsLocalUrl(target)) return;
        var origin = output.TagName == "form" ? returnUrl : request.PathBase + request.Path + request.QueryString;
        if (helper.IsLocalUrl(origin)) output.Attributes.SetAttribute(attribute, QueryHelpers.AddQueryString(target!, "returnUrl", origin));
    }
}
