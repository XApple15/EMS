namespace Shared.Events
{
    public class DeviceUserCreateRequestedEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }
}