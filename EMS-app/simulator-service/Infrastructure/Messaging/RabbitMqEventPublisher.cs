using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace simulator_service.Infrastructure.Messaging
{
    /// <summary>
    /// RabbitMQ implementation of event publisher with retry logic and error handling
    /// </summary>
    public class RabbitMqEventPublisher : IEventPublisher
    {
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqEventPublisher> _logger;

        public RabbitMqEventPublisher(
            IRabbitMqConnectionFactory connectionFactory,
            IOptions<RabbitMqSettings> settings,
            ILogger<RabbitMqEventPublisher> logger)
        {
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Publishes an event to RabbitMQ with retry logic
        /// </summary>
        /// <typeparam name="T">Type of event to publish</typeparam>
        /// <param name="event">Event object to publish</param>
        /// <param name="routingKey">Routing key for topic-based routing</param>
        public async Task PublishAsync<T>(T @event, string routingKey) where T : class
        {
            int retryCount = 0;
            Exception? lastException = null;

            while (retryCount < _settings.MaxRetryAttempts)
            {
                try
                {
                    var connection = _connectionFactory.CreateConnection();
                    using var channel = await connection.CreateChannelAsync();

                    // Declare exchange
                    await channel.ExchangeDeclareAsync(
                        exchange: _settings.ExchangeName,
                        type: _settings.ExchangeType,
                        durable: true,
                        autoDelete: false);

                    // Serialize event to JSON
                    var message = JsonSerializer.Serialize(@event);
                    var body = Encoding.UTF8.GetBytes(message);

                    // Create persistent message properties
                    var properties = new BasicProperties
                    {
                        Persistent = true,
                        ContentType = "application/json",
                        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    };

                    // Publish message
                    await channel.BasicPublishAsync(
                        exchange: _settings.ExchangeName,
                        routingKey: routingKey,
                        mandatory: false,
                        basicProperties: properties,
                        body: body);

                    _logger.LogInformation(
                        "Published event {EventType} with routing key {RoutingKey} to exchange {Exchange}",
                        typeof(T).Name, routingKey, _settings.ExchangeName);

                    return; // Success
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    retryCount++;

                    _logger.LogWarning(ex,
                        "Failed to publish event (attempt {RetryCount}/{MaxRetries}): {EventType}",
                        retryCount, _settings.MaxRetryAttempts, typeof(T).Name);

                    if (retryCount < _settings.MaxRetryAttempts)
                    {
                        var delay = _settings.RetryDelayMilliseconds * retryCount; // Exponential backoff
                        await Task.Delay(delay);
                    }
                }
            }

            _logger.LogError(lastException,
                "Failed to publish event after {MaxRetries} attempts: {EventType}",
                _settings.MaxRetryAttempts, typeof(T).Name);

            throw new InvalidOperationException(
                $"Failed to publish event after {_settings.MaxRetryAttempts} attempts", lastException);
        }
    }
}
