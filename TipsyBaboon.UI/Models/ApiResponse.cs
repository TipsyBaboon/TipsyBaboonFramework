namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// Request payload for the generic batch API endpoint, carrying inserts, updates, and deletes in a single round-trip.
    /// </summary>
    public class BatchRequest
    {
        public List<System.Text.Json.JsonElement>? Inserts { get; set; }

        public List<System.Text.Json.JsonElement>? Updates { get; set; }

        public List<Guid>? Deletes { get; set; }
    }

    /// <summary>
    /// Response payload from the generic batch API endpoint, reporting counts and any per-operation errors.
    /// </summary>
    public class BatchResult
    {
        public int Inserted { get; set; }

        public int Updated { get; set; }

        public int Deleted { get; set; }

        public List<string> Errors { get; set; } = new();
    }
}
