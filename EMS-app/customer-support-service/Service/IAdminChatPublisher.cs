using customer_support_service.Model;

namespace customer_support_service.Service
{
    public interface IAdminChatPublisher
    {
        Task PublishMessageAsync(AdminChatMessage message);
        Task PublishNewChatNotification(ChatSession session);

    }
}
