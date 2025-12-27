using Microsoft.AspNetCore.SignalR;
using websocket_service.Model;
using websocket_service.Service;


namespace websocket_service.Service
{
    public class ChatHub : Hub
    {
        private readonly IRabbitMQService _rabbitMQ;
        private readonly ILogger<ChatHub> _logger;

        private static readonly Dictionary<string, string> _userConnections = new();


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
            _logger.LogInformation("User disconnected: {UserId}", Context.ConnectionId);
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
        public async Task RegisterUser(string userId)
        {
            _userConnections[userId] = Context.ConnectionId;
            _logger.LogInformation("Registered user {UserId} with connection {ConnectionId}",
                userId, Context.ConnectionId);
        }

        // Get connectionId by userId
        public static string? GetConnectionIdByUserId(string userId)
        {
            return _userConnections.TryGetValue(userId, out var connectionId)
                ? connectionId
                : null;
        }
    }
}