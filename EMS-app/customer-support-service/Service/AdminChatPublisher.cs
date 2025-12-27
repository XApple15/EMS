using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text.Json;
using System.Text;
using customer_support_service.Model;

namespace customer_support_service.Service
{
    public class AdminChatPublisher : IAdminChatPublisher
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly ILogger<AdminChatPublisher> _logger;
        private readonly RabbitMQSettings _settings;

        public AdminChatPublisher(
            ILogger<AdminChatPublisher> logger,
            IOptions<RabbitMQSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;

            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = _settings.HostName,
                    Port = _settings.Port,
                    UserName = _settings.UserName,
                    Password = _settings.Password
                };

                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

                // Declare admin chat messages queue
                _channel.QueueDeclareAsync(
                    queue: _settings.AdminChatQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null).GetAwaiter().GetResult();

                // Declare admin notifications queue (for new chat requests)
                _channel.QueueDeclareAsync(
                    queue: "admin_notifications",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null).GetAwaiter().GetResult();

                _logger.LogInformation("Admin Chat Publisher: Connected to RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public async Task PublishMessageAsync(AdminChatMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    Headers = new Dictionary<string, object>
                    {
                        { "MessageType", "ChatMessage" },
                        { "ChatRoomId", message.ChatRoomId },
                        { "SenderId", message.SenderId },
                        { "ReceiverId", message.ReceiverId ??  "null" }
                    }
                };

                await _channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: _settings.AdminChatQueue,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published admin chat message {MessageId} from {SenderId} to {ReceiverId} in room {ChatRoomId}",
                    message.MessageId, message.SenderId, message.ReceiverId, message.ChatRoomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish admin chat message");
                throw;
            }
        }

        public async Task PublishNewChatNotification(ChatSession session)
        {
            try
            {
                // Create notification for admins about new chat request
                var notification = new
                {
                    Type = "NewChatRequest",
                    ChatRoomId = session.ChatRoomId,
                    ClientId = session.ClientId,
                    InitialMessage = session.LastMessage,
                    Timestamp = session.StartedAt
                };

                var json = JsonSerializer.Serialize(notification);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    Headers = new Dictionary<string, object>
                    {
                        { "NotificationType", "NewChatRequest" },
                        { "ChatRoomId", session.ChatRoomId },
                        { "ClientId", session.ClientId }
                    }
                };

                await _channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "admin_notifications",
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published new chat notification for room {ChatRoomId} from client {ClientId}",
                    session.ChatRoomId, session.ClientId);

                // Also send notification to all online admins via WebSocket
                // This will be picked up by the WebSocket service
                await PublishAdminNotificationToWebSocket(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish new chat notification");
                throw;
            }
        }

        private async Task PublishAdminNotificationToWebSocket(ChatSession session)
        {
            try
            {
                // Send to notifications queue that WebSocket service listens to
                var notification = new Notification
                {
                    UserId = "ALL_ADMINS", // Special identifier for broadcasting to all admins
                    Title = "New Chat Request",
                    Message = $"User {session.ClientId} is requesting support",
                    Type = "info",
                    Timestamp = DateTime.UtcNow,
                    Data = new Dictionary<string, string>
                    {
                        { "ChatRoomId", session.ChatRoomId },
                        { "ClientId", session. ClientId },
                        { "InitialMessage", session.LastMessage ??  "" }
                    }
                };

                var json = JsonSerializer.Serialize(notification);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                // Publish to the notifications queue that WebSocket service consumes
                await _channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "admin_notifications",
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published WebSocket notification for new chat {ChatRoomId}",
                    session.ChatRoomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish WebSocket notification");
            }
        }

        public void Dispose()
        {
            try
            {
                _channel?.CloseAsync().GetAwaiter().GetResult();
                _connection?.CloseAsync().GetAwaiter().GetResult();
                _channel?.Dispose();
                _connection?.Dispose();
                _logger.LogInformation("Admin Chat Publisher: RabbitMQ connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing RabbitMQ connection");
            }
        }
    }
}