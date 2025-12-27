using customer_support_service.Model;
using customer_support_service.Service;
using Microsoft.AspNetCore.Mvc;

namespace customer_support_service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminChatController : ControllerBase
    {
        private readonly IAdminChatService _chatService;
        private readonly ILogger<AdminChatController> _logger;

        public AdminChatController(IAdminChatService chatService, ILogger<AdminChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        // User initiates chat
        [HttpPost("initiate")]
        public async Task<IActionResult> InitiateChat([FromBody] InitiateChatRequest request)
        {
            try
            {
                var session = await _chatService.InitiateChatFromUser(request.UserId, request.InitialMessage);
                return Ok(new { success = true, session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating chat");
                return StatusCode(500, new { success = false, message = "Failed to initiate chat" });
            }
        }

        // Get active chat for user
        [HttpGet("active/{userId}")]
        public async Task<IActionResult> GetActiveChat(string userId)
        {
            try
            {
                var session = await _chatService.GetActiveChatForUser(userId);
                if (session == null)
                {
                    return Ok(new { success = true, session = (ChatSession)null });
                }

                return Ok(new { success = true, session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active chat");
                return StatusCode(500, new { success = false, message = "Failed to get active chat" });
            }
        }

        // Admin starts chat with user
        [HttpPost("start")]
        public async Task<IActionResult> StartChat([FromBody] StartChatRequest request)
        {
            try
            {
                var session = await _chatService.StartChatSession(
                    request.AdminId,
                    request.ClientId,
                    request.InitialMessage);

                return Ok(new { success = true, session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat");
                return StatusCode(500, new { success = false, message = "Failed to start chat" });
            }
        }

        // Admin accepts pending chat
        [HttpPost("assign/{chatRoomId}/{adminId}")]
        public async Task<IActionResult> AssignChat(string chatRoomId, string adminId)
        {
            try
            {
                var session = await _chatService.AssignChatToAdmin(chatRoomId, adminId);
                return Ok(new { success = true, session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning chat");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get pending chats (for admins)
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingChats()
        {
            try
            {
                var sessions = await _chatService.GetPendingChats();
                return Ok(new { success = true, sessions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending chats");
                return StatusCode(500, new { success = false, message = "Failed to get pending chats" });
            }
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var message = await _chatService.SendMessage(request);
                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { success = false, message = "Failed to send message" });
            }
        }

        [HttpGet("messages/{chatRoomId}")]
        public async Task<IActionResult> GetMessages(string chatRoomId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            try
            {
                var messages = await _chatService.GetMessages(chatRoomId, skip, take);
                return Ok(new { success = true, messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages");
                return StatusCode(500, new { success = false, message = "Failed to get messages" });
            }
        }

        [HttpGet("sessions/admin/{adminId}")]
        public async Task<IActionResult> GetAdminSessions(string adminId)
        {
            try
            {
                var sessions = await _chatService.GetAdminChatSessions(adminId);
                return Ok(new { success = true, sessions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin sessions");
                return StatusCode(500, new { success = false, message = "Failed to get sessions" });
            }
        }

        [HttpGet("sessions/client/{clientId}")]
        public async Task<IActionResult> GetClientSessions(string clientId)
        {
            try
            {
                var sessions = await _chatService.GetClientChatSessions(clientId);
                return Ok(new { success = true, sessions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting client sessions");
                return StatusCode(500, new { success = false, message = "Failed to get sessions" });
            }
        }

        [HttpPost("read/{chatRoomId}/{userId}")]
        public async Task<IActionResult> MarkAsRead(string chatRoomId, string userId)
        {
            try
            {
                await _chatService.MarkMessagesAsRead(chatRoomId, userId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
                return StatusCode(500, new { success = false, message = "Failed to mark as read" });
            }
        }
    }
}