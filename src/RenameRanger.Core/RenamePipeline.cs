using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RenameRanger.Core;

public sealed class RenamePipeline
{
    private const int WindowsMaxPath = 260;

    private static readonly IReadOnlyDictionary<string, string?> EmptyMetadata =
        new Dictionary<string, string?>();

    private static readonly HashSet<char> InvalidWindowsFileNameChars = BuildInvalidWindowsFileNameChars();

    private static readonly HashSet<string> ReservedWindowsNames =
        new(
            new[]
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            },
            StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<IRenameRule> _rules;

    public RenamePipeline(IEnumerable<IRenameRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
    }

    public IReadOnlyList<RenameProposal> Preview(IEnumerable<RenameItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.ToList();
        var proposals = new List<RenameProposal>(itemList.Count);

        var index = 0;
        foreach (var item in itemList)
        {
            ArgumentNullException.ThrowIfNull(item);

            var extension = NormalizeExtension(item.Extension);
            var context = new RenameContext(
                item.OriginalName,
                extension,
                index,
                item.Metadata ?? EmptyMetadata);

            var currentName = context.CurrentName;
            var errors = new List<string>();
            foreach (var rule in _rules)
            {
                ArgumentNullException.ThrowIfNull(rule);

                if (rule is IRuleIssueSource issueSource && !string.IsNullOrWhiteSpace(issueSource.Issue))
                {
                    errors.Add($"{rule.GetType().Name}: {issueSource.Issue}");
                    continue;
                }

                try
                {
                    currentName = rule.Apply(context.WithCurrentName(currentName));
                }
                catch (Exception ex)
                {
                    errors.Add($"{rule.GetType().Name}: {ex.Message}");
                }
            }

            proposals.Add(
                new RenameProposal(
                    index,
                    item.OriginalName,
                    item.OriginalName + extension,
                    currentName,
                    currentName + extension,
                    errors));

            index++;
        }

        return ApplyBatchValidation(itemList, proposals);
    }

    public IReadOnlyList<string> GetProposedFileNames(IEnumerable<RenameItem> items) =>
        Preview(items).Select(p => p.ProposedFileName).ToList();

    private static IReadOnlyList<RenameProposal> ApplyBatchValidation(
        IReadOnlyList<RenameItem> items,
        IReadOnlyList<RenameProposal> proposals)
    {
        var additionalErrorsByIndex = new Dictionary<int, List<string>>();

        static void AddError(Dictionary<int, List<string>> map, int index, string message)
        {
            if (!map.TryGetValue(index, out var list))
            {
                list = new List<string>();
                map[index] = list;
            }

            list.Add(message);
        }

        foreach (var duplicateGroup in proposals
                     .GroupBy(p => p.ProposedFileName, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            foreach (var proposal in duplicateGroup)
            {
                AddError(
                    additionalErrorsByIndex,
                    proposal.Index,
                    $"Duplicate target filename '{proposal.ProposedFileName}'.");
            }
        }

        foreach (var proposal in proposals)
        {
            var item = items[proposal.Index];
            foreach (var error in ValidateWindowsFileName(proposal.ProposedFileName, item.DirectoryPath))
            {
                AddError(additionalErrorsByIndex, proposal.Index, error);
            }
        }

        var validated = new List<RenameProposal>(proposals.Count);
        foreach (var proposal in proposals)
        {
            if (!additionalErrorsByIndex.TryGetValue(proposal.Index, out var extraErrors))
            {
                validated.Add(proposal);
                continue;
            }

            var mergedErrors = proposal.Errors.Concat(extraErrors).Distinct().ToList();
            validated.Add(proposal with { Errors = mergedErrors });
        }

        return validated;
    }

    private static IEnumerable<string> ValidateWindowsFileName(string fileName, string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            yield return "Proposed filename is empty.";
            yield break;
        }

        if (fileName is "." or "..")
        {
            yield return "Proposed filename cannot be '.' or '..'.";
        }

        if (fileName.EndsWith(' ') || fileName.EndsWith('.'))
        {
            yield return "Windows filenames cannot end with a space or period.";
        }

        foreach (var ch in fileName)
        {
            if (InvalidWindowsFileNameChars.Contains(ch))
            {
                yield return $"Windows filename contains invalid character '{ch}'.";
                break;
            }
        }

        var stem = Path.GetFileNameWithoutExtension(fileName).TrimEnd(' ', '.');
        if (ReservedWindowsNames.Contains(stem))
        {
            yield return $"Windows reserved filename '{stem}' is not allowed.";
        }

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(directoryPath, fileName));
            }
            catch (Exception ex)
            {
                yield return $"Failed to build target path: {ex.Message}";
                yield break;
            }

            if (fullPath.Length > WindowsMaxPath)
            {
                yield return $"Target path exceeds Windows MAX_PATH ({WindowsMaxPath} characters).";
            }
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : $".{extension}";
    }

    private static HashSet<char> BuildInvalidWindowsFileNameChars()
    {
        var invalidChars = new HashSet<char>
        {
            '<', '>', ':', '"', '/', '\\', '|', '?', '*',
        };

        for (var i = 0; i < 32; i++)
        {
            invalidChars.Add((char)i);
        }

        return invalidChars;
    }
}
