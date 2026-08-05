using System;
using System.Collections.Generic;
using RenameRanger.Core;
using RenameRanger.Core.Rules;

namespace RenameRanger.App.ViewModels;

public abstract class RuleViewModel : ObservableObject
{
    public abstract string DisplayName { get; }

    public abstract IRenameRule BuildRule();

    public IRenameRule BuildRuleSafe()
    {
        try
        {
            return BuildRule();
        }
        catch (Exception ex)
        {
            return new InvalidRule($"{DisplayName}: {ex.Message}");
        }
    }
}

internal sealed class InvalidRule : IRenameRule, IRuleIssueSource
{
    public InvalidRule(string issue)
    {
        Issue = string.IsNullOrWhiteSpace(issue) ? "Invalid rule configuration." : issue;
    }

    public string? Issue { get; }

    public string Apply(RenameContext ctx) => ctx.CurrentName;
}

public sealed class FindReplaceRuleViewModel : RuleViewModel
{
    private string _findText = string.Empty;
    private string _replaceText = string.Empty;
    private bool _caseSensitive;

    public override string DisplayName => "Find & Replace";

    public string FindText
    {
        get => _findText;
        set => SetProperty(ref _findText, value);
    }

    public string ReplaceText
    {
        get => _replaceText;
        set => SetProperty(ref _replaceText, value);
    }

    public bool CaseSensitive
    {
        get => _caseSensitive;
        set => SetProperty(ref _caseSensitive, value);
    }

    public override IRenameRule BuildRule()
    {
        if (string.IsNullOrEmpty(FindText))
        {
            return new InvalidRule("Find text cannot be empty.");
        }

        return new FindReplaceRule(FindText, ReplaceText, CaseSensitive);
    }
}

public sealed class RegexRuleViewModel : RuleViewModel
{
    private string _pattern = string.Empty;
    private string _replacement = string.Empty;
    private bool _caseSensitive = true;
    private bool _includeExtensionInMatch;

    public override string DisplayName => "Regex";

    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
    }

    public string Replacement
    {
        get => _replacement;
        set => SetProperty(ref _replacement, value);
    }

    public bool CaseSensitive
    {
        get => _caseSensitive;
        set => SetProperty(ref _caseSensitive, value);
    }

    public bool IncludeExtensionInMatch
    {
        get => _includeExtensionInMatch;
        set => SetProperty(ref _includeExtensionInMatch, value);
    }

    public override IRenameRule BuildRule() =>
        new RegexRule(Pattern, Replacement, CaseSensitive, IncludeExtensionInMatch);
}

public sealed class CaseRuleViewModel : RuleViewModel
{
    private CaseTransform _transform = CaseTransform.Title;

    public override string DisplayName => "Case";

    public static IReadOnlyList<CaseTransform> TransformOptions { get; } =
        Enum.GetValues<CaseTransform>();

    public CaseTransform Transform
    {
        get => _transform;
        set => SetProperty(ref _transform, value);
    }

    public override IRenameRule BuildRule() => new CaseRule(Transform);
}

public sealed class NumberingRuleViewModel : RuleViewModel
{
    private int _start = 1;
    private int _step = 1;
    private int _padWidth = 3;
    private string _prefix = "_";
    private string _suffix = string.Empty;
    private NumberingPlacement _placement = NumberingPlacement.Suffix;

    public override string DisplayName => "Numbering";

    public static IReadOnlyList<NumberingPlacement> PlacementOptions { get; } =
        Enum.GetValues<NumberingPlacement>();

    public int Start
    {
        get => _start;
        set => SetProperty(ref _start, value);
    }

    public int Step
    {
        get => _step;
        set => SetProperty(ref _step, value);
    }

    public int PadWidth
    {
        get => _padWidth;
        set => SetProperty(ref _padWidth, value);
    }

    public string Prefix
    {
        get => _prefix;
        set => SetProperty(ref _prefix, value);
    }

    public string Suffix
    {
        get => _suffix;
        set => SetProperty(ref _suffix, value);
    }

    public NumberingPlacement Placement
    {
        get => _placement;
        set => SetProperty(ref _placement, value);
    }

    public override IRenameRule BuildRule() =>
        new NumberingRule(Start, Step, PadWidth, Prefix, Suffix, Placement);
}

public sealed class TrimCleanRuleViewModel : RuleViewModel
{
    private bool _collapseWhitespace = true;
    private bool _stripBracketedTags = true;
    private bool _normalizeSeparators = true;
    private string _normalizedSeparator = " ";

    public override string DisplayName => "Trim / Clean";

    public bool CollapseWhitespace
    {
        get => _collapseWhitespace;
        set => SetProperty(ref _collapseWhitespace, value);
    }

    public bool StripBracketedTags
    {
        get => _stripBracketedTags;
        set => SetProperty(ref _stripBracketedTags, value);
    }

    public bool NormalizeSeparators
    {
        get => _normalizeSeparators;
        set => SetProperty(ref _normalizeSeparators, value);
    }

    public string NormalizedSeparator
    {
        get => _normalizedSeparator;
        set => SetProperty(ref _normalizedSeparator, value);
    }

    public override IRenameRule BuildRule() =>
        new TrimCleanRule(
            CollapseWhitespace,
            StripBracketedTags,
            NormalizeSeparators,
            NormalizedSeparator);
}

public sealed class MetadataTokenRuleViewModel : RuleViewModel
{
    private string _template = "{name}";

    public override string DisplayName => "Metadata Token";

    public string Template
    {
        get => _template;
        set => SetProperty(ref _template, value);
    }

    public override IRenameRule BuildRule() => new MetadataTokenRule(Template);
}
