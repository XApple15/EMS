using Microsoft.EntityFrameworkCore;
using Shared.Events;
using device_service.Data;
using device_service.Infrastructure.Messaging;
using device_service.Model;

namespace device_service.BackgroundServices
{
    /// <summary>
    /// Background service that consumes DeviceUserCreateRequested events from RabbitMQ
    /// and creates user records in the device-service database
    /// </summary>
    public class DeviceUserCreatedConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly ILogger<DeviceUserCreatedConsumerService> _logger;

        /// <summary>
        /// Routing key for device user creation events (must match user-service publisher)
        /// </summary>
        public const string DeviceUserCreateRoutingKey = "user.device.create";

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
                await _eventConsumer.StartConsumingAsync<DeviceUserCreateRequested>(
                    routingKey: DeviceUserCreateRoutingKey,
                    handler: HandleDeviceUserCreateRequestedAsync,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in DeviceUserCreatedConsumerService");
            }
        }

        /// <summary>
        /// Handles DeviceUserCreateRequested events by creating user records in device-service database.
        /// This handler is idempotent - duplicate events will not create duplicate records.
        /// </summary>
        /// <param name="event">The DeviceUserCreateRequested event</param>
        /// <returns>True if processing succeeded, false to requeue</returns>
        private async Task<bool> HandleDeviceUserCreateRequestedAsync(DeviceUserCreateRequested @event)
        {
            try
            {
                _logger.LogInformation(
                    "Processing DeviceUserCreateRequested event: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);

                // Validate payload
                if (@event.UserId == Guid.Empty)
                {
                    _logger.LogWarning(
                        "Invalid DeviceUserCreateRequested event: UserId is empty. CorrelationId={CorrelationId}",
                        @event.CorrelationId);
                    return true; // Ack the message to avoid retrying invalid data
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DeviceDButils>();

                // Check if user already exists (idempotency check on AuthId)
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.AuthId == @event.UserId);

                if (existingUser != null)
                {
                    _logger.LogInformation(
                        "User already exists in device-service, skipping: UserId={UserId}, CorrelationId={CorrelationId}",
                        @event.UserId, @event.CorrelationId);
                    return true; // Already processed - idempotent
                }

                // Create new user record
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    AuthId = @event.UserId,
                    Username = @event.Username,
                    Address = @event.Address
                };

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "User created in device-service: UserId={UserId}, ProfileId={ProfileId}, CorrelationId={CorrelationId}",
                    @event.UserId, user.Id, @event.CorrelationId);

                return true; // Success
            }
            catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
            {
                // Handle race condition where duplicate insert was attempted
                _logger.LogInformation(
                    "User already exists (concurrent insert): UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);
                return true; // Already exists - idempotent
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
        /// Determines if the exception is due to a unique constraint violation
        /// </summary>
        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // SQL Server unique constraint violation error number is 2627 or 2601
            return ex.InnerException?.Message?.Contains("UNIQUE") == true ||
                   ex.InnerException?.Message?.Contains("duplicate") == true ||
                   ex.InnerException?.Message?.Contains("2627") == true ||
                   ex.InnerException?.Message?.Contains("2601") == true;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DeviceUserCreatedConsumerService stopping...");
            return base.StopAsync(cancellationToken);
        }
    }
}
