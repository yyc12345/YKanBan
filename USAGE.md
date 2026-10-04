# YKanBan Usage

YKanBan is a local/offline kanban board. It works like Git in one respect: all data for a managed project lives in a `.ykanban` folder at the project root, so the board travels with the project folder.

## 1. Workspace

- A **workspace** is a folder that directly contains a `.ykanban` directory. Only the given folder is checked; parent folders are never searched.
- `.ykanban` contains `ykanban.db` (the SQLite database with the board, cards and tags) and `ykanban.lock` (the exclusive lock file).
- A workspace is opened by exactly one YKanBan instance at a time. Opening the same workspace a second time shows the lock-conflict page.

### Command line

```
ykanban <path>
```

- `<path>` is the workspace folder to open.
- Starting without arguments, or with invalid arguments, shows the argument error page.
- If the path does not exist, the folder-missing page is shown.
- If the folder exists but has no `.ykanban`, the not-initialized page is shown with an **Initialize Workspace** button. Initializing creates the database and seeds the preset columns and tags in the current interface language.

## 2. Do not commit `.ykanban` to Git

`.ykanban` holds machine-local state and a lock file; it should not be committed. Add it to your **global** gitignore so it is ignored in every repository:

```
# ~/.gitignore_global  (or %USERPROFILE%\.gitignore_global on Windows)
.ykanban/
```

Then enable the global ignore file once:

```
git config --global core.excludesFile ~/.gitignore_global
```

## 3. Backing up and migrating a workspace

The database runs in SQLite WAL mode. A workspace is therefore **three files**, not one: `ykanban.db`, `ykanban.db-wal` and `ykanban.db-shm`.

To back up or move a workspace, copy **all three files together** while YKanBan is closed. Copying only `ykanban.db` can lose the most recent changes.

## 4. Search syntax

The board search runs only when you press the search button (or press Enter in the search box); there is no live search. A blank query shows everything. The clear button (×) empties the box and restores the full board.

Search keywords (`AND`, `OR`) and qualifier keys are fixed English grammar and are **never localized**.

### 4.1 Grammar

```
query     := orExpr
orExpr    := andExpr ( OR andExpr )*
andExpr   := operand ( (AND)? operand )*     // adjacent terms are an implicit AND
operand   := atom | '(' orExpr ')'
atom      := word | phrase | qualifier
qualifier := key ':' (word | phrase)
key       := tag | title | content | id | column
phrase    := '"' any text '"'                // escapes: \" to literal quote and \\ to literal backslash
```

### 4.2 Semantics

- `AND` (explicit or by adjacency) binds tighter than `OR`; parentheses force grouping.
- Only uppercase `AND` / `OR` are operators. Lowercase `and` / `or` are plain words. To search for the literal words `AND` or `OR`, quote them, for example `"AND"`.
- A bare word or phrase matches a **substring of the title OR the content**.
- `tag:NAME` matches a tag by its **exact** name.
- `column:TITLE` matches a column by its **exact** title.
- `title:TEXT` matches a substring of the card title, and `content:TEXT` matches a substring of the card content.
- `id:N` matches a card by its **exact** numeric id.
- All matching is case-insensitive (SQLite ASCII semantics).
- Quotes allow values with spaces, for example `tag:"release notes"` or `column:"In Progress"`.
- Inside a phrase, `\"` produces a literal quote and `\\` a literal backslash.

### 4.3 Strict validation

An invalid expression opens a modal error dialog and **does not run the search**. The error dialog is centered on the parent window; its text is a localized "query expression is invalid" message plus the parser's non-localized English detail. After closing it, the search box keeps what you typed and the board keeps the previous results.

The following are invalid:

- unbalanced parentheses, for example `(a` or `a)`;
- a dangling operator, for example `bug AND`;
- consecutive operators, for example `a AND OR b`;
- a qualifier without a value, for example `tag:`;
- an unterminated phrase, for example `"abc`;
- an invalid escape inside a phrase (a backslash not followed by `"` or `\`), for example `tag:"a\x"`;
- an unknown qualifier key. Only `tag`, `title`, `content`, `id` and `column` are valid, and the key is case-sensitive, so `Tag:x` is an error. A word whose colon is not preceded by an identifier (for example `:foo`) is treated as plain text.

## 5. Export

The **Export** button writes a human-readable Markdown snapshot of the whole board (columns, cards and the tag table) to a file you choose. The suggested file name and the timestamps in the document follow the current interface language. The export is read-only; there is no import.
