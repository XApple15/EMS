using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace monitoring_service.Infrastructure.Messaging
{
    /// <summary>
    /// RabbitMQ implementation of event consumer with message acknowledgment and error handling
    /// </summary>
    public class RabbitMqEventConsumer : IEventConsumer
    {
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqEventConsumer> _logger;

        public RabbitMqEventConsumer(
            IRabbitMqConnectionFactory connectionFactory,
            IOptions<RabbitMqSettings> settings,
            ILogger<RabbitMqEventConsumer> logger)
        {
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Starts consuming messages from RabbitMQ with manual acknowledgment
        /// </summary>
        /// <typeparam name="T">Type of event to consume</typeparam>
        /// <param name="routingKey">Routing key pattern to bind to</param>
        /// <param name="handler">Handler function that returns true on success, false to requeue</param>
        /// <param name="cancellationToken">Cancellation token for stopping consumption</param>
        public async Task StartConsumingAsync<T>(
            string routingKey,
            Func<T, Task<bool>> handler,
            CancellationToken cancellationToken) where T : class
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var connection = _connectionFactory.CreateConnection();
                    var channel = await connection.CreateChannelAsync();

                    // Declare exchange
                    await channel.ExchangeDeclareAsync(
                        exchange: _settings.ExchangeName,
                        type: _settings.ExchangeType,
                        durable: true,
                        autoDelete: false);

                    // Declare queue
                    await channel.QueueDeclareAsync(
                        queue: _settings.QueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false);

                    // Bind queue to exchange with routing key
                    await channel.QueueBindAsync(
                        queue: _settings.QueueName,
                        exchange: _settings.ExchangeName,
                        routingKey: routingKey);

                    _logger.LogInformation(
                        "Consumer started: Queue={Queue}, Exchange={Exchange}, RoutingKey={RoutingKey}",
                        _settings.QueueName, _settings.ExchangeName, routingKey);

                    // Set prefetch count to 1 for fair dispatch
                    await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

                    // Create consumer
                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        try
                        {
                            _logger.LogInformation(
                                "Received message: RoutingKey={RoutingKey}, DeliveryTag={DeliveryTag}",
                                ea.RoutingKey, ea.DeliveryTag);

                            // Deserialize message
                            var @event = JsonSerializer.Deserialize<T>(message);
                            if (@event == null)
                            {
                                _logger.LogWarning("Failed to deserialize message: {Message}", message);
                                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                                return;
                            }

                            // Process message
                            var success = await handler(@event);

                            if (success)
                            {
                                // Acknowledge message
                                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                                _logger.LogInformation("Message processed successfully: DeliveryTag={DeliveryTag}", 
                                    ea.DeliveryTag);
                            }
                            else
                            {
                                // Requeue message for retry
                                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                                _logger.LogWarning("Message processing failed, requeued: DeliveryTag={DeliveryTag}", 
                                    ea.DeliveryTag);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message: {Message}", message);
                            // Requeue message on error
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                        }
                    };

                    // Start consuming
                    await channel.BasicConsumeAsync(
                        queue: _settings.QueueName,
                        autoAck: false,
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
                    _logger.LogError(ex, "Error in consumer loop, retrying in 5 seconds...");
                    await Task.Delay(5000, cancellationToken);
                }
            }

            _logger.LogInformation("Consumer stopped");
        }
    }
}
