using System.Windows;
using Microsoft.Win32;
using RenameRanger.App.ViewModels;
using Forms = System.Windows.Forms;

namespace RenameRanger.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Title = "Select files to rename",
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.AddPaths(dialog.FileNames);
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select a folder to add for rename preview",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            ViewModel.AddPaths([dialog.SelectedPath]);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] droppedPaths)
        {
            ViewModel.AddPaths(droppedPaths);
        }

        e.Handled = true;
    }
}
