using load_balancer_service.Configuration;
using load_balancer_service.Infrastructure.Messaging;
using load_balancer_service.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Events;
using System.Text;
using System.Text.Json;

namespace load_balancer_service.Services
{
    /// <summary>
    /// Background service that consumes from central device data queue
    /// and distributes messages to per-replica ingest queues
    /// </summary>
    public class LoadBalancingHostedService : BackgroundService
    {
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly LoadBalancerSettings _settings;
        private readonly ReplicaSelectorFactory _selectorFactory;
        private readonly ILogger<LoadBalancingHostedService> _logger;
        private IReplicaSelector? _selector;
        private long _messagesProcessed = 0;
        private readonly Dictionary<string, long> _replicaMessageCounts = new();

        public LoadBalancingHostedService(
            IRabbitMqConnectionFactory connectionFactory,
            IOptions<LoadBalancerSettings> settings,
            ReplicaSelectorFactory selectorFactory,
            ILogger<LoadBalancingHostedService> logger)
        {
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _selectorFactory = selectorFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "LoadBalancingHostedService starting with strategy: {Strategy}, replicas: {ReplicaCount}",
                _settings.Strategy, _settings.Replicas.Count);

            // Initialize replica selector
            _selector = _selectorFactory.CreateSelector();

            // Initialize metrics tracking
            foreach (var replica in _settings.Replicas)
            {
                _replicaMessageCounts[replica.Id] = 0;
            }

            // Start metrics logging task
            _ = Task.Run(() => LogMetricsAsync(stoppingToken), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var connection = _connectionFactory.CreateConnection();
                    var channel = await connection.CreateChannelAsync();

                    // Declare exchange for routing to replicas
                    await channel.ExchangeDeclareAsync(
                        exchange: _settings.ExchangeName,
                        type: _settings.ExchangeType,
                        durable: true,
                        autoDelete: false);

                    // Declare central queue
                    await channel.QueueDeclareAsync(
                        queue: _settings.CentralQueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false);

                    // Declare and bind all replica ingest queues
                    foreach (var replica in _settings.Replicas)
                    {
                        var ingestQueue = string.Format(_settings.IngestQueuePattern, replica.Id);
                        
                        await channel.QueueDeclareAsync(
                            queue: ingestQueue,
                            durable: true,
                            exclusive: false,
                            autoDelete: false);

                        // Bind ingest queue to exchange with routing key = queue name
                        await channel.QueueBindAsync(
                            queue: ingestQueue,
                            exchange: _settings.ExchangeName,
                            routingKey: ingestQueue);

                        _logger.LogInformation("Declared and bound ingest queue: {QueueName}", ingestQueue);
                    }

                    _logger.LogInformation(
                        "Load balancer consuming from central queue: {CentralQueue}",
                        _settings.CentralQueueName);

                    // Set QoS to process one message at a time
                    await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        try
                        {
                            await HandleMessageAsync(channel, ea);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error handling message, will nack and requeue");
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                        }
                    };

                    await channel.BasicConsumeAsync(
                        queue: _settings.CentralQueueName,
                        autoAck: false,
                        consumer: consumer);

                    // Keep service running
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, stoppingToken);
                    }

                    await channel.CloseAsync();
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error in load balancing loop, retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("LoadBalancingHostedService stopped");
        }

        private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                // Deserialize to get device ID for routing
                var simulatorEvent = JsonSerializer.Deserialize<SimulatorDataEvent>(message);
                
                if (simulatorEvent == null)
                {
                    _logger.LogWarning("Failed to deserialize message, rejecting");
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                // Select target replica based on device ID
                var messageKey = simulatorEvent.DeviceId.ToString();
                var selectedReplica = _selector?.SelectReplica(messageKey, _settings.Replicas);

                if (selectedReplica == null)
                {
                    _logger.LogError("No replica selected, requeueing message");
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                    return;
                }

                // Determine target ingest queue
                var targetQueue = string.Format(_settings.IngestQueuePattern, selectedReplica.Id);

                // Publish to replica's ingest queue via exchange
                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                await channel.BasicPublishAsync(
                    exchange: _settings.ExchangeName,
                    routingKey: targetQueue,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                // Acknowledge original message
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                // Update metrics
                Interlocked.Increment(ref _messagesProcessed);
                lock (_replicaMessageCounts)
                {
                    _replicaMessageCounts[selectedReplica.Id]++;
                }

                _logger.LogInformation(
                    "Routed message to replica {ReplicaId} (queue: {Queue}): DeviceId={DeviceId}, CorrelationId={CorrelationId}",
                    selectedReplica.Id, targetQueue, simulatorEvent.DeviceId, simulatorEvent.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to route message: {Message}", message);
                throw;
            }
        }

        private async Task LogMetricsAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                    var totalMessages = Interlocked.Read(ref _messagesProcessed);
                    _logger.LogInformation(
                        "Load balancer metrics: Total messages processed: {TotalMessages}",
                        totalMessages);

                    lock (_replicaMessageCounts)
                    {
                        foreach (var kvp in _replicaMessageCounts)
                        {
                            var percentage = totalMessages > 0 
                                ? (kvp.Value * 100.0 / totalMessages) 
                                : 0;
                            
                            _logger.LogInformation(
                                "  Replica {ReplicaId}: {MessageCount} messages ({Percentage:F2}%)",
                                kvp.Key, kvp.Value, percentage);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics logging task");
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "LoadBalancingHostedService stopping. Total messages processed: {TotalMessages}",
                _messagesProcessed);
            return base.StopAsync(cancellationToken);
        }
    }
}
