using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YKanBan.ViewModels;

/// <summary>One sidebar workspace entry.</summary>
public partial class WorkspaceItemViewModel : ViewModelBase
{
    private readonly SidebarViewModel _owner;

    public WorkspaceItemViewModel(SidebarViewModel owner, string fullPath, string name, long lastEditedAt, bool isOpen)
    {
        _owner = owner;
        FullPath = fullPath;
        Name = name;
        LastEditedAt = lastEditedAt;
        IsOpen = isOpen;
    }

    /// <summary>Canonical workspace folder path.</summary>
    public string FullPath { get; }

    /// <summary>Display name: the folder name.</summary>
    public string Name { get; }

    /// <summary>Last edit time in Unix seconds, used by the "recently updated" sort.</summary>
    public long LastEditedAt { get; }

    /// <summary>Whether the workspace currently has an open session.</summary>
    [ObservableProperty]
    private bool isOpen;

    /// <summary>Opens (or switches to) this workspace.</summary>
    [RelayCommand]
    private void Open()
    {
        _owner.OpenWorkspace(this);
    }

    /// <summary>Releases the workspace resources and removes the sidebar entry.</summary>
    [RelayCommand]
    private void Remove()
    {
        _owner.RemoveWorkspace(this);
    }
}
