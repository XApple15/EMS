using customer_support_service.Model;
using System.Collections.Concurrent;

namespace customer_support_service.Service
{
    public class AdminChatService : IAdminChatService
    {
        private readonly IAdminChatPublisher _publisher;
        private readonly ILogger<AdminChatService> _logger;

        private static readonly ConcurrentDictionary<string, List<AdminChatMessage>> _messages = new();
        private static readonly ConcurrentDictionary<string, ChatSession> _sessions = new();
        private static readonly Queue<ChatSession> _pendingChats = new();
        private static readonly object _queueLock = new();

        public AdminChatService(IAdminChatPublisher publisher, ILogger<AdminChatService> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public async Task<ChatSession> InitiateChatFromUser(string userId, string initialMessage)
        {
            // Check if user already has an active chat
            var existingChat = GetActiveChatForUser(userId).Result;
            if (existingChat != null)
            {
                _logger.LogInformation("User {UserId} already has an active chat:  {ChatRoomId}",
                    userId, existingChat.ChatRoomId);
                return existingChat;
            }

            // Create a new chat session without admin (pending)
            var chatRoomId = $"chat_{userId}_{Guid.NewGuid().ToString().Substring(0, 8)}";

            var session = new ChatSession
            {
                ChatRoomId = chatRoomId,
                AdminId = null, // No admin assigned yet
                ClientId = userId,
                StartedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                LastMessage = initialMessage,
                IsActive = true,
                UnreadCount = 0
            };

            _sessions[chatRoomId] = session;

            // Add to pending queue
            lock (_queueLock)
            {
                _pendingChats.Enqueue(session);
            }

            // Store initial message
            if (!string.IsNullOrWhiteSpace(initialMessage))
            {
                var message = new AdminChatMessage
                {
                    SenderId = userId,
                    SenderRole = "user",
                    ReceiverId = null, // No receiver yet
                    Message = initialMessage,
                    Timestamp = DateTime.UtcNow,
                    ChatRoomId = chatRoomId
                };

                await StoreMessage(message);
            }

            _logger.LogInformation("User {UserId} initiated chat {ChatRoomId}, waiting for admin",
                userId, chatRoomId);

            // Notify admins about new pending chat
            await _publisher.PublishNewChatNotification(session);

            return session;
        }

        public async Task<ChatSession> AssignChatToAdmin(string chatRoomId, string adminId)
        {
            if (!_sessions.TryGetValue(chatRoomId, out var session))
            {
                throw new Exception("Chat session not found");
            }

           

            session.AdminId = adminId;
            session.LastMessageAt = DateTime.UtcNow;

            // Remove from pending queue
            lock (_queueLock)
            {
                var newQueue = new Queue<ChatSession>(_pendingChats.Where(s => s.ChatRoomId != chatRoomId));
                _pendingChats.Clear();
                foreach (var item in newQueue)
                {
                    _pendingChats.Enqueue(item);
                }
            }

            // Notify user that admin joined
            var notification = new AdminChatMessage
            {
                SenderId = "system",
                SenderRole = "system",
                ReceiverId = session.ClientId,
                Message = "An admin has joined the chat",
                Timestamp = DateTime.UtcNow,
                ChatRoomId = chatRoomId
            };

            await StoreAndPublishMessage(notification);

            _logger.LogInformation("Admin {AdminId} assigned to chat {ChatRoomId}", adminId, chatRoomId);

            return session;
        }

        public Task<ChatSession?> GetActiveChatForUser(string userId)
        {
            var activeChat = _sessions.Values
                .FirstOrDefault(s => s.ClientId == userId && s.IsActive);

            return Task.FromResult(activeChat);
        }

        public Task<List<ChatSession>> GetPendingChats()
        {
            lock (_queueLock)
            {
                return Task.FromResult(_pendingChats.ToList());
            }
        }

        public async Task<ChatSession> StartChatSession(string adminId, string clientId, string initialMessage)
        {
            var chatRoomId = $"admin_{adminId}_client_{clientId}";

            var session = new ChatSession
            {
                ChatRoomId = chatRoomId,
                AdminId = adminId,
                ClientId = clientId,
                StartedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                LastMessage = initialMessage,
                IsActive = true
            };

            _sessions[chatRoomId] = session;

            if (!string.IsNullOrWhiteSpace(initialMessage))
            {
                var message = new AdminChatMessage
                {
                    SenderId = adminId,
                    SenderRole = "admin",
                    ReceiverId = clientId,
                    Message = initialMessage,
                    Timestamp = DateTime.UtcNow,
                    ChatRoomId = chatRoomId
                };

                await StoreAndPublishMessage(message);
            }

            _logger.LogInformation("Started chat session {ChatRoomId} between admin {AdminId} and client {ClientId}",
                chatRoomId, adminId, clientId);

            return session;
        }

        public async Task<AdminChatMessage> SendMessage(SendMessageRequest request)
        {
            var message = new AdminChatMessage
            {
                SenderId = request.SenderId,
                SenderRole = request.SenderRole,
                ReceiverId = GetReceiverId(request.ChatRoomId, request.SenderId),
                Message = request.Message,
                Timestamp = DateTime.UtcNow,
                ChatRoomId = request.ChatRoomId
            };

            await StoreAndPublishMessage(message);

            // Update session
            if (_sessions.TryGetValue(request.ChatRoomId, out var session))
            {
                session.LastMessage = request.Message;
                session.LastMessageAt = DateTime.UtcNow;

                // Increment unread count for receiver
                if (request.SenderRole == "admin")
                {
                    session.UnreadCount++;
                }
            }

            return message;
        }

        private async Task StoreMessage(AdminChatMessage message)
        {
            if (!_messages.ContainsKey(message.ChatRoomId))
            {
                _messages[message.ChatRoomId] = new List<AdminChatMessage>();
            }
            _messages[message.ChatRoomId].Add(message);
        }

        private async Task StoreAndPublishMessage(AdminChatMessage message)
        {
            await StoreMessage(message);
            await _publisher.PublishMessageAsync(message);

            _logger.LogInformation("Stored and published message {MessageId} in room {ChatRoomId}",
                message.MessageId, message.ChatRoomId);
        }

        public Task<List<AdminChatMessage>> GetMessages(string chatRoomId, int skip = 0, int take = 50)
        {
            if (_messages.TryGetValue(chatRoomId, out var messages))
            {
                var result = messages
                    .OrderByDescending(m => m.Timestamp)
                    .Skip(skip)
                    .Take(take)
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                return Task.FromResult(result);
            }

            return Task.FromResult(new List<AdminChatMessage>());
        }

        public Task<List<ChatSession>> GetAdminChatSessions(string adminId)
        {
            var sessions = _sessions.Values
                .Where(s => s.AdminId == adminId && s.IsActive)
                .OrderByDescending(s => s.LastMessageAt)
                .ToList();

            return Task.FromResult(sessions);
        }

        public Task<List<ChatSession>> GetClientChatSessions(string clientId)
        {
            var sessions = _sessions.Values
                .Where(s => s.ClientId == clientId && s.IsActive)
                .OrderByDescending(s => s.LastMessageAt)
                .ToList();

            return Task.FromResult(sessions);
        }

        public Task MarkMessagesAsRead(string chatRoomId, string userId)
        {
            if (_messages.TryGetValue(chatRoomId, out var messages))
            {
                foreach (var message in messages.Where(m => m.ReceiverId == userId && !m.IsRead))
                {
                    message.IsRead = true;
                }
            }

            if (_sessions.TryGetValue(chatRoomId, out var session))
            {
                session.UnreadCount = 0;
            }

            return Task.CompletedTask;
        }

        private string GetReceiverId(string chatRoomId, string senderId)
        {
            if (_sessions.TryGetValue(chatRoomId, out var session))
            {
                return senderId == session.AdminId ? session.ClientId : session.AdminId;
            }

            return string.Empty;
        }
    }
}
