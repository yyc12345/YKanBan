using System;

using Avalonia.Controls;
using Avalonia.Controls.Templates;

using YKanBan.ViewModels;

namespace YKanBan;

public class ViewLocator : IDataTemplate {

    public Control? Build(object? param) {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null) {
            return (Control)Activator.CreateInstance(type)!;
        }

        // YYC MARK: the missing-view placeholder is a programmer error (a view
        // model without its matching view), not a user-facing message. It must be
        // caught by the code-behind convention test before release, so it is kept
        // as a hard-coded English diagnostic and is deliberately not localized.
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) {
        return data is ViewModelBase;
    }
}
