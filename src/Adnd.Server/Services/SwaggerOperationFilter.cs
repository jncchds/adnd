using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Adnd.Server.Services;

/// <summary>
/// Adds operation-level tags and descriptions to Swagger endpoints.
/// Groups endpoints by controller name.
/// </summary>
public class SwaggerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controllerAction = context.ApiDescription.ActionDescriptor as ControllerActionDescriptor;
        if (controllerAction == null) return;

        // Add tag based on controller name (strip "Controller" suffix)
        var controllerName = controllerAction.ControllerName;
        if (controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            controllerName = controllerName[..^10];
        }

        operation.Tags ??= new List<OpenApiTag>();
        if (!operation.Tags.Any(t => t.Name == controllerName))
        {
            operation.Tags.Add(new OpenApiTag { Name = controllerName });
        }

        // Add description of required auth if [Authorize] is present
        var hasAuthorize = context.MethodInfo.GetCustomAttributes(true)
            .Any(attr => attr.GetType().Name == "AuthorizeAttribute")
            || (context.ApiDescription.ActionDescriptor as ControllerActionDescriptor)
                ?.ControllerTypeInfo?.GetCustomAttributes(true)
                .Any(attr => attr.GetType().Name == "AuthorizeAttribute") == true;

        if (hasAuthorize)
        {
            operation.Description = "Requires valid JWT bearer token";
        }

        // Add example request body description for POST/PUT if there's a body parameter
        var bodyParam = context.ApiDescription.ParameterDescriptions
            .FirstOrDefault(p => p.Source == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body);
        if (bodyParam != null)
        {
            operation.RequestBody ??= new OpenApiRequestBody();
            operation.RequestBody.Description = bodyParam.Name;
        }
    }
}
