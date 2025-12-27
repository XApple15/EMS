namespace websocket_service.Model
{
    public class AdminChatMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; }
        public string SenderRole { get; set; } // "admin", "user", or "system"
        public string? ReceiverId { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public string ChatRoomId { get; set; }
        public bool IsRead { get; set; }
    }
}
