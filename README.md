# md2html

[![build](https://github.com/compumaster/md2html/actions/workflows/build.yml/badge.svg)](https://github.com/compumaster/md2html/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/compumaster/md2html)](https://github.com/compumaster/md2html/releases/latest)

> **Download:** the latest Windows build is on the **[Releases page](https://github.com/compumaster/md2html/releases/latest)**.

Windows tray app that turns terminal-copied LLM/CLI output or Markdown on the clipboard into rich HTML you can paste into Word, Outlook, Teams, OneNote, Google Docs, etc.

It fixes what breaks when you copy from a terminal:

- Box-drawing tables (`┌─┬─┐ │ │`, ASCII `+---+`) become real tables. Wrapped cells are joined, and URLs that were split mid-word are rejoined.
- Hard-wrapped paragraphs and list items are joined. Short lines such as `Link:` / `Folder:` keep their line breaks.
- The terminal margin and Claude Code's `●` marker are removed.
- Short standalone titles that come right before a list or table (CLI renderers drop the `#`/`**` markup) are made bold.
- Plain Markdown works as well: headings, lists, pipe tables, code, links, and task lists.

## Install

### From a release (no build tools needed)

1. Download one of these from the [latest release](https://github.com/compumaster/md2html/releases/latest):
   - `md2html-win-x64.zip`: works without anything else installed.
   - `md2html-win-x64-framework-dependent.zip`: much smaller, but needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Extract it to a folder you will keep, for example `%LOCALAPPDATA%\Programs\md2html`.
3. Run `md2html.exe`.

### From source

Requires the .NET 10 SDK.

```powershell
./install.ps1
```

This publishes to `%LOCALAPPDATA%\Programs\md2html` and starts the app.

### After installing

On launch the app adds itself to `HKCU\...\CurrentVersion\Run`, so it starts with Windows. If you move the exe, run it once from the new location and the entry follows.

Windows 11 hides new tray icons in the overflow (`^`). To keep it visible, drag the blue **MD** icon onto the taskbar, or turn it on under *Settings → Personalization → Taskbar → Other system tray icons*.

## Use

1. Copy the text from the terminal.
2. **Left-click** the MD tray icon.
3. Paste with Ctrl+V.

The clipboard then holds two formats:

- HTML, for rich editors.
- The cleaned Markdown as plain text, for plain-text targets.

Right-click menu: *Convert clipboard to HTML*, *Start with Windows* (toggle), *Show notifications* (toggle), *Exit*.

`md2html.exe --once` converts the clipboard and exits. You can bind this to a keyboard shortcut through a `.lnk` file's *Shortcut key*.

## Uninstall

```powershell
./uninstall.ps1
```

This stops the app, removes the autostart entry and settings, and deletes `%LOCALAPPDATA%\Programs\md2html`. If you extracted a release somewhere else, run the script and then delete that folder yourself.

## Develop

```powershell
dotnet test
```

- `src/Md2Html.Core`: normalization (`TextNormalizer`, `BoxTable`), Markdown→HTML (`HtmlConverter`, Markdig), and the CF_HTML clipboard payload (`ClipboardHtml`).
- `src/Md2Html`: the WinForms tray app.

### Releasing

GitHub Actions ([`.github/workflows/build.yml`](.github/workflows/build.yml)) builds and tests every push and pull request. Pushing a `v*` tag also publishes both zips to a GitHub release:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

## License

MIT; see [LICENSE](LICENSE). Bundled third-party components are listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
