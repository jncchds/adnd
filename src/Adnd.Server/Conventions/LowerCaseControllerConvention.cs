using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Adnd.Server.Conventions;

/// <summary>
/// Makes all controller routes lowercase by rewriting the [controller] placeholder
/// in attribute routes to its lowercase form. This ensures consistent lowercase
/// REST API paths (e.g., /api/games instead of /api/Games).
/// </summary>
public class LowerCaseControllerConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        var lowerCaseName = controller.ControllerName.ToLowerInvariant();

        foreach (var selector in controller.Selectors)
        {
            var model = selector.AttributeRouteModel;
            if (model?.Template == null) continue;

            // Replace [controller] with lowercase name
            var newTemplate = model.Template.Replace("[controller]", lowerCaseName, StringComparison.Ordinal);

            if (newTemplate != model.Template)
            {
                selector.AttributeRouteModel = new AttributeRouteModel
                {
                    Template = newTemplate,
                    Order = model.Order,
                    Name = model.Name,
                };
            }
        }
    }
}
