using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using YKanBan.ViewModels;
using YKanBan.ViewModels.Dialogs;
using YKanBan.Views.Dialogs;
using Resx = YKanBan.Resources;

namespace YKanBan.Views;

public partial class MainWindow : Window
{
    private bool _interactionsRegistered;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel viewModel && !_interactionsRegistered)
        {
            _interactionsRegistered = true;
            RegisterInteractionHandlers(viewModel);
        }
    }

    /// <summary>Wires the view model's dialog interactions to this window.</summary>
    private void RegisterInteractionHandlers(MainWindowViewModel viewModel)
    {
        viewModel.Sidebar.PickFolder.RegisterHandler(async _ =>
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = Resx.Sidebar_OpenProject,
                    AllowMultiple = false,
                });
            return folders.Count == 1 ? folders[0].TryGetLocalPath() : null;
        });

        viewModel.Sidebar.ConfirmInitialize.RegisterHandler(async folderPath =>
        {
            var dialog = new ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel(
                    Resx.Dialog_InitializeTitle,
                    string.Format(CultureInfo.CurrentCulture, Resx.Dialog_InitializeMessage, folderPath)),
            };
            return await dialog.ShowDialog<bool>(this);
        });
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog
        {
            DataContext = new SettingsDialogViewModel(App.Services),
        };
        await dialog.ShowDialog(this);
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog
        {
            DataContext = new AboutDialogViewModel(),
        };
        await dialog.ShowDialog(this);
    }
}
