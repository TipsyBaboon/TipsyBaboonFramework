using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TipsyBaboon.Core.Extensions
{
    /// <summary>
    /// Helper utilities for object comparison and analysis.
    /// Provides deep diffing and property-level comparisons.
    /// </summary>
    public static class ObjectHelper
    {
        /// <summary>
        /// Performs a deep diff between two objects of the same type.
        /// Compares all public properties and fields, tracking differences at each level.
        /// </summary>
        /// <typeparam name="T">Type of objects to compare.</typeparam>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target object to compare against.</param>
        /// <returns>A collection of property-level differences. Empty if objects are equal.</returns>
        public static IEnumerable<PropertyDifference> DeepDiff<T>(T source, T target) where T : notnull
        {
            var differences = new List<PropertyDifference>();
            DeepDiffInternal(source, target, String.Empty, differences);
            return differences;
        }

        /// <summary>
        /// Performs a deep diff between two objects of the same type, using a path prefix.
        /// </summary>
        /// <typeparam name="T">Type of objects to compare.</typeparam>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target object to compare against.</param>
        /// <param name="pathPrefix">Prefix to prepend to property paths (useful for nested comparisons).</param>
        /// <returns>A collection of property-level differences. Empty if objects are equal.</returns>
        public static IEnumerable<PropertyDifference> DeepDiff<T>(T source, T target, string pathPrefix) where T : notnull
        {
            var differences = new List<PropertyDifference>();
            DeepDiffInternal(source, target, pathPrefix, differences);
            return differences;
        }

        private static void DeepDiffInternal(object? source, object? target, string path, List<PropertyDifference> differences)
        {
            // Handle nulls
            if (source == null && target == null) return;
            if (source == null || target == null)
            {
                differences.Add(new PropertyDifference
                {
                    Path = path,
                    SourceValue = source,
                    TargetValue = target
                });
                return;
            }

            var sourceType = source.GetType();
            var targetType = target.GetType();

            // Handle value types and strings
            if (sourceType.IsValueType || sourceType == typeof(string))
            {
                if (!Equals(source, target))
                {
                    differences.Add(new PropertyDifference
                    {
                        Path = path,
                        SourceValue = source,
                        TargetValue = target
                    });
                }
                return;
            }

            // Type mismatch
            if (sourceType != targetType)
            {
                differences.Add(new PropertyDifference
                {
                    Path = path,
                    SourceValue = source,
                    TargetValue = target
                });
                return;
            }

            // Handle collections
            if (typeof(IEnumerable).IsAssignableFrom(sourceType) && sourceType != typeof(string))
            {
                DiffCollections(source, target, path, differences, sourceType);
                return;
            }

            // Handle complex objects via reflection
            var properties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (!property.CanRead)
                    continue;

                var sourceValue = property.GetValue(source);
                var targetValue = property.GetValue(target);

                if (sourceValue == null && targetValue == null)
                    continue;

                var propertyPath = String.IsNullOrEmpty(path)
                    ? property.Name
                    : $"{path}.{property.Name}";

                if (sourceValue == null || targetValue == null || property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                {
                    if (!Equals(sourceValue, targetValue))
                    {
                        differences.Add(new PropertyDifference
                        {
                            Path = propertyPath,
                            SourceValue = sourceValue,
                            TargetValue = targetValue
                        });
                    }
                }
                else
                {
                    // Recursively diff complex objects
                    DeepDiffInternal(sourceValue, targetValue, propertyPath, differences);
                }
            }

            var fields = sourceType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var sourceValue = field.GetValue(source);
                var targetValue = field.GetValue(target);

                if (sourceValue == null && targetValue == null)
                    continue;

                var fieldPath = String.IsNullOrEmpty(path)
                    ? field.Name
                    : $"{path}.{field.Name}";

                if (sourceValue == null || targetValue == null || field.FieldType.IsValueType || field.FieldType == typeof(string))
                {
                    if (!Equals(sourceValue, targetValue))
                    {
                        differences.Add(new PropertyDifference
                        {
                            Path = fieldPath,
                            SourceValue = sourceValue,
                            TargetValue = targetValue
                        });
                    }
                }
                else
                {
                    // Recursively diff complex objects
                    DeepDiffInternal(sourceValue, targetValue, fieldPath, differences);
                }
            }
        }

        private static void DiffCollections(object source, object target, string path, List<PropertyDifference> differences, Type collectionType)
        {
            var sourceList = ((IEnumerable)source).Cast<object>().ToList();
            var targetList = ((IEnumerable)target).Cast<object>().ToList();

            if (sourceList.Count != targetList.Count)
            {
                differences.Add(new PropertyDifference
                {
                    Path = $"{path} [Count]",
                    SourceValue = sourceList.Count,
                    TargetValue = targetList.Count
                });
            }

            var minCount = Math.Min(sourceList.Count, targetList.Count);
            for (int i = 0; i < minCount; i++)
            {
                var sourceItem = sourceList[i];
                var targetItem = targetList[i];

                var indexPath = $"{path}[{i}]";

                if (sourceItem == null && targetItem == null)
                    continue;

                if (sourceItem == null || targetItem == null)
                {
                    differences.Add(new PropertyDifference
                    {
                        Path = indexPath,
                        SourceValue = sourceItem,
                        TargetValue = targetItem
                    });
                    continue;
                }

                if (sourceItem.GetType().IsValueType || sourceItem.GetType() == typeof(string))
                {
                    if (!Equals(sourceItem, targetItem))
                    {
                        differences.Add(new PropertyDifference
                        {
                            Path = indexPath,
                            SourceValue = sourceItem,
                            TargetValue = targetItem
                        });
                    }
                }
                else
                {
                    // Recursively diff collection items
                    DeepDiffInternal(sourceItem, targetItem, indexPath, differences);
                }
            }
        }
    }

    /// <summary>
    /// Represents a single property-level difference between two objects.
    /// </summary>
    public class PropertyDifference
    {
        /// <summary>
        /// The property path (e.g., "User.Address.Street" or "Items[2].Name").
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// The value from the source object.
        /// </summary>
        public object? SourceValue { get; set; }

        /// <summary>
        /// The value from the target object.
        /// </summary>
        public object? TargetValue { get; set; }

        /// <summary>
        /// Gets a string representation of this difference.
        /// </summary>
        public override string ToString()
        {
            return $"{Path}: '{SourceValue}' → '{TargetValue}'";
        }
    }
}
