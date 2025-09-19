namespace cas_cdrrestore_service.Models
{
    public class RestoreResponse
    {
        public string CallId { get; set; } = string.Empty;
        public Dictionary<string, string> RestoreOutput { get; set; } = [];
    }
}
