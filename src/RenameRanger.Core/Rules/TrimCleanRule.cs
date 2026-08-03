using System;
using System.Text.RegularExpressions;

namespace RenameRanger.Core.Rules;

public sealed class TrimCleanRule : IRenameRule
{
    private static readonly Regex BracketedTagRegex = new(@"\[[^\]]*\]|\([^\)]*\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SeparatorRegex = new(@"[\s_\-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public TrimCleanRule(
        bool collapseWhitespace = true,
        bool stripBracketedTags = true,
        bool normalizeSeparators = true,
        string normalizedSeparator = " ")
    {
        CollapseWhitespace = collapseWhitespace;
        StripBracketedTags = stripBracketedTags;
        NormalizeSeparators = normalizeSeparators;
        NormalizedSeparator = string.IsNullOrEmpty(normalizedSeparator) ? " " : normalizedSeparator;
    }

    public bool CollapseWhitespace { get; }

    public bool StripBracketedTags { get; }

    public bool NormalizeSeparators { get; }

    public string NormalizedSeparator { get; }

    public string Apply(RenameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var current = ctx.CurrentName;

        if (StripBracketedTags)
        {
            current = BracketedTagRegex.Replace(current, " ");
        }

        if (NormalizeSeparators)
        {
            current = SeparatorRegex.Replace(current, NormalizedSeparator);
        }

        if (CollapseWhitespace && !string.Equals(NormalizedSeparator, " ", StringComparison.Ordinal))
        {
            current = Regex.Replace(current, @"\s+", " ", RegexOptions.CultureInvariant);
        }

        return current.Trim();
    }
}
