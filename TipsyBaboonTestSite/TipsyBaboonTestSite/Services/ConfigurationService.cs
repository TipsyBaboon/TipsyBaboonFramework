using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace TipsyBaboonTestSite.Services
{
    public interface IConfigurationService
    {
        T GetConfiguration<T>() where T : class, new();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;

        public ConfigurationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public T GetConfiguration<T>() where T : class, new()
        {
            var sectionName = typeof(T).Name;
            var config = new T();
            _configuration.GetSection(sectionName).Bind(config);
            return config;
        }
    }
}
