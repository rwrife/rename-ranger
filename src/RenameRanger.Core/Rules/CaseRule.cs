using System;
using System.Globalization;

namespace RenameRanger.Core.Rules;

public enum CaseTransform
{
    Upper,
    Lower,
    Title,
    Sentence,
}

public sealed class CaseRule : IRenameRule
{
    public CaseRule(CaseTransform transform)
    {
        Transform = transform;
    }

    public CaseTransform Transform { get; }

    public string Apply(RenameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var input = ctx.CurrentName;
        var culture = CultureInfo.CurrentCulture;

        return Transform switch
        {
            CaseTransform.Upper => input.ToUpper(culture),
            CaseTransform.Lower => input.ToLower(culture),
            CaseTransform.Title => culture.TextInfo.ToTitleCase(input.ToLower(culture)),
            CaseTransform.Sentence => ToSentenceCase(input, culture),
            _ => input,
        };
    }

    private static string ToSentenceCase(string input, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var normalized = input.ToLower(culture);
        var chars = normalized.ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetter(chars[i]))
            {
                continue;
            }

            chars[i] = char.ToUpper(chars[i], culture);
            break;
        }

        return new string(chars);
    }
}
