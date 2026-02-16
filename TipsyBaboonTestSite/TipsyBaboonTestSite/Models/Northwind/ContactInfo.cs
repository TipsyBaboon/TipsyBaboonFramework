using System.Text.Json.Serialization;

namespace Models.Northwind
{
    public class ContactInfo
    {
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("fax")]
        public string? Fax { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }
    }
}
