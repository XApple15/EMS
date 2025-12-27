using websocket_service.Model;

namespace websocket_service.Service
{
    public interface IRabbitMQService : IDisposable
    {
        void PublishMessage(string queueName, ChatMessage message);
    }
}
