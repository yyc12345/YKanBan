# YKanBan

A personal, local/offline kanban board. Cross-platform desktop app built with Avalonia UI and MVVM. Each workspace stores its data in a `.ykanban` folder at the project root, Git-style.

See [USAGE.md](USAGE.md) for workspaces, backups, the search syntax and command-line usage.

## Build and test

```
dotnet build YKanBan.slnx
dotnet test YKanBan.slnx
```

Packages are pinned in `YKanBan/YKanBan.csproj`.
