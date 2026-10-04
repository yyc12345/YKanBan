# Manual acceptance checklist

Everything here is verified by hand on the local Windows build. Automated tests cover storage, the search parser/compiler and the export; the UI is verified manually.

## Startup and workspace

- [ ] `ykanban <path>` with a valid workspace opens the board.
- [ ] `ykanban` with no arguments shows the argument error page.
- [ ] `ykanban a b` (more than one argument) shows the argument error page.
- [ ] A path that does not exist shows the folder-missing page.
- [ ] An existing folder without `.ykanban` shows the not-initialized page with an **Initialize Workspace** button; after clicking it the board opens with the preset columns and tags.
- [ ] Opening the same workspace in a second instance shows the lock-conflict page; closing the first instance lets the second open.
- [ ] The window title is `<workspace name> - YKanBan`, or just `YKanBan` on an error page.

## Language

- [ ] A new workspace is seeded with columns and tags in the language active at creation time.
- [ ] Switching the language in Settings shows the "takes effect after restart" notice and the change only applies after a restart (restart-effective item).
- [ ] After restarting in Chinese, the whole UI is Chinese.

## Theme

- [ ] Settings offers light / dark / follow system; switching applies immediately and the interface recolors completely (theme three-state item).
- [ ] Cancelling Settings keeps the previous theme; accepting keeps the chosen one.
- [ ] Custom surfaces (top bar, columns, cards, error text) recolor with the theme.
- [ ] Tag colors always show their raw `#RRGGBB` value in both themes.

## Board

- [ ] Columns run left to right and cards top to bottom; there is no drag.
- [ ] A card shows the bold `#id` with its title (or just `#id` when untitled), a truncated content preview and colored tag badges; no timestamps.
- [ ] Clicking a card body opens the editor; the card menu opens in place.
- [ ] Add, edit and delete a column and a card; deletes honor the confirmation toggles.
- [ ] The column editor rejects an empty or duplicate title with an inline error.
- [ ] Editing a card with no effective change writes nothing; cancelling writes nothing; closing with unsaved changes asks to discard when that toggle is on.
- [ ] Tag assignment in the card editor adds existing tags and removes them with the ×; the picker only offers tags not already assigned.
- [ ] Move a card to another column; with only one column the picker reports there is nowhere to move.
- [ ] The Display flyout changes column width (narrow/standard/wide/custom) and card/column sort immediately.

## Search

- [ ] Pressing the search button and pressing Enter in the search box behave the same.
- [ ] A blank query shows everything; the clear button restores the full board.
- [ ] A valid query filters cards while keeping every column (empty columns stay visible).
- [ ] The filter status icon is dimmed when not filtering and solid while filtering, and its tooltip states the current status.
- [ ] An invalid query opens the error dialog with the localized message plus the English detail; after closing, the typed text and the previous results remain.
- [ ] Search state persists while switching tabs.

## Tags

- [ ] The tag tab lists color, name, description and usage count.
- [ ] Add, rename, recolor (with the color picker) and re-describe a tag; the board refreshes after a change.
- [ ] Deleting a tag warns that it will be removed from N cards and honors the confirmation toggle.

## Export

- [ ] Export prompts for a file with a language-appropriate default name and writes Markdown with language-appropriate timestamps, matching the current interface language (export-language-follows-UI item).
- [ ] After a successful export a confirmation dialog shows the written path.
