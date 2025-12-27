using Microsoft.EntityFrameworkCore;
using Shared.Events;
using user_service.Data;
using user_service.Infrastructure.Messaging;
using user_service.Model;

namespace user_service.BackgroundServices
{
    public class UserRegisteredConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<UserRegisteredConsumerService> _logger;

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
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.AuthId.ToString() == @event.UserId);
                _logger.LogInformation("After check existing user");
                if (existingUser != null)
                {
                    _logger.LogInformation(
                        "User already exists, skipping: UserId={UserId}, CorrelationId={CorrelationId}",
                        @event.UserId, @event.CorrelationId);
                    return true;
                }

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

                await PublishDeviceUserCreateRequestedEventAsync(@event, user);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process UserRegistered event: UserId={UserId}, CorrelationId={CorrelationId}",
                    @event.UserId, @event.CorrelationId);

                return false;
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("UserRegisteredConsumerService stopping...");
            return base.StopAsync(cancellationToken);
        }

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
                _logger.LogError(ex,
                    "Failed to publish DeviceUserCreateRequested event: UserId={UserId}, CorrelationId={CorrelationId}",
                    user.AuthId, originalEvent.CorrelationId);
            }
        }
    }
}