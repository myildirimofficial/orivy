API reference (DocFX)
======================

This project includes a DocFX configuration at `docs/docfx.json` to generate an API reference from the `Orivy` project.

Quick steps

1. Install DocFX (choose one):

   - Download the latest DocFX binary: https://dotnet.github.io/docfx/
   - Or install as a dotnet global tool (if available)

2. From the repository root run:

```powershell
cd docs
docfx metadata docfx.json
docfx build docfx.json
```

Output will be in `docs/_site`.

Notes
- DocFX requires MSBuild and an available SDK to build the project metadata. Ensure the `Orivy` project builds for `net8.0`.
- If docfx fails to collect metadata, run `dotnet build Orivy/Orivy.csproj -c Debug` first and check for missing references.

Automation
- You can add a CI job (GitHub Actions) that installs DocFX and runs `docfx build` to publish the generated static site to GitHub Pages or an artifact.
