using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// Chooses between two actions on one address by whether the query carries <paramref name="name"/>: the home page with a
/// search text is rate limited as the public reads are, the plain home page is not (review 1 of N1, point 1).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class QueryPresentAttribute(string name, bool present = true) : ActionMethodSelectorAttribute
{
    public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
    {
        ArgumentNullException.ThrowIfNull(routeContext);
        return routeContext.HttpContext.Request.Query.ContainsKey(name) == present;
    }
}
