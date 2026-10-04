using Avalonia.Controls;
using Avalonia.Controls.Templates;
using YKanBan.ViewModels;

namespace YKanBan;

/// <summary>
/// Resolves the view of a view model by naming convention: the type name with
/// "ViewModel" replaced by "View". A missing view is a programming error; it
/// shows a localized placeholder naming the type that was looked for.
/// </summary>
public class ViewLocator : IDataTemplate
{
    /// <inheritdoc/>
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        string name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        Type? type = Type.GetType(name);
        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = string.Format(YKanBan.Resources.Culture, YKanBan.Resources.Error_ViewNotFound, name) };
    }

    /// <inheritdoc/>
    public bool Match(object? data) => data is ViewModelBase;
}
