using System;
using System.Collections.Generic;
using System.Linq;

namespace RenameRanger.Core;

public sealed class RenamePipeline
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyMetadata =
        new Dictionary<string, string?>();

    private readonly IReadOnlyList<IRenameRule> _rules;

    public RenamePipeline(IEnumerable<IRenameRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
    }

    public IReadOnlyList<RenameProposal> Preview(IEnumerable<RenameItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var proposals = new List<RenameProposal>();
        var index = 0;

        foreach (var item in items)
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

        return proposals;
    }

    public IReadOnlyList<string> GetProposedFileNames(IEnumerable<RenameItem> items) =>
        Preview(items).Select(p => p.ProposedFileName).ToList();

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
}
