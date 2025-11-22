using RabbitMQ.Client;

namespace user_service.Infrastructure.Messaging
{
    /// <summary>
    /// Factory for creating and managing RabbitMQ connections
    /// </summary>
    public interface IRabbitMqConnectionFactory
    {
        /// <summary>
        /// Creates a new RabbitMQ connection
        /// </summary>
        /// <returns>A new IConnection instance</returns>
        IConnection CreateConnection();
    }
}
