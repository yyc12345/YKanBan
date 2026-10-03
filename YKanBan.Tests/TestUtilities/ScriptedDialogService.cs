using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Tests.TestUtilities;

/// <summary>
/// Records every dialog shown and lets a test script answer it. The result
/// is whatever the dialog closed with; a dialog the script leaves open
/// counts as dismissed. The save-file dialog answers with
/// <see cref="SaveFilePath"/>.
/// </summary>
internal sealed class ScriptedDialogService : IDialogService
{
    public List<DialogViewModelBase> Shown { get; } = [];

    public Func<DialogViewModelBase, Task> Script { get; set; } = _ => Task.CompletedTask;

    public string? SaveFilePath { get; set; }

    public List<string> SuggestedFileNames { get; } = [];

    public async Task<bool> ShowAsync(DialogViewModelBase dialog)
    {
        Shown.Add(dialog);
        bool? result = null;
        dialog.CloseRequested += (_, closed) => result ??= closed;
        await Script(dialog);
        return result ?? false;
    }

    public Task<string?> PickSaveFilePathAsync(string suggestedFileName)
    {
        SuggestedFileNames.Add(suggestedFileName);
        return Task.FromResult(SaveFilePath);
    }

    /// <summary>Answers every confirmation dialog with the given choice.</summary>
    public static Func<DialogViewModelBase, Task> AnswerConfirmations(bool confirm) => dialog =>
    {
        if (dialog is ConfirmDialogViewModel confirmation)
        {
            if (confirm)
            {
                confirmation.ConfirmCommand.Execute(null);
                return Task.CompletedTask;
            }
            return confirmation.CancelCommand.ExecuteAsync(null);
        }
        return Task.CompletedTask;
    };
}
