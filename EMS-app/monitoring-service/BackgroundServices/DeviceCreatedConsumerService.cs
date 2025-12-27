using monitoring_service.Data;
using monitoring_service.Infrastructure.Messaging;
using monitoring_service.Model;
using Shared.Events;
using System;

namespace monitoring_service.BackgroundServices
{
    public class DeviceCreatedConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly ILogger<DeviceCreatedConsumerService> _logger;

       
        private const string QueueName = "monitoring-service-device-created-queue";

       
        private const string DeviceCreatedRoutingKey = "monitoring.device.created";

        public DeviceCreatedConsumerService(
            IServiceProvider serviceProvider,
            IEventConsumer eventConsumer,
            ILogger<DeviceCreatedConsumerService> logger)
        {
            _serviceProvider = serviceProvider;
            _eventConsumer = eventConsumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DeviceCreatedConsumerService starting with queue: {QueueName}.. .", QueueName);

            try
            {
                await _eventConsumer.StartConsumingAsync<CreatedDeviceEvent>(
                    routingKey: DeviceCreatedRoutingKey,
                    handler: HandleDeviceCreatedEventAsync,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in DeviceCreatedConsumerService");
            }
        }

     
        private async Task<bool> HandleDeviceCreatedEventAsync(CreatedDeviceEvent @event)
        {
            try
            {
                string stringid = @event.id;
                if(stringid == String.Empty)
                {
                    return false;
                }
                Guid guid = Guid.Parse(stringid);
                if (guid == Guid.Empty) {
                    return false;    
                }
                _logger.LogInformation(
                    "Processing DeviceCreated event: DeviceId={Id}, CorrelationId={CorrelationId}",
                    guid, @event.CorrelationId);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MonitorDbUtils>();

                var existingDevice = await dbContext.Devices.FindAsync(@guid);
                if (existingDevice != null)
                {
                    _logger.LogInformation(
                        "Device already exists, skipping: DeviceId={Id}",
                        guid);
                    return true;
                }

                var device = new Device
                {
                    Id = guid,
                    Consumption= @event.Consumption
                };

                dbContext.Devices.Add(device);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "DeviceCreated event processed successfully: DeviceId={Id}, CorrelationId={CorrelationId}",
                    guid, @event.CorrelationId);

                return true; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process DeviceCreated event: DeviceId={Id}, CorrelationId={CorrelationId}",
                    @event.id, @event.CorrelationId);

                
                return false;
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DeviceCreatedConsumerService stopping...");
            return base.StopAsync(cancellationToken);
        }
    }
}