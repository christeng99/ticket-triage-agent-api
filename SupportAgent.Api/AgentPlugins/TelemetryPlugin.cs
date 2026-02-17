using Microsoft.SemanticKernel;
using SupportAgent.Api.Data;
using SupportAgent.Api.Domain;

namespace SupportAgent.Api.AgentPlugins
{
    public class TelemetryPlugin
    {
        private readonly AppDbContext _db;
        public TelemetryPlugin(AppDbContext db) => _db = db;

        [KernelFunction("log_step")]
        public async Task<string> LogAsyncStep(Guid ticketId, string stepName, string? inputJson = null, string? outputJson = null)
        {
            _db.TicketAgentActionLogs.Add(new TicketAgentActionLog
            {
                TicketId = ticketId,
                StepName = stepName,
                InputJson = inputJson,
                OutputJson = outputJson,
                Success = true
            });

            await _db.SaveChangesAsync();
            return """{"ok":true}""";
        }
    }
}
