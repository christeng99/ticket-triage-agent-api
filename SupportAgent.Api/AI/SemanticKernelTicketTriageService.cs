using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SupportAgent.Api.Domain;

namespace SupportAgent.Api.AI
{
    public class SemanticKernelTicketTriageService: ITicketTriageService
    {
        private readonly Kernel _kernel;

        public SemanticKernelTicketTriageService(Kernel kernel) => _kernel = kernel;

        public async Task<TriageResult> TriageAsync(Ticket ticket, CancellationToken ct)
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var history = new ChatHistory();
            history.AddSystemMessage(
"""
You are SupportAgent, an enterprise triage agent.

You MUST use tools when needed:
- ticket.get_ticket(ticketId) to read ticket data
- kb.search_kb(query) to retrieve internal hints
- policy.should_escalate(category, priority) to enforce policy
- policy.requires_human_approval(priority, confidence) to decide human review
- ticket.save_triage_result(...) to persist output
- ticket.set_ticket_status(ticketId, status, note) to update workflow state
- telemetry.log_step(ticketId, stepName, inputJson, outputJson) to create an audit log

Your goal:
1) Read the ticket
2) Produce: category, priority, summary, suggested_reply, confidence (0..1)
3) Enforce policy. If needs human approval, set status AwaitingHuman, else set Triaged.
4) Save triage result and log steps.

Return ONLY final JSON with keys:
category, priority, summary, suggested_reply, confidence
"""
            );

            history.AddUserMessage($"Triage ticketId={ticket.Id}. Use tools. Do not guess details not in the ticket.");

            var result = await chat.GetChatMessageContentAsync(history, settings, _kernel, ct);

            using var doc = JsonDocument.Parse(result.Content ?? "{}");
            var root = doc.RootElement;

            string category = root.GetProperty("category").GetString() ?? "Other";
            string priority = root.GetProperty("priority").GetString() ?? "Medium";
            string summary = root.GetProperty("summary").GetString() ?? "";
            string suggestedReply = root.GetProperty("suggested_reply").GetString() ?? "";
            double confidence = 0.5;
            
            if (root.TryGetProperty("confidence", out var confidenceElement) && 
                double.TryParse(confidenceElement.GetRawText(), out var confValue))
            {
                confidence = confValue;
            }

            // Check if human approval is required based on policy
            bool requiresHumanApproval = DetermineIfHumanApprovalRequired(category, priority, confidence);

            return new TriageResult(
                Category: category,
                Priority: priority,
                Summary: summary,
                SuggestedReply: suggestedReply,
                RequiresHumanApproval: requiresHumanApproval
            );
        }

        private bool DetermineIfHumanApprovalRequired(string category, string priority, double confidence)
        {
            // Sensitive categories always require human approval
            bool isSensitiveCategory = !string.IsNullOrWhiteSpace(category) &&
                (category.Contains("breach", StringComparison.OrdinalIgnoreCase) ||
                 category.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                 category.Contains("security", StringComparison.OrdinalIgnoreCase));

            if (isSensitiveCategory)
                return true;

            // Urgent priority requires human approval
            if (priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase))
                return true;

            // For non-sensitive categories, require human approval if confidence is low
            double confidenceThreshold = priority.Equals("High", StringComparison.OrdinalIgnoreCase) ? 0.45 :
                                         priority.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? 0.35 : 0.3;

            return confidence < confidenceThreshold;
        }
    }
}
