using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace simulator_service.Infrastructure.Messaging
{

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

                    await channel.ExchangeDeclareAsync(
                        exchange: _settings.ExchangeName,
                        type: _settings.ExchangeType,
                        durable: true,
                        autoDelete: false);

                    var message = JsonSerializer.Serialize(@event);
                    var body = Encoding.UTF8.GetBytes(message);

                    var properties = new BasicProperties
                    {
                        Persistent = true,
                        ContentType = "application/json",
                        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    };

                    await channel.BasicPublishAsync(
                        exchange: _settings.ExchangeName,
                        routingKey: routingKey,
                        mandatory: false,
                        basicProperties: properties,
                        body: body);

                    _logger.LogInformation(
                        "Published event {EventType} with routing key {RoutingKey} to exchange {Exchange}",
                        typeof(T).Name, routingKey, _settings.ExchangeName);

                    return;
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
                        var delay = _settings.RetryDelayMilliseconds * retryCount;
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

        public async Task PublishToQueueAsync<T>(T @event, string queueName) where T : class
        {
            int retryCount = 0;
            Exception? lastException = null;

            while (retryCount < _settings.MaxRetryAttempts)
            {
                try
                {
                    var connection = _connectionFactory.CreateConnection();
                    using var channel = await connection.CreateChannelAsync();

                    // Declare the queue
                    await channel.QueueDeclareAsync(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false);

                    var message = JsonSerializer.Serialize(@event);
                    var body = Encoding.UTF8.GetBytes(message);

                    var properties = new BasicProperties
                    {
                        Persistent = true,
                        ContentType = "application/json",
                        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    };

                    // Publish directly to queue (use default exchange with routingKey = queueName)
                    await channel.BasicPublishAsync(
                        exchange: string.Empty,
                        routingKey: queueName,
                        mandatory: false,
                        basicProperties: properties,
                        body: body);

                    _logger.LogInformation(
                        "Published event {EventType} to queue {QueueName}",
                        typeof(T).Name, queueName);

                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    retryCount++;

                    _logger.LogWarning(ex,
                        "Failed to publish event to queue (attempt {RetryCount}/{MaxRetries}): {EventType}",
                        retryCount, _settings.MaxRetryAttempts, typeof(T).Name);

                    if (retryCount < _settings.MaxRetryAttempts)
                    {
                        var delay = _settings.RetryDelayMilliseconds * retryCount;
                        await Task.Delay(delay);
                    }
                }
            }

            _logger.LogError(lastException,
                "Failed to publish event to queue after {MaxRetries} attempts: {EventType}",
                _settings.MaxRetryAttempts, typeof(T).Name);

            throw new InvalidOperationException(
                $"Failed to publish event to queue after {_settings.MaxRetryAttempts} attempts", lastException);
        }
    }
}