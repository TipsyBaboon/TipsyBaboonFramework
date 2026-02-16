using TipsyBaboon.Core.Models;

namespace TipsyBaboon.UI.Extensions
{
    /// <summary>
    /// Extension methods for TipsyBaboon model instances used by the UI layer.
    /// </summary>
    public static class TipsyBaboonModelExtensions
    {
        /// <summary>
        /// Checks whether the object is a <see cref="TipsyBaboonModel"/> with <c>IsInvariant</c> set to <c>true</c>.
        /// Invariant records are code-locked and cannot be edited or deleted through the UI.
        /// </summary>
        /// <param name="obj">The entity to check.</param>
        /// <returns><c>true</c> if the object is an invariant record; otherwise <c>false</c>.</returns>
        public static bool IsInvariantRecord(this object? obj)
        {
            return obj is TipsyBaboonModel model && model.IsInvariant;
        }
    }
}
