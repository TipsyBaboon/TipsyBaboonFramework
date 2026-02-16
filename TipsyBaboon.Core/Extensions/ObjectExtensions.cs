using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TipsyBaboon.Core.Extensions
{
    /// <summary>
    /// Extension methods for object cloning and manipulation.
    /// Provides deep cloning functionality with circular reference handling.
    /// </summary>
    public static class ObjectExtensions
    {
        /// <summary>
        /// Creates a deep clone of an object (typed version).
        /// Handles circular references, collections, and nested objects.
        /// Value types and strings are copied directly; reference types are cloned recursively.
        /// </summary>
        /// <typeparam name="T">Type of the object to clone.</typeparam>
        /// <param name="source">Object to clone.</param>
        /// <returns>Deep clone of the source object.</returns>
        public static T Clone<T>(this T source) where T : new()
        {
            if (source == null) return default!;
            
            return (T)CloneInternal(source, new Dictionary<object, object>(new ReferenceEqualityComparer()))!;
        }

        /// <summary>
        /// Creates a deep clone of an object (untyped version).
        /// Handles circular references, collections, and nested objects.
        /// Value types and strings are copied directly; reference types are cloned recursively.
        /// </summary>
        /// <param name="source">Object to clone.</param>
        /// <returns>Deep clone of the source object.</returns>
        public static object CloneObject(this object source)
        {
            if (source == null) return null!;

            return CloneInternal(source, new Dictionary<object, object>(new ReferenceEqualityComparer()))!;
        }
        
        private static object? CloneInternal(object? source, Dictionary<object, object> visited)
        {
            if (source == null) return null;
            
            var type = source.GetType();
            
            if (type.IsValueType || type == typeof(string) || source is Type)
                return source;
            
            if (visited.ContainsKey(source))
                return visited[source];
            
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                if (type.IsArray)
                {
                    var elementType = type.GetElementType()
                        ?? throw new InvalidOperationException($"Array type '{type.Name}' has no element type");
                    var sourceArray = (Array)source;
                    var clonedArray = Array.CreateInstance(elementType, sourceArray.Length);
                    visited[source] = clonedArray;
                    
                    for (int i = 0; i < sourceArray.Length; i++)
                    {
                        clonedArray.SetValue(CloneInternal(sourceArray.GetValue(i), visited), i);
                    }
                    return clonedArray;
                }
                
                if (type.IsGenericType)
                {
                    var genericTypeDef = type.GetGenericTypeDefinition();
                    
                    if (genericTypeDef == typeof(List<>))
                    {
                        var clonedList = Activator.CreateInstance(type)
                            ?? throw new InvalidOperationException($"Failed to create instance of '{type.Name}'");
                        visited[source] = clonedList;
                        
                        var addMethod = type.GetMethod("Add")
                            ?? throw new InvalidOperationException($"'Add' method not found on type '{type.Name}'");
                        foreach (var item in (IEnumerable)source)
                        {
                            addMethod.Invoke(clonedList, new[] { CloneInternal(item, visited) });
                        }
                        return clonedList;
                    }
                    
                    if (genericTypeDef == typeof(Dictionary<,>))
                    {
                        var clonedDict = Activator.CreateInstance(type)
                            ?? throw new InvalidOperationException($"Failed to create instance of '{type.Name}'");
                        visited[source] = clonedDict;
                        
                        var addMethod = type.GetMethod("Add")
                            ?? throw new InvalidOperationException($"'Add' method not found on type '{type.Name}'");
                        var sourceDict = (IDictionary)source;
                        foreach (DictionaryEntry entry in sourceDict)
                        {
                            addMethod.Invoke(clonedDict, new[] { 
                                CloneInternal(entry.Key, visited), 
                                CloneInternal(entry.Value, visited) 
                            });
                        }
                        return clonedDict;
                    }
                }
            }
            
            var clone = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Failed to create instance of '{type.Name}'");
            visited[source] = clone;
            
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(source);
                field.SetValue(clone, CloneInternal(fieldValue, visited));
            }
            
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    var propertyValue = property.GetValue(source);
                    property.SetValue(clone, CloneInternal(propertyValue, visited));
                }
            }
            
            return clone;
        }
        
        private class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }
            
            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
