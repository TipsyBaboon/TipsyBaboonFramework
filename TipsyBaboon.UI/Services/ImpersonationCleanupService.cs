using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.UI.Services
{
    /// <summary>
    /// Background hosted service that periodically purges expired impersonation sessions.
    /// Runs every 15 minutes, scoping a <see cref="BaseModelStore"/> to delete sessions past their ExpiresOn timestamp.
    /// </summary>
    public class ImpersonationCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImpersonationCleanupService> _logger;

        public ImpersonationCleanupService(
            IServiceProvider serviceProvider,
            ILogger<ImpersonationCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var store = scope.ServiceProvider.GetRequiredService<BaseModelStore>();

                    var expiredSessions = await store.QueryTypedAsync<ImpersonationSession>(
                        s => s.ExpiresOn < DateTime.UtcNow, stoppingToken);

                    var expiredCount = expiredSessions.Count();
                    if (expiredCount > 0)
                    {
                        foreach (var session in expiredSessions)
                        {
                            await store.DeleteTypedAsync<ImpersonationSession>(session.Id, stoppingToken);
                        }

                        _logger.LogInformation("Cleaned up {Count} expired impersonation sessions", expiredCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during impersonation session cleanup");
                }
            }
        }
    }
}
