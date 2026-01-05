namespace simulator_service.Infrastructure.Messaging
{

    public interface IEventPublisher
    {
        Task PublishAsync<T>(T @event, string routingKey) where T : class;
        Task PublishToQueueAsync<T>(T @event, string queueName) where T : class;
    }
}