using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Services;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Pages;

namespace YKanBan.ViewModels;

/// <summary>
/// The sidebar: welcome and open-project buttons, the persistent workspace
/// list with its sort options, and the settings/about entries at the bottom.
/// </summary>
public partial class SidebarViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _owner;
    private readonly AppServices _services;

    public SidebarViewModel(MainWindowViewModel owner, AppServices services)
    {
        _owner = owner;
        _services = services;

        SortOptions =
        [
            new OptionItem(SidebarSortOption.NameAsc, Resources.Sidebar_Sort_NameAsc),
            new OptionItem(SidebarSortOption.NameDesc, Resources.Sidebar_Sort_NameDesc),
            new OptionItem(SidebarSortOption.RecentlyUpdated, Resources.Sidebar_Sort_RecentlyUpdated),
        ];
        selectedSort = SortOptions.Single(option => option.Value.Equals(services.Config.Sort.Sidebar));

        RebuildWorkspaceList();
    }

    /// <summary>Known workspaces in display order.</summary>
    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    /// <summary>The available list sort modes.</summary>
    public IReadOnlyList<OptionItem> SortOptions { get; }

    /// <summary>Asks the view for a folder via the system folder picker; null when cancelled.</summary>
    public Interaction<Unit, string?> PickFolder { get; } = new();

    /// <summary>Asks the view to confirm initializing a plain folder as a workspace.</summary>
    public Interaction<string, bool> ConfirmInitialize { get; } = new();
    /// <summary>The selected list sort mode; persisted with the configuration.</summary>
    [ObservableProperty]
    private OptionItem? selectedSort;

    /// <summary>Shows the welcome page.</summary>
    [RelayCommand]
    private void ShowWelcome()
    {
        _owner.ShowWelcome();
    }

    /// <summary>Picks a folder and opens it as a workspace (offering initialization).</summary>
    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        string? folder = await PickFolder.Handle(default);
        if (folder is null)
        {
            return;
        }
        await _owner.OpenWorkspaceWithInitializePromptAsync(folder);
    }

    /// <summary>Rebuilds the list from the persistent registry, marking open workspaces.</summary>
    public void RebuildWorkspaceList()
    {
        Workspaces.Clear();
        foreach (WorkspaceRecord record in _services.Database.GetWorkspaces())
        {
            bool isOpen = _services.Pool.Find(record.Path) is not null;
            Workspaces.Add(new WorkspaceItemViewModel(this, record.Path, Path.GetFileName(record.Path), record.LastEditedAt, isOpen));
        }
        ReorderWorkspaceList();
    }

    /// <summary>Opens a workspace when its sidebar entry is activated.</summary>
    public void OpenWorkspace(WorkspaceItemViewModel item)
    {
        _owner.TryOpenWorkspaceSession(item.FullPath);
    }

    /// <summary>Releases the workspace resources and forgets its sidebar entry.</summary>
    public void RemoveWorkspace(WorkspaceItemViewModel item)
    {
        _services.Pool.Remove(item.FullPath);
        RebuildWorkspaceList();

        // Removing the active workspace falls back to the welcome page.
        if (_owner.CurrentContent is WorkspacePageViewModel active && active.FullPath == item.FullPath)
        {
            _owner.ShowWelcome();
        }
    }

    /// <summary>Applies the selected sort mode to the list in place.</summary>
    partial void OnSelectedSortChanged(OptionItem? value)
    {
        if (value is null)
        {
            return;
        }
        _services.Config.Sort.Sidebar = (SidebarSortOption)value.Value;
        ReorderWorkspaceList();
    }

    /// <summary>Sorts the workspace collection by the current mode.</summary>
    private void ReorderWorkspaceList()
    {
        if (SelectedSort?.Value is not SidebarSortOption mode)
        {
            return;
        }

        Comparison<WorkspaceItemViewModel> comparison = mode switch
        {
            SidebarSortOption.NameAsc => (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
            SidebarSortOption.NameDesc => (a, b) => string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase),
            SidebarSortOption.RecentlyUpdated => (a, b) => b.LastEditedAt.CompareTo(a.LastEditedAt),
            _ => (a, b) => 0,
        };

        // Move each item into its sorted position (in-place keeps bindings alive).
        List<WorkspaceItemViewModel> ordered = Workspaces.ToList();
        ordered.Sort(comparison);
        for (int index = 0; index < ordered.Count; index++)
        {
            int current = Workspaces.IndexOf(ordered[index]);
            if (current != index)
            {
                Workspaces.Move(current, index);
            }
        }
    }
}
