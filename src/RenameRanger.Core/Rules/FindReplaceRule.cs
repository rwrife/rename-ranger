using System;
using System.Text;

namespace RenameRanger.Core.Rules;

public sealed class FindReplaceRule : IRenameRule
{
    public FindReplaceRule(string findText, string replaceText, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(findText))
        {
            throw new ArgumentException("Find text cannot be null or empty.", nameof(findText));
        }

        FindText = findText;
        ReplaceText = replaceText ?? string.Empty;
        CaseSensitive = caseSensitive;
    }

    public string FindText { get; }

    public string ReplaceText { get; }

    public bool CaseSensitive { get; }

    public string Apply(RenameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        return CaseSensitive
            ? ctx.CurrentName.Replace(FindText, ReplaceText, StringComparison.Ordinal)
            : ReplaceOrdinalIgnoreCase(ctx.CurrentName, FindText, ReplaceText);
    }

    private static string ReplaceOrdinalIgnoreCase(string input, string findText, string replaceText)
    {
        if (findText.Length == 0)
        {
            return input;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;
        var currentIndex = 0;
        var matchIndex = input.IndexOf(findText, comparison);

        if (matchIndex < 0)
        {
            return input;
        }

        var result = new StringBuilder(input.Length);

        while (matchIndex >= 0)
        {
            result.Append(input, currentIndex, matchIndex - currentIndex);
            result.Append(replaceText);

            currentIndex = matchIndex + findText.Length;
            matchIndex = input.IndexOf(findText, currentIndex, comparison);
        }

        result.Append(input, currentIndex, input.Length - currentIndex);
        return result.ToString();
    }
}
