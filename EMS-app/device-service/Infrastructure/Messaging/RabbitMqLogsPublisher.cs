using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;

namespace device_service.Infrastructure.Messaging
{
    /// <summary>
    /// RabbitMQ implementation of logs publisher using a fanout exchange.
    /// Follows the official RabbitMQ .NET tutorial pattern for publish/subscribe.
    /// </summary>
    public class RabbitMqLogsPublisher : ILogsPublisher
    {
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqLogsPublisher> _logger;

        public RabbitMqLogsPublisher(
            IRabbitMqConnectionFactory connectionFactory,
            IOptions<RabbitMqSettings> settings,
            ILogger<RabbitMqLogsPublisher> logger)
        {
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Publishes a log message to the "logs" fanout exchange.
        /// Uses empty routing key as per fanout exchange pattern.
        /// </summary>
        /// <param name="message">The log message to publish</param>
        public async Task PublishLogAsync(string message)
        {
            int retryCount = 0;
            Exception? lastException = null;

            while (retryCount < _settings.MaxRetryAttempts)
            {
                try
                {
                    var connection = _connectionFactory.CreateConnection();
                    using var channel = await connection.CreateChannelAsync();

                    // Declare fanout exchange for logs
                    await channel.ExchangeDeclareAsync(
                        exchange: _settings.LogsExchangeName,
                        type: ExchangeType.Fanout);

                    var body = Encoding.UTF8.GetBytes(message);

                    // Publish to fanout exchange with empty routing key
                    await channel.BasicPublishAsync(
                        exchange: _settings.LogsExchangeName,
                        routingKey: string.Empty,
                        body: body);

                    _logger.LogInformation(
                        "Published log message to fanout exchange {Exchange}: {Message}",
                        _settings.LogsExchangeName, message);

                    return; // Success
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    retryCount++;

                    _logger.LogWarning(ex,
                        "Failed to publish log message (attempt {RetryCount}/{MaxRetries})",
                        retryCount, _settings.MaxRetryAttempts);

                    if (retryCount < _settings.MaxRetryAttempts)
                    {
                        var delay = _settings.RetryDelayMilliseconds * retryCount;
                        await Task.Delay(delay);
                    }
                }
            }

            _logger.LogError(lastException,
                "Failed to publish log message after {MaxRetries} attempts",
                _settings.MaxRetryAttempts);

            throw new InvalidOperationException(
                $"Failed to publish log message after {_settings.MaxRetryAttempts} attempts", lastException);
        }
    }
}
