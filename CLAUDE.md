# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet restore       # Install dependencies
dotnet build         # Debug build
dotnet build --configuration Release  # Release build
dotnet run           # Run the app
dotnet clean         # Clean build artifacts
```

No test framework is configured.

## Architecture

**PoliCoLauncher** is a single-window Avalonia UI desktop app (C#/.NET 8.0) for managing train connections in a multiplayer train simulation (Poli&Co).

### Page flow

The app uses a single `MainWindow` with 5 pages toggled via visibility — there is no router or navigation stack:

1. **WelcomePage** — animated intro splash
2. **DashboardPage** — main menu
3. **RoutePage** — select start/end stations (Radna ↔ Arad routes)
4. **TrainSelectPage** — configure train type, number, locomotive, wagons, departure time, intermediate stops
5. **FinalPage** — timetable display and multiplayer connection (sends data to remote server)

### Key files

- `Program.cs` — entry point, bootstraps Avalonia
- `App.axaml / App.axaml.cs` — app initialization, loads MainWindow
- `MainWindow.axaml` — all UI layout for all 5 pages
- `MainWindow.axaml.cs` — all application logic (~977 lines): page navigation, animations, form handling, timetable generation, API calls, toast notifications

### External API

Two HTTP endpoints on `116.203.229.254`:
- `GET :5001/get_news` — fetch version/news for the dashboard
- `POST :3000/update_hud` — push active train connection data (train type, number, route, times, wagons, locomotive)

### Data model

All state is held as fields directly on `MainWindow`: train type/number/locomotive, route stations, departure time, intermediate stop times, wagon count/numbers, and connection flags. There is no separate data/model layer.

### Dependencies

- `Avalonia 12.0.0` + `Avalonia.Themes.Fluent` — UI framework with Fluent Design theme
- `Newtonsoft.Json 13.0.4` — JSON serialization for API payloads
