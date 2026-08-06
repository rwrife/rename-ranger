using System.Collections.Generic;
using System.IO;
using System.Linq;
using RenameRanger.Core;

namespace RenameRanger.App.ViewModels;

public sealed class FilePreviewItemViewModel : ObservableObject
{
    private string _proposedFileName;
    private bool _hasIssues;
    private string _errorSummary;
    private bool _manualOverrideActive;

    public FilePreviewItemViewModel(string fullPath)
    {
        FullPath = fullPath;
        OriginalFileName = Path.GetFileName(fullPath);
        OriginalName = Path.GetFileNameWithoutExtension(fullPath);
        Extension = Path.GetExtension(fullPath);
        DirectoryPath = Path.GetDirectoryName(fullPath);

        _proposedFileName = OriginalFileName;
        _errorSummary = string.Empty;
    }

    public string FullPath { get; }

    public string OriginalName { get; }

    public string OriginalFileName { get; }

    public string Extension { get; }

    public string? DirectoryPath { get; }

    public bool ManualOverrideActive
    {
        get => _manualOverrideActive;
        private set => SetProperty(ref _manualOverrideActive, value);
    }

    public string ProposedFileName
    {
        get => _proposedFileName;
        set => SetManualProposedFileName(value);
    }

    public bool HasIssues
    {
        get => _hasIssues;
        private set => SetProperty(ref _hasIssues, value);
    }

    public string ErrorSummary
    {
        get => _errorSummary;
        private set => SetProperty(ref _errorSummary, value);
    }

    public RenameItem ToRenameItem()
    {
        return new RenameItem(
            OriginalName,
            Extension,
            metadata: null,
            DirectoryPath: DirectoryPath);
    }

    public void SetManualProposedFileName(string? proposedFileName)
    {
        var normalized = string.IsNullOrWhiteSpace(proposedFileName)
            ? OriginalFileName
            : proposedFileName.Trim();

        ManualOverrideActive = true;
        SetProposedFileNameInternal(normalized);
        HasIssues = false;
        ErrorSummary = string.Empty;
    }

    public void UpdateFromProposal(RenameProposal proposal, bool preserveManualOverride = true)
    {
        if (preserveManualOverride && ManualOverrideActive)
        {
            return;
        }

        ManualOverrideActive = false;
        SetProposedFileNameInternal(proposal.ProposedFileName);
        HasIssues = proposal.HasErrors;
        ErrorSummary = proposal.HasErrors
            ? string.Join("\n", proposal.Errors.Distinct())
            : string.Empty;
    }

    public void ResetPreview()
    {
        ManualOverrideActive = false;
        SetProposedFileNameInternal(OriginalFileName);
        HasIssues = false;
        ErrorSummary = string.Empty;
    }

    private void SetProposedFileNameInternal(string proposedFileName)
    {
        SetProperty(ref _proposedFileName, proposedFileName, nameof(ProposedFileName));
    }
}
