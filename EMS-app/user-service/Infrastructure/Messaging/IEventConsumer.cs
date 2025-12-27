namespace user_service.Infrastructure.Messaging
{

    public interface IEventConsumer
    {
        Task StartConsumingAsync<T>(
            string routingKey,
            Func<T, Task<bool>> handler,
            CancellationToken cancellationToken) where T : class;
    }
}
