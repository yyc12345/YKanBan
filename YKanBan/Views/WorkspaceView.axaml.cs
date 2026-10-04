using System.Text;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using YKanBan.Export;
using YKanBan.ViewModels;
using YKanBan.ViewModels.Dialogs;
using YKanBan.Views.Dialogs;

using Resx = YKanBan.Resources;

namespace YKanBan.Views;

public partial class WorkspaceView : UserControl {
    private bool _registered;

    public WorkspaceView() {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);
        if (DataContext is WorkspaceViewModel viewModel && !_registered) {
            _registered = true;
            viewModel.Export.RegisterHandler(ExportAsync);
        }
    }

    /// <summary>
    /// Prompts for a file and writes the Markdown board export to it.
    /// </summary>
    private async Task ExportAsync(Unit input) {
        if (DataContext is not WorkspaceViewModel viewModel) {
            return;
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) {
            return;
        }

        DateTimeOffset exportedAt = DateTimeOffset.Now;
        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = Resx.Main_Export,
            SuggestedFileName = MarkdownExporter.BuildFileName(viewModel.WorkspaceName, exportedAt),
            DefaultExtension = "md",
            FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }],
        });
        if (file is null) {
            return;
        }

        var repository = viewModel.Session.Repository;
        string markdown = MarkdownExporter.Export(
            viewModel.WorkspaceName,
            repository.GetColumns(),
            repository.GetCards(),
            repository.GetTagsWithUsage(),
            exportedAt);

        await using (Stream stream = await file.OpenWriteAsync())
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))) {
            await writer.WriteAsync(markdown);
        }

        if (TopLevel.GetTopLevel(this) is Window owner) {
            var dialog = new ExportDoneDialog {
                DataContext = new ExportDoneViewModel(file.TryGetLocalPath() ?? file.Name),
            };
            await ModalWindow.ShowAsync(owner, dialog);
        }
    }
}
