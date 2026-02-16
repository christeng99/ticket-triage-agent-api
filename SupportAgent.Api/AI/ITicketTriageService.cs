using SupportAgent.Api.Domain;

namespace SupportAgent.Api.AI
{
    public interface ITicketTriageService
    {
        Task<TriageResult> TriageAsync(Ticket ticket, CancellationToken ct);
    }
}
