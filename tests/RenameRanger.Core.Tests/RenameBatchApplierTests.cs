namespace RenameRanger.Core.Tests;

public sealed class RenameBatchApplierTests : IDisposable
{
    private readonly string _tempRoot;

    public RenameBatchApplierTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"rename-ranger-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void Apply_TwoPhaseRename_HandlesNameSwapWithoutDataLoss()
    {
        var fileA = Path.Combine(_tempRoot, "A.txt");
        var fileB = Path.Combine(_tempRoot, "B.txt");

        File.WriteAllText(fileA, "content-A");
        File.WriteAllText(fileB, "content-B");

        var applier = new RenameBatchApplier(_tempRoot);
        applier.Apply(
            new[]
            {
                new RenameMove(fileA, Path.Combine(_tempRoot, "B.txt")),
                new RenameMove(fileB, Path.Combine(_tempRoot, "A.txt")),
            });

        Assert.Equal("content-B", File.ReadAllText(Path.Combine(_tempRoot, "A.txt")));
        Assert.Equal("content-A", File.ReadAllText(Path.Combine(_tempRoot, "B.txt")));
    }

    [Fact]
    public void Apply_WritesJournal_AndUndoLast_RestoresOriginalNames()
    {
        var file1 = Path.Combine(_tempRoot, "before-1.txt");
        var file2 = Path.Combine(_tempRoot, "before-2.txt");

        File.WriteAllText(file1, "first");
        File.WriteAllText(file2, "second");

        var applier = new RenameBatchApplier(_tempRoot);

        var applyResult = applier.Apply(
            new[]
            {
                new RenameMove(file1, Path.Combine(_tempRoot, "after-1.txt")),
                new RenameMove(file2, Path.Combine(_tempRoot, "after-2.txt")),
            });

        Assert.True(File.Exists(applyResult.JournalPath));
        Assert.True(File.Exists(Path.Combine(_tempRoot, "after-1.txt")));
        Assert.True(File.Exists(Path.Combine(_tempRoot, "after-2.txt")));

        var undoResult = applier.UndoLast();

        Assert.Equal(applyResult.JournalPath, undoResult.JournalPath);
        Assert.False(File.Exists(applyResult.JournalPath));
        Assert.True(File.Exists(file1));
        Assert.True(File.Exists(file2));
        Assert.Equal("first", File.ReadAllText(file1));
        Assert.Equal("second", File.ReadAllText(file2));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for test temp directories.
        }
    }
}
