using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Razor Page route convention that prepends <see cref="TipsyBaboonUIOptions.PageRoutePrefix"/> to
    /// Generic and Governance page routes, enabling consumer-controlled URL namespacing.
    /// </summary>
    public class GenericPageRouteConvention : IPageRouteModelConvention
    {
        public void Apply(PageRouteModel model)
        {
            var prefix = TipsyBaboonUIOptions.PageRoutePrefix;
            if (string.IsNullOrEmpty(prefix))
                return;

            var pagePath = model.ViewEnginePath;
            
            // Apply prefix to Generic pages
            if (pagePath.StartsWith("/Generic/"))
            {
                foreach (var selector in model.Selectors)
                {
                    var template = selector.AttributeRouteModel?.Template;
                    if (string.IsNullOrEmpty(template))
                        continue;

                    if (template.StartsWith("{module}") || template.StartsWith("/{module}"))
                    {
                        var cleanTemplate = template.TrimStart('/');
                        selector.AttributeRouteModel!.Template = $"{prefix}/{cleanTemplate}";
                    }
                }
            }
            // Also apply prefix to module-specific override pages (e.g., /Governance/Layout/*)
            else if (pagePath.StartsWith("/Governance/"))
            {
                foreach (var selector in model.Selectors)
                {
                    var template = selector.AttributeRouteModel?.Template;
                    if (string.IsNullOrEmpty(template))
                        continue;

                    // If route doesn't already have the prefix, add it
                    if (!template.StartsWith(prefix + "/") && !template.StartsWith("/" + prefix + "/"))
                    {
                        var cleanTemplate = template.TrimStart('/');
                        selector.AttributeRouteModel!.Template = $"{prefix}/{cleanTemplate}";
                    }
                }
            }
        }
    }
}
