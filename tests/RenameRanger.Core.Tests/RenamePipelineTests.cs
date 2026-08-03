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

    [Fact]
    public void NumberingRule_SupportsStartStepPaddingAndAffixes()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new NumberingRule(start: 5, step: 2, padWidth: 3, prefix: "_", suffix: "-x", placement: NumberingPlacement.Suffix),
            });

        var result = pipeline.GetProposedFileNames(
            new[]
            {
                new RenameItem("photo", ".jpg"),
                new RenameItem("photo", ".jpg"),
                new RenameItem("photo", ".jpg"),
            });

        Assert.Equal("photo_005-x.jpg", result[0]);
        Assert.Equal("photo_007-x.jpg", result[1]);
        Assert.Equal("photo_009-x.jpg", result[2]);
    }

    [Fact]
    public void MetadataTokenRule_ExpandsCustomDateFormatsAndStandardTokens()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new MetadataTokenRule("{exif:date:yyyy-MM-dd}_{name}_{size}_{ext}"),
            });

        var metadata = new Dictionary<string, string?>
        {
            ["exif:date"] = "2024-10-31T16:20:00Z",
            ["size"] = "4096",
        };

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("IMG_1234", ".JPG", metadata) });

        Assert.Equal("2024-10-31_IMG_1234_4096_JPG.JPG", result.Single());
    }

    [Fact]
    public void MetadataTokenRule_FallsBackToFileModifiedDate_WhenExifMissing()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new MetadataTokenRule("{exif:date:yyyyMMdd}_{name}"),
            });

        var metadata = new Dictionary<string, string?>
        {
            ["file:modified"] = "2022-01-02T03:04:05Z",
        };

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("scan", ".pdf", metadata) });

        Assert.Equal("20220102_scan.pdf", result.Single());
    }

    [Fact]
    public void TrimCleanRule_CollapsesWhitespace_StripsBracketedTags_AndNormalizesSeparators()
    {
        var pipeline = new RenamePipeline(
            new IRenameRule[]
            {
                new TrimCleanRule(),
            });

        var result = pipeline.GetProposedFileNames(
            new[] { new RenameItem("  My__File   [draft] (v2) --- copy  ", ".txt") });

        Assert.Equal("My File copy.txt", result.Single());
    }
}
