using System.Collections.Generic;

namespace RenameRanger.Core;

public sealed record RenameProposal(
    int Index,
    string OriginalName,
    string OriginalFileName,
    string ProposedName,
    string ProposedFileName,
    IReadOnlyList<string>? Errors = null)
{
    public IReadOnlyList<string> Errors { get; init; } = Errors ?? [];

    public bool HasErrors => Errors.Count > 0;
}
