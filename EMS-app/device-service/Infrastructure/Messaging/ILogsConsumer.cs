namespace device_service.Infrastructure.Messaging
{
    /// <summary>
    /// Interface for consuming log messages from the "logs" fanout exchange
    /// following the official RabbitMQ .NET tutorial pattern.
    /// </summary>
    public interface ILogsConsumer
    {
        /// <summary>
        /// Starts consuming log messages from the "logs" fanout exchange.
        /// Uses a server-named queue bound to the exchange with empty routing key.
        /// Messages are auto-acknowledged.
        /// </summary>
        /// <param name="handler">Handler function to process received log messages</param>
        /// <param name="cancellationToken">Cancellation token for stopping consumption</param>
        /// <returns>Task representing the async operation</returns>
        Task StartConsumingAsync(
            Func<string, Task> handler,
            CancellationToken cancellationToken);
    }
}
