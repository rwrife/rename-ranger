using RenameRanger.Core.Rules;

namespace RenameRanger.Core.Tests;

public class RenamePipelineTests
{
    [Fact]
    public void RenamePipeline_RespectsRuleOrdering()
    {
        var items = new[]
        {
            new RenameItem("ab", ".txt"),
        };

        var findThenUpper = new RenamePipeline(
            new IRenameRule[]
            {
                new FindReplaceRule("a", "x", caseSensitive: true),
                new CaseRule(CaseTransform.Upper),
            });

        var upperThenFind = new RenamePipeline(
            new IRenameRule[]
            {
                new CaseRule(CaseTransform.Upper),
                new FindReplaceRule("a", "x", caseSensitive: true),
            });

        Assert.Equal("XB.txt", findThenUpper.GetProposedFileNames(items).Single());
        Assert.Equal("AB.txt", upperThenFind.GetProposedFileNames(items).Single());
    }

    [Fact]
    public void FindReplaceRule_CanRunCaseInsensitive()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new FindReplaceRule("report", "summary", caseSensitive: false),
            });

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("Report_Final", ".txt") });

        Assert.Equal("summary_Final.txt", result.Single());
    }

    [Fact]
    public void FindReplaceRule_CanRunCaseSensitive()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new FindReplaceRule("report", "summary", caseSensitive: true),
            });

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("Report_Final", ".txt") });

        Assert.Equal("Report_Final.txt", result.Single());
    }

    [Theory]
    [InlineData(CaseTransform.Upper, "project plan", "PROJECT PLAN")]
    [InlineData(CaseTransform.Lower, "Project PLAN", "project plan")]
    [InlineData(CaseTransform.Title, "project plan v2", "Project Plan V2")]
    [InlineData(CaseTransform.Sentence, "hELLO WORLD", "Hello world")]
    public void CaseRule_AppliesExpectedTransform(
        CaseTransform transform,
        string input,
        string expected)
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new CaseRule(transform),
            });

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem(input, ".txt") });

        Assert.Equal($"{expected}.txt", result.Single());
    }

    [Fact]
    public void RegexRule_SupportsBackreferences()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new RegexRule("^(?<prefix>[A-Za-z]+)_(\\d{4})_(\\d{2})$", "$2-$3_${prefix}"),
            });

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("Photo_2026_08", ".jpg") });

        Assert.Equal("2026-08_Photo.jpg", result.Single());
    }

    [Fact]
    public void RegexRule_SupportsCaseInsensitiveOption()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new RegexRule("^report", "summary", caseSensitive: false),
            });

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("Report_Final", ".txt") });

        Assert.Equal("summary_Final.txt", result.Single());
    }

    [Fact]
    public void RegexRule_InvalidPattern_IsCapturedAsProposalError()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new RegexRule("[", "x"),
            });

        var proposal = pipeline.Preview(new[] { new RenameItem("demo", ".txt") }).Single();

        Assert.Equal("demo.txt", proposal.ProposedFileName);
        Assert.True(proposal.HasErrors);
        Assert.Contains(proposal.Errors, e => e.Contains("RegexRule"));
    }

    [Fact]
    public void InsertRemoveRule_CanInsertFromStartAndEnd()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                InsertRemoveRule.Insert("pre-", 0, TextAnchor.FromStart),
                InsertRemoveRule.Insert("-v2", 0, TextAnchor.FromEnd),
            });

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("report", ".txt") });

        Assert.Equal("pre-report-v2.txt", result.Single());
    }

    [Fact]
    public void InsertRemoveRule_CanRemoveByRangeAndSubstring()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                InsertRemoveRule.RemoveRange(start: 0, length: 3),
                InsertRemoveRule.RemoveSubstring("draft_", caseSensitive: false),
            });

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("tmpDraft_Report", ".txt") });

        Assert.Equal("Report.txt", result.Single());
    }
}
