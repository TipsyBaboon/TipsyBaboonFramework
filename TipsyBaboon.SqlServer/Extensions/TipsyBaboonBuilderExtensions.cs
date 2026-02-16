using TipsyBaboon.Core.Startup;

namespace TipsyBaboon.SqlServer.Extensions
{
    /// <summary>
    /// Extension methods for configuring TipsyBaboon to use SQL Server as the database provider.
    /// </summary>
    public static class TipsyBaboonBuilderExtensions
    {
        /// <summary>
        /// Configures TipsyBaboon to use SQL Server as the database provider.
        /// Registers SqlServerTipsyBaboonProvider which provides pure ADO.NET-based storage implementations.
        /// </summary>
        /// <param name="builder">TipsyBaboon builder instance.</param>
        /// <returns>Builder for method chaining.</returns>
        /// <example>
        /// services.AddTipsyBaboon(builder => {
        ///     builder.AddModule("Governance", connectionString);
        ///     builder.UseSqlServer();
        /// });
        /// </example>
        public static TipsyBaboonBuilder UseSqlServer(this TipsyBaboonBuilder builder)
        {
            builder.UseProvider(() => new SqlServerTipsyBaboonProvider());
            return builder;
        }
    }
}
