namespace monitoring_service.Infrastructure.Messaging
{
    public interface IEventConsumer
    {
        Task StartConsumingAsync<TEvent>(
            string routingKey,
            Func<TEvent, Task<bool>> handler,
            CancellationToken cancellationToken) where TEvent : class;
    }
}