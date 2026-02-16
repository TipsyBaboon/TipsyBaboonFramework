using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using TipsyBaboon.UI.Controllers;

namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Controller route convention that sets the <see cref="GenericApiController"/> route template
    /// to <c>{ApiRoutePrefix}/{module}</c>, enabling consumer-controlled API URL namespacing.
    /// </summary>
    public class GenericApiRouteConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            if (controller.ControllerType != typeof(GenericApiController))
                return;

            var prefix = TipsyBaboonUIOptions.ApiRoutePrefix;
            var routeTemplate = $"{prefix}/{{module}}";

            controller.Selectors.Clear();
            controller.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(routeTemplate))
            });
        }
    }
}
