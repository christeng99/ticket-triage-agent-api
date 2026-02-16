namespace SupportAgent.Api.Domain
{ 
    public enum TicketStatus
    {
        New = 0,
        Queued = 1,
        Triaging = 2,
        Triaged = 3,
        Failed = 4
    }
    public sealed class Ticket
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";

        public TicketStatus Status { get; set; } = TicketStatus.New;
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public string? Summary { get; set; }
        public string? SuggestedReply { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? TriagedAt { get; set; }
        public string? LastError { get; set; }
    }
}
