namespace device_service.Infrastructure.Messaging
{
    /// <summary>
    /// Interface for consuming events from message broker
    /// </summary>
    public interface IEventConsumer
    {
        /// <summary>
        /// Starts consuming messages from the specified queue with the given routing key
        /// </summary>
        /// <typeparam name="T">Type of event to consume</typeparam>
        /// <param name="routingKey">Routing key pattern to bind to (e.g., "user.#")</param>
        /// <param name="handler">Handler function to process received events</param>
        /// <param name="cancellationToken">Cancellation token for stopping consumption</param>
        /// <returns>Task representing the async operation</returns>
        Task StartConsumingAsync<T>(
            string routingKey,
            Func<T, Task<bool>> handler,
            CancellationToken cancellationToken) where T : class;
    }
}