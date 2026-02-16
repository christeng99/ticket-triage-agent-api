namespace SupportAgent.Api.AI
{
    public sealed record TriageResult (
        string Category,
        string Priority,
        string Summary,
        string SuggestedReply
    );
}
