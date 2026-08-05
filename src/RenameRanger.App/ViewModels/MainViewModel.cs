using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using RenameRanger.Core;

namespace RenameRanger.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
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

    private string _selectedRuleType = "Find & Replace";
    private bool _includeSubfolders = true;
    private string _statusMessage = "Add files or folders to preview rename rules.";

    public MainViewModel()
    {
        Files = [];
        Rules = [];

        AddRuleCommand = new RelayCommand(AddSelectedRule);
        RemoveRuleCommand = new RelayCommand<RuleViewModel>(RemoveRule, rule => rule is not null);
        MoveRuleUpCommand = new RelayCommand<RuleViewModel>(MoveRuleUp, CanMoveRuleUp);
        MoveRuleDownCommand = new RelayCommand<RuleViewModel>(MoveRuleDown, CanMoveRuleDown);
        RemoveFileCommand = new RelayCommand<FilePreviewItemViewModel>(RemoveFile, file => file is not null);
        ClearFilesCommand = new RelayCommand(ClearFiles, () => Files.Count > 0);

        Files.CollectionChanged += FilesOnCollectionChanged;
        Rules.CollectionChanged += RulesOnCollectionChanged;
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

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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
            Files[i].UpdateFromProposal(proposals[i]);
        }

        var issueCount = proposals.Count(p => p.HasErrors);
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
    }
}
