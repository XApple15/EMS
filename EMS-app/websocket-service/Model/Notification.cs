namespace websocket_service.Model
{
    public class Notification
    {
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info"; // info, warning, error, success
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }
}
