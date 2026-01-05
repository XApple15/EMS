using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;
using websocket_service.Model;

namespace websocket_service.Service
{
    public class ChatHub : Hub
    {
        private readonly IRabbitMQService _rabbitMQ;
        private readonly ILogger<ChatHub> _logger;

        private static readonly Dictionary<string, string> _userConnections = new();
        private static readonly Dictionary<string, string> _connectionToUser = new(); // Reverse lookup
        private static readonly Dictionary<string, HashSet<string>> _chatRoomConnections = new();
        private static readonly Dictionary<string, string> _userRoles = new();


        public ChatHub(IRabbitMQService rabbitMQ, ILogger<ChatHub> logger)
        {
            _rabbitMQ = rabbitMQ;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.ConnectionId;
            _logger.LogInformation("User connected: {UserId}", userId);
            await Clients.Caller.SendAsync("Connected", userId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("User disconnected: {UserId}", connectionId);

            // Clean up user connections using reverse lookup for O(1) performance
            if (_connectionToUser.TryGetValue(connectionId, out var userId))
            {
                _userConnections.Remove(userId);
                _userRoles.Remove(userId);
                _connectionToUser.Remove(connectionId);

            }

            // Clean up chat room connections
            foreach (var room in _chatRoomConnections.Values)
            {
                room.Remove(connectionId);
            }

            if (exception != null)
            {
                _logger.LogError(exception, "User disconnected with error");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendQuestion(string message)
        {
            var chatMessage = new ChatMessage
            {
                UserId = Context.ConnectionId,
                Message = message,
                Timestamp = DateTime.UtcNow,
                Type = "question"
            };

            _logger.LogInformation("Received question from {UserId}: {Message}",
                chatMessage.UserId, message);

            // Publish to RabbitMQ
            _rabbitMQ.PublishMessage("customer_questions", chatMessage);

            // Echo back to sender for immediate feedback
            await Clients.Caller.SendAsync("MessageSent", message);
        }

        // Register user ID when client connects
        public async Task RegisterUser(string userId, string role = "user")
        {
            _userConnections[userId] = Context.ConnectionId;
            _connectionToUser[Context.ConnectionId] = userId; // Add reverse lookup
            _userRoles[userId] = role.ToLower();
            _logger.LogInformation("Registered user {UserId} with connection {ConnectionId} as {Role}",
                userId, Context.ConnectionId, role);

            await Clients.Caller.SendAsync("Registered", new { userId, role, connectionId = Context.ConnectionId });
        }

        // Join a chat room
        public async Task JoinChatRoom(string chatRoomId)
        {
            if (!_chatRoomConnections.ContainsKey(chatRoomId))
            {
                _chatRoomConnections[chatRoomId] = new HashSet<string>();
            }

            _chatRoomConnections[chatRoomId].Add(Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, chatRoomId);

            _logger.LogInformation("Connection {ConnectionId} joined chat room {ChatRoomId}",
                Context.ConnectionId, chatRoomId);

            await Clients.Caller.SendAsync("JoinedChatRoom", chatRoomId);
        }

        // Leave a chat room
        public async Task LeaveChatRoom(string chatRoomId)
        {
            if (_chatRoomConnections.ContainsKey(chatRoomId))
            {
                _chatRoomConnections[chatRoomId].Remove(Context.ConnectionId);
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatRoomId);

            _logger.LogInformation("Connection {ConnectionId} left chat room {ChatRoomId}",
                Context.ConnectionId, chatRoomId);
        }

        // Send admin chat message (called from client-side, not typically used but available)
        public async Task SendAdminChatMessage(string chatRoomId, string message, string senderId, string senderRole)
        {
            var chatMessage = new AdminChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = senderId,
                SenderRole = senderRole,
                Message = message,
                Timestamp = DateTime.UtcNow,
                ChatRoomId = chatRoomId
            };

            _logger.LogInformation("Sending admin chat message from {SenderId} in room {ChatRoomId}",
                senderId, chatRoomId);

            // Send to all users in the chat room
            await Clients.Group(chatRoomId).SendAsync("ReceiveAdminChatMessage", chatMessage);

            // Confirm to sender
            await Clients.Caller.SendAsync("AdminChatMessageSent", chatMessage);
        }

        // Get connectionId by userId
        public static string? GetConnectionIdByUserId(string userId)
        {
            return _userConnections.TryGetValue(userId, out var connectionId)
                ? connectionId
                : null;
        }

        // Get user role
        public static string? GetUserRole(string userId)
        {
            return _userRoles.TryGetValue(userId, out var role)
                ? role
                : null;
        }
    }
}