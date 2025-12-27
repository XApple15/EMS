namespace monitoring_service.Model
{
    public class Notification
    {
        public string UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Data { get; set; }

    }
}
