namespace Shared.Events
{
    public class UserRegisteredEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime RegisteredAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }
}
