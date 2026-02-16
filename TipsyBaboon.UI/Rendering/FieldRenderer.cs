using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections;
using System.Text.Encodings.Web;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Extensions;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.UI.Configuration;
using TipsyBaboon.UI.Models;
using TipsyBaboon.UI.Services;

namespace TipsyBaboon.UI.Rendering
{
    public enum GridAlignment
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Parameters for registering a custom field renderer for CLR type <typeparamref name="T"/>.
    /// Controls which Razor partial is used, optional value transformation, and grid alignment.
    /// </summary>
    public class FieldRegistrationParams<T>
    {
        public string? PartialPath { get; set; }

        public string? ControlName { get; set; }

        public bool IsCollection { get; set; }

        public FieldTransformFunc<T>? TransformFunc { get; set; }

        public Type? ModelType { get; set; }

        public GridAlignment GridAlignment { get; set; } = GridAlignment.Left;

        public List<string>? RequiredScripts { get; set; }
    }

    public delegate object FieldTransformFunc<T>(FieldViewModel<T> model);

    internal record FieldRegistration(
        Type PropertyType,
        string? ControlName,
        bool IsCollection,
        string PartialPath,
        Delegate? TransformFunc,
        Type? ModelType,
        GridAlignment GridAlignment,
        List<string>? RequiredScripts);

    /// <summary>
    /// Central field rendering engine that maps CLR property types to Razor partial views.
    /// Consumers register custom renderers via <see cref="Register{T}"/>; built-in types
    /// (string, bool, DateTime, enums, collections, etc.) are auto-registered on first use.
    /// Called by detail/create pages and inline-editable grids to produce consistent field HTML.
    /// </summary>
    public static class FieldRenderer
    {
        private static readonly List<FieldRegistration> _registrations = new();
        private static readonly object _lock = new();
        private static bool _initialized = false;

        private static readonly AsyncLocal<HashSet<string>?> _requestScripts = new();

        /// <summary>Begins collecting required script keys for the current async context.</summary>
        public static void BeginScriptCollection()
        {
            _requestScripts.Value = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Ends script collection and returns the accumulated script keys.</summary>
        public static List<string> EndScriptCollection()
        {
            var scripts = _requestScripts.Value?.ToList() ?? new List<string>();
            _requestScripts.Value = null;
            return scripts;
        }

        /// <summary>Returns the script keys collected so far in the current async context.</summary>
        public static IReadOnlyCollection<string> GetCollectedScripts()
        {
            return _requestScripts.Value ?? (IReadOnlyCollection<string>)Array.Empty<string>();
        }

        /// <summary>Adds a script key to the current request's collected scripts.</summary>
        public static void AddScript(string scriptKey)
        {
            _requestScripts.Value?.Add(scriptKey);
        }

        private static void CollectScripts(FieldRegistration? registration)
        {
            if (registration?.RequiredScripts == null || _requestScripts.Value == null) return;
            foreach (var script in registration.RequiredScripts)
            {
                _requestScripts.Value.Add(script);
            }
        }

        private static string? GetParentKeyValue<T>(FieldViewModel<T> model)
        {
            if (model.Instance == null)
            {
                System.Diagnostics.Debug.WriteLine($"[GetParentKeyValue] Instance is null for property {model.Column.PropertyName}");
                return null;
            }
            if (model.Schema.KeyColumns.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[GetParentKeyValue] No key columns for schema {model.Schema.TypeName}");
                return null;
            }
            var keyPropName = model.Schema.KeyColumns[0].PropertyName;
            var prop = model.Instance.GetType().GetProperty(keyPropName);
            if (prop == null)
            {
                System.Diagnostics.Debug.WriteLine($"[GetParentKeyValue] Property '{keyPropName}' not found on {model.Instance.GetType().Name}");
                return null;
            }
            var rawValue = prop.GetValue(model.Instance);
            if (rawValue == null)
            {
                System.Diagnostics.Debug.WriteLine($"[GetParentKeyValue] Schema={model.Schema.TypeName} KeyProp={keyPropName} RawValue=null");
                return null;
            }
            var stringValue = rawValue.ToString();
            System.Diagnostics.Debug.WriteLine($"[GetParentKeyValue] Schema={model.Schema.TypeName} KeyProp={keyPropName} RawValue={rawValue} Type={rawValue.GetType().Name} StringValue={stringValue}");
            return stringValue;
        }

        private static readonly Dictionary<Type, int> _typePriority = new()
        {
            { typeof(object), 9999 },
            { typeof(ValueType), 9000 },
            { typeof(Enum), 8000 },
            
            { typeof(string), 210 },
            { typeof(long), 200 },
            { typeof(int), 190 },
            { typeof(short), 180 },
            { typeof(decimal), 170 },
            { typeof(double), 160 },
            { typeof(float), 150 },
            { typeof(bool), 140 },
            { typeof(byte), 130 },
            { typeof(DateTime), 120 },
            { typeof(DateTimeOffset), 110 },
            { typeof(DateOnly), 100 },
            { typeof(TimeOnly), 90 },
            { typeof(Guid), 80 },
        };

        #region Registration Methods

        /// <summary>
        /// Registers a custom field renderer for CLR type <typeparamref name="T"/>.
        /// The registration maps the type to a Razor partial view, with optional value transformation and alignment.
        /// </summary>
        /// <typeparam name="T">The CLR type this registration handles.</typeparam>
        /// <param name="registrationParams">Registration parameters including partial path and optional transform.</param>
        public static void Register<T>(FieldRegistrationParams<T> registrationParams)
        {
            lock (_lock)
            {
                if (registrationParams.PartialPath == null)
                    return;

                Delegate? transformDelegate = null;
                if (registrationParams.TransformFunc != null)
                {
                    var originalFunc = registrationParams.TransformFunc;
                    FieldTransformFunc<object> wrappedFunc = (FieldViewModel<object> model) =>
                    {
                        T typedValue;
                        bool wasNull = model.Value == null;

                        if (wasNull)
                        {
                            typedValue = default!;
                        }
                        else if (model.Value is T tv)
                        {
                            typedValue = tv;
                        }
                        else
                        {
                            typedValue = ConvertToType<T>(model.Value);
                        }

                        var typedModel = new FieldViewModel<T>
                        {
                            Schema = model.Schema,
                            Column = model.Column,
                            Instance = model.Instance,
                            Value = typedValue,
                            FormId = model.FormId,
                            WasNull = wasNull
                        };
                        return originalFunc(typedModel);
                    };
                    transformDelegate = wrappedFunc;
                }

                var registration = new FieldRegistration(
                    typeof(T),
                    registrationParams.ControlName,
                    registrationParams.IsCollection,
                    registrationParams.PartialPath,
                    transformDelegate,
                    registrationParams.ModelType,
                    registrationParams.GridAlignment,
                    registrationParams.RequiredScripts);

                var existingIndex = _registrations.FindIndex(r =>
                    r.PropertyType == typeof(T) &&
                    r.ControlName == registrationParams.ControlName &&
                    r.IsCollection == registrationParams.IsCollection);

                if (existingIndex >= 0)
                {
                    _registrations[existingIndex] = registration;
                }
                else
                {
                    _registrations.Add(registration);
                }
            }
        }

        private static int GetTypePriority(Type type)
        {
            // Exact match in priority dictionary
            if (_typePriority.TryGetValue(type, out var priority))
                return priority;

            return 0;
        }

        private static T ConvertToType<T>(object? value)
        {
            if (value == null)
                return default!;

            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var sourceType = value.GetType();

            if (sourceType == targetType || sourceType == underlyingType)
                return (T)value;

            if (underlyingType == typeof(Guid))
            {
                if (value is Guid g)
                    return (T)(object)g;
                if (value is string s && Guid.TryParse(s, out var parsed))
                    return (T)(object)parsed;
                return default!;
            }

            if (underlyingType.IsEnum)
            {
                if (value is string enumStr)
                    return (T)Enum.Parse(underlyingType, enumStr);
                if (value is int || value is long || value is short || value is byte)
                    return (T)Enum.ToObject(underlyingType, value);
                return default!;
            }

            if (underlyingType == typeof(TimeSpan))
            {
                if (value is TimeSpan ts)
                    return (T)(object)ts;
                if (value is string tsStr && TimeSpan.TryParse(tsStr, out var parsedTs))
                    return (T)(object)parsedTs;
                return default!;
            }

            if (underlyingType == typeof(DateTimeOffset))
            {
                if (value is DateTimeOffset dto)
                    return (T)(object)dto;
                if (value is DateTime dt)
                    return (T)(object)new DateTimeOffset(dt);
                if (value is string dtoStr && DateTimeOffset.TryParse(dtoStr, out var parsedDto))
                    return (T)(object)parsedDto;
                return default!;
            }

            if (value is IConvertible)
            {
                try
                {
                    return (T)Convert.ChangeType(value, underlyingType);
                }
                catch
                {
                    return default!;
                }
            }

            // Last resort: direct cast
            try
            {
                return (T)value;
            }
            catch
            {
                return default!;
            }
        }

        private static FieldRegistration? FindRegistration(Type propertyType, string? controlName, bool isCollection)
        {
            // Order by:
            // 1. Type priority (lower = matched first, so concrete types before base types)
            // 2. ControlName specificity (non-null first)
            var matches = _registrations
                .Where(r => r.IsCollection == isCollection
                    && propertyType.IsAssignableTo(r.PropertyType)
                    && (r.ControlName == controlName || r.ControlName == null))
                .OrderBy(r => GetTypePriority(r.PropertyType))
                .ThenByDescending(r => r.ControlName != null);

            var result = matches.FirstOrDefault();
            
            // Debug logging for collection controlName resolution
            if (isCollection && controlName != null)
            {
                System.Diagnostics.Debug.WriteLine($"[FindRegistration] Type={propertyType.Name} ControlName={controlName} IsCollection={isCollection}");
                System.Diagnostics.Debug.WriteLine($"[FindRegistration] Found {matches.Count()} matches:");
                foreach (var match in matches.Take(3))
                {
                    System.Diagnostics.Debug.WriteLine($"  - Type={match.PropertyType.Name} ControlName={match.ControlName ?? "(null)"} Priority={GetTypePriority(match.PropertyType)}");
                }
                System.Diagnostics.Debug.WriteLine($"[FindRegistration] Selected: {(result != null ? $"Type={result.PropertyType.Name} ControlName={result.ControlName ?? "(null)"}" : "(none)")}");
            }

            return result;
        }

        #endregion

        #region Public API

        /// <summary>Returns the preferred grid column alignment for a column based on its registered renderer.</summary>
        public static GridAlignment GetGridAlignment(ColumnDefinition column)
        {
            EnsureInitialized();

            var propertyType = column.ClrType;
            var controlName = column.ControlName;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            var isCollection = IsCollectionType(underlyingType);

            var registration = FindRegistration(underlyingType, controlName, isCollection);
            return registration?.GridAlignment ?? GridAlignment.Left;
        }

        /// <summary>
        /// Renders a field for a form (detail/create) context using the appropriate registered partial view.
        /// </summary>
        public static IHtmlContent RenderField(IHtmlHelper htmlHelper, SchemaDefinition schema, ColumnDefinition column, object? instance, string formId)
        {
            return RenderField(htmlHelper, schema, column, instance, formId, isGridContext: false, rowId: null, fieldName: null);
        }

        /// <summary>
        /// Renders a field for an inline-editable grid context using the appropriate registered partial view.
        /// </summary>
        public static IHtmlContent RenderField(IHtmlHelper htmlHelper, SchemaDefinition schema, ColumnDefinition column, object? instance, string formId, string rowId, string? fieldName)
        {
            return RenderField(htmlHelper, schema, column, instance, formId, isGridContext: true, rowId: rowId, fieldName: fieldName);
        }

        /// <summary>
        /// Generate wrapper record context attributes for a given schema and instance.
        /// Returns: data-module="..." data-model="..." data-record-keys='...'
        /// </summary>
        public static string GetWrapperRecordContextAttributes(SchemaDefinition schema, object? instance)
        {
            var moduleName = schema.ModuleName;
            var modelName = schema.TypeName;
            var recordKeysJson = "{}";

            if (instance != null)
            {
                var keyColumns = schema.KeyColumns;
                if (keyColumns.Count > 0)
                {
                    var keys = new Dictionary<string, string>();
                    foreach (var keyCol in keyColumns)
                    {
                        var keyValue = instance.GetType().GetProperty(keyCol.PropertyName)?.GetValue(instance)?.ToString() ?? "";
                        keys[keyCol.PropertyName] = keyValue;
                    }
                    recordKeysJson = System.Text.Json.JsonSerializer.Serialize(keys);
                }
            }

            var encodedKeys = System.Net.WebUtility.HtmlEncode(recordKeysJson);
            return $"data-module=\"{moduleName}\" data-model=\"{modelName}\" data-record-keys='{encodedKeys}'";
        }

        private static IHtmlContent RenderField(IHtmlHelper htmlHelper, SchemaDefinition schema, ColumnDefinition column, object? instance, string formId, bool isGridContext, string? rowId, string? fieldName)
        {
            EnsureInitialized();

            var value = ExtractPropertyValue(instance, column.PropertyName);
            var propertyType = column.ClrType;
            var controlName = column.ControlName;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            var isCollection = IsCollectionType(underlyingType);

            // Special case: simple foreign key with a related type that has a RecordName
            if (column.IsForeignKey
                && !string.IsNullOrEmpty(column.ForeignKeyTarget)
                && !isCollection
                && string.IsNullOrEmpty(controlName))
            {
                var fkResult = TryRenderForeignKeyLookup(htmlHelper, schema, column, instance, value, formId, isGridContext, rowId, fieldName);
                if (fkResult != null)
                    return fkResult;
            }

            if (isCollection)
            {
                return RenderCollection(htmlHelper, schema, column, instance, value, controlName, underlyingType, formId, isGridContext, rowId, fieldName);
            }

            return RenderScalar(htmlHelper, schema, column, instance, value, controlName, underlyingType, formId, isGridContext, rowId, fieldName);
        }

        #endregion

        #region Internal Rendering Methods

        internal static string RenderString(IHtmlHelper htmlHelper, SchemaDefinition schema, ColumnDefinition column, object? instance, string formId)
        {
            var htmlContent = RenderField(htmlHelper, schema, column, instance, formId);
            return ConvertToString(htmlContent);
        }

        private static IHtmlContent RenderCollection(
            IHtmlHelper htmlHelper,
            SchemaDefinition schema,
            ColumnDefinition column,
            object? instance,
            object? value,
            string? controlName,
            Type collectionType,
            string formId,
            bool isGridContext,
            string? rowId,
            string? fieldName)
        {
            var elementType = GetCollectionElementType(collectionType) ?? typeof(object);

            System.Diagnostics.Debug.WriteLine($"[RenderCollection] Property={column.PropertyName} ControlName={controlName ?? "(null)"} ElementType={elementType.Name}");

            var registration = FindRegistration(elementType, controlName, isCollection: true);
            if (registration != null)
            {
                return RenderPartial(htmlHelper, schema, column, instance, value, registration.PartialPath, registration, formId, isGridContext, rowId, fieldName);
            }

            // Fallback for unknown collection types
            return RenderPartial(htmlHelper, schema, column, instance, value, "~/Pages/Fields/_UnknownTypeField.cshtml", null, formId, isGridContext, rowId, fieldName);
        }

        private static IHtmlContent RenderScalar(
            IHtmlHelper htmlHelper,
            SchemaDefinition schema,
            ColumnDefinition column,
            object? instance,
            object? value,
            string? controlName,
            Type propertyType,
            string formId,
            bool isGridContext,
            string? rowId,
            string? fieldName)
        {
            var registration = FindRegistration(propertyType, controlName, isCollection: false);
            if (registration != null)
            {
                return RenderPartial(htmlHelper, schema, column, instance, value, registration.PartialPath, registration, formId, isGridContext, rowId, fieldName);
            }

            // Fallback for unknown types: display as read-only with type name
            return RenderPartial(htmlHelper, schema, column, instance, value, "~/Pages/Fields/_UnknownTypeField.cshtml", null, formId, isGridContext, rowId, fieldName);
        }

        private static IHtmlContent? TryRenderForeignKeyLookup(
            IHtmlHelper htmlHelper,
            SchemaDefinition schema,
            ColumnDefinition column,
            object? instance,
            object? value,
            string formId,
            bool isGridContext,
            string? rowId,
            string? fieldName)
        {
            var relatedSchema = ModelRegistry.GetModel(column.ForeignKeyTarget!);
            if (relatedSchema == null || string.IsNullOrEmpty(relatedSchema.RecordNameProperty))
                return null;

            // Single-key related types only (not composite)
            if (relatedSchema.KeyColumns.Count != 1)
                return null;

            string? displayValue = null;
            if (value != null)
            {
                var relatedKeyProp = relatedSchema.KeyColumns[0].PropertyName;
                displayValue = ResolveRecordNameDisplay(htmlHelper, relatedSchema, relatedKeyProp, value);
            }

            var model = new ForeignKeyLookupViewModel
            {
                Schema = schema,
                Column = column,
                RelatedSchema = relatedSchema,
                Value = value,
                DisplayValue = displayValue,
                Instance = instance,
                FormId = formId,
                IsGridContext = isGridContext,
                RowId = rowId,
                FieldName = fieldName,
                EnableModalView = relatedSchema.FullScreenMode.HasFlag(FullScreenMode.Detail) ? false : TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.EnableModalView
            };

            return htmlHelper.Partial("~/Pages/Fields/_ForeignKeyLookupField.cshtml", model);
        }

        private static string? ResolveRecordNameDisplay(IHtmlHelper htmlHelper, SchemaDefinition relatedSchema, string keyPropertyName, object keyValue)
        {
            try
            {
                var services = htmlHelper.ViewContext.HttpContext.RequestServices;
                var store = services.GetService(typeof(BaseModelStore)) as BaseModelStore;
                if (store == null) return null;

                var keyValues = new Dictionary<string, object> { { keyPropertyName, keyValue } };
                var result = store.GetByIdAsync(relatedSchema.ModuleName!, relatedSchema.TypeName, keyValues).GetAwaiter().GetResult();
                if (result == null) return null;

                var recordNameProp = result.GetType().GetProperty(relatedSchema.RecordNameProperty!);
                return recordNameProp?.GetValue(result)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static IHtmlContent RenderPartial(IHtmlHelper htmlHelper, SchemaDefinition schema, ColumnDefinition column, object? instance, object? value, string partialName, FieldRegistration? registration, string formId, bool isGridContext, string? rowId, string? fieldName)
        {
            // Collect required scripts for this field registration
            CollectScripts(registration);

            var underlyingType = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
            if (IsCollectionType(underlyingType))
            {
#pragma warning disable CS8601 // schema is non-nullable by method signature
                var model = new FieldViewModel<object>
                {
                    Schema = schema,
                    Column = column,
                    Instance = instance,
                    Value = value,
                    FormId = formId,
                    IsGridContext = isGridContext,
                    RowId = rowId,
                    FieldName = fieldName
                };
#pragma warning restore CS8601

                if (registration?.TransformFunc != null)
                {
                    var transformFunc = (FieldTransformFunc<object>)registration.TransformFunc;
                    var transformedModel = transformFunc(model);
                    return htmlHelper.Partial(partialName, transformedModel);
                }

                return htmlHelper.Partial(partialName, model);
            }

            // Special handling for enums: convert to Dictionary<int, string> for the partial
            // This is framework-level handling since all enums render the same way
            if (underlyingType.IsEnum)
            {
                var dictionary = underlyingType.ToDictionary();
                var enumModel = new FieldViewModel<Dictionary<int, string>>
                {
                    Schema = schema,
                    Column = column,
                    Instance = instance,
                    Value = dictionary,
                    FormId = formId,
                    IsGridContext = isGridContext,
                    RowId = rowId,
                    FieldName = fieldName
                };
                return htmlHelper.Partial(partialName, enumModel);
            }

            // Determine the target type for the FieldViewModel
            // Use the registration's ModelType if specified, otherwise PropertyType
            // DateTime/DateOnly/TimeOnly/DateTimeOffset use nullable wrappers for their specific partials
            Type targetType;
            if (underlyingType == typeof(DateTime))
            {
                targetType = typeof(DateTime?);
            }
            else if (underlyingType == typeof(DateTimeOffset))
            {
                targetType = typeof(DateTimeOffset?);
            }
            else if (underlyingType == typeof(DateOnly))
            {
                targetType = typeof(DateOnly?);
            }
            else if (underlyingType == typeof(TimeOnly))
            {
                targetType = typeof(TimeOnly?);
            }
            else if (registration != null)
            {
                targetType = registration.ModelType ?? registration.PropertyType
                    ?? throw new InvalidOperationException($"Field registration for '{column.PropertyName}' has no ModelType or PropertyType configured");
            }
            else
            {
                targetType = typeof(object);
            }

            var fieldViewModelType = typeof(FieldViewModel<>).MakeGenericType(targetType);
            var typedModel = Activator.CreateInstance(fieldViewModelType)!;

            var schemaProperty = fieldViewModelType.GetProperty("Schema")!;
            var columnProperty = fieldViewModelType.GetProperty("Column")!;
            var instanceProperty = fieldViewModelType.GetProperty("Instance")!;
            var valueProperty = fieldViewModelType.GetProperty("Value")!;
            var formIdProperty = fieldViewModelType.GetProperty("FormId")!;
            var isGridContextProperty = fieldViewModelType.GetProperty("IsGridContext")!;
            var rowIdProperty = fieldViewModelType.GetProperty("RowId")!;
            var fieldNameProperty = fieldViewModelType.GetProperty("FieldName")!;

            schemaProperty.SetValue(typedModel, schema);
            columnProperty.SetValue(typedModel, column);
            instanceProperty.SetValue(typedModel, instance);
            formIdProperty.SetValue(typedModel, formId);
            isGridContextProperty.SetValue(typedModel, isGridContext);
            rowIdProperty.SetValue(typedModel, rowId);
            fieldNameProperty.SetValue(typedModel, fieldName);

            // Set the value, with special handling for nullable types
            if (targetType == typeof(DateTime?) && value is DateTime dateValue)
            {
                valueProperty.SetValue(typedModel, dateValue);
            }
            else if (targetType == typeof(DateTime?) && value == null)
            {
                valueProperty.SetValue(typedModel, null);
            }
            else if (targetType == typeof(DateTimeOffset?) && value is DateTimeOffset dateTimeOffsetValue)
            {
                valueProperty.SetValue(typedModel, dateTimeOffsetValue);
            }
            else if (targetType == typeof(DateTimeOffset?) && value == null)
            {
                valueProperty.SetValue(typedModel, null);
            }
            else if (targetType == typeof(DateOnly?) && value is DateOnly dateOnlyValue)
            {
                valueProperty.SetValue(typedModel, dateOnlyValue);
            }
            else if (targetType == typeof(DateOnly?) && value == null)
            {
                valueProperty.SetValue(typedModel, null);
            }
            else if (targetType == typeof(TimeOnly?) && value is TimeOnly timeOnlyValue)
            {
                valueProperty.SetValue(typedModel, timeOnlyValue);
            }
            else if (targetType == typeof(TimeOnly?) && value == null)
            {
                valueProperty.SetValue(typedModel, null);
            }
            else
            {
                valueProperty.SetValue(typedModel, value);
            }

            // If there's a transform delegate, it was wrapped as FieldTransformFunc<object>
            // Create a FieldViewModel<object> and invoke directly - the wrapper handles type conversion
            if (registration?.TransformFunc != null)
            {
                var objectModel = new FieldViewModel<object>
                {
                    Schema = schema,
                    Column = column,
                    Instance = instance,
                    Value = value!,
                    FormId = formId,
                    IsGridContext = isGridContext,
                    RowId = rowId,
                    FieldName = fieldName
                };
                var transformFunc = (FieldTransformFunc<object>)registration.TransformFunc;
                var transformedModel = transformFunc(objectModel);
                return htmlHelper.Partial(partialName, transformedModel);
            }

            return htmlHelper.Partial(partialName, typedModel);
        }
        #endregion

        #region Helper Methods

        private static object? ExtractPropertyValue(object? instance, string propertyName)
        {
            if (instance == null) return null;

            if (instance is IDictionary<string, object?> dict)
                return dict.TryGetValue(propertyName, out var val) ? val : null;

            var propertyInfo = instance.GetType().GetProperty(propertyName);
            return propertyInfo?.GetValue(instance);
        }

        private static string ConvertToString(IHtmlContent htmlContent)
        {
            using var writer = new StringWriter();
            htmlContent.WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }

        internal static Type? GetCollectionElementType(Type collectionType)
        {
            if (collectionType == typeof(string)) return null;

            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (collectionType.IsGenericType)
            {
                return collectionType.GetGenericArguments().FirstOrDefault();
            }

            var enumerableInterface = collectionType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerableInterface?.GetGenericArguments().FirstOrDefault();
        }

        private static bool IsCollectionType(Type type)
        {
            if (type == typeof(string)) return false;

            return type.IsArray ||
                   (type.IsGenericType && type.GetGenericTypeDefinition().IsAssignableTo(typeof(ICollection<>))) ||
                   type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().IsAssignableTo(typeof(IEnumerable<>)));
        }

        #endregion

        #region Initialization

        private static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                RegisterDefaultPartials();
                _initialized = true;
            }
        }

        private static void RegisterDefaultPartials()
        {
            // Primitive types
            Register(new FieldRegistrationParams<string> { PartialPath = "~/Pages/Fields/_TextField.cshtml" });

            // String custom controls
            Register(new FieldRegistrationParams<string> { PartialPath = "~/Pages/Fields/_TextAreaField.cshtml", ControlName = "TextArea" });
            Register(new FieldRegistrationParams<string> { PartialPath = "~/Pages/Fields/_PasswordField.cshtml", ControlName = "Password" });

            // Integer types → transform to long? for the partial
            // Use WasNull to preserve null values from nullable source columns
            Register(new FieldRegistrationParams<int>
            {
                PartialPath = "~/Pages/Fields/_IntegerField.cshtml",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<long?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<long>
            {
                PartialPath = "~/Pages/Fields/_IntegerField.cshtml",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<long?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<short>
            {
                PartialPath = "~/Pages/Fields/_IntegerField.cshtml",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<long?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value,
                    FormId = model.FormId
                }
            });

            // Decimal types → transform to decimal? for the partial
            Register(new FieldRegistrationParams<decimal>
            {
                PartialPath = "~/Pages/Fields/_DecimalField.cshtml",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<decimal?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<double>
            {
                PartialPath = "~/Pages/Fields/_DecimalField.cshtml",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<decimal?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : (decimal)model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<float>
            {
                PartialPath = "~/Pages/Fields/_DecimalField.cshtml",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<decimal?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : (decimal)model.Value,
                    FormId = model.FormId
                }
            });

            // Numeric custom controls - Spinner (actual number input)
            Register(new FieldRegistrationParams<int>
            {
                PartialPath = "~/Pages/Fields/_SpinnerField.cshtml",
                ControlName = "Spinner",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<long?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<long>
            {
                PartialPath = "~/Pages/Fields/_SpinnerField.cshtml",
                ControlName = "Spinner",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<long?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<short>
            {
                PartialPath = "~/Pages/Fields/_SpinnerField.cshtml",
                ControlName = "Spinner",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<long?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<decimal>
            {
                PartialPath = "~/Pages/Fields/_DecimalSpinnerField.cshtml",
                ControlName = "Spinner",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<decimal?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<double>
            {
                PartialPath = "~/Pages/Fields/_DecimalSpinnerField.cshtml",
                ControlName = "Spinner",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<decimal?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : (decimal)model.Value,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<float>
            {
                PartialPath = "~/Pages/Fields/_DecimalSpinnerField.cshtml",
                ControlName = "Spinner",
                GridAlignment = GridAlignment.Right,
                TransformFunc = (model) => new FieldViewModel<decimal?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : (decimal)model.Value,
                    FormId = model.FormId
                }
            });

            // byte → bool (common database pattern)
            Register(new FieldRegistrationParams<byte>
            {
                PartialPath = "~/Pages/Fields/_BooleanField.cshtml",
                GridAlignment = GridAlignment.Center,
                TransformFunc = (model) => new FieldViewModel<bool>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.Value != 0,
                    FormId = model.FormId
                }
            });

            Register(new FieldRegistrationParams<bool> { PartialPath = "~/Pages/Fields/_BooleanField.cshtml", GridAlignment = GridAlignment.Center });

            // Boolean custom controls
            Register(new FieldRegistrationParams<bool> { PartialPath = "~/Pages/Fields/_BooleanField.cshtml", ControlName = "checkbox", GridAlignment = GridAlignment.Center });

            Register(new FieldRegistrationParams<DateTime> { PartialPath = "~/Pages/Fields/_DateField.cshtml" });

            // DateTime custom controls
            Register(new FieldRegistrationParams<DateTime> { PartialPath = "~/Pages/Fields/_DateAndTimeField.cshtml", ControlName = "DateAndTime" });
            Register(new FieldRegistrationParams<DateTime> { PartialPath = "~/Pages/Fields/_DateTimeToDateOnlyField.cshtml", ControlName = "DateOnly" });

            // DateTimeOffset → transform to DateTime? for the partial (convert to local time)
            Register(new FieldRegistrationParams<DateTimeOffset>
            {
                PartialPath = "~/Pages/Fields/_DateField.cshtml",
                TransformFunc = (model) => new FieldViewModel<DateTime?>
                {
                    Schema = model.Schema,
                    Column = model.Column,
                    Instance = model.Instance,
                    Value = model.WasNull ? null : model.Value.DateTime,
                    FormId = model.FormId
                }
            });
            Register(new FieldRegistrationParams<DateOnly> { PartialPath = "~/Pages/Fields/_DateOnlyField.cshtml" });
            Register(new FieldRegistrationParams<TimeOnly> { PartialPath = "~/Pages/Fields/_TimeOnlyField.cshtml" });

            Register(new FieldRegistrationParams<Guid> { PartialPath = "~/Pages/Fields/_GuidField.cshtml" });

            // PermissionLevel - custom icon-based dropdown (must be registered before generic Enum)
            Register(new FieldRegistrationParams<PermissionLevel>
            {
                PartialPath = "~/Pages/Fields/_PermissionLevelField.cshtml",
                GridAlignment = GridAlignment.Center,
                RequiredScripts = new List<string> { "/_content/TipsyBaboon.UI/js/tipsybaboon-permission-level.js" }
            });

            // Enum base type (matches any enum via IsAssignableTo)
            // Note: Enums are handled inline in RenderPartial - this registration is for the partial path lookup
            Register(new FieldRegistrationParams<Enum> { PartialPath = "~/Pages/Fields/_EnumField.cshtml" });

            // Enum custom controls
            Register(new FieldRegistrationParams<Enum> { PartialPath = "~/Pages/Fields/_EnumField.cshtml", ControlName = "buttongroup" });
            Register(new FieldRegistrationParams<Enum> { PartialPath = "~/Pages/Fields/_EnumField.cshtml", ControlName = "buttons" });
            Register(new FieldRegistrationParams<Enum> { PartialPath = "~/Pages/Fields/_EnumField.cshtml", ControlName = "radio" });

            // Collection types - specific element types
            Register(new FieldRegistrationParams<UserRoleAssignment>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                TransformFunc = (model) =>
                {
                    var items = model.Value as IEnumerable ?? Array.Empty<object>();
                    var itemsList = items.Cast<object>().ToList();
                    var parentId = GetParentKeyValue(model);

                    var navProp = model.Schema.Columns.FirstOrDefault(c => c.PropertyName == model.Column.PropertyName && c.IsNavigationProperty)
                        ?? throw new InvalidOperationException($"Navigation property '{model.Column.PropertyName}' not found in schema for UserRoleAssignment");
                    var foreignKeyProp = navProp.NavigationForeignKeyProperty
                        ?? throw new InvalidOperationException($"NavigationForeignKeyProperty not set for '{model.Column.PropertyName}' on UserRoleAssignment");

                    var contextFilters = !string.IsNullOrEmpty(parentId)
                        ? new Dictionary<string, object?> { { foreignKeyProp, parentId } }
                        : null;

                    return GridBuilder.Build(typeof(UserRoleAssignment), itemsList, new GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = false,
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        MaxHeight = "300px",
                        IsEditable = true,
                        EditableColumnNames = new List<string> { "IsAssigned" },
                        FieldName = model.Column.PropertyName,
                        ParentIdPropertyName = foreignKeyProp,
                        ParentEntityId = parentId,
                        UseInlineData = true,
                        ParentField = foreignKeyProp,
                        ParentTypeName = model.Schema.TypeName
                    });
                }
            });

            Register(new FieldRegistrationParams<RoleUserAssignment>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                TransformFunc = (model) =>
                {
                    var items = model.Value as IEnumerable ?? Array.Empty<object>();
                    var itemsList = items.Cast<object>().ToList();
                    var parentId = GetParentKeyValue(model);

                    var navProp = model.Schema.Columns.FirstOrDefault(c => c.PropertyName == model.Column.PropertyName && c.IsNavigationProperty)
                        ?? throw new InvalidOperationException($"Navigation property '{model.Column.PropertyName}' not found in schema for RoleUserAssignment");
                    var foreignKeyProp = navProp.NavigationForeignKeyProperty
                        ?? throw new InvalidOperationException($"NavigationForeignKeyProperty not set for '{model.Column.PropertyName}' on RoleUserAssignment");

                    var contextFilters = !string.IsNullOrEmpty(parentId)
                        ? new Dictionary<string, object?> { { foreignKeyProp, parentId } }
                        : null;

                    return GridBuilder.Build(typeof(RoleUserAssignment), itemsList, new GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = false,
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        MaxHeight = "300px",
                        IsEditable = true,
                        EditableColumnNames = new List<string> { "IsAssigned" },
                        FieldName = model.Column.PropertyName,
                        ParentIdPropertyName = foreignKeyProp,
                        ParentEntityId = parentId,
                        UseInlineData = true,
                        ParentField = foreignKeyProp,
                        ParentTypeName = model.Schema.TypeName
                    });
                }
            });

            Register(new FieldRegistrationParams<ExternalLinkRoleAssignment>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                TransformFunc = (model) =>
                {
                    var items = model.Value as IEnumerable ?? Array.Empty<object>();
                    var itemsList = items.Cast<object>().ToList();
                    var parentId = GetParentKeyValue(model);

                    var navProp = model.Schema.Columns.FirstOrDefault(c => c.PropertyName == model.Column.PropertyName && c.IsNavigationProperty)
                        ?? throw new InvalidOperationException($"Navigation property '{model.Column.PropertyName}' not found in schema for ExternalLinkRoleAssignment");
                    var foreignKeyProp = navProp.NavigationForeignKeyProperty
                        ?? throw new InvalidOperationException($"NavigationForeignKeyProperty not set for '{model.Column.PropertyName}' on ExternalLinkRoleAssignment");

                    var contextFilters = !string.IsNullOrEmpty(parentId)
                        ? new Dictionary<string, object?> { { foreignKeyProp, parentId } }
                        : null;

                    return GridBuilder.Build(typeof(ExternalLinkRoleAssignment), itemsList, new GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = false,
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        MaxHeight = "300px",
                        IsEditable = true,
                        EditableColumnNames = new List<string> { "IsAssigned" },
                        FieldName = model.Column.PropertyName,
                        ParentIdPropertyName = foreignKeyProp,
                        ParentEntityId = parentId,
                        UseInlineData = true,
                        ParentField = foreignKeyProp,
                        ParentTypeName = model.Schema.TypeName
                    });
                }
            });

            Register(new FieldRegistrationParams<RoleExternalLinkAssignment>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                TransformFunc = (model) =>
                {
                    var items = model.Value as IEnumerable ?? Array.Empty<object>();
                    var itemsList = items.Cast<object>().ToList();
                    var parentId = GetParentKeyValue(model);

                    var navProp = model.Schema.Columns.FirstOrDefault(c => c.PropertyName == model.Column.PropertyName && c.IsNavigationProperty)
                        ?? throw new InvalidOperationException($"Navigation property '{model.Column.PropertyName}' not found in schema for RoleExternalLinkAssignment");
                    var foreignKeyProp = navProp.NavigationForeignKeyProperty
                        ?? throw new InvalidOperationException($"NavigationForeignKeyProperty not set for '{model.Column.PropertyName}' on RoleExternalLinkAssignment");

                    var contextFilters = !string.IsNullOrEmpty(parentId)
                        ? new Dictionary<string, object?> { { foreignKeyProp, parentId } }
                        : null;

                    return GridBuilder.Build(typeof(RoleExternalLinkAssignment), itemsList, new GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = false,
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        MaxHeight = "300px",
                        IsEditable = true,
                        EditableColumnNames = new List<string> { "IsAssigned" },
                        FieldName = model.Column.PropertyName,
                        ParentIdPropertyName = foreignKeyProp,
                        ParentEntityId = parentId,
                        UseInlineData = true,
                        ParentField = foreignKeyProp,
                        ParentTypeName = model.Schema.TypeName
                    });
                }
            });

            Register(new FieldRegistrationParams<UserUserGroupAssignment>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                TransformFunc = (model) =>
                {
                    var items = model.Value as IEnumerable ?? Array.Empty<object>();
                    var itemsList = items.Cast<object>().ToList();
                    var parentId = GetParentKeyValue(model);

                    var navProp = model.Schema.Columns.FirstOrDefault(c => c.PropertyName == model.Column.PropertyName && c.IsNavigationProperty)
                        ?? throw new InvalidOperationException($"Navigation property '{model.Column.PropertyName}' not found in schema for UserUserGroupAssignment");
                    var foreignKeyProp = navProp.NavigationForeignKeyProperty
                        ?? throw new InvalidOperationException($"NavigationForeignKeyProperty not set for '{model.Column.PropertyName}' on UserUserGroupAssignment");

                    var contextFilters = !string.IsNullOrEmpty(parentId)
                        ? new Dictionary<string, object?> { { foreignKeyProp, parentId } }
                        : null;

                    return GridBuilder.Build(typeof(UserUserGroupAssignment), itemsList, new GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = false,
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        MaxHeight = "300px",
                        AutoSave = UIConfiguration.Instance.AutoSave,
                        IsEditable = true,
                        EditableColumnNames = new List<string> { "IsAssigned" },
                        FieldName = model.Column.PropertyName,
                        ParentIdPropertyName = foreignKeyProp,
                        ParentEntityId = parentId,
                        UseInlineData = true,
                        ParentField = foreignKeyProp,
                        ParentTypeName = model.Schema.TypeName
                    });
                }
            });

            Register(new FieldRegistrationParams<GroupUserUserGroupAssignment>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                TransformFunc = (model) =>
                {
                    var items = model.Value as IEnumerable ?? Array.Empty<object>();
                    var itemsList = items.Cast<object>().ToList();
                    var parentId = GetParentKeyValue(model);

                    var navProp = model.Schema.Columns.FirstOrDefault(c => c.PropertyName == model.Column.PropertyName && c.IsNavigationProperty)
                        ?? throw new InvalidOperationException($"Navigation property '{model.Column.PropertyName}' not found in schema for GroupUserUserGroupAssignment");
                    var foreignKeyProp = navProp.NavigationForeignKeyProperty
                        ?? throw new InvalidOperationException($"NavigationForeignKeyProperty not set for '{model.Column.PropertyName}' on GroupUserUserGroupAssignment");

                    var contextFilters = !string.IsNullOrEmpty(parentId)
                        ? new Dictionary<string, object?> { { foreignKeyProp, parentId } }
                        : null;

                    return GridBuilder.Build(typeof(GroupUserUserGroupAssignment), itemsList, new GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = false,
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        MaxHeight = "300px",
                        IsEditable = true,
                        AutoSave = UIConfiguration.Instance.AutoSave,
                        EditableColumnNames = new List<string> { "IsAssigned" },
                        FieldName = model.Column.PropertyName,
                        ParentIdPropertyName = foreignKeyProp,
                        ParentEntityId = parentId,
                        UseInlineData = true,
                        ParentField = foreignKeyProp,
                        ParentTypeName = model.Schema.TypeName
                    });
                }
            });

            Register(new FieldRegistrationParams<RolePermission>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                TransformFunc = (model) =>
                {
                    var items = model.Value as IEnumerable ?? Array.Empty<object>();
                    var itemsList = items.Cast<object>().ToList();
                    var parentId = GetParentKeyValue(model);
                    var contextFilters = !string.IsNullOrEmpty(parentId)
                        ? new Dictionary<string, object?> { { "RoleId", parentId } }
                        : null;

                    var rolePermissionSchema = ModelRegistry.GetModel("RolePermission", "Governance");

                    var editableColumns = rolePermissionSchema?.Columns
                        .Where(c => !c.IsPrimaryKey && !c.IsUIReadOnly && (c.PropertyName == "UsePages" || c.PropertyName == "CanCreate" || c.PropertyName == "ReadLevel" || c.PropertyName == "UpdateLevel" || c.PropertyName == "DeleteLevel"))
                        .Select(c => c.PropertyName)
                        .ToList() ?? new List<string>();

                    return GridBuilder.Build(typeof(RolePermission), itemsList, new GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = false,
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        MaxHeight = "400px",
                        IsEditable = editableColumns.Any(),
                        AutoSave = true,
                        EditableColumnNames = editableColumns,
                        FieldName = model.Column.PropertyName,
                        ParentIdPropertyName = "RoleId",
                        ParentEntityId = parentId,
                        UseInlineData = true,
                        ParentField = "RoleId",
                        ParentTypeName = model.Schema.TypeName
                    });
                }
            });

            Register(new FieldRegistrationParams<RolePrivilegeView>
            {
                PartialPath = "~/Pages/Fields/_RolePrivilegeField.cshtml",
                IsCollection = true,
                RequiredScripts = new List<string> { "/_content/TipsyBaboon.UI/js/tipsybaboon-role-privilege.js" },
                TransformFunc = (model) =>
                {
                    var parentId = GetParentKeyValue(model);
                    if (string.IsNullOrEmpty(parentId))
                        return new RolePrivilegeGroupModel();

                    var items = model.Value as IEnumerable ?? Array.Empty<object>();
                    var privilegeItems = items.Cast<RolePrivilegeView>()
                        .OrderBy(v => v.ModelName)
                        .ThenBy(v => v.PrivilegeName)
                        .Select(view => new RolePrivilegeItem
                        {
                            RoleId = view.RoleId,
                            PrivilegeId = view.PrivilegeId,
                            ModelId = view.ModelId,
                            PrivilegeName = view.PrivilegeName,
                            ModelName = view.ModelName,
                            IsAssigned = view.IsAssigned
                        }).ToList();

                    var privilegesByModel = privilegeItems
                        .GroupBy(p => p.ModelName)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    return new RolePrivilegeGroupModel
                    {
                        ContainerId = $"role-privilege-container-{Guid.NewGuid():N}",
                        FieldName = model.Column.PropertyName,
                        ApiEndpoint = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.BuildApiUrl("Governance", "RolePrivilegeView"),
                        ParentRoleId = Guid.Parse(parentId!),
                        PrivilegesByModel = privilegesByModel
                    };
                }
            });

            // Editable grid control for navigation properties - inline editing with auto-save
            Register(new FieldRegistrationParams<object>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                ControlName = "editable-grid",
                TransformFunc = (model) =>
                {
                    var collectionType = Nullable.GetUnderlyingType(model.Column.ClrType) ?? model.Column.ClrType;
                    var elementType = GetCollectionElementType(collectionType);

                    if (elementType == null)
                        throw new InvalidOperationException($"Cannot determine element type for collection property {model.Column.PropertyName}");

                    var parentId = GetParentKeyValue(model);

                    // Try to get foreign key from navigation property metadata first (most reliable)
                    string? filterPropertyName = null;
                    if (model.Column.IsNavigationProperty && !string.IsNullOrEmpty(model.Column.NavigationForeignKeyProperty))
                    {
                        filterPropertyName = model.Column.NavigationForeignKeyProperty;
                    }
                    else
                    {
                        // Fallback to convention-based detection
                        filterPropertyName = DetermineFilterProperty(elementType, model.Schema.TypeName);
                    }

                    // Parse editable columns from ControlConfig
                    List<string>? editableColumns = null;
                    if (model.Column.ControlConfig != null && model.Column.ControlConfig.TryGetValue("EditableColumns", out var editableColumnsValue))
                    {
                        editableColumns = editableColumnsValue
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim())
                            .Where(c => !string.IsNullOrEmpty(c))
                            .ToList();
                    }

                    // Build context filters for the parent entity relationship
                    var contextFilters = !string.IsNullOrEmpty(parentId) && filterPropertyName != null
                        ? new Dictionary<string, object?> { { filterPropertyName, parentId } }
                        : null;

                    return Services.GridBuilder.Build(elementType, new List<object>(), new Services.GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = true,
                        CanCreate = true,
                        CanEdit = true,
                        CanDelete = true,
                        IsEditable = editableColumns != null && editableColumns.Any(),
                        AutoSave = true,
                        EditableColumnNames = editableColumns,
                        MaxHeight = "400px",
                        ParentTypeName = model.Schema.TypeName,
                        ParentIdPropertyName = filterPropertyName,
                        ParentEntityId = parentId
                    });
                }
            });

            // Generic collection fallback (last resort for collections - uses typeof(object) which has highest priority number)
            Register(new FieldRegistrationParams<object>
            {
                PartialPath = "~/Pages/Fields/_CollectionField.cshtml",
                IsCollection = true,
                TransformFunc = (model) =>
                {
                    var collectionType = Nullable.GetUnderlyingType(model.Column.ClrType) ?? model.Column.ClrType;
                    var elementType = GetCollectionElementType(collectionType);

                    if (elementType == null)
                        throw new InvalidOperationException($"Cannot determine element type for collection property {model.Column.PropertyName}");

                    var parentId = GetParentKeyValue(model);

                    // Try to get foreign key from navigation property metadata first (most reliable)
                    string? filterPropertyName = null;
                    if (model.Column.IsNavigationProperty && !string.IsNullOrEmpty(model.Column.NavigationForeignKeyProperty))
                    {
                        filterPropertyName = model.Column.NavigationForeignKeyProperty;
                    }
                    else
                    {
                        // Fallback to convention-based detection
                        filterPropertyName = DetermineFilterProperty(elementType, model.Schema.TypeName);
                    }

                    // DEBUG logging
                    System.Diagnostics.Debug.WriteLine($"[GridCollection] Property={model.Column.PropertyName} ParentType={model.Schema.TypeName} " +
                        $"ElementType={elementType.Name} FilterProperty={filterPropertyName} ParentId={parentId} " +
                        $"IsNav={model.Column.IsNavigationProperty} NavFK={model.Column.NavigationForeignKeyProperty}");

                    // Build context filters for the parent entity relationship
                    var contextFilters = !string.IsNullOrEmpty(parentId) && filterPropertyName != null
                        ? new Dictionary<string, object?> { { filterPropertyName, parentId } }
                        : null;

                    return Services.GridBuilder.Build(elementType, new List<object>(), new Services.GridBuildOptions
                    {
                        ContextFilters = contextFilters,
                        EnableModalView = true,
                        CanCreate = true,
                        CanEdit = true,
                        CanDelete = true,
                        MaxHeight = "400px",
                        ParentTypeName = model.Schema.TypeName,
                        ParentIdPropertyName = filterPropertyName,
                        ParentEntityId = parentId
                    });
                }
            });
        }

        private static string? DetermineFilterProperty(Type childType, string parentTypeName)
        {
            var schema = ModelRegistry.GetModel(childType);
            if (schema == null)
                return null;

            var candidateNames = new[]
            {
                parentTypeName + "Id",
                parentTypeName.TrimEnd('s') + "Id",
                "ParentId"
            };

            foreach (var name in candidateNames)
            {
                var col = schema.Columns.FirstOrDefault(c =>
                    c.PropertyName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    (c.ClrType == typeof(Guid) || c.ClrType == typeof(Guid?)));
                if (col != null)
                    return col.PropertyName;
            }

            var fkColumns = schema.Columns.Where(c => c.IsForeignKey).ToList();
            if (fkColumns.Count == 1)
                return fkColumns[0].PropertyName;

            return null;
        }

        #endregion
    }
}
