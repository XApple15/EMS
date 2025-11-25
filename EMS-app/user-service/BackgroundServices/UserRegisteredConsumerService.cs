using Microsoft.EntityFrameworkCore;
using Shared.Events;
using user_service.Data;
using user_service.Infrastructure.Messaging;
using user_service.Model;

namespace user_service.BackgroundServices
{
    /// <summary>
    /// Background service that consumes UserRegistered events from RabbitMQ
    /// </summary>
    public class UserRegisteredConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<UserRegisteredConsumerService> _logger;

        /// <summary>
        /// Routing key for device-service user creation events
        /// </summary>
        public const string DeviceUserCreateRoutingKey = "user.device.create";

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

                // Publish DeviceUserCreateRequested event for device-service
                try
                {
                    var deviceUserEvent = new DeviceUserCreateRequested
                    {
                        UserId = user.AuthId,
                        Email = @event.Username, // Username is typically email in this system
                        Username = @event.Username,
                        Address = @event.Address,
                        RegisteredAt = @event.RegisteredAt,
                        CorrelationId = @event.CorrelationId
                    };

                    await _eventPublisher.PublishAsync(deviceUserEvent, DeviceUserCreateRoutingKey);

                    _logger.LogInformation(
                        "Published DeviceUserCreateRequested event: UserId={UserId}, CorrelationId={CorrelationId}",
                        user.AuthId, @event.CorrelationId);
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx,
                        "Failed to publish DeviceUserCreateRequested event for user {UserId}, CorrelationId={CorrelationId}. User was created but device-service notification failed.",
                        user.AuthId, @event.CorrelationId);
                    // Continue - user is created but event publishing failed
                    // The user creation should not be rolled back due to downstream publishing failure
                }

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
    }
}
