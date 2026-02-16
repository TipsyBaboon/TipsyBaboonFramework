using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.SqlServer.Storage
{
    /// <summary>
    /// SQL Server implementation of IViewModelStore for registering and querying database views.
    /// Dynamically creates/updates SQL Server views from ViewDefinition metadata at registration time.
    /// </summary>
    /// <remarks>
    /// Views are created with naming convention: vw_{TypeName}
    /// View definitions are translated to SQL Server CREATE VIEW statements with SELECT/FROM/JOIN/WHERE clauses.
    /// Registration is synchronous (blocks on CreateOrUpdateViewAsync) to ensure views exist before first query.
    /// View creation gracefully handles missing tables during startup (SqlException 208).
    /// </remarks>
    public class TipsyBaboonViewModelStore : IViewModelStore
    {
        private readonly Dictionary<Type, string> _viewQueries = new();

        private static readonly Regex ForbiddenSqlPattern = new(
            @"(?i)\b(drop|delete|insert|update|truncate|alter|create|exec|execute|xp_|sp_|WAITFOR|SHUTDOWN|DBCC)\b|--|/\*|\*/|;\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static void ValidateSqlExpression(string? expression, string paramName)
        {
            if (string.IsNullOrWhiteSpace(expression)) return;
            
            if (ForbiddenSqlPattern.IsMatch(expression))
            {
                throw new ArgumentException(
                    $"SQL expression contains forbidden keywords or patterns. " +
                    $"ViewDefinition must only be created from trusted code, never from user input.",
                    paramName);
            }
        }

        public TipsyBaboonViewModelStore()
        {
        }

        /// <summary>
        /// Registers a view definition and immediately creates/updates the corresponding SQL Server view.
        /// </summary>
        /// <typeparam name="T">Model type representing the view structure</typeparam>
        /// <param name="definition">View definition with columns, tables, joins, and optional WHERE clause</param>
        /// <remarks>
        /// Creates a view named vw_{TypeName} in the database.
        /// Builds SQL Server CREATE VIEW statement from ViewDefinition metadata.
        /// Drops and recreates the view if it already exists.
        /// Blocks synchronously to ensure view creation completes before returning.
        /// Silently skips creation if referenced tables don't exist yet (SqlException 208).
        /// </remarks>
        public void RegisterView<T>(ViewDefinition definition) where T : class
        {
            var query = BuildSqlServerQuery(definition);
            _viewQueries[typeof(T)] = query;
            CreateOrUpdateViewAsync<T>(query).GetAwaiter().GetResult();
        }

        private async Task CreateOrUpdateViewAsync<T>(string selectQuery) where T : class
        {
            var viewName = $"vw_{typeof(T).Name}";
            var moduleName = ModelRegistry.GetModuleName(typeof(T));
            var connString = ModelRegistry.GetConnectionString(moduleName);

            if (string.IsNullOrEmpty(connString))
                throw new InvalidOperationException($"No connection string registered for module '{moduleName}'. Cannot create view '{viewName}'.");

            var createViewSql = $@"
IF OBJECT_ID('dbo.{viewName}', 'V') IS NOT NULL
    DROP VIEW [dbo].[{viewName}];
GO
CREATE VIEW [dbo].[{viewName}] AS
{selectQuery}";

            try
            {
                await using var conn = new SqlConnection(connString);
                await conn.OpenAsync();

                var batches = createViewSql.Split(new[] { "\r\nGO\r\n", "\nGO\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    
                    await using var cmd = new SqlCommand(batch, conn);
                    cmd.CommandTimeout = 30;
                    await cmd.ExecuteNonQueryAsync();
                }

                // view created/updated
            }
            catch (SqlException ex) when (ex.Number == 208) // Invalid object name
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModelStore] Skipping view creation — referenced tables do not exist yet: {ex.Message}");
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Checks whether a view has been registered for the specified type.
        /// </summary>
        /// <typeparam name="T">Model type representing the view structure</typeparam>
        /// <returns>True if view is registered, false otherwise</returns>
        public bool IsViewRegistered<T>() where T : class
        {
            return _viewQueries.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Queries a registered view and returns all rows as strongly-typed entities.
        /// </summary>
        /// <typeparam name="T">Model type representing the view structure</typeparam>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of view entities</returns>
        /// <exception cref="InvalidOperationException">Thrown if view not registered or module connection string missing</exception>
        /// <remarks>
        /// Executes SELECT * FROM [dbo].[vw_{TypeName}].
        /// Maps columns to properties by name (case-insensitive).
        /// Handles type conversions: int→bool, string→enum, numeric→enum.
        /// Resolves connection string from ModelRegistry using type's module.
        /// </remarks>
        public async Task<IEnumerable<T>> GetViewAsync<T>(CancellationToken cancellationToken = default) where T : class
        {
            if (!_viewQueries.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException(
                    $"No view registered for type {typeof(T).Name}. Call RegisterView<T>() before querying.");
            }

            var viewName = $"vw_{typeof(T).Name}";
            var moduleName = ModelRegistry.GetModuleName(typeof(T));
            var connString = ModelRegistry.GetConnectionString(moduleName);

            if (string.IsNullOrEmpty(connString))
            {
                throw new InvalidOperationException(
                    $"No connection string registered for module '{moduleName}'. " +
                    "Register the module via builder.AddModule() in services.AddTipsyBaboon().");
            }

            var results = new List<T>();

            await using var conn = new SqlConnection(connString);
            await conn.OpenAsync(cancellationToken);

            var query = $"SELECT * FROM [dbo].[{viewName}]";
            await using var cmd = new SqlCommand(query, conn);
            cmd.CommandTimeout = 30;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            while (await reader.ReadAsync(cancellationToken))
            {
                var instance = Activator.CreateInstance<T>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    if (properties.TryGetValue(columnName, out var prop))
                    {
                        var value = reader.GetValue(i);
                        if (value != DBNull.Value)
                        {
                            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            
                            if (targetType == typeof(bool) && value is int intValue)
                            {
                                prop.SetValue(instance, intValue != 0);
                            }
                            else if (targetType.IsEnum && value is string strValue)
                            {
                                prop.SetValue(instance, Enum.Parse(targetType, strValue));
                            }
                            else if (targetType.IsEnum)
                            {
                                var underlyingType = Enum.GetUnderlyingType(targetType);
                                var convertedValue = Convert.ChangeType(value, underlyingType);
                                prop.SetValue(instance, Enum.ToObject(targetType, convertedValue));
                            }
                            else
                            {
                                prop.SetValue(instance, Convert.ChangeType(value, targetType));
                            }
                        }
                    }
                }

                results.Add(instance);
            }


            return results;
        }

        private string BuildSqlServerQuery(ViewDefinition def)
        {
            if (def.Tables.Count == 0)
                throw new ArgumentException("ViewDefinition must have at least one table.", nameof(def));

            // Validate all SQL expressions for injection attempts
            foreach (var col in def.Columns)
            {
                ValidateSqlExpression(col.SourceExpression, "SourceExpression");
                ValidateSqlExpression(col.TargetProperty, "TargetProperty");
            }
            
            foreach (var table in def.Tables)
            {
                ValidateSqlExpression(table.TableName, "TableName");
                ValidateSqlExpression(table.Alias, "Alias");
            }
            
            foreach (var join in def.Joins)
            {
                ValidateSqlExpression(join.LeftColumn, "LeftColumn");
                ValidateSqlExpression(join.RightColumn, "RightColumn");
            }
            
            ValidateSqlExpression(def.WhereClause, "WhereClause");

            var sb = new StringBuilder();

            var columns = string.Join(", ", def.Columns.Select(c =>
                $"{c.SourceExpression} AS [{c.TargetProperty}]"));
            sb.Append("SELECT ").Append(columns);

            var fromTable = def.Tables[0];
            sb.Append($" FROM [dbo].[{fromTable.TableName}] {fromTable.Alias}");

            for (int i = 1; i < def.Tables.Count; i++)
            {
                var table = def.Tables[i];
                
                var joins = def.Joins.Where(j => j.RightTable == table.Alias || j.LeftTable == table.Alias).ToList();

                var joinType = table.JoinType switch
                {
                    JoinType.Cross => "CROSS JOIN",
                    JoinType.Left => "LEFT JOIN",
                    JoinType.Right => "RIGHT JOIN",
                    JoinType.Inner => "INNER JOIN",
                    JoinType.Full => "FULL OUTER JOIN",
                    _ => "JOIN"
                };

                sb.Append($" {joinType} [dbo].[{table.TableName}] {table.Alias}");

                if (table.JoinType != JoinType.Cross && joins.Count > 0)
                {
                    var conditions = joins.Select(j => 
                        $"{j.LeftTable}.[{j.LeftColumn}] = {j.RightTable}.[{j.RightColumn}]");
                    sb.Append($" ON ").Append(string.Join(" AND ", conditions));
                }
            }

            if (!string.IsNullOrWhiteSpace(def.WhereClause))
            {
                sb.Append($" WHERE {def.WhereClause}");
            }

            return sb.ToString();
        }
    }
}
