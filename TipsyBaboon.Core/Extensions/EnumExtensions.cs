using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace TipsyBaboon.Core.Extensions
{
    /// <summary>
    /// Extension methods for enum types.
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Converts an enum type to a dictionary of integer values to display names.
        /// Uses [Display(Name="")] attribute if present, otherwise uses the enum member name.
        /// </summary>
        /// <param name="enumType">Enum type to convert.</param>
        /// <returns>Dictionary mapping enum integer values to display names.</returns>
        /// <exception cref="ArgumentException">Thrown if the type is not an enum.</exception>
        public static Dictionary<int, string> ToDictionary(this Type enumType)
        {
            if (!enumType.IsEnum)
                throw new ArgumentException($"Type {enumType.Name} is not an enum type.", nameof(enumType));

            var result = new Dictionary<int, string>();
            
            foreach (var value in Enum.GetValues(enumType))
            {
                var intValue = Convert.ToInt32(value);
                var name = Enum.GetName(enumType, value);
                
                if (name == null) continue;
                
                var field = enumType.GetField(name);
                var displayAttribute = field?.GetCustomAttributes(typeof(DisplayAttribute), false)
                    .FirstOrDefault() as DisplayAttribute;
                
                var displayName = displayAttribute?.Name ?? name;
                result[intValue] = displayName;
            }
            
            return result;
        }
    }
}
