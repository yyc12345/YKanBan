# YKanBan

A personal, local/offline kanban board. Cross-platform desktop app built with Avalonia UI and MVVM. Each workspace stores its data in a `.ykanban` folder at the project root, Git-style.

See [USAGE.md](USAGE.md) for workspaces, backups, the search syntax and command-line usage.

## Building

Requires the .NET 10 SDK.

```
dotnet build YKanBan.slnx
dotnet test YKanBan.slnx
dotnet run --project YKanBan -- <path>
```

Packages are pinned in `YKanBan/YKanBan.csproj`.

Release archives for win-x64, linux-x64, osx-x64 and osx-arm64 are produced by the manually triggered workflow in `.github/workflows/build.yml`; `packaging/package.sh` packs one published RID.

## License

MIT — see [LICENSE](LICENSE).
