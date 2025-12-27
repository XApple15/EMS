namespace device_service.Infrastructure.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(T @event, string routingKey) where T : class;
    }
}
