using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RenameRanger.Core.Rules;

public sealed class MetadataTokenRule : IRenameRule
{
    private static readonly Regex TokenRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public MetadataTokenRule(string template)
    {
        Template = template ?? string.Empty;
    }

    public string Template { get; }

    public string Apply(RenameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (string.IsNullOrEmpty(Template))
        {
            return ctx.CurrentName;
        }

        return TokenRegex.Replace(
            Template,
            m => ResolveToken(m.Groups[1].Value, ctx));
    }

    private static string ResolveToken(string token, RenameContext ctx)
    {
        if (string.Equals(token, "name", StringComparison.OrdinalIgnoreCase))
        {
            return ctx.CurrentName;
        }

        if (string.Equals(token, "ext", StringComparison.OrdinalIgnoreCase))
        {
            return ctx.Extension.TrimStart('.');
        }

        if (string.Equals(token, "size", StringComparison.OrdinalIgnoreCase))
        {
            return GetMetadataValue(ctx.Metadata, "size") ?? string.Empty;
        }

        if (token.StartsWith("exif:date", StringComparison.OrdinalIgnoreCase))
        {
            var format = ExtractFormat(token, "exif:date", "yyyy-MM-dd");
            var exifDate = TryGetDate(
                ctx.Metadata,
                "exif:date",
                "exif.date",
                "exifDate");

            var fallback = TryGetDate(
                ctx.Metadata,
                "file:modified",
                "file.modified",
                "fileModified");

            return FormatDate(exifDate ?? fallback, format);
        }

        if (token.StartsWith("file:created", StringComparison.OrdinalIgnoreCase))
        {
            var format = ExtractFormat(token, "file:created", "yyyy-MM-dd");
            var created = TryGetDate(
                ctx.Metadata,
                "file:created",
                "file.created",
                "fileCreated");

            return FormatDate(created, format);
        }

        if (token.StartsWith("file:modified", StringComparison.OrdinalIgnoreCase))
        {
            var format = ExtractFormat(token, "file:modified", "yyyy-MM-dd");
            var modified = TryGetDate(
                ctx.Metadata,
                "file:modified",
                "file.modified",
                "fileModified");

            return FormatDate(modified, format);
        }

        return "{" + token + "}";
    }

    private static string ExtractFormat(string token, string prefix, string defaultFormat)
    {
        if (token.Length <= prefix.Length + 1 || token[prefix.Length] != ':')
        {
            return defaultFormat;
        }

        return token[(prefix.Length + 1)..];
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, string?> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            return value;
        }

        return null;
    }

    private static DateTimeOffset? TryGetDate(IReadOnlyDictionary<string, string?> metadata, params string[] keys)
    {
        var raw = GetMetadataValue(metadata, keys);
        if (raw is null)
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static string FormatDate(DateTimeOffset? date, string format)
    {
        if (date is null)
        {
            return string.Empty;
        }

        try
        {
            return date.Value.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
