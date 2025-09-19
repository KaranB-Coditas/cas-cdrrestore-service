using System.Text.Json.Serialization;

namespace cas_cdrrestore_service.Models
{
    public class RestoreRequest
    {
        [JsonPropertyName("callDate")]
        public DateTime CallDate { get; set; }
        [JsonPropertyName("callId")]
        public string CallId { get; set; } = string.Empty;
    }
}
