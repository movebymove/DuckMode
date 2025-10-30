# DuckMode

AI chat + reminders (WPF). Supports local Ollama or Gemini cloud.

## Quick start (run without building)
1. Download release zip (or build/publish below).
2. In the same folder as `DuckMode.App.exe`, create a file named `gemini.key` containing your Gemini API key on a single line.
3. Run `DuckMode.App.exe`.

Alternative to step 2: set an environment variable before running the app:

```powershell
setx GEMINI_API_KEY "YOUR_GEMINI_API_KEY_HERE"
```

Or for current PowerShell session only:

```powershell
$env:GEMINI_API_KEY = "YOUR_GEMINI_API_KEY_HERE"
```

## Build

```powershell
dotnet build DuckMode.sln -c Release
```

## Publish (single-file, self-contained)

```powershell
dotnet publish DuckMode.App/DuckMode.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeAllContentForSelfExtract=true
```

Output folder: `DuckMode.App/bin/Release/net8.0-windows/win-x64/publish`.

Place `gemini.key` next to `DuckMode.App.exe` in that folder, then zip and share.

You can provide a template file for users:

```text
gemini.key.example  -> copy/rename to  gemini.key  and paste your API key
```

## Notes
- `gemini.key` is ignored by git (see `.gitignore`). Do not commit real keys.
- You can switch to Ollama locally if desired (configure in DI and ensure the model is present in your Ollama).
- Reminders support: task reminders (`/r ...`), water breaks, and movement breaks with Psyduck pop-ups.


