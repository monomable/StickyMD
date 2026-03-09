# StickyMD

StickyMD is a lightweight WPF sticky note app for Windows with Markdown support.

## Features

- Windows Sticky Notes style main board + individual note windows
- Markdown rendering (Markdig)
- Multi-note management, search, color tags
- Auto-save (1s debounce) + save on exit
- JSON storage: `%AppData%/StickyMD/notes.json`
- Tray mode (New Note / Show Notes / Quit)
- Single-instance app (secondary launch activates existing instance)
- Start with Windows (Registry Run key)
- Keyboard shortcuts
  - `Ctrl+N` new note
  - `Ctrl+F` focus search
  - `Ctrl+B` bold
  - `Ctrl+I` italic
  - `Ctrl+S` save

## Build

```bash
dotnet build StickyMD.sln
```

## Run

```bash
dotnet run --project StickyMD.csproj
```

## Publish (Single File)

```bash
dotnet publish StickyMD.csproj -c Release
```

Output:

`bin/Release/net8.0-windows/win-x64/publish/StickyMD.exe`

> Release publish is configured as **single-file, framework-dependent** to keep the EXE size small.
