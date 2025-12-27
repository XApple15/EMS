using Microsoft.EntityFrameworkCore;
using Shared.Events;
using device_service.Data;
using device_service.Infrastructure.Messaging;
using device_service.Model;

namespace device_service.BackgroundServices
{
     public class DeviceUserCreatedConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly ILogger<DeviceUserCreatedConsumerService> _logger;

        
        private const string DeviceUserRoutingKey = "user.created.device";

        public DeviceUserCreatedConsumerService(
            IServiceProvider serviceProvider,
            IEventConsumer eventConsumer,
            ILogger<DeviceUserCreatedConsumerService> logger)
        {
            _serviceProvider = serviceProvider;
            _eventConsumer = eventConsumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DeviceUserCreatedConsumerService starting...");

            try
            {
                await _eventConsumer.StartConsumingAsync<DeviceUserCreateRequestedEvent>(
                    routingKey: DeviceUserRoutingKey,
                    handler: HandleDeviceUserCreateRequestedEventAsync,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in DeviceUserCreatedConsumerService");
            }
        }

        private async Task<bool> HandleDeviceUserCreateRequestedEventAsync(DeviceUserCreateRequestedEvent @event)
        {
            try
            {
                _logger.LogInformation(
                    "Processing DeviceUserCreateRequested event: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DeviceDButils>();

                var authId = Guid.Parse(@event.UserId);
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.AuthId == authId);

                if (existingUser != null)
                {
                    _logger.LogInformation(
                        "User already exists in device-service, skipping: UserId={UserId}, CorrelationId={CorrelationId}",
                        @event.UserId, @event.CorrelationId);
                    return true;
                }

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    AuthId = authId,
                    Username = @event.Username,
                    Address = @event.Address
                };

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "User created successfully in device-service: UserId={UserId}, DeviceUserId={DeviceUserId}, CorrelationId={CorrelationId}",
                    @event.UserId, user.Id, @event.CorrelationId);

                return true;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogWarning(
                    "Duplicate user insert attempted (likely race condition), acknowledging: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process DeviceUserCreateRequested event: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);

                return false;
            }
        }

      
        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException?.Message?.Contains("2601") == true ||
                   ex.InnerException?.Message?.Contains("2627") == true ||
                   ex.InnerException?.Message?.Contains("unique") == true;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DeviceUserCreatedConsumerService stopping...");
            return base.StopAsync(cancellationToken);
        }
    }
}