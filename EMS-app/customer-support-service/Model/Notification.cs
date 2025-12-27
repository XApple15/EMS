namespace customer_support_service.Model
{
    public class Notification
    {
        public string UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; } // info, success, warning, error
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string>? Data { get; set; } // Additional metadata
    }
}