using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using websocket_service.Model;

namespace websocket_service.Service
{
    public class RabbitMQService : IRabbitMQService
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly RabbitMQSettings _settings;

        public RabbitMQService(
            IHubContext<ChatHub> hubContext,
            ILogger<RabbitMQService> logger,
            IOptions<RabbitMQSettings> settings)
        {
            _hubContext = hubContext;
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

                // Declare queues
                DeclareQueue(_settings.QuestionsQueue);
                DeclareQueue(_settings.AnswersQueue);
                DeclareQueue(_settings.NotificationsQueue);
                DeclareQueue(_settings.AdminChatQueue);
                DeclareQueue(_settings.AdminNotificationsQueue);

                StartListening();
                StartListeningForNotifications();
                StartListeningForAdminChatMessages();
                StartListeningForAdminNotifications();
                _logger.LogInformation("WebSocket Service: Connected to RabbitMQ and listening for answers, notifications, and admin chat messages");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        private void DeclareQueue(string queueName)
        {
            _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null).GetAwaiter().GetResult();

            _logger.LogInformation("Declared queue: {QueueName}", queueName);
        }

        public void PublishMessage(string queueName, ChatMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                _channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body).GetAwaiter().GetResult();

                _logger.LogInformation("Published to {Queue}: {Message}", queueName, message.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to {Queue}", queueName);
                throw;
            }
        }

        private void StartListening()
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var chatMessage = JsonSerializer.Deserialize<ChatMessage>(message);

                    if (chatMessage != null)
                    {
                        _logger.LogInformation("Received answer for user {UserId}: {Message}",
                            chatMessage.UserId, chatMessage.Message);

                        // Send answer to specific user via SignalR
                        await _hubContext.Clients.Client(chatMessage.UserId)
                            .SendAsync("ReceiveAnswer", chatMessage.Message, chatMessage.Timestamp);

                        _logger.LogInformation("Sent answer to user {UserId} via SignalR", chatMessage.UserId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from RabbitMQ");
                }
            };

            _channel.BasicConsumeAsync(
                queue: _settings.AnswersQueue,
                autoAck: true,
                consumer: consumer).GetAwaiter().GetResult();

            _logger.LogInformation("Started consuming from queue: {Queue}", _settings.AnswersQueue);
        }

        private void StartListeningForNotifications()
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var notification = JsonSerializer.Deserialize<Notification>(message);

                    if (notification != null)
                    {
                        _logger.LogInformation("Received notification for user {UserId}: {Title}",
                            notification.UserId, notification.Title);

                        // Send notification to specific user via SignalR
                        var connectionId = ChatHub.GetConnectionIdByUserId(notification.UserId);

                        if (connectionId != null)
                        {
                            await _hubContext.Clients.Client(connectionId)
                                .SendAsync("ReceiveNotification", notification);
                            _logger.LogInformation("Sent notification to user {UserId} via SignalR with connection ID: {connID}", notification.UserId, connectionId);

                        }
                        else
                        {
                            _logger.LogWarning("No connection ID found for user {UserId}, notification not sent", notification.UserId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing notification from RabbitMQ");
                }
            };

            _channel.BasicConsumeAsync(
                queue: _settings.NotificationsQueue,
                autoAck: true,
                consumer: consumer).GetAwaiter().GetResult();

            _logger.LogInformation("Started consuming notifications from queue: {Queue}", _settings.NotificationsQueue);
        }

        private void StartListeningForAdminChatMessages()
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var chatMessage = JsonSerializer.Deserialize<AdminChatMessage>(message);

                    if (chatMessage != null)
                    {
                        _logger.LogInformation("Received admin chat message {MessageId} for room {ChatRoomId}",
                            chatMessage.MessageId, chatMessage.ChatRoomId);

                        // Send to all users in the chat room
                        await _hubContext.Clients.Group(chatMessage.ChatRoomId)
                            .SendAsync("ReceiveAdminChatMessage", chatMessage);

                        _logger.LogInformation("Broadcasted admin chat message to room {ChatRoomId}", chatMessage.ChatRoomId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing admin chat message from RabbitMQ");
                }
            };

            _channel.BasicConsumeAsync(
                queue: _settings.AdminChatQueue,
                autoAck: true,
                consumer: consumer).GetAwaiter().GetResult();

            _logger.LogInformation("Started consuming admin chat messages from queue: {Queue}", _settings.AdminChatQueue);
        }

        private void StartListeningForAdminNotifications()
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // Try to deserialize as Notification first
                    var notification = JsonSerializer.Deserialize<Notification>(message);

                    if (notification != null && notification.UserId == "ALL_ADMINS")
                    {
                        _logger.LogInformation("Received admin notification: {Title}", notification.Title);

                        // Broadcast to all connected admins
                        // We'll send to all connections and let the client filter
                        await _hubContext.Clients.All.SendAsync("ReceiveAdminNotification", notification);

                        _logger.LogInformation("Broadcasted admin notification to all connected admins");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing admin notification from RabbitMQ");
                }
            };

            _channel.BasicConsumeAsync(
                queue: _settings.AdminNotificationsQueue,
                autoAck: true,
                consumer: consumer).GetAwaiter().GetResult();

            _logger.LogInformation("Started consuming admin notifications from queue: {Queue}", _settings.AdminNotificationsQueue);
        }

        public void Dispose()
        {
            try
            {
                _channel?.CloseAsync().GetAwaiter().GetResult();
                _connection?.CloseAsync().GetAwaiter().GetResult();
                _channel?.Dispose();
                _connection?.Dispose();
                _logger.LogInformation("RabbitMQ connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing RabbitMQ connection");
            }
        }
    }
}