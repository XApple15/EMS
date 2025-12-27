using RabbitMQ.Client;

namespace device_service.Infrastructure.Messaging
{
    public interface IRabbitMqConnectionFactory
    {

        IConnection CreateConnection();
    }
}