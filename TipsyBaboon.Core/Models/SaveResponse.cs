namespace TipsyBaboon.Core.Models
{
    /// <summary>
    /// Response object returned from IModelWithSaveAction.BeforeChangeAsync hooks.
    /// Indicates whether the save operation should continue, has already been applied, or encountered an error.
    /// </summary>
    public class SaveResponse
    {
        /// <summary>
        /// Type of response indicating the save operation status (Continue, ChangeApplied, or Error).
        /// </summary>
        public SaveResponseType ResponseType { get; set; }
        
        /// <summary>
        /// Optional message providing additional details about the response.
        /// Used primarily for error messages or user notifications.
        /// </summary>
        public string? Message { get; set; }
    }
}