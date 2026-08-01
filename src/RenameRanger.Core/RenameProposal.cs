namespace RenameRanger.Core;

public sealed record RenameProposal(
    int Index,
    string OriginalName,
    string OriginalFileName,
    string ProposedName,
    string ProposedFileName);
