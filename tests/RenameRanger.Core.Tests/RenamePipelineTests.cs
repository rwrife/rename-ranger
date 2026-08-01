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
}
