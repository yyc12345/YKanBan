# YKanBan Usage

YKanBan is a local, offline kanban board. Like Git, it keeps its data in a `.ykanban` folder inside the project folder it manages (the **workspace**). Each YKanBan window manages exactly one workspace; several windows can run at the same time on different workspaces.

## Installing

Each release build is a self-contained archive; no .NET runtime is needed.

| Platform | Archive | Executable |
|---|---|---|
| Windows x64 | `YKanBan-<version>-win-x64.zip` | `YKanBan.exe` |
| Linux x64 | `YKanBan-<version>-linux-x64.tar.gz` | `YKanBan` |
| macOS Apple silicon | `YKanBan-<version>-osx-arm64.tar.gz` | `YKanBan.app` (see [macOS](#macos)) |

Only Apple silicon macOS is a release target; there is no Intel Mac build.

Extract the archive anywhere. To type `ykanban <path>` as shown below, put the executable on your `PATH` under that name, for example:

```sh
# Linux
ln -s /opt/YKanBan-<version>-linux-x64/YKanBan ~/.local/bin/ykanban
# macOS
ln -s /Applications/YKanBan.app/Contents/MacOS/YKanBan /usr/local/bin/ykanban
```

On Windows, add the extracted folder to `PATH`; `ykanban` then finds `YKanBan.exe` because Windows ignores case.

## Starting YKanBan

The only way to start YKanBan is with the workspace path:

```sh
ykanban <path>
```

- Exactly one argument is accepted. A relative path is resolved against the current working directory, so `ykanban .` opens the current folder (but see [macOS](#macos) when starting through `open`).
- YKanBan looks for `.ykanban` **only in `<path>` itself**; it does not search parent folders the way Git does.
- There are no command-line options. An argument starting with `-` is never treated as a path; to open a folder whose name starts with `-`, write it as `./-name`.

The path must exist and be a folder. When the board cannot be shown, the whole window shows one of these pages instead:

| Page | When |
|---|---|
| Invalid arguments | No argument, more than one argument, an empty or whitespace-only argument, an argument starting with `-` (including `--help`), a path the operating system cannot represent (for example one containing a NUL character), or a path that does not exist or is not a folder. Shows the usage line. |
| No workspace here | `<path>` exists but has no `.ykanban`. The **Initialize workspace** button creates one, with the columns To Do / In Progress / Done and the preset tags (both named in the current interface language). |
| Workspace locked | Another YKanBan window already has this workspace open. Close that window first. Also shown when another window takes the lock while you initialize. |
| Cannot open workspace | `.ykanban` exists but `ykanban.db` in it is missing (restore it from a backup, or delete `.ykanban` and initialize again; YKanBan never silently creates an empty board), the database was created by a newer YKanBan, is damaged or cannot be opened, or a file-system or permission error occurred (also when initializing fails for a reason other than the lock). The English error details can be selected and copied. |

The window title is `<workspace name> - YKanBan` while a workspace is open and just `YKanBan` on these pages.

## macOS

The app is not notarized. After extracting, remove the download quarantine once, or macOS refuses to open it:

```sh
xattr -dr com.apple.quarantine YKanBan.app
```

Double-clicking `YKanBan.app` starts it without a path, so it only shows the **Invalid arguments** page. Start it from a terminal instead, either through Launch Services or by running the executable inside the bundle:

```sh
open -n -a YKanBan --args /path/to/project
/Applications/YKanBan.app/Contents/MacOS/YKanBan /path/to/project
```

- Keep `-n`: without it, `open` only brings an already running YKanBan to the front and drops the path, instead of starting another window.
- Give `open` an **absolute** path. An app started by `open` runs in `/`, so `open -n -a YKanBan --args .` opens `/`, not the current folder. Running the executable inside the bundle resolves relative paths normally.
- `open -a YKanBan` finds the app once it is in `/Applications` (or another folder Launch Services knows about).

## Workspaces, Git and Backups

- **Do not commit `.ykanban` to Git.** Add it to your global ignore file once, so no repository picks it up. If `git config --global core.excludesFile` prints a file, add the line `.ykanban/` to that file; otherwise create one:

  ```sh
  echo ".ykanban/" >> ~/.gitignore_global
  git config --global core.excludesFile ~/.gitignore_global
  ```

- **Back up or move all three database files together**: `ykanban.db`, `ykanban.db-wal` and `ykanban.db-shm` in `.ykanban/`. Recent changes may still be in the `-wal` file; copying only `ykanban.db` can lose them. The simplest safe backup is to close YKanBan and copy the whole `.ykanban` folder.
- **Network shares (SMB, NFS, …) are not supported.** The database and the workspace lock rely on local file-system features that do not work reliably over a network. YKanBan does not detect this; keep workspaces on a local disk.
- `.ykanban/ykanban.lock` is held while a window has the workspace open and is released automatically when YKanBan exits or crashes. The file itself is kept empty; only the exclusive OS handle it carries has meaning.

## Search Syntax

Type an expression in the search box on the **Board** tab and click **Search** (or press Enter). Searching only happens then; there is no search while typing. The **×** button clears the box and shows every card again.

- Columns are always shown, even when none of their cards match.
- An empty or blank expression shows every card.
- An active search keeps the result set it produced until you search again: after adding, editing, moving or deleting cards the board shows the same matching cards as before, minus any that were deleted. Adding or editing a card does not change whether it is currently shown; press **Search** again to refresh the result.

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
phrase    := '"' one or more characters '"'   -- escapes: \" and \\
```

### Operators

- `AND` and `OR` are operators **only in uppercase**. `and` and `or` are ordinary words.
- Writing words next to each other means `AND`: `a b` is `a AND b`.
- `AND` binds tighter than `OR`: `a OR b c` is `a OR (b AND c)`. Use parentheses to group differently: `(a OR b) c`.
- To search for the literal text `AND` or `OR`, quote it: `"AND"`, `tag:"OR"`.
- The search keywords (`AND`, `OR`, `tag`, `title`, `content`, `id`, `column`) are always English, whatever the interface language. Qualifier keys are lowercase only: `Tag:bug` is an unknown qualifier.

### What Each Atom Matches

| Atom | Matches when |
|---|---|
| word or phrase | the card title **or** content contains the text |
| `title:` value | the card title contains the text |
| `content:` value | the card content contains the text |
| `tag:` value | the card has a tag whose name **equals** the text |
| `column:` value | the card is in a column whose title **equals** the text |
| `id:` value | the card number equals the value |

- A qualifier value can be a word (`tag:bug`) or a phrase (`tag:"needs review"`). Use a phrase for values containing spaces, `(`, `)`, `:` or `\`, and for the values `AND` / `OR`.
- `id:` takes an unquoted positive integer, optionally prefixed with `#`: `id:61`, `id:#61`.

### Quoting and Escapes

- A phrase is text in double quotes. Inside it, `\"` stands for a quote and `\\` for a backslash; any other backslash is an error.
- Outside quotes, `\` is not allowed at all, and `:` always starts a qualifier. Quote any text containing them: `"C:\\temp"`, `"a:b"`.
- A phrase cannot be empty, and must be separated from neighbouring words and phrases by a space or a parenthesis: write `a "b"`, not `a"b"`.
- A qualifier's phrase value follows the `:` directly: `tag:"needs review"`, not `tag: "needs review"`.

### Case

All matching ignores case, for letters of every language: `tag:BUG` finds `bug`, and `ÄRGER` finds `ärger`. Text is otherwise compared literally: `%`, `_` and `*` have no special meaning.

Tag names and column titles must each be unique, but the uniqueness check is case-sensitive: `Bug` and `bug` are different names and can both exist. Because matching folds case, a query such as `tag:bug` then matches both.

### Invalid Expressions

An invalid expression does not search. A dialog shows "The query expression is invalid. Details: …" followed by the parser's message (always in English). After closing it, the box keeps your text for correcting and the board keeps the previous result.

These are invalid:

| Problem | Example |
|---|---|
| Unbalanced parentheses | `(tag:bug`, `tag:bug)` |
| An operator with a missing side | `bug AND`, `OR bug` |
| Consecutive operators | `a AND OR b` |
| A qualifier without a value | `tag:` |
| `AND` / `OR` as an unquoted qualifier value | `tag:AND` (write `tag:"AND"`) |
| An unterminated phrase | `"dark mode` |
| An empty phrase | `""`, `tag:""` |
| A phrase touching other text | `a"b"`, `"a"b`, `"a""b"` |
| A space between a qualifier and its phrase value | `tag: "needs review"` |
| An invalid escape in a phrase | `tag:"a\x"` |
| A backslash outside a phrase | `C:\temp` |
| An unknown qualifier key | `label:bug`, `Tag:bug`, `http://x` |
| An unquoted value containing `:` | `title:a:b` |
| An `id:` value that is not an unquoted positive integer | `id:abc`, `id:0`, `id:"61"` |

## Settings File

Settings (language, theme, card sort order, column width and the confirmation switches) are stored in `app.json`:

| Platform | Location |
|---|---|
| Windows | `%APPDATA%\YKanBan\app.json` |
| Linux, macOS | `~/.config/YKanBan/app.json` (`$XDG_CONFIG_HOME/YKanBan/app.json` when that is set) |

- The file is written when YKanBan exits. With several instances running, the last one to exit overwrites the file with its own settings.
- A language change takes effect after restarting YKanBan; a theme change applies as soon as the settings dialog is accepted.
- A missing file is normal on first run and yields the defaults. If the file exists but cannot be read (an I/O or permission error), the defaults are used.
- If the file cannot be parsed, it is saved as `app.json.bak` and the defaults are used. This also covers an invalid enum value: an unknown theme, sort or width string, or a numeric enum value, makes the whole file fall back to defaults. A section that is simply missing falls back to its own defaults.
