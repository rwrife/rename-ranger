using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RenameRanger.Core;

public sealed record RenameMove(string SourcePath, string DestinationPath);

public sealed record RenameApplyResult(string JournalPath, IReadOnlyList<RenameMove> Moves);

public sealed record RenameUndoResult(string JournalPath, IReadOnlyList<RenameMove> Moves);

public sealed class RenameBatchApplier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _journalDirectory;

    public RenameBatchApplier(string? appDataRoot = null)
    {
        var resolvedAppDataRoot =
            string.IsNullOrWhiteSpace(appDataRoot)
                ? ResolveDefaultAppDataPath()
                : appDataRoot;

        _journalDirectory = Path.Combine(resolvedAppDataRoot, "rename-ranger", "journal");
        Directory.CreateDirectory(_journalDirectory);
    }

    public string JournalDirectory => _journalDirectory;

    public RenameApplyResult Apply(IEnumerable<RenameMove> moves)
    {
        var normalizedMoves = NormalizeAndValidateMoveSet(moves);

        ExecuteTwoPhaseRename(normalizedMoves);

        var journalEntry = new RenameJournalEntry(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            normalizedMoves);

        var journalPath = WriteJournalEntry(journalEntry);

        return new RenameApplyResult(journalPath, normalizedMoves);
    }

    public RenameUndoResult UndoLast()
    {
        var journalPath = GetLatestJournalPath();
        if (journalPath is null)
        {
            throw new InvalidOperationException("No rename journal entries found to undo.");
        }

        var journalEntry = ReadJournalEntry(journalPath);
        var reverseMoves = journalEntry.Moves
            .Reverse()
            .Select(move => new RenameMove(move.DestinationPath, move.SourcePath))
            .ToList();

        ExecuteTwoPhaseRename(reverseMoves);

        File.Delete(journalPath);

        return new RenameUndoResult(journalPath, reverseMoves);
    }

    private static List<RenameMove> NormalizeAndValidateMoveSet(IEnumerable<RenameMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var normalizedMoves = new List<RenameMove>();
        foreach (var move in moves)
        {
            ArgumentNullException.ThrowIfNull(move);

            if (string.IsNullOrWhiteSpace(move.SourcePath) || string.IsNullOrWhiteSpace(move.DestinationPath))
            {
                throw new ArgumentException("SourcePath and DestinationPath are required for every rename move.");
            }

            var sourcePath = Path.GetFullPath(move.SourcePath);
            var destinationPath = Path.GetFullPath(move.DestinationPath);

            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            normalizedMoves.Add(new RenameMove(sourcePath, destinationPath));
        }

        if (normalizedMoves.Count == 0)
        {
            throw new InvalidOperationException("No effective rename operations were provided.");
        }

        var duplicateSources = normalizedMoves
            .GroupBy(move => move.SourcePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSources is not null)
        {
            throw new InvalidOperationException($"Duplicate source path detected: '{duplicateSources.Key}'.");
        }

        var duplicateDestinations = normalizedMoves
            .GroupBy(move => move.DestinationPath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateDestinations is not null)
        {
            throw new InvalidOperationException($"Duplicate destination path detected: '{duplicateDestinations.Key}'.");
        }

        var sourceSet = normalizedMoves
            .Select(move => move.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var move in normalizedMoves)
        {
            if (!File.Exists(move.SourcePath))
            {
                throw new FileNotFoundException($"Source file does not exist: '{move.SourcePath}'.", move.SourcePath);
            }

            var destinationDirectory = Path.GetDirectoryName(move.DestinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Destination directory does not exist for '{move.DestinationPath}'.");
            }

            if (File.Exists(move.DestinationPath) && !sourceSet.Contains(move.DestinationPath))
            {
                throw new InvalidOperationException(
                    $"Destination file already exists and is not part of this rename batch: '{move.DestinationPath}'.");
            }
        }

        return normalizedMoves;
    }

    private static void ExecuteTwoPhaseRename(IReadOnlyList<RenameMove> moves)
    {
        var tempMoves = new List<TempMove>(moves.Count);

        try
        {
            foreach (var move in moves)
            {
                var tempPath = CreateTempPath(Path.GetDirectoryName(move.SourcePath)!);
                File.Move(move.SourcePath, tempPath);
                tempMoves.Add(new TempMove(move.SourcePath, move.DestinationPath, tempPath));
            }

            foreach (var tempMove in tempMoves)
            {
                File.Move(tempMove.TempPath, tempMove.DestinationPath);
                tempMove.CompletedFinalMove = true;
            }
        }
        catch (Exception ex)
        {
            RollBackBestEffort(tempMoves);
            throw new InvalidOperationException("Two-phase rename failed (rollback attempted).", ex);
        }
    }

    private static void RollBackBestEffort(IEnumerable<TempMove> tempMoves)
    {
        foreach (var tempMove in tempMoves.Reverse())
        {
            try
            {
                if (tempMove.CompletedFinalMove)
                {
                    if (File.Exists(tempMove.DestinationPath) && !File.Exists(tempMove.SourcePath))
                    {
                        File.Move(tempMove.DestinationPath, tempMove.SourcePath);
                    }
                }
                else if (File.Exists(tempMove.TempPath) && !File.Exists(tempMove.SourcePath))
                {
                    File.Move(tempMove.TempPath, tempMove.SourcePath);
                }
            }
            catch
            {
                // Best effort rollback only.
            }
        }
    }

    private static string CreateTempPath(string directory)
    {
        while (true)
        {
            var tempPath = Path.Combine(directory, $".rename-ranger.tmp.{Guid.NewGuid():N}");
            if (!File.Exists(tempPath) && !Directory.Exists(tempPath))
            {
                return tempPath;
            }
        }
    }

    private string? GetLatestJournalPath()
    {
        return Directory
            .EnumerateFiles(_journalDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(Path.GetFileName)
            .FirstOrDefault();
    }

    private string WriteJournalEntry(RenameJournalEntry entry)
    {
        var fileName = $"{entry.CreatedAtUtc:yyyyMMddHHmmssfff}_{entry.OperationId}.json";
        var path = Path.Combine(_journalDirectory, fileName);

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        File.WriteAllText(path, json);

        return path;
    }

    private static RenameJournalEntry ReadJournalEntry(string path)
    {
        var json = File.ReadAllText(path);
        var entry = JsonSerializer.Deserialize<RenameJournalEntry>(json, JsonOptions);

        if (entry is null)
        {
            throw new InvalidOperationException($"Unable to deserialize rename journal at '{path}'.");
        }

        return entry;
    }

    private static string ResolveDefaultAppDataPath()
    {
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (!string.IsNullOrWhiteSpace(appData))
        {
            return appData;
        }

        var specialFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(specialFolder))
        {
            return specialFolder;
        }

        return Path.GetTempPath();
    }

    private sealed class TempMove
    {
        public TempMove(string sourcePath, string destinationPath, string tempPath)
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            TempPath = tempPath;
        }

        public string SourcePath { get; }

        public string DestinationPath { get; }

        public string TempPath { get; }

        public bool CompletedFinalMove { get; set; }
    }

    private sealed record RenameJournalEntry(
        string OperationId,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<RenameMove> Moves);
}
