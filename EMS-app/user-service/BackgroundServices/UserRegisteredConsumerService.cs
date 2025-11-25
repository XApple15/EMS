using Microsoft.EntityFrameworkCore;
using Shared.Events;
using user_service.Data;
using user_service.Infrastructure.Messaging;
using user_service.Model;

namespace user_service.BackgroundServices
{
    /// <summary>
    /// Background service that consumes UserRegistered events from RabbitMQ
    /// and publishes DeviceUserCreateRequested events for device-service
    /// </summary>
    public class UserRegisteredConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<UserRegisteredConsumerService> _logger;

        /// <summary>
        /// Routing key used for publishing device user creation events.
        /// Device-service consumes events with this routing key.
        /// </summary>
        private const string DeviceUserRoutingKey = "user.created.device";

        public UserRegisteredConsumerService(
            IServiceProvider serviceProvider,
            IEventConsumer eventConsumer,
            IEventPublisher eventPublisher,
            ILogger<UserRegisteredConsumerService> logger)
        {
            _serviceProvider = serviceProvider;
            _eventConsumer = eventConsumer;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("UserRegisteredConsumerService starting...");

            try
            {
                await _eventConsumer.StartConsumingAsync<UserRegisteredEvent>(
                    routingKey: "user.registered",
                    handler: HandleUserRegisteredEventAsync,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in UserRegisteredConsumerService");
            }
        }

        /// <summary>
        /// Handles UserRegistered events by creating user profiles
        /// </summary>
        /// <param name="event">The UserRegistered event</param>
        /// <returns>True if processing succeeded, false to requeue</returns>
        private async Task<bool> HandleUserRegisteredEventAsync(UserRegisteredEvent @event)
        {
            try
            {
                _logger.LogInformation(
                    "Processing UserRegistered event: UserId={UserId},  CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<UserDButils>();
                _logger.LogInformation("Before checking existing user");
                // Check if user already exists (idempotency)
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.AuthId.ToString() == @event.UserId);
                _logger.LogInformation("After check existing user");
                if (existingUser != null)
                {
                    _logger.LogInformation(
                        "User already exists, skipping: UserId={UserId}, CorrelationId={CorrelationId}",
                        @event.UserId, @event.CorrelationId);
                    return true; // Already processed
                }

                // Create new user profile
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    AuthId = Guid.Parse(@event.UserId),
                    Username = @event.Username,
                    Address = @event.Address
                };

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "User profile created successfully: UserId={UserId}, ProfileId={ProfileId}, CorrelationId={CorrelationId}",
                    @event.UserId, user.Id, @event.CorrelationId);

                // Publish event for device-service after successful DB commit
                // This ensures the user exists in user-service before device-service processes
                await PublishDeviceUserCreateRequestedEventAsync(@event, user);

                return true; // Success
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process UserRegistered event: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);

                // Return false to requeue the message
                return false;
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("UserRegisteredConsumerService stopping...");
            return base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Publishes DeviceUserCreateRequested event for device-service to create a user record
        /// </summary>
        /// <param name="originalEvent">The original UserRegistered event</param>
        /// <param name="user">The created user profile</param>
        /// <remarks>
        /// Sample JSON payload published:
        /// <code>
        /// {
        ///     "UserId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
        ///     "Username": "johndoe",
        ///     "Address": "123 Main St",
        ///     "CreatedAt": "2024-11-25T10:30:00Z",
        ///     "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
        /// }
        /// </code>
        /// </remarks>
        private async Task PublishDeviceUserCreateRequestedEventAsync(UserRegisteredEvent originalEvent, User user)
        {
            try
            {
                var deviceUserEvent = new DeviceUserCreateRequestedEvent
                {
                    UserId = user.AuthId.ToString(),
                    Username = user.Username,
                    Address = user.Address,
                    CreatedAt = DateTime.UtcNow,
                    CorrelationId = originalEvent.CorrelationId
                };

                await _eventPublisher.PublishAsync(deviceUserEvent, DeviceUserRoutingKey);

                _logger.LogInformation(
                    "Published DeviceUserCreateRequested event: UserId={UserId}, RoutingKey={RoutingKey}, CorrelationId={CorrelationId}",
                    deviceUserEvent.UserId, DeviceUserRoutingKey, deviceUserEvent.CorrelationId);
            }
            catch (Exception ex)
            {
                // Log but don't fail the original operation - device-service can recover via retry or manual sync
                _logger.LogError(ex,
                    "Failed to publish DeviceUserCreateRequested event: UserId={UserId}, CorrelationId={CorrelationId}",
                    user.AuthId, originalEvent.CorrelationId);
            }
        }
    }
}
