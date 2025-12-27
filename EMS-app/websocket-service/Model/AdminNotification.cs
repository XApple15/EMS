namespace websocket_service.Model
{
    public class AdminNotification
    {
        public string Type { get; set; } // NewChatRequest, ChatAssigned, ChatClosed
        public string ChatRoomId { get; set; }
        public string ClientId { get; set; }
        public string? InitialMessage { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; }
    }
}
