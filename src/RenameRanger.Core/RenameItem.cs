using System.Collections.Generic;

namespace RenameRanger.Core;

public sealed record RenameItem(
    string OriginalName,
    string Extension,
    IReadOnlyDictionary<string, string?>? Metadata = null,
    string? DirectoryPath = null);
