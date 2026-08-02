using System;
using System.Text.RegularExpressions;

namespace RenameRanger.Core.Rules;

public sealed class RegexRule : IRenameRule, IRuleIssueSource
{
    private readonly Regex? _regex;

    public RegexRule(
        string pattern,
        string replacement,
        bool caseSensitive = true,
        bool includeExtensionInMatch = false)
    {
        Pattern = pattern ?? string.Empty;
        Replacement = replacement ?? string.Empty;
        CaseSensitive = caseSensitive;
        IncludeExtensionInMatch = includeExtensionInMatch;

        if (string.IsNullOrWhiteSpace(Pattern))
        {
            Issue = "Pattern cannot be empty.";
            return;
        }

        var options = RegexOptions.CultureInvariant;
        if (!CaseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }

        try
        {
            _regex = new Regex(Pattern, options);
        }
        catch (ArgumentException ex)
        {
            Issue = ex.Message;
        }
    }

    public string Pattern { get; }

    public string Replacement { get; }

    public bool CaseSensitive { get; }

    public bool IncludeExtensionInMatch { get; }

    public string? Issue { get; }

    public string Apply(RenameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (_regex is null)
        {
            return ctx.CurrentName;
        }

        if (!IncludeExtensionInMatch)
        {
            return _regex.Replace(ctx.CurrentName, Replacement);
        }

        var input = ctx.CurrentName + ctx.Extension;
        var replaced = _regex.Replace(input, Replacement);

        if (string.IsNullOrEmpty(ctx.Extension))
        {
            return replaced;
        }

        return replaced.EndsWith(ctx.Extension, StringComparison.OrdinalIgnoreCase)
            ? replaced[..^ctx.Extension.Length]
            : replaced;
    }
}
