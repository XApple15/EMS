using customer_support_service.Model;

namespace customer_support_service.Service
{
    public interface IRabbitMQSupportService : IDisposable
    {
        void PublishAnswer(ChatMessage answer);
    }
}
