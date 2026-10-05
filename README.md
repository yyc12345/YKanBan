# YKanBan

A personal, local/offline kanban board. Cross-platform desktop app built with Avalonia UI and MVVM. Each workspace stores its data in a `.ykanban` folder at the project root, Git-style; start it with `ykanban <path>`.

- One board per workspace, with columns, cards and colored tags.
- Strict search expressions, for example `tag:bug OR title:login`.
- Markdown export of the whole board, and a tag manager.
- Multi-language interface; light, dark or system theme.

See [USAGE.md](USAGE.md) for installing, starting YKanBan, workspaces and backups, the search syntax and the settings file.

## Building

Requires the .NET 10 SDK.

```
dotnet build YKanBan.slnx
dotnet test YKanBan.slnx
dotnet run --project YKanBan -- <path>
```

Packages are pinned in `YKanBan/YKanBan.csproj`.

Release archives for win-x64, linux-x64 and osx-arm64 are produced by the manually triggered workflow in `.github/workflows/build.yml`; `packaging/package.sh` packs one published RID.

## License

MIT — see [LICENSE](LICENSE).
