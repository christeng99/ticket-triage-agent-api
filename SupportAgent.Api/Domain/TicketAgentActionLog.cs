namespace SupportAgent.Api.Domain
{
    public class TicketAgentActionLog
    {
        public long Id { get; set; }
        public Guid TicketId { get; set; }
        public string StepName { get; set; } = "";
        public string? InputJson { get; set; }
        public string? OutputJson { get; set; }
        public bool Success { get; set; } = true;
        public string? Error { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    }
}
