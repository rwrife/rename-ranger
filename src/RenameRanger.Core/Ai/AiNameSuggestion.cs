namespace RenameRanger.Core.Ai;

public sealed record AiNameSuggestion(
    string SuggestedName,
    bool UsedFallback,
    string? FailureReason = null)
{
    public static AiNameSuggestion Fallback(string fallbackName, string? reason = null) =>
        new(fallbackName, UsedFallback: true, reason);
}
