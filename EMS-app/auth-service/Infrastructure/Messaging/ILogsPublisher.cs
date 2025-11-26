namespace auth_service.Infrastructure.Messaging
{
    /// <summary>
    /// Interface for publishing log messages to the "logs" fanout exchange
    /// following the official RabbitMQ .NET tutorial pattern.
    /// </summary>
    public interface ILogsPublisher
    {
        /// <summary>
        /// Publishes a log message to the "logs" fanout exchange.
        /// The message is broadcast to all bound queues.
        /// </summary>
        /// <param name="message">The log message to publish</param>
        /// <returns>Task representing the async operation</returns>
        Task PublishLogAsync(string message);
    }
}
