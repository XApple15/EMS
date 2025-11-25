using Microsoft.EntityFrameworkCore;
using Shared.Events;
using device_service.Data;
using device_service.Infrastructure.Messaging;
using device_service.Model;

namespace device_service.BackgroundServices
{
    /// <summary>
    /// Background service that consumes DeviceUserCreateRequested events from RabbitMQ
    /// and creates user records in device-service database.
    /// 
    /// <example>
    /// Sample JSON payload consumed:
    /// <code>
    /// {
    ///     "UserId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    ///     "Username": "johndoe",
    ///     "Address": "123 Main St",
    ///     "CreatedAt": "2024-11-25T10:30:00Z",
    ///     "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    /// }
    /// </code>
    /// </example>
    /// 
    /// Routing Key: user.created.device
    /// Queue: device-service-queue
    /// </summary>
    public class DeviceUserCreatedConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly ILogger<DeviceUserCreatedConsumerService> _logger;

        /// <summary>
        /// Routing key for device user creation events.
        /// Matches the routing key published by user-service.
        /// </summary>
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

        /// <summary>
        /// Handles DeviceUserCreateRequested events by creating user records in device-service DB.
        /// Implements idempotency by checking if user already exists by AuthId.
        /// </summary>
        /// <param name="event">The DeviceUserCreateRequested event</param>
        /// <returns>True if processing succeeded, false to requeue</returns>
        private async Task<bool> HandleDeviceUserCreateRequestedEventAsync(DeviceUserCreateRequestedEvent @event)
        {
            try
            {
                _logger.LogInformation(
                    "Processing DeviceUserCreateRequested event: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DeviceDButils>();

                // Idempotency check: verify user doesn't already exist
                var authId = Guid.Parse(@event.UserId);
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.AuthId == authId);

                if (existingUser != null)
                {
                    _logger.LogInformation(
                        "User already exists in device-service, skipping: UserId={UserId}, CorrelationId={CorrelationId}",
                        @event.UserId, @event.CorrelationId);
                    return true; // Already processed - acknowledge message
                }

                // Create new user in device-service
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

                return true; // Success
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Handle race condition where duplicate insert occurred
                _logger.LogWarning(
                    "Duplicate user insert attempted (likely race condition), acknowledging: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);
                return true; // Acknowledge - user already exists
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process DeviceUserCreateRequested event: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);

                // Return false to requeue the message
                return false;
            }
        }

        /// <summary>
        /// Checks if the exception is a unique constraint violation
        /// </summary>
        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // SQL Server unique constraint violation error numbers: 2601 (unique index), 2627 (unique constraint)
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
