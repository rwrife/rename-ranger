namespace RenameRanger.Core;

public interface IRenameRule
{
    string Apply(RenameContext ctx);
}
