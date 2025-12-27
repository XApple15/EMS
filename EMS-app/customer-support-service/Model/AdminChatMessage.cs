namespace customer_support_service.Model
{
    public class AdminChatMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; } = string.Empty; // Admin or Client userId
        public string SenderRole { get; set; } = string.Empty; // "admin" or "client"
        public string ReceiverId { get; set; } = string.Empty; // Client or Admin userId
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ChatRoomId { get; set; } = string.Empty; // Format: "admin_{adminId}_client_{clientId}"
        public bool IsRead { get; set; } = false;
    }


    public class ChatSession
    {
        public string ChatRoomId { get; set; } = string.Empty;
        public string AdminId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime LastMessageAt { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public int UnreadCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class StartChatRequest
    {
        public string AdminId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string InitialMessage { get; set; } = string.Empty;
    }

    public class SendMessageRequest
    {
        public string ChatRoomId { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class GetMessagesRequest
    {
        public string ChatRoomId { get; set; } = string.Empty;
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }

    public class InitiateChatRequest
    {
        public string UserId { get; set; }
        public string InitialMessage { get; set; }
    }
}
