
Getting Started
===============

This page walks you through getting the repository, building the solution, running the demo app, and common troubleshooting steps.

Prerequisites
- Windows 10/11 (primary target) — Orivy is developed for desktop Windows but the core library is cross-platform where Skia supports it.
- .NET SDK 8.x (or later)
- `dotnet` CLI or Visual Studio 2022/2023
- Optional: Visual Studio with the .NET desktop workload for easier debugging

Clone and build

```powershell
git clone https://github.com/myildirimofficial/orivy.git
cd sdui
dotnet build Orivy.sln -c Debug
```

Run the example app

```powershell
dotnet run --project Orivy.Example/Orivy.Example.csproj -c Debug
```

If you prefer Visual Studio, open `Orivy.sln`, set `Orivy.Example` as the startup project and press F5.

Development workflow and tips
- Hot code iteration: modify code in `Orivy` project and re-run the example app. The solution is small enough for quick builds; use Release builds for performance testing.
- Logs and diagnostics: message loop exceptions are written to Debug output (`System.Diagnostics.Debug`). Use Visual Studio Output window or attach a debugger.
- Fonts: Application.SharedDefaultFont sets a default SKFont. If you see typography differences, ensure the target system has the expected font family (default tries "Inter", falls back to SKTypeface.Default).

Troubleshooting
- Skia native dependencies: the NuGet packages include native runtimes for common platforms. If Skia fails to initialize, examine the application output for SkiaSharp errors and ensure the right runtime package is referenced for your target platform.
- GPU (DirectX) rendering: the project attempts to use a DirectX11 path when available. Ensure GPU drivers are up-to-date. If the GPU path fails, the library falls back to CPU rendering.
- Build errors: run `dotnet restore` then `dotnet build` to ensure package restore completed. If you see missing types, confirm the project is built for net8.0.

Next steps
- Read [Overview](overview.md) to understand the design and key abstractions.
- Read [Architecture](architecture.md) for a component-level description and class references.
- Try the examples under `docs/examples/` to learn common usage patterns.

