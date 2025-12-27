using customer_support_service.Model;

namespace customer_support_service.Service
{
    public interface IAdminChatService
    {
        Task<ChatSession> InitiateChatFromUser(string userId, string initialMessage);
        Task<ChatSession> StartChatSession(string adminId, string clientId, string initialMessage);
        Task<AdminChatMessage> SendMessage(SendMessageRequest request);
        Task<List<AdminChatMessage>> GetMessages(string chatRoomId, int skip = 0, int take = 50);
        Task<List<ChatSession>> GetAdminChatSessions(string adminId);
        Task<List<ChatSession>> GetClientChatSessions(string clientId);
        Task<List<ChatSession>> GetPendingChats();
        Task<ChatSession> AssignChatToAdmin(string chatRoomId, string adminId);
        Task MarkMessagesAsRead(string chatRoomId, string userId);
        Task<ChatSession?> GetActiveChatForUser(string userId);
    }
}
