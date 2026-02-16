using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Writers;
using SupportAgent.Api.AI;
using SupportAgent.Api.Data;
using SupportAgent.Api.Domain;

namespace SupportAgent.Api.Orchestration
{
    public class TicketTriageWorker: BackgroundService
    {
        private readonly Channel<Guid> _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TicketTriageWorker> _logger;

        public TicketTriageWorker(Channel<Guid> queue, IServiceScopeFactory scopeFactory, ILogger<TicketTriageWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TicketTriageWorker started.");

            await foreach (Guid ticketId in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                _logger.LogInformation("Dequeued ticket {TicketId}", ticketId);

                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var triage = scope.ServiceProvider.GetRequiredService<ITicketTriageService>();

                    Ticket? ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, stoppingToken);
                    if (ticket is null)
                    {
                        _logger.LogWarning("Ticket {TicketId} not found.", ticketId);
                        continue;
                    }

                    ticket.Status = TicketStatus.Triaging;
                    ticket.LastError = null;
                    await db.SaveChangesAsync(stoppingToken);

                    TriageResult result = await triage.TriageAsync(ticket, stoppingToken);
                    ticket.Category = result.Category;
                    ticket.Priority = result.Priority;
                    ticket.Summary = result.Summary;
                    ticket.SuggestedReply = result.SuggestedReply;
                    ticket.Status = TicketStatus.Triaged;
                    ticket.TriagedAt = DateTimeOffset.UtcNow;

                    await db.SaveChangesAsync();
                    _logger.LogInformation("Ticket {TicketId} triaged successfully.", ticketId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed triaging ticket {TicketId}", ticketId);

                    try
                    {
                        using IServiceScope scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        Ticket? ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, stoppingToken);
                        if (ticket is not null)
                        {
                            ticket.Status = TicketStatus.Failed;
                            ticket.LastError = ex.Message;
                            await db.SaveChangesAsync(stoppingToken);
                        }
                    }
                    catch
                    {

                    }
                }
                
            }
        }
    }
}
