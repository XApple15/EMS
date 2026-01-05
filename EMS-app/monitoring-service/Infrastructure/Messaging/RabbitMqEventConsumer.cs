using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace monitoring_service.Infrastructure.Messaging
{

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

                    await channel.ExchangeDeclareAsync(
                        exchange: _settings.ExchangeName,
                        type: _settings.ExchangeType,
                        durable: true,
                        autoDelete: false);

                    await channel.QueueDeclareAsync(
                        queue: _settings.QueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false);

                    await channel.QueueBindAsync(
                        queue: _settings.QueueName,
                        exchange: _settings.ExchangeName,
                        routingKey: routingKey);

                    _logger.LogInformation(
                        "Consumer started: Queue={Queue}, Exchange={Exchange}, RoutingKey={RoutingKey}",
                        _settings.QueueName, _settings.ExchangeName, routingKey);

                    await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

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

                            var @event = JsonSerializer.Deserialize<T>(message);
                            if (@event == null)
                            {
                                _logger.LogWarning("Failed to deserialize message: {Message}", message);
                                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                                return;
                            }

                            var success = await handler(@event);

                            if (success)
                            {
                                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                                _logger.LogInformation("Message processed successfully: DeliveryTag={DeliveryTag}",
                                    ea.DeliveryTag);
                            }
                            else
                            {
                                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                                _logger.LogWarning("Message processing failed, requeued: DeliveryTag={DeliveryTag}",
                                    ea.DeliveryTag);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message: {Message}", message);
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                        }
                    };

                    await channel.BasicConsumeAsync(
                        queue: _settings.QueueName,
                        autoAck: false,
                        consumer: consumer);

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

        public async Task StartConsumingFromQueueAsync<T>(
            string queueName,
            Func<T, Task<bool>> handler,
            CancellationToken cancellationToken) where T : class
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var connection = _connectionFactory.CreateConnection();
                    var channel = await connection.CreateChannelAsync();

                    // Declare queue (will be created by load balancer, but this ensures it exists)
                    await channel.QueueDeclareAsync(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false);

                    _logger.LogInformation(
                        "Consumer started consuming from queue: {Queue}",
                        queueName);

                    await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        try
                        {
                            _logger.LogInformation(
                                "Received message from queue {Queue}: DeliveryTag={DeliveryTag}",
                                queueName, ea.DeliveryTag);

                            var @event = JsonSerializer.Deserialize<T>(message);
                            if (@event == null)
                            {
                                _logger.LogWarning("Failed to deserialize message: {Message}", message);
                                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                                return;
                            }

                            var success = await handler(@event);

                            if (success)
                            {
                                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                                _logger.LogInformation("Message processed successfully: DeliveryTag={DeliveryTag}",
                                    ea.DeliveryTag);
                            }
                            else
                            {
                                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                                _logger.LogWarning("Message processing failed, requeued: DeliveryTag={DeliveryTag}",
                                    ea.DeliveryTag);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message: {Message}", message);
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                        }
                    };

                    await channel.BasicConsumeAsync(
                        queue: queueName,
                        autoAck: false,
                        consumer: consumer);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }

                    await channel.CloseAsync();
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error in consumer loop for queue {Queue}, retrying in 5 seconds...", queueName);
                    await Task.Delay(5000, cancellationToken);
                }
            }

            _logger.LogInformation("Consumer stopped for queue: {Queue}", queueName);
        }
    }
}