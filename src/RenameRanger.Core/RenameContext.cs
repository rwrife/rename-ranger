using System.Collections.Generic;

namespace RenameRanger.Core;

public sealed record RenameContext(
    string OriginalName,
    string Extension,
    int Index,
    IReadOnlyDictionary<string, string?> Metadata,
    string CurrentName)
{
    public RenameContext(
        string originalName,
        string extension,
        int index,
        IReadOnlyDictionary<string, string?>? metadata = null)
        : this(
            originalName,
            extension,
            index,
            metadata ?? new Dictionary<string, string?>(),
            originalName)
    {
    }

    public RenameContext WithCurrentName(string currentName) =>
        this with { CurrentName = currentName };
}
