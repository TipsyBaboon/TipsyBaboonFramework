using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using TipsyBaboon.UI.Controllers;

namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Controller route convention that sets the <see cref="UserPreferenceApiController"/> route template
    /// to <c>{ApiRoutePrefix}/preferences</c>, enabling consumer-controlled API URL namespacing.
    /// </summary>
    public class UserPreferenceApiRouteConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            if (controller.ControllerType != typeof(UserPreferenceApiController))
                return;

            var prefix = TipsyBaboonUIOptions.ApiRoutePrefix;
            var routeTemplate = $"{prefix}/preferences";

            controller.Selectors.Clear();
            controller.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(routeTemplate))
            });
        }
    }
}
