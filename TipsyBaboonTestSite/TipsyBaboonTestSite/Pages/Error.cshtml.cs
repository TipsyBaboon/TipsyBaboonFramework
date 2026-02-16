using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TipsyBaboonTestSite.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }
        public new int StatusCode { get; set; }
        public string ErrorTitle { get; set; } = "Error";
        public string ErrorMessage { get; set; } = "An error occurred while processing your request.";
        public string? ErrorDetails { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public ErrorModel()
        {
        }

        public void OnGet(int? statusCode = null)
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            StatusCode = statusCode ?? HttpContext.Response.StatusCode;

            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var statusCodeFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

            switch (StatusCode)
            {
                case 403:
                    ErrorTitle = "Access Denied";
                    ErrorMessage = "You don't have permission to access this resource.";
                    
                    var originalPath = statusCodeFeature?.OriginalPath ?? exceptionFeature?.Path;
                    if (!string.IsNullOrEmpty(originalPath))
                    {
                        ErrorDetails = $"Access to '{originalPath}' requires additional permissions.";
                        
                        if (originalPath.Contains("/ChangeLog", StringComparison.OrdinalIgnoreCase))
                        {
                            ErrorDetails += " You need the 'View Change Log' privilege on the RADModule model.";
                        }
                        else if (originalPath.Contains("/Configuration", StringComparison.OrdinalIgnoreCase))
                        {
                            ErrorDetails += " You need the 'Manage Configuration' privilege on the RADModule model.";
                        }
                    }
                    break;

                case 404:
                    ErrorTitle = "Not Found";
                    ErrorMessage = "The page you're looking for doesn't exist.";
                    break;

                case 500:
                    ErrorTitle = "Server Error";
                    ErrorMessage = "An internal server error occurred.";
                    if (exceptionFeature?.Error != null)
                    {
                        ErrorDetails = exceptionFeature.Error.Message;
                    }
                    break;

                default:
                    ErrorTitle = $"Error {StatusCode}";
                    ErrorMessage = "Something went wrong while processing your request.";
                    break;
            }
        }
    }

}
