# Elsun-Siren

Windows siren reminder app that can run every hour (or any interval) between a start and end time.

## Features

- Scheduler with configurable window (default 08:00 to 20:00)
- Robust trigger timing (does not miss alarm due to timer second drift)
- Custom interval in minutes (default 60)
- Validation for invalid time ranges (end time must be after start time)
- Weekday-only mode
- Start with Windows (current user)
- Start minimized to tray
- Tray icon controls (open, trigger now, exit)
- Optional balloon notifications
- Optional window flash signal
- Built-in siren tone with configurable frequencies and durations
- Optional custom WAV file siren
- Repeat count and pause settings
- Persistent JSON settings under `%APPDATA%/ElsunSiren/settings.json`

## Build

1. Install the .NET 8 SDK for Windows.
2. Build:

```bash
dotnet build SirenScheduler/SirenScheduler.csproj -c Release
```

3. Publish single EXE:

```bash
dotnet publish SirenScheduler/SirenScheduler.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Published EXE output is in:

`SirenScheduler/bin/Release/net8.0-windows/win-x64/publish/`

## Usage

- Run `SirenScheduler.exe`.
- Configure times and interval (for your case: start `08:00`, end `20:00`, interval `60`).
- Click **Save settings**.
- Optionally click **Start with Windows** to auto-run on PC startup.
- Use **Test siren now** to verify signal/sound.

