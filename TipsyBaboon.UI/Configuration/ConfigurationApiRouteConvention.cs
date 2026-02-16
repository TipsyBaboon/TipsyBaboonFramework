using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using TipsyBaboon.UI.Controllers;

namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Controller route convention that sets the <see cref="ConfigurationApiController"/> route template
    /// to <c>{ApiRoutePrefix}/configuration</c>, enabling consumer-controlled API URL namespacing.
    /// </summary>
    public class ConfigurationApiRouteConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            if (controller.ControllerType != typeof(ConfigurationApiController))
                return;

            var prefix = TipsyBaboonUIOptions.ApiRoutePrefix;
            var routeTemplate = $"{prefix}/configuration";

            controller.Selectors.Clear();
            controller.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(routeTemplate))
            });
        }
    }
}
