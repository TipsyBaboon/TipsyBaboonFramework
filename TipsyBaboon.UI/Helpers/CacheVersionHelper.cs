using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TipsyBaboon.UI.Helpers
{
    /// <summary>
    /// Provides a simple cache-busting URL helper using the file's last-write time (Unix seconds).
    /// Use from Razor as: @Html.CacheVersionUrl("/path/to/file.js")
    /// </summary>
    public static class CacheVersionHelper
    {
        public static string CacheVersionUrl(this IHtmlHelper htmlHelper, string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath)) return virtualPath;

            var env = htmlHelper.ViewContext.HttpContext.RequestServices.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
            var webRoot = env?.WebRootPath ?? string.Empty;
            var phys = Path.Combine(webRoot, virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            string version;
            if (File.Exists(phys))
            {
                version = new DateTimeOffset(File.GetLastWriteTimeUtc(phys)).ToUnixTimeSeconds().ToString();
            }
            else
            {
                version = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            }

            return virtualPath + "?v=" + version;
        }
    }
}





