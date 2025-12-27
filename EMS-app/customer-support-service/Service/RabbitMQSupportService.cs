using Microsoft.AspNetCore.Connections;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using customer_support_service.Model;

namespace customer_support_service.Service
{
    public class RabbitMQSupportService : IRabbitMQSupportService
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly ISupportAgent _agent;
        private readonly ILogger<RabbitMQSupportService> _logger;
        private readonly RabbitMQSettings _settings;

        public RabbitMQSupportService(
            ISupportAgent agent,
            ILogger<RabbitMQSupportService> logger,
            IOptions<RabbitMQSettings> settings)
        {
            _agent = agent;
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

                StartListening();
                _logger.LogInformation("Customer Support Service: Connected to RabbitMQ and listening for questions");
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

        private void StartListening()
        {
            _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false).GetAwaiter().GetResult();

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
                        _logger.LogInformation("Received question from user {UserId}: {Message}",
                            chatMessage.UserId, chatMessage.Message);

                        // Process the question and generate answer
                        var answer = await _agent.ProcessQuestion(chatMessage);

                        // Send answer back
                        PublishAnswer(answer);

                        // Acknowledge the message
                        await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from RabbitMQ");
                    // Reject and requeue the message
                    await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsumeAsync(
                queue: _settings.QuestionsQueue,
                autoAck: false,
                consumer: consumer).GetAwaiter().GetResult();

            _logger.LogInformation("Started consuming from queue: {Queue}", _settings.QuestionsQueue);
        }

        public void PublishAnswer(ChatMessage answer)
        {
            try
            {
                var json = JsonSerializer.Serialize(answer);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                _channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: _settings.AnswersQueue,
                    mandatory: false,
                    basicProperties: properties,
                    body: body).GetAwaiter().GetResult();

                _logger.LogInformation("Published answer to user {UserId}", answer.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish answer");
                throw;
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
                _logger.LogInformation("RabbitMQ connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing RabbitMQ connection");
            }
        }
    }
}
