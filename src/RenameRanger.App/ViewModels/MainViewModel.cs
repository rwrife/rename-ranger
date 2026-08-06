using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using RenameRanger.App.Settings;
using RenameRanger.Core;
using RenameRanger.Core.Ai;

namespace RenameRanger.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private static readonly HashSet<string> TextSnippetExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".md",
            ".csv",
            ".json",
            ".xml",
            ".yaml",
            ".yml",
            ".log",
        };

    private readonly Dictionary<string, Func<RuleViewModel>> _ruleFactories =
        new(StringComparer.Ordinal)
        {
            ["Find & Replace"] = () => new FindReplaceRuleViewModel(),
            ["Regex"] = () => new RegexRuleViewModel(),
            ["Case"] = () => new CaseRuleViewModel(),
            ["Numbering"] = () => new NumberingRuleViewModel(),
            ["Trim / Clean"] = () => new TrimCleanRuleViewModel(),
            ["Metadata Token"] = () => new MetadataTokenRuleViewModel(),
        };

    private readonly SettingsStore _settingsStore;
    private readonly OpenAiCompatibleRenameClient _aiClient;

    private bool _suppressSettingsPersist;
    private string _selectedRuleType = "Find & Replace";
    private bool _includeSubfolders = true;
    private string _statusMessage = "Add files or folders to preview rename rules.";
    private bool _isAiEnabled;
    private string _aiEndpointUrl = LocalAiSettings.DefaultEndpointUrl;
    private string _aiModel = LocalAiSettings.DefaultModel;
    private bool _isSuggestingName;

    public MainViewModel()
    {
        _settingsStore = new SettingsStore();
        _aiClient = new OpenAiCompatibleRenameClient(new HttpClient());

        Files = [];
        Rules = [];

        AddRuleCommand = new RelayCommand(AddSelectedRule);
        RemoveRuleCommand = new RelayCommand<RuleViewModel>(RemoveRule, rule => rule is not null);
        MoveRuleUpCommand = new RelayCommand<RuleViewModel>(MoveRuleUp, CanMoveRuleUp);
        MoveRuleDownCommand = new RelayCommand<RuleViewModel>(MoveRuleDown, CanMoveRuleDown);
        RemoveFileCommand = new RelayCommand<FilePreviewItemViewModel>(RemoveFile, file => file is not null);
        ClearFilesCommand = new RelayCommand(ClearFiles, () => Files.Count > 0);
        SuggestNameCommand = new RelayCommand<FilePreviewItemViewModel>(
            file => _ = SuggestNameForFileAsync(file),
            CanSuggestName);

        Files.CollectionChanged += FilesOnCollectionChanged;
        Rules.CollectionChanged += RulesOnCollectionChanged;

        LoadSettings();
    }

    public ObservableCollection<FilePreviewItemViewModel> Files { get; }

    public ObservableCollection<RuleViewModel> Rules { get; }

    public IEnumerable<string> RuleTypeOptions => _ruleFactories.Keys;

    public ICommand AddRuleCommand { get; }

    public ICommand RemoveRuleCommand { get; }

    public ICommand MoveRuleUpCommand { get; }

    public ICommand MoveRuleDownCommand { get; }

    public ICommand RemoveFileCommand { get; }

    public ICommand ClearFilesCommand { get; }

    public ICommand SuggestNameCommand { get; }

    public string SelectedRuleType
    {
        get => _selectedRuleType;
        set => SetProperty(ref _selectedRuleType, value);
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set => SetProperty(ref _includeSubfolders, value);
    }

    public bool IsAiEnabled
    {
        get => _isAiEnabled;
        set
        {
            if (!SetProperty(ref _isAiEnabled, value))
            {
                return;
            }

            PersistSettings();
            NotifyCommandStateChanged();
        }
    }

    public string AiEndpointUrl
    {
        get => _aiEndpointUrl;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? LocalAiSettings.DefaultEndpointUrl
                : value.Trim();

            if (!SetProperty(ref _aiEndpointUrl, normalized))
            {
                return;
            }

            PersistSettings();
        }
    }

    public string AiModel
    {
        get => _aiModel;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? LocalAiSettings.DefaultModel
                : value.Trim();

            if (!SetProperty(ref _aiModel, normalized))
            {
                return;
            }

            PersistSettings();
        }
    }

    public string AiSettingsLocation => _settingsStore.SettingsFilePath;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private bool IsSuggestingName
    {
        get => _isSuggestingName;
        set
        {
            if (!SetProperty(ref _isSuggestingName, value))
            {
                return;
            }

            NotifyCommandStateChanged();
        }
    }

    public void AddPaths(IEnumerable<string> rawPaths)
    {
        var candidatePaths = rawPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidatePaths.Count == 0)
        {
            return;
        }

        var discoveredFiles = new List<string>();
        var errors = new List<string>();

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                discoveredFiles.Add(path);
                continue;
            }

            if (!Directory.Exists(path))
            {
                errors.Add($"Path not found: {path}");
                continue;
            }

            var searchOption = IncludeSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            try
            {
                discoveredFiles.AddRange(Directory.EnumerateFiles(path, "*", searchOption));
            }
            catch (Exception ex)
            {
                errors.Add($"{path}: {ex.Message}");
            }
        }

        if (discoveredFiles.Count == 0)
        {
            StatusMessage = errors.Count == 0
                ? "No files were discovered from the selected paths."
                : $"No files added. {string.Join(" | ", errors)}";
            return;
        }

        var existing = Files
            .Select(file => file.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var file in discoveredFiles
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!existing.Add(file))
            {
                continue;
            }

            Files.Add(new FilePreviewItemViewModel(file));
            added++;
        }

        RecomputePreview();

        var statusPrefix = added > 0
            ? $"Added {added} file(s)."
            : "No new files added.";

        StatusMessage = errors.Count == 0
            ? statusPrefix
            : $"{statusPrefix} Warnings: {string.Join(" | ", errors)}";
    }

    private void LoadSettings()
    {
        var loaded = _settingsStore.Load();

        _suppressSettingsPersist = true;
        try
        {
            IsAiEnabled = loaded.LocalAi.Enabled;
            AiEndpointUrl = string.IsNullOrWhiteSpace(loaded.LocalAi.EndpointUrl)
                ? LocalAiSettings.DefaultEndpointUrl
                : loaded.LocalAi.EndpointUrl;
            AiModel = string.IsNullOrWhiteSpace(loaded.LocalAi.Model)
                ? LocalAiSettings.DefaultModel
                : loaded.LocalAi.Model;
        }
        finally
        {
            _suppressSettingsPersist = false;
        }

        PersistSettings();
    }

    private void PersistSettings()
    {
        if (_suppressSettingsPersist)
        {
            return;
        }

        try
        {
            _settingsStore.Save(
                new AppSettings
                {
                    LocalAi = new LocalAiSettings
                    {
                        Enabled = IsAiEnabled,
                        EndpointUrl = AiEndpointUrl,
                        Model = AiModel,
                    },
                });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
    }

    private async Task SuggestNameForFileAsync(FilePreviewItemViewModel? file)
    {
        if (file is null || !IsAiEnabled || IsSuggestingName)
        {
            return;
        }

        IsSuggestingName = true;
        try
        {
            var ruleBasedStem = Path.GetFileNameWithoutExtension(file.ProposedFileName);
            var request = BuildAiRenameRequest(file);
            var suggestion = await _aiClient
                .SuggestNameOrFallbackAsync(AiEndpointUrl, AiModel, request, ruleBasedStem)
                .ConfigureAwait(true);

            var proposedFileName = string.Concat(suggestion.SuggestedName, file.Extension);
            file.SetManualProposedFileName(proposedFileName);

            StatusMessage = suggestion.UsedFallback
                ? $"AI unavailable for '{file.OriginalFileName}'. Kept rule-based name."
                : $"Applied AI suggestion for '{file.OriginalFileName}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI suggestion failed: {ex.Message}";
        }
        finally
        {
            IsSuggestingName = false;
        }
    }

    private static AiRenameRequest BuildAiRenameRequest(FilePreviewItemViewModel file)
    {
        var metadata = BuildFileMetadata(file.FullPath);
        var snippet = TryReadTextSnippet(file.FullPath);

        return new AiRenameRequest(
            OriginalFileName: file.OriginalFileName,
            OriginalName: file.OriginalName,
            Extension: file.Extension,
            Metadata: metadata,
            TextSnippet: snippet);
    }

    private static IReadOnlyDictionary<string, string?> BuildFileMetadata(string fullPath)
    {
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var fileInfo = new FileInfo(fullPath);
            metadata["file:path"] = fullPath;
            metadata["file:folder"] = fileInfo.DirectoryName;
            metadata["file:size"] = fileInfo.Exists
                ? fileInfo.Length.ToString(CultureInfo.InvariantCulture)
                : null;
            metadata["file:created"] = fileInfo.Exists
                ? fileInfo.CreationTimeUtc.ToString("O", CultureInfo.InvariantCulture)
                : null;
            metadata["file:modified"] = fileInfo.Exists
                ? fileInfo.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture)
                : null;
        }
        catch
        {
            // Best-effort metadata extraction.
        }

        return metadata;
    }

    private static string? TryReadTextSnippet(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        if (!TextSnippetExtensions.Contains(extension))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var buffer = new char[800];
            var read = reader.ReadBlock(buffer, 0, buffer.Length);

            return read <= 0
                ? null
                : new string(buffer, 0, read);
        }
        catch
        {
            return null;
        }
    }

    private bool CanSuggestName(FilePreviewItemViewModel? file)
    {
        return file is not null && IsAiEnabled && !IsSuggestingName;
    }

    private void AddSelectedRule()
    {
        if (!_ruleFactories.TryGetValue(SelectedRuleType, out var factory))
        {
            factory = _ruleFactories.Values.First();
        }

        Rules.Add(factory());
        RecomputePreview();
    }

    private void RemoveRule(RuleViewModel? rule)
    {
        if (rule is null)
        {
            return;
        }

        Rules.Remove(rule);
        RecomputePreview();
    }

    private void MoveRuleUp(RuleViewModel? rule)
    {
        if (rule is null)
        {
            return;
        }

        var index = Rules.IndexOf(rule);
        if (index <= 0)
        {
            return;
        }

        Rules.Move(index, index - 1);
        RecomputePreview();
    }

    private void MoveRuleDown(RuleViewModel? rule)
    {
        if (rule is null)
        {
            return;
        }

        var index = Rules.IndexOf(rule);
        if (index < 0 || index >= Rules.Count - 1)
        {
            return;
        }

        Rules.Move(index, index + 1);
        RecomputePreview();
    }

    private bool CanMoveRuleUp(RuleViewModel? rule)
    {
        if (rule is null)
        {
            return false;
        }

        return Rules.IndexOf(rule) > 0;
    }

    private bool CanMoveRuleDown(RuleViewModel? rule)
    {
        if (rule is null)
        {
            return false;
        }

        var index = Rules.IndexOf(rule);
        return index >= 0 && index < Rules.Count - 1;
    }

    private void RemoveFile(FilePreviewItemViewModel? file)
    {
        if (file is null)
        {
            return;
        }

        Files.Remove(file);
        RecomputePreview();
    }

    private void ClearFiles()
    {
        Files.Clear();
        RecomputePreview();
    }

    private void FilesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            StatusMessage = "File list cleared.";
        }

        RecomputePreview();
        NotifyCommandStateChanged();
    }

    private void RulesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<RuleViewModel>())
            {
                oldItem.PropertyChanged -= RuleOnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<RuleViewModel>())
            {
                newItem.PropertyChanged += RuleOnPropertyChanged;
            }
        }

        RecomputePreview();
        NotifyCommandStateChanged();
    }

    private void RuleOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RecomputePreview();
    }

    private void RecomputePreview()
    {
        if (Files.Count == 0)
        {
            StatusMessage = Rules.Count == 0
                ? "Add files or folders to preview rename rules."
                : $"{Rules.Count} rule(s) configured. Add files to preview.";
            return;
        }

        var pipeline = new RenamePipeline(Rules.Select(rule => rule.BuildRuleSafe()));
        var proposals = pipeline.Preview(Files.Select(file => file.ToRenameItem())).ToList();

        for (var i = 0; i < Files.Count && i < proposals.Count; i++)
        {
            Files[i].UpdateFromProposal(proposals[i], preserveManualOverride: true);
        }

        var issueCount = Files.Count(f => f.HasIssues);
        StatusMessage = issueCount == 0
            ? $"Preview ready for {Files.Count} file(s)."
            : $"Preview has {issueCount} item(s) with conflicts/invalid names.";
    }

    private void NotifyCommandStateChanged()
    {
        (ClearFilesCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveRuleUpCommand as RelayCommand<RuleViewModel>)?.NotifyCanExecuteChanged();
        (MoveRuleDownCommand as RelayCommand<RuleViewModel>)?.NotifyCanExecuteChanged();
        (RemoveRuleCommand as RelayCommand<RuleViewModel>)?.NotifyCanExecuteChanged();
        (RemoveFileCommand as RelayCommand<FilePreviewItemViewModel>)?.NotifyCanExecuteChanged();
        (SuggestNameCommand as RelayCommand<FilePreviewItemViewModel>)?.NotifyCanExecuteChanged();
    }
}
