using RabbitMQ.Client;

namespace user_service.Infrastructure.Messaging
{
    public interface IRabbitMqConnectionFactory
    {

        IConnection CreateConnection();
    }
}
