using RabbitMQ.Client;

namespace simulator_service.Infrastructure.Messaging
{

    public interface IRabbitMqConnectionFactory
    {
        IConnection CreateConnection();
    }
}