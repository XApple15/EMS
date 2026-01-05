using RabbitMQ.Client;

namespace load_balancer_service.Infrastructure.Messaging
{
    /// <summary>
    /// Factory interface for creating RabbitMQ connections
    /// </summary>
    public interface IRabbitMqConnectionFactory
    {
        /// <summary>
        /// Creates a new RabbitMQ connection
        /// </summary>
        IConnection CreateConnection();
    }
}
