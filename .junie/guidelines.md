# Project Guidelines

These guidelines help Junie and contributors work effectively with the InsaitTextEditor project.

## Project Overview
InsaitTextEditor is a cross-platform text editor built with Avalonia UI targeting .NET 9. The solution contains a single Avalonia application project with custom controls, view models, and rendering utilities (including SkiaSharp-based components).

## Repository Structure
- InsaitTextEditor.sln — Solution file
- InsaitTextEditor/ — Main application project
  - Controls/ — Custom Avalonia controls (e.g., LinedTextInput, DocumentTab, TabsPanel)
  - Icons/ — Image and icon assets
  - Models/ — Data models used by the app
  - Scripts/ — Rendering and utility code
    - SkiaSharp/ — SkiaSharp-based rendering (e.g., RichTextOverlay.cs)
    - Text/ — Text processing helpers
    - Windows/ — Windows-specific code (if any)
    - WindowsControl/ — Generic window control utilities
  - Services/ — Application services and abstractions
  - UI/ — Additional UI resources
  - ViewModels/ — MVVM view models (e.g., DocumentTabViewModel)
  - Windows/ — Avalonia windows (e.g., FileMenuWindow)
  - App.axaml — App entry XAML
  - MainWindow.axaml — Main window XAML

Build artifacts are generated under bin/ and obj/ for Debug/Release and net9.0 target.

## Build and Run
- Preferred IDE: JetBrains Rider (or Visual Studio 2022+). Open InsaitTextEditor.sln and run the InsaitTextEditor project.
- CLI:
  - Build: dotnet build InsaitTextEditor.sln -c Debug
  - Run: dotnet run --project InsaitTextEditor/InsaitTextEditor.csproj -c Debug
- Cross-platform: Avalonia enables Windows, macOS, and Linux. Platform-specific runtime assets are in bin/…/runtimes.

## Testing
- There is no dedicated test project in this repository at the moment.
- For changes impacting rendering or UI behavior, prefer manual verification by running the app and exercising the affected controls (e.g., open/close tabs, type in LinedTextInput, check SkiaSharp overlays).
- If you add tests in future, place them in a separate test project and update this guideline accordingly.

## What Junie Should Do
- Minimal changes: Make the smallest safe modifications to satisfy the issue.
- Prefer editing existing files over adding new ones unless the issue requires new files.
- Keep users informed using the <UPDATE> plan, progress, and next steps.
- Use Windows-style paths (\) in commands and respect the tool rules provided by the environment.

## Code Style
- C#: Follow standard .NET conventions (PascalCase for types/methods, camelCase for locals/fields as applicable). Use expression-bodied members and using directives where appropriate.
- XAML (Avalonia): Keep XAML clean and declarative; factor reusable pieces into Controls/ or separate resource dictionaries when they grow.
- Naming: Prefer descriptive names for controls and view models (e.g., DocumentTabViewModel).
- Nullability: Enable and respect nullable reference types where possible; guard against nulls when interacting with platform APIs.
- Comments: Document non-obvious logic, especially in Scripts/SkiaSharp where drawing details matter.

## Build and Submission Expectations
- For typical issues (docs/config changes), building the solution is not required.
- For code changes, ensure the solution builds locally before submitting.
- If you introduce behavior changes that are user-facing, briefly describe manual verification steps in your final update.

## Contribution Notes
- Keep diffs small and focused on the issue.
- Avoid committing build outputs (bin/, obj/).
- Commit messages: Use imperative mood and briefly state what and why (e.g., "Add project overview to .junie/guidelines.md").

Last updated: 2025-08-19.
