namespace YKanBan.ViewModels.Pages;

/// <summary>A failure state page: a localized title with a detail line.</summary>
public sealed class MessagePageViewModel : ViewModelBase
{
    public MessagePageViewModel(string title, string detail)
    {
        Title = title;
        Detail = detail;
    }

    /// <summary>Localized failure heading.</summary>
    public string Title { get; }

    /// <summary>Localized failure explanation.</summary>
    public string Detail { get; }
}
