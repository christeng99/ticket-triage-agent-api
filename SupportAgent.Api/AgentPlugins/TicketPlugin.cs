using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SupportAgent.Api.Data;
using SupportAgent.Api.Domain;

namespace SupportAgent.Api.AgentPlugins
{
    public sealed class TicketPlugin
    {
        private readonly AppDbContext _db;
        private readonly ILogger<TicketPlugin> _logger;

        public TicketPlugin(AppDbContext db, ILogger<TicketPlugin> logger)
        {
            _db = db;
            _logger = logger;
        }

        [KernelFunction("get_ticket")]
        public async Task<string> GetTicketAsync(Guid ticketId)
        {
            var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId);
            if (t is null) return "{}";

            return JsonSerializer.Serialize(new
            {
                t.Id,
                t.Title,
                t.Description,
                Status = t.Status.ToString(),
                t.CreatedAt
            });
        }

        [KernelFunction("set_ticket_status")]
        public async Task<string> SetTicketStatusAsync(Guid ticketId, string status, string? note = null)
        {
            _logger.LogInformation("Setting ticket status: TicketId={TicketId}, Status={Status}", ticketId, status);

            var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId);
            if (t is null) return """{"ok":false,"error":"not_found"}""";

            if (Enum.TryParse<TicketStatus>(status, true, out var parsed))
                t.Status = parsed;

            if (!string.IsNullOrWhiteSpace(note))
                t.AgentNotes = note;

            await _db.SaveChangesAsync();
            return """{"ok":true}""";
        }

        [KernelFunction("save_triage_state")]
        public async Task<string> SaveTriageStateAsync(
            Guid ticketId,
            string category,
            string priority,
            string summary,
            string suggestedReply,
            double? confidence = null)
        {

            var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId);
            if (t is null) return """{"ok":false,"error":"not_found"}""";

            t.Category = category;
            t.Priority = priority;
            t.Summary = summary;
            t.SuggestedReply = suggestedReply;
            t.Confidence = confidence;
            t.TriagedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            return """{"ok":true}""";
        }


    }
}
