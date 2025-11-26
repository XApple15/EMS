using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace device_service.Infrastructure.Messaging
{
    /// <summary>
    /// RabbitMQ implementation of logs consumer using a fanout exchange.
    /// Follows the official RabbitMQ .NET tutorial pattern for publish/subscribe.
    /// Uses server-named queues and auto-acknowledgment.
    /// </summary>
    public class RabbitMqLogsConsumer : ILogsConsumer
    {
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqLogsConsumer> _logger;

        public RabbitMqLogsConsumer(
            IRabbitMqConnectionFactory connectionFactory,
            IOptions<RabbitMqSettings> settings,
            ILogger<RabbitMqLogsConsumer> logger)
        {
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Starts consuming log messages from the "logs" fanout exchange.
        /// Uses a server-named exclusive queue bound to the exchange with empty routing key.
        /// Messages are auto-acknowledged as per the official tutorial pattern.
        /// </summary>
        /// <param name="handler">Handler function to process received log messages</param>
        /// <param name="cancellationToken">Cancellation token for stopping consumption</param>
        public async Task StartConsumingAsync(
            Func<string, Task> handler,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var connection = _connectionFactory.CreateConnection();
                    var channel = await connection.CreateChannelAsync();

                    // Declare fanout exchange for logs
                    await channel.ExchangeDeclareAsync(
                        exchange: _settings.LogsExchangeName,
                        type: ExchangeType.Fanout);

                    // Declare a server-named queue (exclusive, auto-delete)
                    QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
                    string queueName = queueDeclareResult.QueueName;

                    // Bind queue to fanout exchange with empty routing key
                    await channel.QueueBindAsync(
                        queue: queueName,
                        exchange: _settings.LogsExchangeName,
                        routingKey: string.Empty);

                    _logger.LogInformation(
                        "Logs consumer started: Queue={Queue}, Exchange={Exchange}",
                        queueName, _settings.LogsExchangeName);

                    // Create async consumer
                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        byte[] body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        try
                        {
                            _logger.LogInformation("Received log message: {Message}", message);
                            await handler(message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing log message: {Message}", message);
                        }
                        // No manual ack needed - using autoAck: true
                    };

                    // Start consuming with auto-acknowledgment
                    await channel.BasicConsumeAsync(
                        queue: queueName,
                        autoAck: true,
                        consumer: consumer);

                    // Keep the consumer running until cancellation
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }

                    await channel.CloseAsync();
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error in logs consumer loop, retrying in 5 seconds...");
                    await Task.Delay(5000, cancellationToken);
                }
            }

            _logger.LogInformation("Logs consumer stopped");
        }
    }
}
