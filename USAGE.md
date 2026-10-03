# YKanBan usage

YKanBan is a local, offline kanban board. Like Git, it keeps its data in a
`.ykanban` folder inside the project folder it manages (the **workspace**).
Each YKanBan window manages exactly one workspace; several windows can run at
the same time on different workspaces.

- [Installing](#installing)
- [Starting YKanBan](#starting-ykanban)
- [macOS](#macos)
- [Workspaces, Git and backups](#workspaces-git-and-backups)
- [Search syntax](#search-syntax)
- [Settings file](#settings-file)

## Installing

Each release build is a self-contained archive; no .NET runtime is needed.

| Platform | Archive | Executable |
|---|---|---|
| Windows x64 | `YKanBan-<version>-win-x64.zip` | `YKanBan.exe` |
| Linux x64 | `YKanBan-<version>-linux-x64.tar.gz` | `YKanBan` |
| macOS Intel | `YKanBan-<version>-osx-x64.tar.gz` | `YKanBan.app` (see [macOS](#macos)) |
| macOS Apple silicon | `YKanBan-<version>-osx-arm64.tar.gz` | `YKanBan.app` (see [macOS](#macos)) |

Extract the archive anywhere. To type `ykanban <path>` as shown below, put the
executable on your `PATH` under that name, for example:

```sh
# Linux
ln -s /opt/YKanBan-<version>-linux-x64/YKanBan ~/.local/bin/ykanban
# macOS
ln -s /Applications/YKanBan.app/Contents/MacOS/YKanBan /usr/local/bin/ykanban
```

On Windows, add the extracted folder to `PATH`; `ykanban` then finds
`YKanBan.exe` because Windows ignores case.

## Starting YKanBan

The only way to start YKanBan is with the workspace path:

```sh
ykanban <path>
```

- Exactly one argument is accepted. A relative path is resolved against the
  current working directory, so `ykanban .` opens the current folder.
- YKanBan looks for `.ykanban` **only in `<path>` itself**; it does not search
  parent folders the way Git does.
- There are no command-line options. An argument starting with `-` is never
  treated as a path; to open a folder whose name starts with `-`, write it as
  `./-name`.

When the board cannot be shown, the whole window shows one of these pages
instead:

| Page | When |
|---|---|
| Invalid arguments | No argument, more than one argument, an empty argument, or an argument starting with `-` (including `--help`). Shows the usage line. |
| Folder not found | `<path>` does not exist or is not a folder. |
| No workspace here | `<path>` exists but has no `.ykanban`. The **Initialize workspace** button creates one, with the columns To Do / In Progress / Done (named in the current interface language). |
| Workspace locked | Another YKanBan window already has this workspace open. Close that window first. |
| Cannot open workspace | The database was created by a newer YKanBan, is damaged or cannot be opened, or a file-system or permission error occurred (also when initializing fails). The English error details can be selected and copied. |

The window title is `<workspace name> - YKanBan` while a workspace is open and
just `YKanBan` on these pages.

## macOS

The app is not notarized. After extracting, remove the download quarantine
once, or macOS refuses to open it:

```sh
xattr -dr com.apple.quarantine YKanBan.app
```

Double-clicking `YKanBan.app` starts it without a path, so it only shows the
**Invalid arguments** page. Start it from a terminal instead, either through
Launch Services or by running the executable inside the bundle:

```sh
open -a YKanBan --args /path/to/project
/Applications/YKanBan.app/Contents/MacOS/YKanBan /path/to/project
```

`open -a YKanBan` finds the app once it is in `/Applications` (or another
folder Launch Services knows about).

## Workspaces, Git and backups

- **Do not commit `.ykanban` to Git.** Add it to your global ignore file once,
  so no repository picks it up. If `git config --global core.excludesFile`
  prints a file, add the line `.ykanban/` to that file; otherwise create one:

  ```sh
  echo ".ykanban/" >> ~/.gitignore_global
  git config --global core.excludesFile ~/.gitignore_global
  ```

- **Back up or move all three database files together**: `ykanban.db`,
  `ykanban.db-wal` and `ykanban.db-shm` in `.ykanban/`. Recent changes may
  still be in the `-wal` file; copying only `ykanban.db` can lose them. The
  simplest safe backup is to close YKanBan and copy the whole `.ykanban`
  folder.
- **Network shares (SMB, NFS, …) are not supported.** The database and the
  workspace lock rely on local file-system features that do not work reliably
  over a network. YKanBan does not detect this; keep workspaces on a local
  disk.
- `.ykanban/ykanban.lock` is held while a window has the workspace open and is
  released automatically when YKanBan exits or crashes.

## Search syntax

Type an expression in the search box on the **Board** tab and click
**Search** (or press Enter). Searching only happens then; there is no search
while typing. The **×** button clears the box and shows every card again.

- Columns are always shown, even when none of their cards match.
- An empty or blank expression shows every card.
- After any change (adding, editing, moving or deleting cards, columns or
  tags), the board re-runs the **last expression that searched successfully**,
  not whatever is in the box at that moment. Cards that stop matching
  disappear; new cards that do not match are not shown.

### Examples

| Expression | Finds cards… |
|---|---|
| `login` | whose title or content contains "login" |
| `login crash` | containing both words (anywhere in title or content) |
| `login OR signup` | containing either word |
| `tag:bug title:login` | tagged `bug` whose title contains "login" |
| `tag:bug OR tag:ui` | tagged `bug` or `ui` |
| `(tag:bug OR tag:ui) column:Done` | tagged `bug` or `ui` in the column titled "Done" |
| `"dark mode"` | whose title or content contains "dark mode" |
| `tag:"needs review"` | tagged `needs review` |
| `id:61` or `id:#61` | number 61 |
| `"http://example.com"` | containing the URL (a `:` must be quoted) |
| `"AND"` | containing the text "AND" |

### Grammar

```
query     := orExpr
orExpr    := andExpr ( OR andExpr )*
andExpr   := operand ( (AND)? operand )*      -- neighbours are joined by AND
operand   := atom | '(' orExpr ')'
atom      := word | phrase | qualifier
qualifier := key ':' value
key       := tag | title | content | id | column
value     := word | phrase                    -- tag / title / content / column
           | '#'? digit+                       -- id: a positive decimal integer
word      := one or more characters, except whitespace and ( ) " : \ ;
             not equal to AND or OR
phrase    := '"' any text '"'                 -- escapes: \" and \\
```

### Operators

- `AND` and `OR` are operators **only in uppercase**. `and` and `or` are
  ordinary words.
- Writing words next to each other means `AND`: `a b` is `a AND b`.
- `AND` binds tighter than `OR`: `a OR b c` is `a OR (b AND c)`. Use
  parentheses to group differently: `(a OR b) c`.
- To search for the literal text `AND` or `OR`, quote it: `"AND"`,
  `tag:"OR"`.
- The search keywords (`AND`, `OR`, `tag`, `title`, `content`, `id`,
  `column`) are always English, whatever the interface language. Qualifier
  keys are lowercase only: `Tag:bug` is an unknown qualifier.

### What each atom matches

| Atom | Matches when |
|---|---|
| word or phrase | the card title **or** content contains the text |
| `title:` value | the card title contains the text |
| `content:` value | the card content contains the text |
| `tag:` value | the card has a tag whose name **equals** the text |
| `column:` value | the card is in a column whose title **equals** the text |
| `id:` value | the card number equals the value |

- A qualifier value can be a word (`tag:bug`) or a phrase
  (`tag:"needs review"`). Use a phrase for values containing spaces, `(`,
  `)`, `:` or `\`, and for the values `AND` / `OR`.
- `id:` takes an unquoted positive integer, optionally prefixed with `#`:
  `id:61`, `id:#61`.

### Quoting and escapes

- A phrase is text in double quotes. Inside it, `\"` stands for a quote and
  `\\` for a backslash; any other backslash is an error.
- Outside quotes, `\` is not allowed at all, and `:` always starts a
  qualifier. Quote any text containing them: `"C:\\temp"`, `"a:b"`.

### Case

All matching ignores case, for letters of every language: `tag:BUG` finds
`bug`, and `ÄRGER` finds `ärger`. Text is otherwise compared literally: `%`,
`_` and `*` have no special meaning.

Tag names and column titles must be unique ignoring case for the letters A–Z
only. Two tags such as `Ä` and `ä` can therefore both exist, and `tag:ä`
matches both.

### Invalid expressions

An invalid expression does not search. A dialog shows "The query expression is
invalid. Details: …" followed by the parser's message (always in English).
After closing it, the box keeps your text for correcting and the board keeps
the previous result.

These are invalid:

| Problem | Example |
|---|---|
| Unbalanced parentheses | `(tag:bug`, `tag:bug)` |
| An operator with a missing side | `bug AND`, `OR bug` |
| Consecutive operators | `a AND OR b` |
| A qualifier without a value | `tag:` |
| `AND` / `OR` as an unquoted qualifier value | `tag:AND` (write `tag:"AND"`) |
| An unterminated phrase | `"dark mode` |
| An invalid escape in a phrase | `tag:"a\x"` |
| A backslash outside a phrase | `C:\temp` |
| An unknown qualifier key | `label:bug`, `Tag:bug`, `http://x` |
| An unquoted value containing `:` | `title:a:b` |
| An `id:` value that is not an unquoted positive integer | `id:abc`, `id:0`, `id:"61"` |

## Settings file

Settings (language, theme, card sort order, column width and the confirmation
switches) are stored in `app.json`:

| Platform | Location |
|---|---|
| Windows | `%APPDATA%\YKanBan\app.json` |
| Linux, macOS | `~/.config/YKanBan/app.json` (`$XDG_CONFIG_HOME/YKanBan/app.json` when that is set) |

- The file is written when YKanBan exits, and only if a setting was changed in
  that session. With several windows open, the last one closed after changing
  a setting wins.
- A language change takes effect after restarting YKanBan; a theme change
  applies immediately.
- If the file cannot be parsed, it is saved as `app.json.bak` and the defaults
  are used. A single invalid value falls back to its default; the other
  values are kept.
