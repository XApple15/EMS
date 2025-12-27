namespace customer_support_service.Model
{
    public class ChatMessage
    {
        public string UserId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = "question";
    }
}
