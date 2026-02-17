using System.Text.Json;
using OpenAI.Chat;
using SupportAgent.Api.Domain;

namespace SupportAgent.Api.AI
{
    public class OpenRouterTicketTriageService : ITicketTriageService
    {
        private readonly ChatClient _chat;
        private readonly ILogger<OpenRouterTicketTriageService> _logger;
        public OpenRouterTicketTriageService(ChatClient chat, ILogger<OpenRouterTicketTriageService> logger)
        {
            _chat = chat;
            _logger = logger;
        }


        public async Task<TriageResult> TriageAsync(Ticket ticket, CancellationToken ct)
        {
            var prompt = $"""
You are an enterprise support triage assistant.

Return ONLY valid JSON (no markdown, no backticks), with exactly these keys:
- category
- priority  (one of: Low, Medium, High, Urgent)
- summary
- suggested_reply

Rules:
- category should be short like: Billing, Bug, Access, Integration, Performance, Other
- summary <= 2 sentences
- suggested_reply should be friendly and actionable

Ticket:
Title: {ticket.Title}
Description: {ticket.Description}
""";

            List<ChatMessage> messages = [
                new UserChatMessage(prompt)
            ];

            ChatCompletion completion = await _chat.CompleteChatAsync(messages, cancellationToken: ct);

            var raw = completion.Content[0].Text;
            _logger.LogInformation("Model raw triage ouput: {Raw}", raw);
            using JsonDocument doct = JsonDocument.Parse(raw);

            string category = doct.RootElement.GetProperty("category").GetString() ?? "Other";
            string priority = doct.RootElement.GetProperty("priority").GetString() ?? "Medium";
            string summary = doct.RootElement.GetProperty("summary").GetString() ?? "";
            string suggestedReply = doct.RootElement.GetProperty("suggested_reply").GetString() ?? "";

            // Determine if human approval is required
            bool requiresHumanApproval = DetermineIfHumanApprovalRequired(category, priority);

            return new TriageResult(category, priority, summary, suggestedReply, requiresHumanApproval);
        }

        private bool DetermineIfHumanApprovalRequired(string category, string priority)
        {
            // Sensitive categories always require human approval
            bool isSensitiveCategory = !string.IsNullOrWhiteSpace(category) &&
                (category.Contains("breach", StringComparison.OrdinalIgnoreCase) ||
                 category.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                 category.Contains("security", StringComparison.OrdinalIgnoreCase));

            if (isSensitiveCategory)
                return true;

            // Urgent priority requires human approval
            return priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase);
        }
    }
}
