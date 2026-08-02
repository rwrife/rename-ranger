using System;
using System.Text;

namespace RenameRanger.Core.Rules;

public enum TextAnchor
{
    FromStart,
    FromEnd,
}

public enum InsertRemoveMode
{
    Insert,
    RemoveRange,
    RemoveSubstring,
}

public sealed class InsertRemoveRule : IRenameRule
{
    private InsertRemoveRule(
        InsertRemoveMode mode,
        string text,
        int position,
        int length,
        bool caseSensitive,
        TextAnchor anchor)
    {
        Mode = mode;
        Text = text;
        Position = position;
        Length = length;
        CaseSensitive = caseSensitive;
        Anchor = anchor;
    }

    public InsertRemoveMode Mode { get; }

    public string Text { get; }

    public int Position { get; }

    public int Length { get; }

    public bool CaseSensitive { get; }

    public TextAnchor Anchor { get; }

    public static InsertRemoveRule Insert(string text, int position, TextAnchor anchor = TextAnchor.FromStart)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return new InsertRemoveRule(InsertRemoveMode.Insert, text, position, 0, true, anchor);
    }

    public static InsertRemoveRule RemoveRange(int start, int length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return new InsertRemoveRule(InsertRemoveMode.RemoveRange, string.Empty, start, length, true, TextAnchor.FromStart);
    }

    public static InsertRemoveRule RemoveSubstring(string pattern, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(pattern));
        }

        return new InsertRemoveRule(InsertRemoveMode.RemoveSubstring, pattern, 0, 0, caseSensitive, TextAnchor.FromStart);
    }

    public string Apply(RenameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        return Mode switch
        {
            InsertRemoveMode.Insert => InsertAt(ctx.CurrentName),
            InsertRemoveMode.RemoveRange => RemoveRange(ctx.CurrentName),
            InsertRemoveMode.RemoveSubstring => RemoveSubstring(ctx.CurrentName),
            _ => ctx.CurrentName,
        };
    }

    private string InsertAt(string input)
    {
        var index = Anchor == TextAnchor.FromEnd
            ? input.Length - Position
            : Position;

        index = Math.Clamp(index, 0, input.Length);
        return input.Insert(index, Text);
    }

    private string RemoveRange(string input)
    {
        if (Position >= input.Length || Length == 0)
        {
            return input;
        }

        var clampedLength = Math.Min(Length, input.Length - Position);
        return input.Remove(Position, clampedLength);
    }

    private string RemoveSubstring(string input)
    {
        return CaseSensitive
            ? input.Replace(Text, string.Empty, StringComparison.Ordinal)
            : ReplaceOrdinalIgnoreCase(input, Text, string.Empty);
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
