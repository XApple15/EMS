using customer_support_service.Model;

namespace customer_support_service.Service
{
    public interface ISupportAgent
    {
        Task<ChatMessage> ProcessQuestion(ChatMessage question);

    }
}
