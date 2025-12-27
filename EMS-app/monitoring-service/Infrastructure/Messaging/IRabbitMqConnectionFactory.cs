using RabbitMQ.Client;

namespace monitoring_service.Infrastructure.Messaging
{
    public interface IRabbitMqConnectionFactory
    {
        IConnection CreateConnection();
    }
}