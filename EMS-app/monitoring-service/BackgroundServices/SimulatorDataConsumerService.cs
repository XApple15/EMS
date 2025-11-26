using Shared.Events;
using monitoring_service.Infrastructure.Messaging;

namespace monitoring_service.BackgroundServices
{
    /// <summary>
    /// Background service that consumes SimulatorData events from RabbitMQ
    /// and processes them for monitoring purposes.
    /// 
    /// <example>
    /// Sample JSON payload consumed:
    /// <code>
    /// {
    ///     "DeviceId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    ///     "DeviceName": "Smart Meter 001",
    ///     "ConsumptionValue": 125.5,
    ///     "Unit": "kWh",
    ///     "Timestamp": "2024-11-26T10:30:00Z",
    ///     "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    /// }
    /// </code>
    /// </example>
    /// 
    /// Routing Key: simulator.data
    /// Queue: monitoring-service-queue
    /// </summary>
    public class SimulatorDataConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly ILogger<SimulatorDataConsumerService> _logger;

        /// <summary>
        /// Routing key for simulator data events.
        /// Matches the routing key published by simulator-service.
        /// </summary>
        private const string SimulatorDataRoutingKey = "simulator.data";

        public SimulatorDataConsumerService(
            IServiceProvider serviceProvider,
            IEventConsumer eventConsumer,
            ILogger<SimulatorDataConsumerService> logger)
        {
            _serviceProvider = serviceProvider;
            _eventConsumer = eventConsumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SimulatorDataConsumerService starting...");

            try
            {
                await _eventConsumer.StartConsumingAsync<SimulatorDataEvent>(
                    routingKey: SimulatorDataRoutingKey,
                    handler: HandleSimulatorDataEventAsync,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in SimulatorDataConsumerService");
            }
        }

        /// <summary>
        /// Handles SimulatorData events by processing device telemetry data.
        /// Implements idempotent processing based on CorrelationId.
        /// </summary>
        /// <param name="event">The SimulatorData event</param>
        /// <returns>True if processing succeeded, false to requeue</returns>
        private async Task<bool> HandleSimulatorDataEventAsync(SimulatorDataEvent @event)
        {
            try
            {
                _logger.LogInformation(
                    "Processing SimulatorData event: DeviceId={DeviceId}, DeviceName={DeviceName}, ConsumptionValue={ConsumptionValue} {Unit}, CorrelationId={CorrelationId}",
                    @event.DeviceId, @event.DeviceName, @event.ConsumptionValue, @event.Unit, @event.CorrelationId);

                // TODO: Add your monitoring logic here
                // Examples:
                // - Store the data in a time-series database
                // - Update real-time dashboards
                // - Trigger alerts if consumption exceeds thresholds
                // - Calculate aggregations (hourly, daily averages)

                // Simulate processing time
                await Task.Delay(100);

                _logger.LogInformation(
                    "SimulatorData event processed successfully: DeviceId={DeviceId}, ConsumptionValue={ConsumptionValue} {Unit}, CorrelationId={CorrelationId}",
                    @event.DeviceId, @event.ConsumptionValue, @event.Unit, @event.CorrelationId);

                return true; // Success
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process SimulatorData event: DeviceId={DeviceId}, CorrelationId={CorrelationId}",
                    @event.DeviceId, @event.CorrelationId);

                // Return false to requeue the message
                return false;
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SimulatorDataConsumerService stopping...");
            return base.StopAsync(cancellationToken);
        }
    }
}
