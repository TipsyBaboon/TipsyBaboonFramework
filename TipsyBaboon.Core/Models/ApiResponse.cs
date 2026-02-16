using System;
using System.Collections.Generic;

namespace TipsyBaboon.Core.Models
{
    /// <summary>
    /// Standardized API response wrapper for controller actions.
    /// Provides success/failure status, optional data payload, user messages, and error details.
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// Indicates whether the operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// User-friendly message describing the result.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Optional data payload to return to the client.
        /// </summary>
        public object? Data { get; set; }

        /// <summary>
        /// List of detailed error messages (used in development or for validation errors).
        /// </summary>
        public List<string>? Errors { get; set; }

        /// <summary>
        /// Indicates whether the client should refresh the page or re-query data.
        /// </summary>
        public bool RefreshRequired { get; set; }

        /// <summary>
        /// Marks this response as requiring a client-side refresh (fluent).
        /// </summary>
        /// <returns>This ApiResponse instance for method chaining.</returns>
        public ApiResponse WithRefresh()
        {
            RefreshRequired = true;
            return this;
        }

        /// <summary>
        /// Creates a successful response with optional data and message.
        /// </summary>
        /// <param name="data">Optional data payload.</param>
        /// <param name="message">Optional success message.</param>
        /// <returns>ApiResponse with Success=true.</returns>
        public static ApiResponse Ok(object? data = null, string? message = null)
        {
            return new ApiResponse
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        /// <summary>
        /// Creates an error response with a message and optional detailed error list.
        /// </summary>
        /// <param name="message">User-friendly error message.</param>
        /// <param name="errors">Optional list of detailed error strings.</param>
        /// <returns>ApiResponse with Success=false.</returns>
        public static ApiResponse Error(string message, List<string>? errors = null)
        {
            return new ApiResponse
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }

        /// <summary>
        /// Creates an error response from an exception.
        /// In development mode, includes exception details and stack trace.
        /// In production, only includes the user-friendly message.
        /// </summary>
        /// <param name="userMessage">User-friendly error message.</param>
        /// <param name="ex">Optional exception for detailed error info.</param>
        /// <param name="isDevelopment">Whether to include exception details (development mode).</param>
        /// <returns>ApiResponse with Success=false and optional exception details.</returns>
        public static ApiResponse FromException(string userMessage, Exception? ex = null, bool isDevelopment = false)
        {
            if (!isDevelopment || ex == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = userMessage
                };
            }

            var errors = new List<string>
            {
                $"Exception Type: {ex.GetType().FullName}",
                $"Message: {ex.Message}"
            };

            if (ex.InnerException != null)
            {
                errors.Add($"Inner Exception: {ex.InnerException.GetType().FullName}");
                errors.Add($"Inner Message: {ex.InnerException.Message}");
            }

            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                errors.Add($"Stack Trace: {ex.StackTrace}");
            }

            return new ApiResponse
            {
                Success = false,
                Message = userMessage,
                Errors = errors
            };
        }
    }
}
