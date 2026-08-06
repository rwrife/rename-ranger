using System.Collections.Generic;

namespace RenameRanger.Core.Ai;

public sealed record AiRenameRequest(
    string OriginalFileName,
    string OriginalName,
    string Extension,
    IReadOnlyDictionary<string, string?> Metadata,
    string? TextSnippet);
