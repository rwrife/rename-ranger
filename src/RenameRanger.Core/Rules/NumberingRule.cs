using System;
using System.Globalization;

namespace RenameRanger.Core.Rules;

public enum NumberingPlacement
{
    Prefix,
    Suffix,
}

public sealed class NumberingRule : IRenameRule
{
    public NumberingRule(
        int start = 1,
        int step = 1,
        int padWidth = 0,
        string prefix = "",
        string suffix = "",
        NumberingPlacement placement = NumberingPlacement.Suffix)
    {
        if (step == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step cannot be 0.");
        }

        if (padWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(padWidth));
        }

        Start = start;
        Step = step;
        PadWidth = padWidth;
        Prefix = prefix ?? string.Empty;
        Suffix = suffix ?? string.Empty;
        Placement = placement;
    }

    public int Start { get; }

    public int Step { get; }

    public int PadWidth { get; }

    public string Prefix { get; }

    public string Suffix { get; }

    public NumberingPlacement Placement { get; }

    public string Apply(RenameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var value = Start + (ctx.Index * Step);
        var formatted = PadWidth > 0
            ? value.ToString($"D{PadWidth}", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

        var token = $"{Prefix}{formatted}{Suffix}";

        return Placement == NumberingPlacement.Prefix
            ? token + ctx.CurrentName
            : ctx.CurrentName + token;
    }
}
