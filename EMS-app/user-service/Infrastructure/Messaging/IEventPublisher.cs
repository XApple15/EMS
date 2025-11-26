namespace user_service.Infrastructure.Messaging
{
    /// <summary>
    /// Interface for publishing events to message broker
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes an event to the message broker
        /// </summary>
        /// <typeparam name="T">Type of event to publish</typeparam>
        /// <param name="event">Event object to publish</param>
        /// <param name="routingKey">Routing key for topic-based routing</param>
        /// <returns>Task representing the async operation</returns>
        Task PublishAsync<T>(T @event, string routingKey) where T : class;
    }
}