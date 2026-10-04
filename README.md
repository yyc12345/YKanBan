# YKanBan

My personal used local/offline kanban.

- Cross-platform desktop app (Windows / Linux / macOS) built with Avalonia.
- Git-like storage: the board lives in a `.ykanban` folder inside the project
  folder it manages; start it with `ykanban <path>`.
- One board per workspace with columns, cards and colored tags; strict search
  expressions (`tag:bug OR title:login`); Markdown export.
- English and Simplified Chinese interface; light, dark or system theme.

See **[USAGE.md](USAGE.md)** for installing, starting YKanBan, backups and
Git, and the full search syntax.

## Building

Requires the .NET 10 SDK.

```sh
dotnet build
dotnet test
dotnet run --project YKanBan -- <path>
```

Release archives for win-x64, linux-x64, osx-x64 and osx-arm64 are produced
by the manually triggered workflow in `.github/workflows/build.yml`;
`packaging/package.sh` packs one published RID.

## License

MIT — see [LICENSE](LICENSE).
