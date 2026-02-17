using System.Text.Json;
using Microsoft.SemanticKernel;

namespace SupportAgent.Api.AgentPlugins
{
    public class PolicyPlugin
    {
        [KernelFunction("should_escalate")]
        public string ShouldEscalate(string category, string priority)
        {
            bool escalate =
                priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase) ||
                category.Equals("Security", StringComparison.OrdinalIgnoreCase) ||
                category.Equals("Billing", StringComparison.OrdinalIgnoreCase);

            return JsonSerializer.Serialize(new
            {
                escalate,
                reason = escalate ? "policy_rule_match" : "no_policy_rule_match"
            });
        }

        [KernelFunction("requires_human_approval")]
        public string RequiresHumanApproval(string priority, double? confidence = null)
        {
            bool needsHuman =
                priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase) ||
                (confidence.HasValue && confidence.Value < 0.35);

            return JsonSerializer.Serialize(new
            {
                needsHuman,
                reason = needsHuman ? "urgent_or_low_confidence" : "ok"
            });
        }
    }
}
